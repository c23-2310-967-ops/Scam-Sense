-- =====================================================================================
-- Server score check
-- The game no longer writes its own score to public.game_results. It sends the player's answers to
-- public.submit_game_result, which recalculates the result with the server's copy of each room's rules and
-- answer key (the same formula as ScoringSystem.cs) and stores that. A player can't send a made-up score.
--   scoring.level_rules / scoring.level_messages   rules and answer key; not reachable through the API.
--                                                  Filled by the generated *_scoring_key.sql migrations
--                                                  (Tools > Scam Sense > Export Server Scoring Key).
--   scoring.compute_result(level, answers)         the scoring formula (no writes)
--   public.submit_game_result(run, level, answers) the only way to add a game result
-- =====================================================================================

create schema if not exists scoring;
revoke all on schema scoring from public, anon, authenticated;

create table if not exists scoring.level_rules (
  level_id                   text primary key check (level_id ~ '^level_[0-9]{1,3}$'),
  base_points                integer not null,
  speed_bonus                integer not null,
  fast_window                double precision not null,
  wrong_penalty              integer not null,
  recovery_points            integer not null,
  overtime_step_seconds      double precision not null,
  overtime_penalty_per_step  integer not null,
  max_time_penalty           integer not null,
  min_correct_points         integer not null,
  one_star                   integer not null,
  two_star                   integer not null,
  three_star                 integer not null,
  unlock_score               integer not null,
  message_count              integer not null check (message_count >= 0)
);

create table if not exists scoring.level_messages (
  level_id           text not null references scoring.level_rules (level_id) on delete cascade,
  message_id         text not null,
  is_scam            boolean not null,
  allowance_seconds  double precision not null,   -- ScoringSystem.ReadingAllowance for this message
  primary key (level_id, message_id)
);

-- Not exposed through the API anyway; RLS with no policies is a second lock.
alter table scoring.level_rules enable row level security;
alter table scoring.level_messages enable row level security;
revoke all on scoring.level_rules, scoring.level_messages from public, anon, authenticated;

-- Mathf.RoundToInt rounds halves to the nearest even number (2.5 -> 2, 3.5 -> 4).
create or replace function scoring.round_half_even(x double precision)
returns integer
language sql immutable
set search_path = ''
as $$
  select case when abs(x - trunc(x)) = 0.5 then (2 * round(x / 2))::integer else round(x)::integer end
$$;

-- The scoring formula of ScoringSystem.cs (TryRecord, PointsFor, TryRecordRecovery, TotalScore, StarsFor).
-- p_answers: [{"id", "verdict": "scam"|"legit", "seconds", "recovery_verdict": ""|"scam"|"legit", "recovery_hint"}]
-- A message that wasn't answered earns nothing. Invalid input raises an error (HTTP 400 through the API).
create or replace function scoring.compute_result(p_level_id text, p_answers jsonb)
returns jsonb
language plpgsql stable
set search_path = ''
as $$
declare
  r scoring.level_rules;
  m scoring.level_messages;
  a jsonb;
  seen text[] := '{}';
  normalized jsonb := '[]';
  v_id text; v_verdict text; v_secs double precision; v_recheck text; v_hint boolean;
  v_correct boolean; v_points integer; v_bonus integer; v_overtime integer; v_steps integer; v_recovery integer;
  v_fast double precision; v_k double precision;
  earned integer := 0; penalties integer := 0; n_correct integer := 0; n_missed integer := 0;
  n_false_alarms integer := 0; n_recovered integer := 0; total_secs double precision := 0;
  v_score integer; v_stars integer;
begin
  select * into r from scoring.level_rules where level_id = p_level_id;
  if not found then
    raise exception 'unknown room: %', p_level_id using errcode = '22023';
  end if;
  if jsonb_typeof(p_answers) is distinct from 'array' then
    raise exception 'answers must be a list' using errcode = '22023';
  end if;
  if jsonb_array_length(p_answers) > r.message_count then
    raise exception 'more answers than messages in %', p_level_id using errcode = '22023';
  end if;

  for a in select value from jsonb_array_elements(p_answers) loop
    if jsonb_typeof(a) is distinct from 'object' then
      raise exception 'each answer must be an object' using errcode = '22023';
    end if;

    v_id := a ->> 'id';
    select * into m from scoring.level_messages where level_id = p_level_id and message_id = v_id;
    if not found then
      raise exception 'unknown message in %: %', p_level_id, v_id using errcode = '22023';
    end if;
    if v_id = any (seen) then
      raise exception 'message answered twice: %', v_id using errcode = '22023';
    end if;
    seen := seen || v_id;

    v_verdict := a ->> 'verdict';
    if v_verdict is null or v_verdict not in ('scam', 'legit') then
      raise exception 'verdict must be scam or legit' using errcode = '22023';
    end if;
    if jsonb_typeof(a -> 'seconds') is distinct from 'number' then
      raise exception 'seconds must be a number' using errcode = '22023';
    end if;
    v_secs := (a ->> 'seconds')::double precision;
    if not (v_secs >= 0 and v_secs <= 86400) then
      raise exception 'seconds out of range' using errcode = '22023';
    end if;
    total_secs := total_secs + v_secs;

    -- First answer: base points with a speed bonus and a capped overtime deduction; a wrong answer costs the penalty.
    v_correct := (v_verdict = 'scam') = m.is_scam;
    v_points := 0; v_bonus := 0; v_overtime := 0;
    if v_correct then
      if r.speed_bonus > 0 then
        v_fast := m.allowance_seconds * r.fast_window;
        if v_secs <= v_fast or m.allowance_seconds = v_fast then
          v_k := 1;
        else
          v_k := 1 - least(1, greatest(0, (v_secs - v_fast) / (m.allowance_seconds - v_fast)));
        end if;
        v_bonus := scoring.round_half_even(r.speed_bonus * least(1, greatest(0, v_k)));
      end if;
      if v_secs > m.allowance_seconds and r.overtime_penalty_per_step > 0 and r.max_time_penalty > 0 then
        v_steps := floor((v_secs - m.allowance_seconds) / greatest(r.overtime_step_seconds, 0.001))::integer;
        v_overtime := least(r.max_time_penalty, v_steps * r.overtime_penalty_per_step,
                            greatest(0, r.base_points - r.min_correct_points));
      end if;
      v_points := greatest(r.min_correct_points, r.base_points - v_overtime) + v_bonus;
      earned := earned + v_points;
      n_correct := n_correct + 1;
    else
      penalties := penalties + r.wrong_penalty;
      if m.is_scam then n_missed := n_missed + 1; else n_false_alarms := n_false_alarms + 1; end if;
    end if;

    -- Follow-up re-check (only after a wrong first answer): recovery points, half with a hint.
    v_recheck := coalesce(a ->> 'recovery_verdict', '');
    v_hint := false;
    v_recovery := 0;
    if v_recheck <> '' then
      if v_correct then
        raise exception 'a correct answer has no re-check: %', v_id using errcode = '22023';
      end if;
      if v_recheck not in ('scam', 'legit') then
        raise exception 'recovery_verdict must be scam, legit or empty' using errcode = '22023';
      end if;
      if a ? 'recovery_hint' and jsonb_typeof(a -> 'recovery_hint') is distinct from 'boolean' then
        raise exception 'recovery_hint must be true or false' using errcode = '22023';
      end if;
      v_hint := coalesce((a ->> 'recovery_hint')::boolean, false);
      if (v_recheck = 'scam') = m.is_scam then
        v_recovery := case when v_hint then r.recovery_points / 2 else r.recovery_points end;
        earned := earned + v_recovery;
        n_recovered := n_recovered + 1;
      end if;
    end if;

    normalized := normalized || jsonb_build_array(jsonb_build_object(
      'id', v_id, 'verdict', v_verdict, 'seconds', v_secs, 'correct', v_correct, 'points', v_points,
      'speed_bonus', v_bonus, 'overtime', v_overtime, 'recovery_verdict', v_recheck, 'recovery_hint', v_hint,
      'recovery_points', v_recovery));
  end loop;

  v_score := greatest(0, earned - penalties);
  v_stars := case when v_score >= r.three_star then 3 when v_score >= r.two_star then 2
                  when v_score >= r.one_star then 1 else 0 end;
  return jsonb_build_object(
    'score', v_score,
    'max_score', (greatest(0, r.base_points) + greatest(0, r.speed_bonus)) * r.message_count,
    'stars', v_stars,
    'passed', v_score >= r.unlock_score,
    'correct_answers', n_correct,
    'total_questions', r.message_count,
    'total_answer_seconds', total_secs,
    'missed_scams', n_missed,
    'false_alarms', n_false_alarms,
    'recovered', n_recovered,
    'answers', normalized);
end
$$;

revoke all on function scoring.round_half_even(double precision) from public, anon, authenticated;
revoke all on function scoring.compute_result(text, jsonb) from public, anon, authenticated;

-- ---------- game_results: written only by submit_game_result ----------
alter table public.game_results
  add column if not exists answers jsonb,                                  -- per-message verdicts and points, as scored
  add column if not exists scored_by_server boolean not null default false;  -- false for rows uploaded before this check

drop policy if exists "game_results: guests and accounts insert own" on public.game_results;
revoke insert, update, delete on public.game_results from anon, authenticated;

create or replace function public.submit_game_result(p_run_id uuid, p_level_id text, p_answers jsonb,
                                                     p_completed_at timestamptz default null)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  uid uuid := auth.uid();
  existing public.game_results;
  res jsonb;
  done_at timestamptz;
begin
  -- Same rule as the table policies: a signed-in account or guest.
  if uid is null or (select auth.jwt() ->> 'is_anonymous') is null then
    raise exception 'not signed in' using errcode = '42501';
  end if;
  if p_run_id is null then
    raise exception 'run id missing' using errcode = '22023';
  end if;

  -- A retried upload of the same run returns what was stored the first time.
  select * into existing from public.game_results where user_id = uid and run_id = p_run_id;
  if found then
    return jsonb_build_object('score', existing.score, 'max_score', existing.max_score, 'stars', existing.stars,
                              'passed', existing.passed, 'correct_answers', existing.correct_answers,
                              'total_questions', existing.total_questions, 'stored', true);
  end if;

  res := scoring.compute_result(p_level_id, p_answers);

  -- Runs played offline upload later, so the game's finish time is kept unless it is impossible.
  done_at := coalesce(p_completed_at, now());
  if done_at > now() + interval '5 minutes' or done_at < now() - interval '1 year' then
    done_at := now();
  end if;

  insert into public.game_results (user_id, run_id, level_id, score, max_score, stars, correct_answers,
                                   total_questions, passed, total_answer_seconds, completed_at, answers, scored_by_server)
  values (uid, p_run_id, p_level_id, (res ->> 'score')::integer, (res ->> 'max_score')::integer,
          (res ->> 'stars')::smallint, (res ->> 'correct_answers')::integer, (res ->> 'total_questions')::integer,
          (res ->> 'passed')::boolean, (res ->> 'total_answer_seconds')::real, done_at, res -> 'answers', true)
  on conflict (user_id, run_id) do nothing;

  return (res - 'answers') || jsonb_build_object('stored', found);
end
$$;

revoke all on function public.submit_game_result(uuid, text, jsonb, timestamptz) from public, anon;
grant execute on function public.submit_game_result(uuid, text, jsonb, timestamptz) to authenticated;
