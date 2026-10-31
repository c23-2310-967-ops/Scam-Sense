-- Scam Sense: a guest can't set or change its display name. The check reads auth.users (the user's real
-- state), not the token, so a guest that has just become an account isn't held back by its older guest token.
create or replace function public.profiles_guest_name() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  if exists (select 1 from auth.users u where u.id = new.id and u.is_anonymous) then
    if tg_op = 'INSERT' then
      new.display_name := 'Guest';
    else
      new.display_name := old.display_name;
    end if;
  end if;
  return new;
end $$;
revoke execute on function public.profiles_guest_name() from public, anon, authenticated;
