-- =====================================================================================
-- Scam Sense: guests (Supabase anonymous users) vs accounts - security hardening
-- Run once in Supabase Dashboard > SQL Editor, after 001 and 002. Safe to run again.
--
-- Background
--   * The publishable (anon) API key only identifies the project. A request made with the key
--     alone runs as the Postgres role "anon", which has no access to any of these tables.
--   * PLAY AS GUEST uses Supabase Anonymous Sign-Ins. That creates a real user in auth.users and
--     returns an access token whose role is "authenticated" and whose claim is_anonymous = true.
--     So a guest passes every policy written "to authenticated", exactly like an account, and
--     anyone holding the public key can create a guest. "authenticated" therefore never means
--     "a registered player"; policies must say what guests may do.
--
-- What this migration does
--   1. Every policy now states who it is for: guests (is_anonymous = true) and accounts
--      (is_anonymous = false), each limited to their own rows. Tokens without the claim
--      (anything that is not a Supabase Auth user session) are refused.
--   2. Guests can't choose a display name. It stays "Guest" until the guest becomes an
--      account; the name given when creating the account is then copied to the profile.
--   3. Removes table privileges the game never uses (TRUNCATE, TRIGGER, REFERENCES, and
--      UPDATE / DELETE where no policy allows them). TRUNCATE ignores Row Level Security.
--   4. Limits what one throwaway identity can store: only valid level / achievement ids,
--      at most 1000 run-history rows and 100 achievements per player.
-- =====================================================================================

-- ---------- 1. Policies: guests and accounts, own rows only ----------
-- (select auth.jwt() ->> 'is_anonymous') is 'true' for guests and 'false' for accounts; it is
-- null only for tokens that Supabase Auth did not issue for a user.

-- profiles
drop policy if exists "profiles: read own"   on public.profiles;
drop policy if exists "profiles: insert own" on public.profiles;
drop policy if exists "profiles: update own" on public.profiles;
drop policy if exists "profiles: guests and accounts read own"   on public.profiles;
drop policy if exists "profiles: guests and accounts insert own" on public.profiles;
drop policy if exists "profiles: guests and accounts update own" on public.profiles;
create policy "profiles: guests and accounts read own" on public.profiles for select to authenticated
  using ((select auth.uid()) = id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "profiles: guests and accounts insert own" on public.profiles for insert to authenticated
  with check ((select auth.uid()) = id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "profiles: guests and accounts update own" on public.profiles for update to authenticated
  using ((select auth.uid()) = id and (select auth.jwt() ->> 'is_anonymous') is not null)
  with check ((select auth.uid()) = id and (select auth.jwt() ->> 'is_anonymous') is not null);

-- level_progress (delete is used by RESET PROGRESS, for guests too)
drop policy if exists "level_progress: read own"   on public.level_progress;
drop policy if exists "level_progress: insert own" on public.level_progress;
drop policy if exists "level_progress: update own" on public.level_progress;
drop policy if exists "level_progress: delete own" on public.level_progress;
drop policy if exists "level_progress: guests and accounts read own"   on public.level_progress;
drop policy if exists "level_progress: guests and accounts insert own" on public.level_progress;
drop policy if exists "level_progress: guests and accounts update own" on public.level_progress;
drop policy if exists "level_progress: guests and accounts delete own" on public.level_progress;
create policy "level_progress: guests and accounts read own" on public.level_progress for select to authenticated
  using ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "level_progress: guests and accounts insert own" on public.level_progress for insert to authenticated
  with check ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "level_progress: guests and accounts update own" on public.level_progress for update to authenticated
  using ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null)
  with check ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "level_progress: guests and accounts delete own" on public.level_progress for delete to authenticated
  using ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);

-- achievements
drop policy if exists "achievements: read own"   on public.achievements;
drop policy if exists "achievements: insert own" on public.achievements;
drop policy if exists "achievements: update own" on public.achievements;
drop policy if exists "achievements: delete own" on public.achievements;
drop policy if exists "achievements: guests and accounts read own"   on public.achievements;
drop policy if exists "achievements: guests and accounts insert own" on public.achievements;
drop policy if exists "achievements: guests and accounts update own" on public.achievements;
drop policy if exists "achievements: guests and accounts delete own" on public.achievements;
create policy "achievements: guests and accounts read own" on public.achievements for select to authenticated
  using ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "achievements: guests and accounts insert own" on public.achievements for insert to authenticated
  with check ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "achievements: guests and accounts update own" on public.achievements for update to authenticated
  using ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null)
  with check ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "achievements: guests and accounts delete own" on public.achievements for delete to authenticated
  using ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);

-- game_results: add and read your own runs; the history can't be edited or deleted from the game
drop policy if exists "game_results: read own"   on public.game_results;
drop policy if exists "game_results: insert own" on public.game_results;
drop policy if exists "game_results: guests and accounts read own"   on public.game_results;
drop policy if exists "game_results: guests and accounts insert own" on public.game_results;
create policy "game_results: guests and accounts read own" on public.game_results for select to authenticated
  using ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);
create policy "game_results: guests and accounts insert own" on public.game_results for insert to authenticated
  with check ((select auth.uid()) = user_id and (select auth.jwt() ->> 'is_anonymous') is not null);

-- ---------- 2. Display names: guests are always "Guest" ----------
-- New users: a guest's sign-up metadata is ignored (anyone can send any metadata with an anonymous sign-up).
create or replace function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  insert into public.profiles (id, display_name)
  values (
    new.id,
    case when new.is_anonymous then 'Guest'
         else coalesce(nullif(left(trim(new.raw_user_meta_data ->> 'display_name'), 32), ''),
                       nullif(left(trim(new.raw_user_meta_data ->> 'full_name'), 32), ''),
                       nullif(left(trim(new.raw_user_meta_data ->> 'name'), 32), ''),
                       'Investigator')
    end
  )
  on conflict (id) do nothing;
  return new;
end $$;

-- A guest can't set or change its display name. The check reads auth.users (the user's real state), not the
-- token, so a guest that has just become an account isn't held back by its older guest token.
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

drop trigger if exists profiles_guest_name on public.profiles;
create trigger profiles_guest_name before insert or update on public.profiles
  for each row execute function public.profiles_guest_name();

-- A guest that becomes an account (email added and confirmed) gets the name it chose.
create or replace function public.handle_user_upgraded() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  update public.profiles
     set display_name = left(trim(new.raw_user_meta_data ->> 'display_name'), 32)
   where id = new.id
     and display_name = 'Guest'
     and nullif(trim(new.raw_user_meta_data ->> 'display_name'), '') is not null;
  return new;
end $$;

drop trigger if exists on_auth_user_upgraded on auth.users;
create trigger on_auth_user_upgraded after update on auth.users
  for each row when (not new.is_anonymous and new.raw_user_meta_data ->> 'display_name' is not null)
  execute function public.handle_user_upgraded();

-- ---------- 3. Only the privileges the game uses ----------
revoke all on public.profiles, public.level_progress, public.achievements, public.game_results from anon, authenticated;
grant select, insert, update         on public.profiles       to authenticated;
grant select, insert, update, delete on public.level_progress to authenticated;
grant select, insert, update, delete on public.achievements   to authenticated;
grant select, insert                 on public.game_results   to authenticated;

-- ---------- 4. Limits for throwaway identities ----------
alter table public.level_progress drop constraint if exists level_progress_level_id_format;
alter table public.level_progress add constraint level_progress_level_id_format check (level_id ~ '^level_[0-9]{1,3}$');
alter table public.game_results drop constraint if exists game_results_level_id_format;
alter table public.game_results add constraint game_results_level_id_format check (level_id ~ '^level_[0-9]{1,3}$');
alter table public.achievements drop constraint if exists achievements_id_format;
alter table public.achievements add constraint achievements_id_format check (achievement_id ~ '^[a-z0-9_]{1,64}$');

-- Rows over the limit are skipped quietly (no error), so a player's sync never gets stuck; the full
-- history always stays in the local save.
create or replace function public.limit_rows_per_player() returns trigger
language plpgsql set search_path = '' as $$
declare
  n integer;
begin
  if tg_table_name = 'game_results' then
    select count(*) into n from public.game_results where user_id = new.user_id;
    if n >= 1000 then return null; end if;
  elsif tg_table_name = 'achievements' then
    select count(*) into n from public.achievements where user_id = new.user_id;
    if n >= 100 then return null; end if;
  end if;
  return new;
end $$;

drop trigger if exists game_results_limit on public.game_results;
create trigger game_results_limit before insert on public.game_results
  for each row execute function public.limit_rows_per_player();
drop trigger if exists achievements_limit on public.achievements;
create trigger achievements_limit before insert on public.achievements
  for each row execute function public.limit_rows_per_player();

-- Trigger functions run only from their triggers: nobody can call them through the API.
revoke execute on function public.handle_new_user()        from public, anon, authenticated;
revoke execute on function public.profiles_guest_name()    from public, anon, authenticated;
revoke execute on function public.handle_user_upgraded()   from public, anon, authenticated;
revoke execute on function public.limit_rows_per_player()  from public, anon, authenticated;
