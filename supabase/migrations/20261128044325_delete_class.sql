-- =====================================================================================
-- Scam Sense: teachers can delete a class they made (CLASSROOM > CLASSES > DELETE).
-- Run after 20261126074705_classroom_roles_classes_records.sql. Safe to run again.
--
-- Only classes with NO recorded results can be deleted. Results are final (the
-- results_are_final trigger), and deleting a class would have to change every result
-- row that points to it (game_results / question_results.class_id is "on delete set
-- null"), so the database would refuse anyway. Classes with results are archived
-- instead (set_class_archived) and the game hides archived classes by default.
-- Deleting removes the class code and its enrolments (class_members cascade), so
-- enrolled students are simply free to join another class.
-- Returns {ok: true, members_removed} or {ok: false, error}.
-- =====================================================================================

create or replace function public.delete_class(p_class uuid) returns jsonb
language plpgsql security definer set search_path = '' as $$
declare
  c          public.classes;
  n_results  integer;
  n_members  integer;
begin
  if not public.is_teacher() then
    raise exception 'Only teachers can delete classes.' using errcode = '42501';
  end if;
  select * into c from public.classes where id = p_class and teacher_id = auth.uid() for update;
  if not found then
    return jsonb_build_object('ok', false, 'error', 'That class was not found (it may already be deleted).');
  end if;
  select count(*) into n_results from public.game_results where class_id = p_class;
  if n_results > 0 then
    return jsonb_build_object('ok', false, 'error',
      format('%s has %s recorded result%s, so it can''t be deleted (student records are never removed). Archive it instead: archived classes are hidden from your list.',
             c.name, n_results, case when n_results = 1 then '' else 's' end));
  end if;
  select count(*) into n_members from public.class_members where class_id = p_class and removed_at is null;
  delete from public.classes where id = p_class;
  return jsonb_build_object('ok', true, 'members_removed', n_members);
end $$;

revoke execute on function public.delete_class(uuid) from public, anon;
grant execute on function public.delete_class(uuid) to authenticated;
