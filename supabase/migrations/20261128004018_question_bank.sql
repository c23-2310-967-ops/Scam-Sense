-- =====================================================================================
-- Scam Sense: managed question bank (data-driven learning content). Run after
-- 20261126074705_classroom_roles_classes_records.sql. Safe to run again.
-- The rows themselves are loaded by 20261128004100_question_bank_seed.sql (generated from
-- Assets/Resources/Content/question_bank.json by Tools > Scam Sense > Content > Export Supabase Seed).
--
-- What it adds
--   content_admins         content administrators (managed from the dashboard / SQL editor only).
--   content_categories     the allowed category names (Phishing, Smishing, Vishing, ...).
--   content_levels         per-level texts (briefing, objectives, case notes, lessons, tutorial, inbox)
--                          and the level's scoring numbers used to keep the catalog's scores right.
--   question_bank          one row per question. Management columns (level, order, active, category,
--                          difficulty, type) + the full question record as jsonb (same shape as the
--                          JSON file: Docs/ContentManagement.md).
--   question_bank_history  every previous version of a changed or deleted question (audit / undo).
--   get_content_bundle()   what the game downloads: the bank in the JSON file's shape.
--
-- Keeping the classroom catalog right
--   submit_game_result() checks runs against public.activities / public.questions. Whenever the
--   bank changes, refresh_content_catalog() recomputes each level's question_count, max_score and
--   pass_score from its active questions (same rules as the game: LevelContentRules.cs) and
--   upserts the catalog questions. Catalog rows are never deleted (old results point to them).
--
-- Content flow: Teacher / Administrator -> Supabase (this schema) -> Unity (get_content_bundle) -> Students.
--
-- Who can do what (Row Level Security; roles are kept on the server, never set by the game)
--   * Students and guests: read active, assigned content through get_content_bundle() only.
--   * Teachers (user_roles.role = 'teacher', from the classroom migration): add and edit questions and
--     level texts, switch questions on / off, move them between levels; read everything (inactive,
--     reserve, history). They can't hard-delete questions or change the category list.
--   * Content admins (a row in content_admins): everything teachers can, plus delete questions and
--     manage categories. Add one in the SQL editor:
--       insert into public.content_admins (user_id, label) values ('<auth user id>', 'Admin name');
--   Edits are global (every class plays the same bank); every change is kept in question_bank_history.
-- =====================================================================================

-- ---------- Tables ----------
create table if not exists public.content_admins (
  user_id     uuid primary key references auth.users (id) on delete cascade,
  label       text,
  granted_at  timestamptz not null default now()
);

create table if not exists public.content_categories (
  name        text primary key check (char_length(name) between 2 and 40),
  sort_order  integer not null default 0
);

create table if not exists public.content_levels (
  level_id             text primary key references public.activities (level_id),
  -- Scoring numbers from the level asset (LevelDefinition.scoring); only used to keep the catalog right.
  points_per_question  integer not null check (points_per_question > 0),   -- basePoints + speedBonus
  base_points          integer not null check (base_points > 0),
  min_correct_points   integer not null check (min_correct_points >= 0),
  wrong_penalty        integer not null check (wrong_penalty >= 0),
  content              jsonb not null default '{}'::jsonb check (jsonb_typeof(content) = 'object'),
  updated_at           timestamptz not null default now(),
  updated_by           uuid default auth.uid()
);

create table if not exists public.question_bank (
  question_id   text primary key check (question_id ~ '^[A-Za-z0-9_-]{1,40}$'),
  level_id      text references public.activities (level_id),      -- null = reserve (not in any level)
  sort_order    integer not null default 0,
  is_active     boolean not null default true,
  category      text not null default '',
  difficulty    text not null default 'Easy' check (difficulty in ('Easy', 'Medium', 'Hard', '')),
  question_type text not null default '',
  message_type  text not null check (message_type in ('SMS', 'Email', 'Website', 'Case', 'Chat', 'Question')),
  content       jsonb not null check (jsonb_typeof(content) = 'object'),
  updated_at    timestamptz not null default now(),
  updated_by    uuid default auth.uid()
);
create index if not exists question_bank_level_idx on public.question_bank (level_id, is_active, sort_order);

create table if not exists public.question_bank_history (
  id           bigint generated always as identity primary key,
  question_id  text not null,
  operation    text not null check (operation in ('UPDATE', 'DELETE')),
  old_row      jsonb not null,
  changed_at   timestamptz not null default now(),
  changed_by   uuid default auth.uid()
);
create index if not exists question_bank_history_q_idx on public.question_bank_history (question_id, changed_at desc);

-- Catalog question ids may now be any bank id (was ^[A-Z][0-9]{2}$: E01-H10 and L01-L02 still match).
alter table public.questions drop constraint if exists questions_id_check;
alter table public.questions add constraint questions_id_check check (id ~ '^[A-Za-z0-9_-]{1,40}$');

-- ---------- Helpers ----------
-- Content administrator: full control of the bank. Never a guest.
create or replace function public.is_content_admin() returns boolean
language sql stable security definer set search_path = '' as $$
  select auth.uid() is not null
     and coalesce((auth.jwt() ->> 'is_anonymous')::boolean, false) = false
     and exists (select 1 from public.content_admins a where a.user_id = auth.uid());
$$;

-- May add and edit content: a content admin or a teacher (public.is_teacher() from the classroom migration).
create or replace function public.can_edit_content() returns boolean
language sql stable security definer set search_path = '' as $$
  select public.is_content_admin()
      or (auth.uid() is not null
          and coalesce((auth.jwt() ->> 'is_anonymous')::boolean, false) = false
          and public.is_teacher());
$$;

-- The core rules the game also checks (QuestionBankValidator.cs), so an active question the server accepts
-- is one the game plays and the catalog's question count matches what players answer.
-- Returns '' when fine, otherwise the first problem.
create or replace function public.question_problem(p_type text, p_content jsonb) returns text
language plpgsql immutable set search_path = '' as $$
declare
  n integer := coalesce(jsonb_array_length(case when jsonb_typeof(p_content -> 'choices') = 'array' then p_content -> 'choices' end), 0);
  c integer := coalesce((p_content ->> 'correctChoice')::integer, 0);
  kind text := coalesce(p_content #>> '{activity,kind}', '');
begin
  if coalesce(btrim(p_content ->> 'message'), '') = '' then return 'message (the question text) is empty'; end if;
  if coalesce(btrim(p_content ->> 'explanation'), '') = '' then return 'explanation is empty'; end if;
  if p_type in ('Email', 'Website') and coalesce(btrim(p_content ->> 'subject'), '') = '' then return 'emails and web pages need a subject'; end if;
  if n = 1 then return 'a choice question needs at least 2 choices'; end if;
  if n >= 2 then
    if c < 0 or c >= n then return format('correctChoice %s is out of range (0-%s)', c, n - 1); end if;
    if p_type <> 'Question' then return 'choice questions must use message_type Question'; end if;
    if exists (select 1 from jsonb_array_elements_text(p_content -> 'choices') x where btrim(x) = '') then return 'a choice is empty'; end if;
    if (select count(distinct x) from jsonb_array_elements_text(p_content -> 'choices') x) <> n then return 'two choices are identical'; end if;
  end if;
  if kind not in ('', 'None') and n < 2 then return 'activities need answer choices'; end if;
  return '';
end $$;

-- ---------- Triggers ----------
create or replace function public.question_bank_before_write() returns trigger
language plpgsql set search_path = '' as $$
declare problem text;
begin
  new.updated_at := now();
  new.updated_by := coalesce(auth.uid(), new.updated_by);
  if new.is_active then
    problem := public.question_problem(new.message_type, new.content);
    if problem <> '' then
      raise exception 'Question %: %', new.question_id, problem using errcode = '22023';
    end if;
  end if;
  return new;
end $$;
drop trigger if exists question_bank_before_write on public.question_bank;
create trigger question_bank_before_write before insert or update on public.question_bank
  for each row execute function public.question_bank_before_write();

create or replace function public.question_bank_keep_history() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  insert into public.question_bank_history (question_id, operation, old_row) values (old.question_id, tg_op, to_jsonb(old));
  return case when tg_op = 'DELETE' then old else new end;
end $$;
drop trigger if exists question_bank_history on public.question_bank;
create trigger question_bank_history after update or delete on public.question_bank
  for each row execute function public.question_bank_keep_history();

create or replace function public.content_levels_touch() returns trigger
language plpgsql set search_path = '' as $$
begin
  new.updated_at := now();
  new.updated_by := coalesce(auth.uid(), new.updated_by);
  return new;
end $$;
drop trigger if exists content_levels_touch on public.content_levels;
create trigger content_levels_touch before insert or update on public.content_levels
  for each row execute function public.content_levels_touch();

-- Keeps activities / questions (the classroom catalog) in step with the bank. Same rules as LevelContentRules.cs:
-- unlock = about 60% right (never all), each right answer worth at least min_correct_points, misses cost wrong_penalty.
create or replace function public.refresh_content_catalog() returns trigger
language plpgsql security definer set search_path = '' as $$
declare
  lv record;
  n integer; k1 integer; k2 integer; slow integer; one integer; two integer;
begin
  insert into public.questions (id, level_id, course, topic, learning_objective)
  select q.question_id, q.level_id,
         coalesce(nullif(btrim(split_part(q.content ->> 'curriculumRef', ' — ', 1)), ''), 'Scam Sense'),
         coalesce(case when strpos(q.content ->> 'curriculumRef', ' — ') > 0
                       then nullif(btrim(substr(q.content ->> 'curriculumRef', strpos(q.content ->> 'curriculumRef', ' — ') + 3)), '') end,
                  nullif(q.category, ''), 'General'),
         coalesce(nullif(q.content ->> 'learningObjective', ''), '-')
    from public.question_bank q
   where q.is_active and q.level_id is not null
  on conflict (id) do update set level_id = excluded.level_id, course = excluded.course, topic = excluded.topic,
                                 learning_objective = excluded.learning_objective;

  for lv in select * from public.content_levels loop
    select count(*) into n from public.question_bank q where q.is_active and q.level_id = lv.level_id;
    if n = 0 then continue; end if;   -- the game falls back to its built-in questions; leave the catalog as it was
    k2 := least(greatest(ceil(n * 0.6)::integer, 1), greatest(1, n - 1));
    k1 := least(greatest(n / 2, 1), k2);
    slow := least(lv.base_points, greatest(0, lv.min_correct_points));
    one := greatest(1, k1 * slow - (n - k1) * lv.wrong_penalty);
    two := greatest(one, k2 * slow - (n - k2) * lv.wrong_penalty);
    update public.activities a
       set question_count = n, max_score = n * lv.points_per_question, pass_score = two
     where a.level_id = lv.level_id
       and (a.question_count, a.max_score, a.pass_score) is distinct from (n, n * lv.points_per_question, two);
  end loop;
  return null;
end $$;
drop trigger if exists question_bank_catalog on public.question_bank;
create trigger question_bank_catalog after insert or update or delete on public.question_bank
  for each statement execute function public.refresh_content_catalog();
drop trigger if exists content_levels_catalog on public.content_levels;
create trigger content_levels_catalog after insert or update or delete on public.content_levels
  for each statement execute function public.refresh_content_catalog();

-- ---------- What the game downloads ----------
-- The bank in the shape of Assets/Resources/Content/question_bank.json (schema version 2). Players get active,
-- assigned questions only; editors get everything (reserve and inactive too).
create or replace function public.get_content_bundle() returns jsonb
language sql stable security definer set search_path = '' as $$
  with editor as (select public.can_edit_content() as yes),
  qs as (
    select q.* from public.question_bank q, editor
     where editor.yes or (q.is_active and q.level_id is not null)
  )
  select jsonb_build_object(
    'schemaVersion', 2,
    'contentVersion', coalesce(to_char(greatest((select max(updated_at) from public.question_bank),
                                                (select max(updated_at) from public.content_levels)) at time zone 'UTC',
                                       'YYYY-MM-DD"T"HH24:MI:SS.US"Z"'), ''),
    'source', 'supabase',
    'categories', coalesce((select jsonb_agg(c.name order by c.sort_order, c.name) from public.content_categories c), '[]'::jsonb),
    'levels', coalesce((select jsonb_agg(l.content || jsonb_build_object('levelId', l.level_id) order by l.level_id)
                          from public.content_levels l), '[]'::jsonb),
    'questions', coalesce((select jsonb_agg(q.content || jsonb_build_object(
                             'questionId', q.question_id, 'levelId', coalesce(q.level_id, ''), 'order', q.sort_order,
                             'isActive', q.is_active, 'category', q.category, 'difficulty', q.difficulty,
                             'questionType', q.question_type, 'messageType', q.message_type)
                           order by q.level_id nulls last, q.sort_order, q.question_id) from qs q), '[]'::jsonb)
  );
$$;

-- ---------- Row Level Security ----------
alter table public.content_admins         enable row level security;
alter table public.content_categories     enable row level security;
alter table public.content_levels         enable row level security;
alter table public.question_bank          enable row level security;
alter table public.question_bank_history  enable row level security;

drop policy if exists "content_admins: read own" on public.content_admins;
create policy "content_admins: read own" on public.content_admins for select to authenticated
  using ((select auth.uid()) = user_id);

-- Categories: everyone reads, admins manage.
drop policy if exists "content_categories: read" on public.content_categories;
drop policy if exists "content_categories: admins write" on public.content_categories;
create policy "content_categories: read" on public.content_categories for select to authenticated using (true);
create policy "content_categories: admins write" on public.content_categories for all to authenticated
  using (public.is_content_admin()) with check (public.is_content_admin());

-- Level texts: teachers and admins read and edit. Levels themselves are added by developers (scene + asset).
drop policy if exists "content_levels: editors read" on public.content_levels;
drop policy if exists "content_levels: editors update" on public.content_levels;
drop policy if exists "content_levels: admins insert" on public.content_levels;
drop policy if exists "content_levels: admins delete" on public.content_levels;
create policy "content_levels: editors read" on public.content_levels for select to authenticated
  using (public.can_edit_content());
create policy "content_levels: editors update" on public.content_levels for update to authenticated
  using (public.can_edit_content()) with check (public.can_edit_content());
create policy "content_levels: admins insert" on public.content_levels for insert to authenticated
  with check (public.is_content_admin());
create policy "content_levels: admins delete" on public.content_levels for delete to authenticated
  using (public.is_content_admin());

-- Questions: teachers and admins add, edit, switch on/off and move; only admins hard-delete.
drop policy if exists "question_bank: editors read" on public.question_bank;
drop policy if exists "question_bank: editors insert" on public.question_bank;
drop policy if exists "question_bank: editors update" on public.question_bank;
drop policy if exists "question_bank: admins delete" on public.question_bank;
create policy "question_bank: editors read" on public.question_bank for select to authenticated
  using (public.can_edit_content());
create policy "question_bank: editors insert" on public.question_bank for insert to authenticated
  with check (public.can_edit_content());
create policy "question_bank: editors update" on public.question_bank for update to authenticated
  using (public.can_edit_content()) with check (public.can_edit_content());
create policy "question_bank: admins delete" on public.question_bank for delete to authenticated
  using (public.is_content_admin());

drop policy if exists "question_bank_history: editors read" on public.question_bank_history;
create policy "question_bank_history: editors read" on public.question_bank_history for select to authenticated
  using (public.can_edit_content());

-- ---------- Privileges ----------
revoke all on public.content_admins, public.content_categories, public.content_levels, public.question_bank,
              public.question_bank_history from anon, authenticated;
grant select on public.content_admins, public.content_categories to authenticated;
grant select, insert, update, delete on public.content_categories, public.content_levels, public.question_bank to authenticated;
grant select on public.question_bank_history to authenticated;
-- content_admins is never writable from the game; RLS above narrows the table grants per role.

revoke execute on function public.is_content_admin()                 from public, anon;
revoke execute on function public.can_edit_content()                 from public, anon;
revoke execute on function public.question_problem(text, jsonb)      from public, anon;
revoke execute on function public.question_bank_before_write()       from public, anon, authenticated;
revoke execute on function public.question_bank_keep_history()       from public, anon, authenticated;
revoke execute on function public.content_levels_touch()             from public, anon, authenticated;
revoke execute on function public.refresh_content_catalog()          from public, anon, authenticated;
revoke execute on function public.get_content_bundle()               from public, anon;
grant execute on function public.is_content_admin(), public.can_edit_content(), public.question_problem(text, jsonb),
                          public.get_content_bundle() to authenticated;