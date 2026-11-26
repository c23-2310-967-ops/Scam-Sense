-- =====================================================================================
-- Scam Sense: classroom mode - teacher / student roles, classes, class codes and
-- per-question records. Run after the earlier migrations. Safe to run again.
--
-- What it adds
--   user_roles         the role of each account, kept on the server (no row = student).
--                      Players can read their own role but never write it.
--   teacher_invites    secret invite codes (stored hashed) that turn an account into a
--                      teacher. Managed only from the Supabase dashboard / SQL editor.
--   classes            a teacher's classes, each with a unique 6-character class code.
--   class_members      who is enrolled. One active class per student; removals are kept
--                      (removed_at) so a removed student can't rejoin without the teacher.
--   activities         read-only catalog: the 5 levels, question count, max and pass score.
--   questions          read-only catalog: the 30 questions (E01-H10), their course, topic
--                      (curriculum reference) and learning objective. Topics come from here.
--   question_results   one row per question per finished run (first answer right or wrong,
--                      recovered, hint used, time). Used for weak topics / hardest activities.
--   game_results       gains class_id, incorrect_answers, completed and attempt_number.
--
-- How results are written
--   The game no longer inserts into game_results directly. It calls
--   submit_game_result(p_run), which checks the run against the catalog (level exists,
--   question count, score <= the level's maximum, every question belongs to that level,
--   pass flag matches the pass score), stamps the student's current class and the attempt
--   number on the server, and stores the per-question rows. run_id makes a retried upload
--   harmless. Nobody can update result rows afterwards (no UPDATE grant + a trigger).
--
-- Who can read what (Row Level Security)
--   * Students (guests and accounts): only their own rows, as before.
--   * Teachers: rows of students who are currently enrolled in one of their own classes.
--     Results and per-question rows are limited to the ones recorded in that class.
--     Teachers see display names only, never email addresses.
--   * Teacher-only functions check the role on the server and refuse everyone else.
-- =====================================================================================

-- ---------- Roles ----------
create table if not exists public.user_roles (
  user_id     uuid primary key references auth.users (id) on delete cascade,
  role        text not null default 'student' check (role in ('student', 'teacher')),
  granted_at  timestamptz not null default now()
);

create table if not exists public.teacher_invites (
  code_hash   text primary key,                 -- sha256 of the normalised code (the code itself is never stored)
  label       text,                             -- who it was made for (e.g. "Ms. Santos")
  max_uses    integer not null default 1 check (max_uses between 1 and 500),
  uses        integer not null default 0 check (uses >= 0),
  expires_at  timestamptz,
  created_at  timestamptz not null default now()
);

-- Failed class-code / invite-code attempts, to slow down guessing.
create table if not exists public.classroom_attempts (
  id       bigint generated always as identity primary key,
  user_id  uuid not null references auth.users (id) on delete cascade,
  kind     text not null check (kind in ('join', 'invite')),
  at       timestamptz not null default now()
);
create index if not exists classroom_attempts_user_idx on public.classroom_attempts (user_id, kind, at desc);

-- ---------- Classes ----------
create table if not exists public.classes (
  id          uuid primary key default gen_random_uuid(),
  teacher_id  uuid not null references auth.users (id) on delete cascade,
  name        text not null check (char_length(name) between 2 and 60),
  -- 6 characters from ABCDEFGHJKLMNPQRSTUVWXYZ23456789 (no I, O, 0 or 1: easy to read aloud)
  code        text not null unique check (code ~ '^[A-HJ-NP-Z2-9]{6}$'),
  archived    boolean not null default false,
  created_at  timestamptz not null default now()
);
create index if not exists classes_teacher_idx on public.classes (teacher_id);

create table if not exists public.class_members (
  class_id    uuid not null references public.classes (id) on delete cascade,
  student_id  uuid not null references auth.users (id) on delete cascade,
  joined_at   timestamptz not null default now(),
  removed_at  timestamptz,
  removed_by  text check (removed_by in ('teacher', 'student')),
  primary key (class_id, student_id)
);
create unique index if not exists class_members_one_active_class on public.class_members (student_id) where removed_at is null;

-- ---------- Catalog (must match the game: Assets/Levels/LevelCatalog.asset; checked by ClassroomTests) ----------
create table if not exists public.activities (
  level_id        text primary key check (level_id ~ '^level_[0-9]{1,3}$'),
  level_number    integer not null,
  title           text not null,
  question_count  integer not null check (question_count > 0),
  max_score       integer not null check (max_score > 0),
  pass_score      integer not null check (pass_score >= 0)
);

create table if not exists public.questions (
  id                  text primary key check (id ~ '^[A-Z][0-9]{2}$'),   -- E/M/H = validated bank, L = Level 1 legitimate examples
  level_id            text not null references public.activities (level_id),
  course              text not null,
  topic               text not null,
  learning_objective  text not null
);

insert into public.activities (level_id, level_number, title, question_count, max_score, pass_score) values
  ('level_1', 1, 'The First Case', 4, 400, 230),
  ('level_2', 2, 'The Investigation', 6, 900, 450),
  ('level_3', 3, 'The Digital Trap', 6, 1320, 590),
  ('level_4', 4, 'Digital Forensics Lab', 6, 1650, 730),
  ('level_5', 5, 'The Compromised Campus Network', 6, 1980, 870)
on conflict (level_id) do update set
  level_number = excluded.level_number, title = excluded.title, question_count = excluded.question_count,
  max_score = excluded.max_score, pass_score = excluded.pass_score;

insert into public.questions (id, level_id, course, topic, learning_objective) values
  ('E01', 'level_1', 'Information Assurance in Security 1 (BIT1101)', 'Week 3: Fundamentals of Information Security (CIA Triad)', 'Name the security goal that a scam breaks.'),
  ('E02', 'level_1', 'Information Assurance in Security 1 (BIT1101)', 'Week 16: Information Security Best Practices (user awareness)', 'Spot a fake delivery text: unexpected, rushed, a small fee and a strange link.'),
  ('E03', 'level_1', 'Computer Security (BIT1225)', 'Introduction to Computer Security: types of security threats', 'Name phishing and spot a fake look-alike address.'),
  ('E04', 'level_1', 'Computer Security (BIT1225)', 'Authentication and Access Control', 'Never share an OTP, and call back on a number you trust.'),
  ('E05', 'level_1', 'Computer Security (BIT1225)', 'Malware and Threats: preventing, detecting and removing malware', 'Treat any request to turn off security tools as a warning sign.'),
  ('E06', 'level_1', 'Social and Professional Issues (BIT2105)', 'Weeks 10-11: Ethics for IT Users (responsible use of social media)', 'Know that fun-looking posts can collect your personal info.'),
  ('L01', 'level_1', 'Information Assurance in Security 1 (BIT1101)', 'Week 16: Information Security Best Practices (user awareness)', 'Recognise a real notice: it matches something you did, gives you time and asks for nothing secret.'),
  ('L02', 'level_1', 'Information Assurance in Security 1 (BIT1101)', 'Week 16: Information Security Best Practices (user awareness)', 'Know that not every message is a scam: a real one from someone you know just shares details.'),
  ('E07', 'level_2', 'Social and Professional Issues (BIT2105)', 'Week 15: Data Privacy Act (RA 10173)', 'Share personal data only when it''s really needed and the reason is explained.'),
  ('E08', 'level_2', 'Information Assurance in Security 1 (BIT1101)', 'Week 9: Information Assets and Asset Management', 'Protect first the account that unlocks all the others.'),
  ('E09', 'level_2', 'Computer Security (BIT1225)', 'Operating System Security (patch management, security configuration)', 'Know where real security alerts come from.'),
  ('E10', 'level_2', 'Cyber Forensics (BIT2226)', 'Digital Evidence and Evidence Collection', 'Save proof before doing anything else.'),
  ('M01', 'level_2', 'Information Assurance in Security 1 (BIT1101)', 'Weeks 7-8: Principles of Information Assurance (authentication)', 'Real messages exist too: check the facts before calling something a scam.'),
  ('M02', 'level_2', 'Computer Security (BIT1225)', 'Web and Application Security', 'Find the real website in a link by reading right to left.'),
  ('M03', 'level_3', 'Advanced Cybersecurity Strategies (BIT4233)', 'Modern Cryptography (secure data in transit and at rest)', 'Know that a padlock doesn''t mean a site is real.'),
  ('M04', 'level_3', 'Information Assurance in Security 1 (BIT1101)', 'Weeks 10-11: Cybersecurity Threats and Attack Landscape', 'Spot scams that use feelings and rush you, and check through a number you know.'),
  ('M05', 'level_3', 'Social and Professional Issues (BIT2105)', 'Weeks 13-14: Security Incidents', 'Report a security mistake quickly and honestly.'),
  ('M06', 'level_3', 'Information Assurance in Security 1 (BIT1101)', 'Week 15: Security Controls and Protection Mechanisms', 'Pick the protection that fits the problem.'),
  ('M07', 'level_3', 'Computer Security (BIT1225)', 'Network Security (firewalls, IDS/IPS, VPNs)', 'Spot fake Wi-Fi hotspots, and know what a VPN can''t do.'),
  ('M08', 'level_3', 'Cyber Forensics (BIT2226)', 'Mobile Device Forensics', 'Know that the name on your phone doesn''t prove who sent a text.'),
  ('M09', 'level_4', 'Social and Professional Issues (BIT2105)', 'Week 9: Ethics for IT Workers', 'Follow the checking steps, even when someone important is rushing you.'),
  ('M10', 'level_4', 'Computer Security (BIT1225)', 'Malware and Threats', 'Don''t click "Enable Content" on unexpected attachments.'),
  ('H01', 'level_4', 'Advanced Cybersecurity Strategies (BIT4233)', 'Advanced Pen Testing (reconnaissance) and APT Defense', 'Know that personal details can come from public pages and don''t prove who sent a message.'),
  ('H02', 'level_4', 'Advanced Cybersecurity Strategies (BIT4233)', 'Cloud Security (identity management in SaaS)', 'Read what an app asks for before you click Allow.'),
  ('H03', 'level_4', 'Advanced Cybersecurity Strategies (BIT4233)', 'Secure Network Design (zero-trust models)', 'Double-check risky requests another way, even from trusted accounts.'),
  ('H04', 'level_4', 'Information Assurance in Security 1 (BIT1101)', 'Weeks 13-14: Vulnerabilities and Risk Management', 'Fix risks by how likely and how harmful they are.'),
  ('H05', 'level_5', 'Cyber Forensics (BIT2226)', 'Legal Aspects of Cyber Forensics (evidence integrity)', 'Keep evidence complete, unchanged and recorded.'),
  ('H06', 'level_5', 'Cyber Forensics (BIT2226)', 'Network Forensics and Cybercrime Investigation', 'Trust an email''s hidden checks, not just what it shows.'),
  ('H07', 'level_5', 'Social and Professional Issues (BIT2105)', 'Week 15: Data Privacy Act (RA 10173)', 'Tell a normal data request from identity theft.'),
  ('H08', 'level_5', 'Computer Security (BIT1225)', 'Incident Response and Forensics', 'Know the first steps: stop the spread, keep the evidence, report.'),
  ('H09', 'level_5', 'Information Assurance in Security 1 (BIT1101)', 'Week 17: Real-World Applications and Case Studies', 'Treat QR codes like links, and look for stickers placed on top.'),
  ('H10', 'level_5', 'Social and Professional Issues (BIT2105)', 'Week 16: Professional Organization and ACM Code of Ethics', 'Report wrongdoing without causing more harm.')
on conflict (id) do update set
  level_id = excluded.level_id, course = excluded.course, topic = excluded.topic, learning_objective = excluded.learning_objective;

-- ---------- Results: new columns ----------
alter table public.game_results add column if not exists class_id          uuid references public.classes (id) on delete set null;
alter table public.game_results add column if not exists incorrect_answers integer not null default 0;
alter table public.game_results add column if not exists completed         boolean not null default true;
alter table public.game_results add column if not exists attempt_number    integer not null default 1;

-- Rows recorded before this migration: fill the new columns from what they already hold.
update public.game_results set incorrect_answers = total_questions - correct_answers
 where incorrect_answers <> total_questions - correct_answers;
with numbered as (
  select id, row_number() over (partition by user_id, level_id order by completed_at, id) as n from public.game_results
)
update public.game_results g set attempt_number = numbered.n from numbered where numbered.id = g.id and g.attempt_number <> numbered.n;

alter table public.game_results drop constraint if exists game_results_answers_add_up;
alter table public.game_results add constraint game_results_answers_add_up
  check (incorrect_answers >= 0 and correct_answers + incorrect_answers = total_questions and attempt_number >= 1);
create index if not exists game_results_class_idx on public.game_results (class_id, completed_at desc);

create table if not exists public.question_results (
  id              bigint generated always as identity primary key,
  result_id       bigint not null references public.game_results (id) on delete cascade,
  user_id         uuid not null references auth.users (id) on delete cascade,
  class_id        uuid references public.classes (id) on delete set null,
  level_id        text not null references public.activities (level_id),
  question_id     text not null references public.questions (id),
  correct         boolean not null,              -- the first answer
  recovered       boolean not null default false,-- a wrong first answer fixed in the follow-up
  hint_used       boolean not null default false,
  answer_seconds  real not null default 0 check (answer_seconds >= 0 and answer_seconds < 36000),
  completed_at    timestamptz not null,
  unique (result_id, question_id)
);
create index if not exists question_results_user_idx  on public.question_results (user_id, question_id);
create index if not exists question_results_class_idx on public.question_results (class_id, question_id);

-- History is never edited (not even by functions running as the owner).
create or replace function public.results_are_final() returns trigger
language plpgsql set search_path = '' as $$
begin
  raise exception 'Results can''t be changed after they are recorded.' using errcode = '42501';
end $$;
drop trigger if exists game_results_final on public.game_results;
create trigger game_results_final before update on public.game_results
  for each row execute function public.results_are_final();
drop trigger if exists question_results_final on public.question_results;
create trigger question_results_final before update on public.question_results
  for each row execute function public.results_are_final();

-- ---------- Helper checks (security definer so policies can use them without recursion) ----------
create or replace function public.is_teacher() returns boolean
language sql stable security definer set search_path = '' as $$
  select exists (select 1 from public.user_roles r where r.user_id = auth.uid() and r.role = 'teacher');
$$;

-- p_student is currently enrolled in one of the caller's classes.
create or replace function public.teaches_student(p_student uuid) returns boolean
language sql stable security definer set search_path = '' as $$
  select exists (
    select 1 from public.class_members m join public.classes c on c.id = m.class_id
     where m.student_id = p_student and m.removed_at is null and c.teacher_id = auth.uid());
$$;

-- p_student was ever enrolled in one of the caller's classes (roster shows removed students too).
create or replace function public.ever_taught_student(p_student uuid) returns boolean
language sql stable security definer set search_path = '' as $$
  select exists (
    select 1 from public.class_members m join public.classes c on c.id = m.class_id
     where m.student_id = p_student and c.teacher_id = auth.uid());
$$;

-- A row recorded in p_class by p_student is visible to the caller as that class's teacher.
create or replace function public.teaches_in_class(p_class uuid, p_student uuid) returns boolean
language sql stable security definer set search_path = '' as $$
  select p_class is not null and exists (
    select 1 from public.class_members m join public.classes c on c.id = m.class_id
     where c.id = p_class and c.teacher_id = auth.uid() and m.student_id = p_student and m.removed_at is null);
$$;

create or replace function public.is_class_member(p_class uuid) returns boolean
language sql stable security definer set search_path = '' as $$
  select exists (select 1 from public.class_members m
                  where m.class_id = p_class and m.student_id = auth.uid() and m.removed_at is null);
$$;

-- ---------- Row Level Security ----------
alter table public.user_roles          enable row level security;
alter table public.teacher_invites     enable row level security;
alter table public.classroom_attempts  enable row level security;
alter table public.classes             enable row level security;
alter table public.class_members       enable row level security;
alter table public.activities          enable row level security;
alter table public.questions           enable row level security;
alter table public.question_results    enable row level security;

drop policy if exists "user_roles: read own" on public.user_roles;
create policy "user_roles: read own" on public.user_roles for select to authenticated
  using ((select auth.uid()) = user_id);

drop policy if exists "activities: read" on public.activities;
create policy "activities: read" on public.activities for select to authenticated using (true);
drop policy if exists "questions: read" on public.questions;
create policy "questions: read" on public.questions for select to authenticated using (true);

drop policy if exists "classes: teacher reads own" on public.classes;
drop policy if exists "classes: student reads own class" on public.classes;
create policy "classes: teacher reads own" on public.classes for select to authenticated
  using ((select auth.uid()) = teacher_id);
create policy "classes: student reads own class" on public.classes for select to authenticated
  using (public.is_class_member(id));

drop policy if exists "class_members: student reads own" on public.class_members;
drop policy if exists "class_members: teacher reads roster" on public.class_members;
create policy "class_members: student reads own" on public.class_members for select to authenticated
  using ((select auth.uid()) = student_id);
create policy "class_members: teacher reads roster" on public.class_members for select to authenticated
  using (exists (select 1 from public.classes c where c.id = class_id and c.teacher_id = (select auth.uid())));

drop policy if exists "question_results: read own" on public.question_results;
drop policy if exists "question_results: teacher reads class" on public.question_results;
create policy "question_results: read own" on public.question_results for select to authenticated
  using ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "question_results: teacher reads class" on public.question_results for select to authenticated
  using (public.teaches_in_class(class_id, user_id));

-- Teachers' read access to the existing tables (students keep their own-row policies).
drop policy if exists "profiles: teacher reads students" on public.profiles;
create policy "profiles: teacher reads students" on public.profiles for select to authenticated
  using (public.ever_taught_student(id));
drop policy if exists "level_progress: teacher reads students" on public.level_progress;
create policy "level_progress: teacher reads students" on public.level_progress for select to authenticated
  using (public.teaches_student(user_id));
drop policy if exists "achievements: teacher reads students" on public.achievements;
create policy "achievements: teacher reads students" on public.achievements for select to authenticated
  using (public.teaches_student(user_id));
drop policy if exists "game_results: teacher reads class" on public.game_results;
create policy "game_results: teacher reads class" on public.game_results for select to authenticated
  using (public.teaches_in_class(class_id, user_id));

-- Results are written only through submit_game_result (below).
drop policy if exists "game_results: guests and accounts insert own" on public.game_results;

-- ---------- Privileges ----------
revoke all on public.user_roles, public.teacher_invites, public.classroom_attempts, public.classes, public.class_members,
              public.activities, public.questions, public.question_results from anon, authenticated;
revoke insert on public.game_results from authenticated;
grant select on public.user_roles, public.classes, public.class_members, public.activities, public.questions,
                public.question_results to authenticated;
-- teacher_invites and classroom_attempts: no access from the game at all.

-- ---------- Functions: roles ----------
create or replace function public.my_role() returns text
language sql stable security definer set search_path = '' as $$
  select coalesce((select r.role from public.user_roles r where r.user_id = auth.uid()), 'student');
$$;

create or replace function public.invite_hash(p_code text) returns text
language sql immutable set search_path = '' as $$
  select encode(sha256(convert_to(upper(regexp_replace(coalesce(p_code, ''), '\s', '', 'g')), 'UTF8')), 'hex');
$$;

-- Dashboard / SQL editor only: select public.add_teacher_invite('SECRET-CODE', 'Ms. Santos', 1, 30);
create or replace function public.add_teacher_invite(p_code text, p_label text default null, p_max_uses integer default 1,
                                                     p_valid_days integer default 30) returns void
language plpgsql security definer set search_path = '' as $$
begin
  if char_length(regexp_replace(coalesce(p_code, ''), '\s', '', 'g')) < 10 then
    raise exception 'Use an invite code of at least 10 characters.' using errcode = '22023';
  end if;
  insert into public.teacher_invites (code_hash, label, max_uses, expires_at)
  values (public.invite_hash(p_code), p_label, p_max_uses,
          case when p_valid_days is null then null else now() + make_interval(days => p_valid_days) end)
  on conflict (code_hash) do update set label = excluded.label, max_uses = excluded.max_uses, expires_at = excluded.expires_at;
end $$;

-- An account (never a guest) enters a teacher invite code. Returns {ok, role} or {ok:false, error}.
create or replace function public.claim_teacher_role(p_code text) returns jsonb
language plpgsql security definer set search_path = '' as $$
declare
  uid uuid := auth.uid();
  inv public.teacher_invites;
  fails integer;
begin
  if uid is null then raise exception 'Sign in first.' using errcode = '42501'; end if;
  if exists (select 1 from auth.users u where u.id = uid and u.is_anonymous) then
    return jsonb_build_object('ok', false, 'error', 'Guests can''t be teachers. Create an account first.');
  end if;
  if public.is_teacher() then return jsonb_build_object('ok', true, 'role', 'teacher'); end if;
  select count(*) into fails from public.classroom_attempts
   where user_id = uid and kind = 'invite' and at > now() - interval '15 minutes';
  if fails >= 5 then
    return jsonb_build_object('ok', false, 'error', 'Too many wrong codes. Wait 15 minutes and try again.');
  end if;
  select * into inv from public.teacher_invites
   where code_hash = public.invite_hash(p_code) and uses < max_uses and (expires_at is null or expires_at > now())
   for update;
  if not found then
    insert into public.classroom_attempts (user_id, kind) values (uid, 'invite');
    return jsonb_build_object('ok', false, 'error', 'That teacher code isn''t valid (or it was already used).');
  end if;
  update public.teacher_invites set uses = uses + 1 where code_hash = inv.code_hash;
  insert into public.user_roles (user_id, role) values (uid, 'teacher')
  on conflict (user_id) do update set role = 'teacher', granted_at = now();
  -- A teacher isn't also a student in a class.
  update public.class_members set removed_at = now(), removed_by = 'student' where student_id = uid and removed_at is null;
  return jsonb_build_object('ok', true, 'role', 'teacher');
end $$;

-- ---------- Functions: class codes and classes ----------
create or replace function public.new_class_code() returns text
language plpgsql volatile set search_path = '' as $$
declare
  alphabet constant text := 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';   -- 32 characters: every byte maps evenly
  b bytea := uuid_send(gen_random_uuid());                         -- bytes 0-5 of a v4 UUID are random
  code text := '';
begin
  for i in 0..5 loop
    code := code || substr(alphabet, (get_byte(b, i) % 32) + 1, 1);
  end loop;
  return code;
end $$;

create or replace function public.normalize_class_code(p_code text) returns text
language sql immutable set search_path = '' as $$
  select upper(regexp_replace(coalesce(p_code, ''), '[^A-Za-z0-9]', '', 'g'));
$$;

create or replace function public.create_class(p_name text) returns jsonb
language plpgsql security definer set search_path = '' as $$
declare
  r public.classes;
  n integer;
  tries integer := 0;
begin
  if not public.is_teacher() then raise exception 'Only teachers can create classes.' using errcode = '42501'; end if;
  p_name := trim(coalesce(p_name, ''));
  if char_length(p_name) < 2 or char_length(p_name) > 60 then
    raise exception 'Class names are 2 to 60 characters long.' using errcode = '22023';
  end if;
  select count(*) into n from public.classes where teacher_id = auth.uid() and not archived;
  if n >= 30 then raise exception 'You already have 30 active classes. Archive one first.' using errcode = '22023'; end if;
  loop
    begin
      insert into public.classes (teacher_id, name, code) values (auth.uid(), p_name, public.new_class_code()) returning * into r;
      return to_jsonb(r);
    exception when unique_violation then
      tries := tries + 1;
      if tries >= 10 then raise; end if;
    end;
  end loop;
end $$;

create or replace function public.set_class_archived(p_class uuid, p_archived boolean) returns void
language plpgsql security definer set search_path = '' as $$
begin
  if not public.is_teacher() then raise exception 'Only teachers can change classes.' using errcode = '42501'; end if;
  update public.classes set archived = coalesce(p_archived, true) where id = p_class and teacher_id = auth.uid();
  if not found then raise exception 'That class isn''t one of yours.' using errcode = '42501'; end if;
end $$;

create or replace function public.remove_student(p_class uuid, p_student uuid) returns void
language plpgsql security definer set search_path = '' as $$
begin
  if not public.is_teacher() then raise exception 'Only teachers can remove students.' using errcode = '42501'; end if;
  if not exists (select 1 from public.classes c where c.id = p_class and c.teacher_id = auth.uid()) then
    raise exception 'That class isn''t one of yours.' using errcode = '42501';
  end if;
  update public.class_members set removed_at = now(), removed_by = 'teacher'
   where class_id = p_class and student_id = p_student and removed_at is null;
end $$;

create or replace function public.readmit_student(p_class uuid, p_student uuid) returns void
language plpgsql security definer set search_path = '' as $$
begin
  if not public.is_teacher() then raise exception 'Only teachers can re-admit students.' using errcode = '42501'; end if;
  if not exists (select 1 from public.classes c where c.id = p_class and c.teacher_id = auth.uid()) then
    raise exception 'That class isn''t one of yours.' using errcode = '42501';
  end if;
  if exists (select 1 from public.class_members m where m.student_id = p_student and m.removed_at is null and m.class_id <> p_class) then
    raise exception 'This student has joined another class since. They can rejoin yours with the class code.' using errcode = '22023';
  end if;
  update public.class_members set removed_at = null, removed_by = null
   where class_id = p_class and student_id = p_student and removed_at is not null;
end $$;

-- ---------- Functions: students ----------
-- Join with a class code. Returns {ok, class_id, class_name, teacher_name} or {ok:false, error}.
create or replace function public.join_class(p_code text) returns jsonb
language plpgsql security definer set search_path = '' as $$
declare
  uid uuid := auth.uid();
  c public.classes;
  m public.class_members;
  fails integer;
  teacher text;
begin
  if uid is null then raise exception 'Sign in first.' using errcode = '42501'; end if;
  if exists (select 1 from auth.users u where u.id = uid and u.is_anonymous) then
    return jsonb_build_object('ok', false, 'error', 'Create an account (or sign in) to join a class - your guest progress comes with you.');
  end if;
  if public.is_teacher() then
    return jsonb_build_object('ok', false, 'error', 'Teacher accounts can''t join a class as a student.');
  end if;
  select count(*) into fails from public.classroom_attempts
   where user_id = uid and kind = 'join' and at > now() - interval '10 minutes';
  if fails >= 10 then
    return jsonb_build_object('ok', false, 'error', 'Too many wrong codes. Wait 10 minutes and try again.');
  end if;
  select * into c from public.classes where code = public.normalize_class_code(p_code) and not archived;
  if not found then
    insert into public.classroom_attempts (user_id, kind) values (uid, 'join');
    return jsonb_build_object('ok', false, 'error', 'No class has that code. Check it with your teacher.');
  end if;
  select * into m from public.class_members where class_id = c.id and student_id = uid;
  if found and m.removed_at is not null and m.removed_by = 'teacher' then
    return jsonb_build_object('ok', false, 'error', 'Your teacher removed you from this class. Ask them to re-admit you.');
  end if;
  -- One class at a time: joining a new class leaves the old one (its records stay with that class).
  update public.class_members set removed_at = now(), removed_by = 'student'
   where student_id = uid and removed_at is null and class_id <> c.id;
  insert into public.class_members (class_id, student_id) values (c.id, uid)
  on conflict (class_id, student_id) do update set removed_at = null, removed_by = null,
    joined_at = case when public.class_members.removed_at is null then public.class_members.joined_at else now() end;
  select p.display_name into teacher from public.profiles p where p.id = c.teacher_id;
  return jsonb_build_object('ok', true, 'class_id', c.id, 'class_name', c.name, 'teacher_name', coalesce(teacher, 'Teacher'));
end $$;

create or replace function public.leave_class() returns void
language plpgsql security definer set search_path = '' as $$
begin
  update public.class_members set removed_at = now(), removed_by = 'student'
   where student_id = auth.uid() and removed_at is null;
end $$;

-- The caller's current class: {class_id, class_name, teacher_name, joined_at} or null.
create or replace function public.my_class() returns jsonb
language sql stable security definer set search_path = '' as $$
  select jsonb_build_object('class_id', c.id, 'class_name', c.name, 'teacher_name', coalesce(p.display_name, 'Teacher'),
                            'joined_at', m.joined_at)
    from public.class_members m
    join public.classes c on c.id = m.class_id
    left join public.profiles p on p.id = c.teacher_id
   where m.student_id = auth.uid() and m.removed_at is null
   limit 1;
$$;

-- ---------- Functions: recording a finished run ----------
-- p_run: {run_id, level_id, score, max_score, stars, correct_answers, total_questions, passed, completed,
--         total_answer_seconds, completed_at, questions: [{question_id, correct, recovered, hint_used, seconds}]}
-- Returns {status: "recorded" | "duplicate" | "limit", result_id, attempt_number}. Invalid runs raise 22023.
create or replace function public.submit_game_result(p_run jsonb) returns jsonb
language plpgsql security definer set search_path = '' as $$
declare
  uid        uuid := auth.uid();
  act        public.activities;
  v_run      uuid;
  v_level    text := p_run ->> 'level_id';
  v_score    integer := (p_run ->> 'score')::integer;
  v_max      integer := (p_run ->> 'max_score')::integer;
  v_stars    integer := coalesce((p_run ->> 'stars')::integer, 0);
  v_correct  integer := (p_run ->> 'correct_answers')::integer;
  v_total    integer := (p_run ->> 'total_questions')::integer;
  v_passed   boolean := coalesce((p_run ->> 'passed')::boolean, false);
  v_complete boolean := coalesce((p_run ->> 'completed')::boolean, true);
  v_seconds  real := greatest(0, coalesce((p_run ->> 'total_answer_seconds')::real, 0));
  v_at       timestamptz := coalesce((p_run ->> 'completed_at')::timestamptz, now());
  v_qs       jsonb := coalesce(p_run -> 'questions', '[]'::jsonb);
  v_qcount   integer;
  v_class    uuid;
  v_id       bigint;
  v_attempt  integer;
  bad        text;
begin
  if uid is null or (select auth.jwt() ->> 'is_anonymous') is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  begin
    v_run := (p_run ->> 'run_id')::uuid;
  exception when others then
    raise exception 'The run id is missing or not valid.' using errcode = '22023';
  end;
  if v_run is null then raise exception 'The run id is missing.' using errcode = '22023'; end if;

  select * into act from public.activities where level_id = v_level;
  if not found then raise exception 'Unknown level %.', coalesce(v_level, '(none)') using errcode = '22023'; end if;
  if v_total is distinct from act.question_count then
    raise exception 'Level % has % questions, not %.', v_level, act.question_count, v_total using errcode = '22023';
  end if;
  if v_max is distinct from act.max_score then
    raise exception 'Level % is scored out of %, not %.', v_level, act.max_score, v_max using errcode = '22023';
  end if;
  if v_score is null or v_score < 0 or v_score > act.max_score then
    raise exception 'Score % is outside 0-% for %.', v_score, act.max_score, v_level using errcode = '22023';
  end if;
  if v_correct is null or v_correct < 0 or v_correct > v_total then
    raise exception 'Correct answers % is outside 0-%.', v_correct, v_total using errcode = '22023';
  end if;
  if v_stars < 0 or v_stars > 3 then raise exception 'Stars must be 0-3.' using errcode = '22023'; end if;
  if v_passed <> (v_score >= act.pass_score) then
    raise exception 'The pass flag doesn''t match the pass score (%).', act.pass_score using errcode = '22023';
  end if;
  if v_at > now() + interval '10 minutes' or v_at < now() - interval '365 days' then
    raise exception 'The finish time is not plausible.' using errcode = '22023';
  end if;

  -- Per-question rows (older queued runs may have none): each question once, all from this level,
  -- and their first answers must add up to the run's correct count.
  if jsonb_typeof(v_qs) <> 'array' then raise exception 'questions must be a list.' using errcode = '22023'; end if;
  v_qcount := jsonb_array_length(v_qs);
  if v_qcount > 0 then
    if v_qcount > v_total or (v_complete and v_qcount <> v_total) then
      raise exception 'Expected % question results, got %.', v_total, v_qcount using errcode = '22023';
    end if;
    if (select count(distinct q ->> 'question_id') from jsonb_array_elements(v_qs) q) <> v_qcount then
      raise exception 'A question appears twice in the run.' using errcode = '22023';
    end if;
    select q ->> 'question_id' into bad from jsonb_array_elements(v_qs) q
     where not exists (select 1 from public.questions x where x.id = q ->> 'question_id' and x.level_id = v_level) limit 1;
    if bad is not null then
      raise exception 'Question % is not part of %.', bad, v_level using errcode = '22023';
    end if;
    if (select count(*) from jsonb_array_elements(v_qs) q where coalesce((q ->> 'correct')::boolean, false)) <> v_correct then
      raise exception 'The question results don''t add up to % correct answers.', v_correct using errcode = '22023';
    end if;
  end if;

  -- Already recorded (a retried upload): report it, change nothing.
  select g.id, g.attempt_number into v_id, v_attempt from public.game_results g where g.user_id = uid and g.run_id = v_run;
  if found then
    return jsonb_build_object('status', 'duplicate', 'result_id', v_id, 'attempt_number', v_attempt);
  end if;

  -- One upload at a time per player, so attempt numbers never collide.
  perform pg_advisory_xact_lock(hashtextextended(uid::text, 0));
  select m.class_id into v_class from public.class_members m join public.classes c on c.id = m.class_id
   where m.student_id = uid and m.removed_at is null and not c.archived limit 1;
  select coalesce(max(g.attempt_number), 0) + 1 into v_attempt from public.game_results g
   where g.user_id = uid and g.level_id = v_level;

  insert into public.game_results (user_id, run_id, level_id, score, max_score, stars, correct_answers, total_questions,
                                   incorrect_answers, passed, completed, total_answer_seconds, completed_at, class_id, attempt_number)
  values (uid, v_run, v_level, v_score, v_max, v_stars, v_correct, v_total, v_total - v_correct, v_passed, v_complete,
          v_seconds, v_at, v_class, v_attempt)
  on conflict (user_id, run_id) do nothing
  returning id into v_id;

  if v_id is null then
    -- Either a concurrent upload of the same run, or the per-player history limit (1000 runs) was reached.
    select g.id, g.attempt_number into v_id, v_attempt from public.game_results g where g.user_id = uid and g.run_id = v_run;
    if found then return jsonb_build_object('status', 'duplicate', 'result_id', v_id, 'attempt_number', v_attempt); end if;
    return jsonb_build_object('status', 'limit');
  end if;

  insert into public.question_results (result_id, user_id, class_id, level_id, question_id, correct, recovered, hint_used,
                                       answer_seconds, completed_at)
  select v_id, uid, v_class, v_level, q ->> 'question_id', coalesce((q ->> 'correct')::boolean, false),
         coalesce((q ->> 'recovered')::boolean, false), coalesce((q ->> 'hint_used')::boolean, false),
         least(35999, greatest(0, coalesce((q ->> 'seconds')::real, 0))), v_at
    from jsonb_array_elements(v_qs) q;

  return jsonb_build_object('status', 'recorded', 'result_id', v_id, 'attempt_number', v_attempt, 'class_id', v_class);
end $$;

-- ---------- Who may call what ----------
revoke execute on function public.results_are_final()                    from public, anon, authenticated;
revoke execute on function public.add_teacher_invite(text, text, integer, integer) from public, anon, authenticated;
revoke execute on function public.invite_hash(text)                      from public, anon, authenticated;
revoke execute on function public.new_class_code()                       from public, anon, authenticated;
revoke execute on function public.is_teacher()                           from public, anon;
revoke execute on function public.teaches_student(uuid)                  from public, anon;
revoke execute on function public.ever_taught_student(uuid)              from public, anon;
revoke execute on function public.teaches_in_class(uuid, uuid)           from public, anon;
revoke execute on function public.is_class_member(uuid)                  from public, anon;
revoke execute on function public.normalize_class_code(text)             from public, anon;
revoke execute on function public.my_role()                              from public, anon;
revoke execute on function public.claim_teacher_role(text)               from public, anon;
revoke execute on function public.create_class(text)                     from public, anon;
revoke execute on function public.set_class_archived(uuid, boolean)      from public, anon;
revoke execute on function public.remove_student(uuid, uuid)             from public, anon;
revoke execute on function public.readmit_student(uuid, uuid)            from public, anon;
revoke execute on function public.join_class(text)                       from public, anon;
revoke execute on function public.leave_class()                          from public, anon;
revoke execute on function public.my_class()                             from public, anon;
revoke execute on function public.submit_game_result(jsonb)              from public, anon;

grant execute on function public.is_teacher(), public.teaches_student(uuid), public.ever_taught_student(uuid),
                          public.teaches_in_class(uuid, uuid), public.is_class_member(uuid), public.normalize_class_code(text),
                          public.my_role(), public.claim_teacher_role(text), public.create_class(text),
                          public.set_class_archived(uuid, boolean), public.remove_student(uuid, uuid),
                          public.readmit_student(uuid, uuid), public.join_class(text), public.leave_class(),
                          public.my_class(), public.submit_game_result(jsonb)
  to authenticated;
