-- Scam Sense: new players who sign in with Google or Facebook get their provider name as the display name
-- (the game's own display_name still wins when it is set). Safe to run again.
create or replace function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  insert into public.profiles (id, display_name)
  values (
    new.id,
    coalesce(nullif(left(trim(new.raw_user_meta_data ->> 'display_name'), 32), ''),
             nullif(left(trim(new.raw_user_meta_data ->> 'full_name'), 32), ''),
             nullif(left(trim(new.raw_user_meta_data ->> 'name'), 32), ''),
             case when new.is_anonymous then 'Guest' else 'Investigator' end)
  )
  on conflict (id) do nothing;
  return new;
end $$;

revoke execute on function public.handle_new_user() from public, anon, authenticated;
