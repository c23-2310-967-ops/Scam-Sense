-- =====================================================================================
-- Scam Sense: Think Before You Click - player accounts and progress (Supabase)
-- Applied to the project as migration 20261020022638 (safe to run again: everything is
-- "if not exists" / "or replace", and policies are dropped before they are recreated).
--
-- Tables
--   profiles        one row per player (display name, last progress reset)
--   level_progress  one row per player per level: best results and unlock state
--   achievements    one row per unlocked career achievement
--   game_results    one row per finished run (history; never edited)
-- Every table has Row Level Security: a signed-in player can only see and change
-- their own rows (auth.uid()). Anonymous (guest) sessions are normal auth users too.
-- Times written by the game are .NET UTC ticks (bigint), matching the local save.
-- =====================================================================================

-- ---------- profiles ----------
create table if not exists public.profiles (
  id                  uuid primary key references auth.users (id) on delete cascade,
  display_name        text not null default 'Investigator'
                      check (char_length(display_name) between 1 and 32),
  progress_reset_utc  bigint not null default 0 check (progress_reset_utc >= 0),
  created_at          timestamptz not null default now(),
  updated_at          timestamptz not null default now()
);

-- ---------- level_progress ----------
create table if not exists public.level_progress (
  user_id          uuid not null references auth.users (id) on delete cascade,
  level_id         text not null check (char_length(level_id) between 1 and 64),
  unlocked         boolean not null default false,
  completed        boolean not null default false,
  passed           boolean not null default false,
  best_score       integer not null default 0 check (best_score between 0 and 100000),
  best_stars       smallint not null default 0 check (best_stars between 0 and 3),
  last_score       integer not null default 0 check (last_score between 0 and 100000),
  last_stars       smallint not null default 0 check (last_stars between 0 and 3),
  times_completed  integer not null default 0 check (times_completed >= 0),
  best_xp          integer not null default 0 check (best_xp >= 0),
  best_correct     integer not null default 0 check (best_correct >= 0),
  message_count    integer not null default 0 check (message_count >= 0),
  perfect          boolean not null default false,
  last_played_utc  bigint not null default 0,
  changed_utc      bigint not null default 0,
  updated_at       timestamptz not null default now(),
  primary key (user_id, level_id)
);

-- ---------- achievements ----------
create table if not exists public.achievements (
  user_id         uuid not null references auth.users (id) on delete cascade,
  achievement_id  text not null check (char_length(achievement_id) between 1 and 64),
  unlocked_utc    bigint not null default 0,
  primary key (user_id, achievement_id)
);

-- ---------- game_results ----------
create table if not exists public.game_results (
  id                    bigint generated always as identity primary key,
  user_id               uuid not null default auth.uid() references auth.users (id) on delete cascade,
  run_id                uuid not null,          -- made by the game; stops duplicate uploads of one run
  level_id              text not null check (char_length(level_id) between 1 and 64),
  score                 integer not null check (score between 0 and 100000),
  max_score             integer not null check (max_score between 0 and 100000),
  stars                 smallint not null check (stars between 0 and 3),
  correct_answers       integer not null check (correct_answers >= 0),
  total_questions       integer not null check (total_questions >= 0 and correct_answers <= total_questions),
  passed                boolean not null default false,
  total_answer_seconds  real not null default 0 check (total_answer_seconds >= 0),
  completed_at          timestamptz not null default now(),
  unique (user_id, run_id)
);
create index if not exists game_results_user_level_idx on public.game_results (user_id, level_id, completed_at desc);

-- ---------- keep updated_at current ----------
create or replace function public.touch_updated_at() returns trigger
language plpgsql set search_path = '' as $$
begin
  new.updated_at := now();
  return new;
end $$;

drop trigger if exists profiles_touch on public.profiles;
create trigger profiles_touch before update on public.profiles
  for each row execute function public.touch_updated_at();

-- ---------- progress only ever improves on the server ----------
-- Same rules as the game's local merge: unlocks/completion never go back, bests only go up,
-- the "last" result comes from the most recent run. A stale or faulty client can't lower progress.
-- (A RESET PROGRESS deletes the rows first, so it is not blocked by this.)
create or replace function public.level_progress_keep_best() returns trigger
language plpgsql set search_path = '' as $$
begin
  new.unlocked        := old.unlocked or new.unlocked;
  new.completed       := old.completed or new.completed;
  new.passed          := old.passed or new.passed;
  new.perfect         := old.perfect or new.perfect;
  new.best_score      := greatest(old.best_score, new.best_score);
  new.best_stars      := greatest(old.best_stars, new.best_stars);
  new.times_completed := greatest(old.times_completed, new.times_completed);
  new.best_xp         := greatest(old.best_xp, new.best_xp);
  new.message_count   := greatest(old.message_count, new.message_count);
  new.best_correct    := least(greatest(old.best_correct, new.best_correct), greatest(new.message_count, 0));
  new.changed_utc     := greatest(old.changed_utc, new.changed_utc);
  if new.last_played_utc < old.last_played_utc then
    new.last_score      := old.last_score;
    new.last_stars      := old.last_stars;
    new.last_played_utc := old.last_played_utc;
  end if;
  new.updated_at := now();
  return new;
end $$;

drop trigger if exists level_progress_keep_best on public.level_progress;
create trigger level_progress_keep_best before update on public.level_progress
  for each row execute function public.level_progress_keep_best();

-- ---------- a profile for every new auth user (accounts and guests) ----------
create or replace function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  insert into public.profiles (id, display_name)
  values (
    new.id,
    coalesce(nullif(left(trim(new.raw_user_meta_data ->> 'display_name'), 32), ''),
             case when new.is_anonymous then 'Guest' else 'Investigator' end)
  )
  on conflict (id) do nothing;
  return new;
end $$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created after insert on auth.users
  for each row execute function public.handle_new_user();

-- ---------- Row Level Security ----------
alter table public.profiles       enable row level security;
alter table public.level_progress enable row level security;
alter table public.achievements   enable row level security;
alter table public.game_results   enable row level security;

-- profiles: read / create / update your own row only (no delete: deleting the auth user cascades)
drop policy if exists "profiles: read own"   on public.profiles;
drop policy if exists "profiles: insert own" on public.profiles;
drop policy if exists "profiles: update own" on public.profiles;
create policy "profiles: read own"   on public.profiles for select to authenticated using ((select auth.uid()) = id);
create policy "profiles: insert own" on public.profiles for insert to authenticated with check ((select auth.uid()) = id);
create policy "profiles: update own" on public.profiles for update to authenticated
  using ((select auth.uid()) = id) with check ((select auth.uid()) = id);

-- level_progress: full control of your own rows (delete is used by RESET PROGRESS)
drop policy if exists "level_progress: read own"   on public.level_progress;
drop policy if exists "level_progress: insert own" on public.level_progress;
drop policy if exists "level_progress: update own" on public.level_progress;
drop policy if exists "level_progress: delete own" on public.level_progress;
create policy "level_progress: read own"   on public.level_progress for select to authenticated using ((select auth.uid()) = user_id);
create policy "level_progress: insert own" on public.level_progress for insert to authenticated with check ((select auth.uid()) = user_id);
create policy "level_progress: update own" on public.level_progress for update to authenticated
  using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);
create policy "level_progress: delete own" on public.level_progress for delete to authenticated using ((select auth.uid()) = user_id);

-- achievements: same as level_progress
drop policy if exists "achievements: read own"   on public.achievements;
drop policy if exists "achievements: insert own" on public.achievements;
drop policy if exists "achievements: update own" on public.achievements;
drop policy if exists "achievements: delete own" on public.achievements;
create policy "achievements: read own"   on public.achievements for select to authenticated using ((select auth.uid()) = user_id);
create policy "achievements: insert own" on public.achievements for insert to authenticated with check ((select auth.uid()) = user_id);
create policy "achievements: update own" on public.achievements for update to authenticated
  using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);
create policy "achievements: delete own" on public.achievements for delete to authenticated using ((select auth.uid()) = user_id);

-- game_results: add and read your own runs; history can't be edited or deleted from the game
drop policy if exists "game_results: read own"   on public.game_results;
drop policy if exists "game_results: insert own" on public.game_results;
create policy "game_results: read own"   on public.game_results for select to authenticated using ((select auth.uid()) = user_id);
create policy "game_results: insert own" on public.game_results for insert to authenticated with check ((select auth.uid()) = user_id);

-- Nothing is readable without signing in (guests sign in anonymously).
revoke all on public.profiles, public.level_progress, public.achievements, public.game_results from anon;
grant select, insert, update          on public.profiles       to authenticated;
grant select, insert, update, delete  on public.level_progress to authenticated;
grant select, insert, update, delete  on public.achievements   to authenticated;
grant select, insert                  on public.game_results   to authenticated;
