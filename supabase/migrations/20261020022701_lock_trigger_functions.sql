-- Scam Sense: trigger functions run only from their triggers - nobody can call them through the API.
revoke execute on function public.handle_new_user() from public, anon, authenticated;
revoke execute on function public.touch_updated_at() from public, anon, authenticated;
revoke execute on function public.level_progress_keep_best() from public, anon, authenticated;
