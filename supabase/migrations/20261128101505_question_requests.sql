-- =====================================================================================
-- Scam Sense: Admin / Teacher / Student question management with approval.
-- Run after 20261128004018_question_bank.sql. Safe to run again.
--
-- Roles (all kept on the server; the game never decides them):
--   Admin    a row in public.content_admins (added in the SQL editor, never from the game).
--            Full control of public.question_bank: add, edit, delete, turn on/off, move between
--            levels; reviews teacher requests; reads the change history.
--   Teacher  public.user_roles.role = 'teacher' (invite code). Reads every question, but can NOT
--            change public.question_bank. Submits ADD / EDIT / DELETE requests with a reason,
--            tracks them, and may withdraw a pending one.
--   Student  everyone else (guests included). Plays the published questions only
--            (get_content_bundle); answers are recorded in public.question_results.
--
-- What it changes
--   question_bank / content_levels: insert, update and delete become ADMIN-ONLY (teachers had
--   direct write access before). Teachers keep read access.
--   question_requests: one row per request; written only through the functions below.
--
-- Functions
--   submit_question_request(type, question_id, proposed, reason)  teacher -> {ok, request_id | error}
--   withdraw_question_request(id)                                  teacher, own pending request
--   my_question_requests()                                         teacher, own requests (+ current question)
--   admin_question_requests(status)                                admin, all requests with names
--   review_question_request(id, approve, remarks)                  admin; approval applies the change
--                                                                  in the same transaction
--   question_history(question_id)                                  admin, previous versions with names
-- =====================================================================================

-- ---------- Table ----------
create table if not exists public.question_requests (
  id               uuid primary key default gen_random_uuid(),
  teacher_id       uuid not null references auth.users (id) on delete cascade,
  request_type     text not null check (request_type in ('add', 'edit', 'delete')),
  question_id      text not null check (question_id ~ '^[A-Za-z0-9_-]{1,40}$'),
  -- The proposed question_bank row (level_id, sort_order, is_active, category, difficulty,
  -- question_type, message_type, content). Null for a delete request.
  proposed         jsonb check (proposed is null or jsonb_typeof(proposed) = 'object'),
  -- The question's updated_at when the request was made (edit / delete), to spot later changes.
  base_updated_at  timestamptz,
  reason           text not null check (char_length(btrim(reason)) between 5 and 1000),
  status           text not null default 'pending' check (status in ('pending', 'approved', 'rejected', 'withdrawn')),
  admin_remarks    text check (admin_remarks is null or char_length(admin_remarks) <= 1000),
  reviewed_by      uuid references auth.users (id) on delete set null,
  created_at       timestamptz not null default now(),
  reviewed_at      timestamptz,
  check ((request_type = 'delete') = (proposed is null))
);
create index if not exists question_requests_status_idx  on public.question_requests (status, created_at);
create index if not exists question_requests_teacher_idx on public.question_requests (teacher_id, created_at desc);
-- One pending request per teacher per question (no duplicates piling up for the admin).
create unique index if not exists question_requests_one_pending
  on public.question_requests (teacher_id, question_id) where status = 'pending';

alter table public.question_requests enable row level security;
drop policy if exists "question_requests: teacher reads own" on public.question_requests;
drop policy if exists "question_requests: admin reads all" on public.question_requests;
create policy "question_requests: teacher reads own" on public.question_requests for select to authenticated
  using ((select auth.uid()) = teacher_id);
create policy "question_requests: admin reads all" on public.question_requests for select to authenticated
  using (public.is_content_admin());
revoke all on public.question_requests from anon, authenticated;
grant select on public.question_requests to authenticated;   -- rows only through the functions below

-- ---------- Questions: writes become admin-only ----------
drop policy if exists "question_bank: editors insert" on public.question_bank;
drop policy if exists "question_bank: editors update" on public.question_bank;
drop policy if exists "question_bank: admins insert" on public.question_bank;
drop policy if exists "question_bank: admins update" on public.question_bank;
create policy "question_bank: admins insert" on public.question_bank for insert to authenticated
  with check (public.is_content_admin());
create policy "question_bank: admins update" on public.question_bank for update to authenticated
  using (public.is_content_admin()) with check (public.is_content_admin());
-- (read: "question_bank: editors read" - teachers and admins; delete: "question_bank: admins delete")

drop policy if exists "content_levels: editors update" on public.content_levels;
drop policy if exists "content_levels: admins update" on public.content_levels;
create policy "content_levels: admins update" on public.content_levels for update to authenticated
  using (public.is_content_admin()) with check (public.is_content_admin());

-- ---------- Helpers ----------
-- Checks a proposed row the way question_bank's trigger will, so a teacher hears about problems at once.
create or replace function public.question_row_problem(p_row jsonb) returns text
language plpgsql stable set search_path = '' as $$
declare
  v_type  text := p_row ->> 'message_type';
  v_level text := nullif(p_row ->> 'level_id', '');
begin
  if p_row is null or jsonb_typeof(p_row) <> 'object' then return 'the proposed question is missing'; end if;
  if jsonb_typeof(p_row -> 'content') is distinct from 'object' then return 'the proposed question has no content'; end if;
  if v_type is null or v_type not in ('SMS', 'Email', 'Website', 'Case', 'Chat', 'Question') then return 'message_type is not valid'; end if;
  if coalesce(p_row ->> 'difficulty', 'Easy') not in ('Easy', 'Medium', 'Hard', '') then return 'difficulty must be Easy, Medium or Hard'; end if;
  if v_level is not null and not exists (select 1 from public.activities a where a.level_id = v_level) then return format('level %s does not exist', v_level); end if;
  if coalesce((p_row ->> 'is_active')::boolean, true) then return public.question_problem(v_type, p_row -> 'content'); end if;
  return '';
end $$;

-- Writes a proposed row into question_bank (insert or replace). Runs as the function owner, so only
-- call it from functions that have already checked the caller is an admin.
create or replace function public.apply_question_row(p_question text, p_row jsonb, p_insert boolean) returns void
language plpgsql security definer set search_path = '' as $$
begin
  if p_insert then
    insert into public.question_bank (question_id, level_id, sort_order, is_active, category, difficulty, question_type, message_type, content)
    values (p_question, nullif(p_row ->> 'level_id', ''), coalesce((p_row ->> 'sort_order')::integer, 0),
            coalesce((p_row ->> 'is_active')::boolean, true), coalesce(p_row ->> 'category', ''),
            coalesce(p_row ->> 'difficulty', 'Easy'), coalesce(p_row ->> 'question_type', ''), p_row ->> 'message_type', p_row -> 'content');
  else
    update public.question_bank set
      level_id = nullif(p_row ->> 'level_id', ''), sort_order = coalesce((p_row ->> 'sort_order')::integer, 0),
      is_active = coalesce((p_row ->> 'is_active')::boolean, true), category = coalesce(p_row ->> 'category', ''),
      difficulty = coalesce(p_row ->> 'difficulty', 'Easy'), question_type = coalesce(p_row ->> 'question_type', ''),
      message_type = p_row ->> 'message_type', content = p_row -> 'content'
    where question_id = p_question;
    if not found then raise exception 'Question % no longer exists.', p_question using errcode = '22023'; end if;
  end if;
end $$;

-- ---------- Teacher functions ----------
create or replace function public.submit_question_request(p_type text, p_question text, p_proposed jsonb, p_reason text)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
  uid      uuid := auth.uid();
  v_base   timestamptz;
  v_exists boolean;
  problem  text;
  v_id     uuid;
begin
  if uid is null or coalesce((auth.jwt() ->> 'is_anonymous')::boolean, false) or not public.is_teacher() then
    raise exception 'Only teachers can request question changes.' using errcode = '42501';
  end if;
  if p_type not in ('add', 'edit', 'delete') then return jsonb_build_object('ok', false, 'error', 'The request type must be add, edit or delete.'); end if;
  if p_question is null or p_question !~ '^[A-Za-z0-9_-]{1,40}$' then return jsonb_build_object('ok', false, 'error', 'The question id may only use letters, digits, - and _.'); end if;
  if char_length(btrim(coalesce(p_reason, ''))) < 5 then return jsonb_build_object('ok', false, 'error', 'Explain the reason for this request (at least 5 characters).'); end if;
  if char_length(p_reason) > 1000 then return jsonb_build_object('ok', false, 'error', 'The reason is too long (1000 characters at most).'); end if;
  if (select count(*) from public.question_requests r where r.teacher_id = uid and r.status = 'pending') >= 50 then
    return jsonb_build_object('ok', false, 'error', 'You have 50 pending requests. Wait for the admin to review some first.');
  end if;
  if exists (select 1 from public.question_requests r where r.teacher_id = uid and r.question_id = p_question and r.status = 'pending') then
    return jsonb_build_object('ok', false, 'error', format('You already have a pending request for %s. Withdraw it first to send a new one.', p_question));
  end if;

  select true, q.updated_at into v_exists, v_base from public.question_bank q where q.question_id = p_question;
  v_exists := coalesce(v_exists, false);
  if p_type = 'add' then
    if v_exists or exists (select 1 from public.question_requests r where r.question_id = p_question and r.request_type = 'add' and r.status = 'pending') then
      return jsonb_build_object('ok', false, 'error', format('The id %s is already used. Pick another one.', p_question));
    end if;
  elsif not v_exists then
    return jsonb_build_object('ok', false, 'error', format('Question %s does not exist.', p_question));
  end if;

  if p_type = 'delete' then
    p_proposed := null;
  else
    problem := public.question_row_problem(p_proposed);
    if problem <> '' then return jsonb_build_object('ok', false, 'error', format('Question %s: %s', p_question, problem)); end if;
  end if;

  insert into public.question_requests (teacher_id, request_type, question_id, proposed, base_updated_at, reason)
  values (uid, p_type, p_question, p_proposed, v_base, btrim(p_reason))
  returning id into v_id;
  return jsonb_build_object('ok', true, 'request_id', v_id);
end $$;

create or replace function public.withdraw_question_request(p_request uuid) returns jsonb
language plpgsql security definer set search_path = '' as $$
begin
  update public.question_requests set status = 'withdrawn', reviewed_at = now()
   where id = p_request and teacher_id = auth.uid() and status = 'pending';
  if not found then return jsonb_build_object('ok', false, 'error', 'Only your own pending requests can be withdrawn.'); end if;
  return jsonb_build_object('ok', true);
end $$;

-- The request list with what a reviewer needs: names, the question as it is now, and whether it changed since.
create or replace function public.question_requests_view(p_teacher uuid, p_status text) returns jsonb
language sql stable security definer set search_path = '' as $$
  select coalesce(jsonb_agg(jsonb_build_object(
           'id', r.id, 'teacher_id', r.teacher_id, 'teacher_name', coalesce(tp.display_name, 'Teacher'),
           'request_type', r.request_type, 'question_id', r.question_id, 'proposed', r.proposed,
           'reason', r.reason, 'status', r.status, 'admin_remarks', coalesce(r.admin_remarks, ''),
           'reviewer_name', coalesce(rp.display_name, ''), 'created_at', r.created_at, 'reviewed_at', r.reviewed_at,
           'current', case when q.question_id is null then null else jsonb_build_object(
               'level_id', q.level_id, 'sort_order', q.sort_order, 'is_active', q.is_active, 'category', q.category,
               'difficulty', q.difficulty, 'question_type', q.question_type, 'message_type', q.message_type, 'content', q.content) end,
           'changed_since', r.base_updated_at is not null and q.updated_at is distinct from r.base_updated_at)
         order by (r.status = 'pending') desc, r.created_at desc), '[]'::jsonb)
    from public.question_requests r
    left join public.profiles tp on tp.id = r.teacher_id
    left join public.profiles rp on rp.id = r.reviewed_by
    left join public.question_bank q on q.question_id = r.question_id
   where (p_teacher is null or r.teacher_id = p_teacher)
     and (p_status is null or p_status = '' or r.status = p_status);
$$;

create or replace function public.my_question_requests() returns jsonb
language plpgsql stable security definer set search_path = '' as $$
begin
  if auth.uid() is null then raise exception 'Sign in first.' using errcode = '42501'; end if;
  return public.question_requests_view(auth.uid(), null);
end $$;

-- ---------- Admin functions ----------
create or replace function public.admin_question_requests(p_status text default 'pending') returns jsonb
language plpgsql stable security definer set search_path = '' as $$
begin
  if not public.is_content_admin() then raise exception 'Only admins can review requests.' using errcode = '42501'; end if;
  return public.question_requests_view(null, p_status);
end $$;

create or replace function public.review_question_request(p_request uuid, p_approve boolean, p_remarks text default null)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
  r public.question_requests;
begin
  if not public.is_content_admin() then raise exception 'Only admins can review requests.' using errcode = '42501'; end if;
  select * into r from public.question_requests where id = p_request for update;
  if not found then return jsonb_build_object('ok', false, 'error', 'That request was not found.'); end if;
  if r.status <> 'pending' then return jsonb_build_object('ok', false, 'error', format('That request was already %s.', r.status)); end if;
  if not p_approve and char_length(btrim(coalesce(p_remarks, ''))) < 3 then
    return jsonb_build_object('ok', false, 'error', 'Add a remark so the teacher knows why it was rejected.');
  end if;

  if p_approve then
    begin
      if r.request_type = 'add' then
        if exists (select 1 from public.question_bank q where q.question_id = r.question_id) then
          return jsonb_build_object('ok', false, 'error', format('The id %s is already used by another question. Reject this request and ask for a new id.', r.question_id));
        end if;
        perform public.apply_question_row(r.question_id, r.proposed, true);
      elsif r.request_type = 'edit' then
        perform public.apply_question_row(r.question_id, r.proposed, false);
      else
        delete from public.question_bank where question_id = r.question_id;
      end if;
    exception when sqlstate '22023' then
      return jsonb_build_object('ok', false, 'error', 'Not applied: ' || sqlerrm);
    end;
  end if;

  update public.question_requests
     set status = case when p_approve then 'approved' else 'rejected' end,
         admin_remarks = nullif(btrim(coalesce(p_remarks, '')), ''), reviewed_by = auth.uid(), reviewed_at = now()
   where id = r.id;
  return jsonb_build_object('ok', true, 'status', case when p_approve then 'approved' else 'rejected' end);
end $$;

create or replace function public.question_history(p_question text) returns jsonb
language plpgsql stable security definer set search_path = '' as $$
begin
  if not public.is_content_admin() then raise exception 'Only admins can read the history.' using errcode = '42501'; end if;
  return (select coalesce(jsonb_agg(jsonb_build_object(
            'operation', h.operation, 'changed_at', h.changed_at, 'changed_by', coalesce(p.display_name, ''),
            'old_row', h.old_row) order by h.changed_at desc), '[]'::jsonb)
            from public.question_bank_history h left join public.profiles p on p.id = h.changed_by
           where h.question_id = p_question);
end $$;

-- ---------- Who may call what ----------
revoke execute on function public.question_row_problem(jsonb)                          from public, anon;
revoke execute on function public.apply_question_row(text, jsonb, boolean)              from public, anon, authenticated;
revoke execute on function public.question_requests_view(uuid, text)                    from public, anon, authenticated;
revoke execute on function public.submit_question_request(text, text, jsonb, text)      from public, anon;
revoke execute on function public.withdraw_question_request(uuid)                       from public, anon;
revoke execute on function public.my_question_requests()                                from public, anon;
revoke execute on function public.admin_question_requests(text)                         from public, anon;
revoke execute on function public.review_question_request(uuid, boolean, text)          from public, anon;
revoke execute on function public.question_history(text)                                from public, anon;
grant execute on function public.question_row_problem(jsonb), public.submit_question_request(text, text, jsonb, text),
                          public.withdraw_question_request(uuid), public.my_question_requests(),
                          public.admin_question_requests(text), public.review_question_request(uuid, boolean, text),
                          public.question_history(text)
  to authenticated;
