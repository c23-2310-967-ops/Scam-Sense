# Accounts and cloud progress (Supabase)

Supabase project: **Scam Sense Database** (`ayiyvvekyyhotbytabtm`, region ap-northeast-1).
This replaces the earlier Unity Gaming Services setup. The menu flow and panels are unchanged (see `Docs/CloudAccounts.md` for that flow). Only the service behind them changed.

## Sign-in methods
| Method | How it works | Status |
|---|---|---|
| Guest | Supabase anonymous user. The same guest comes back on this PC, and after signing out (PLAY AS GUEST). | Needs **Allow anonymous sign-ins** |
| Email + password | Account with a display name. Signing in as a guest merges that guest's progress into the account. | Working (email confirmation is on, see below) |
| Play offline | Local save only; uploads when back online | Working |

## Files
| File | Role |
|---|---|
| `Assets/Scripts/Online/SupabaseSettings.cs` | Reads `Assets/Resources/SupabaseSettings.json`: project URL + **publishable** key. Git-ignored; refuses secret / service_role keys. |
| `Assets/Scripts/Online/SupabaseClient.cs` | Minimal HTTP client for Supabase Auth and the database (UnityWebRequest; 15 s timeouts; no packages). |
| `Assets/Scripts/Online/CloudAccountService.cs` | Accounts, sessions, sync (download → merge with LocalDataManager's rules → upload), offline retry. |
| `Assets/Scripts/Online/GameResultLog.cs` | Finished runs queued per save, uploaded to `game_results` with a `run_id` (no duplicates). |
| `Assets/Scripts/UI/AccountUI.cs` | Email form with display name; PLAY AS GUEST, sign in, create account, account panel. |
| `supabase/migrations/*.sql` | Database schema and security, in the order applied (file names start with the migration version). |
| `supabase/SupabaseSettings.example.json` | Template for the settings file. |

**Inspector:** nothing to assign. The new fields and buttons are saved in `Menu.unity`, and the code also adds them if they are missing.

## Database (applied)
- `profiles`: display name, last progress reset.
- `level_progress`: one row per player per level.
- `achievements`
- `game_results`: run history; can't be edited or deleted from the game.

Every table has **Row Level Security**: a signed-in player (guest or account) can only read and write rows where the user id is their own (`auth.uid()`). Signed-out requests can read nothing. A trigger also stops progress from going down on the server: unlocks stay unlocked and best scores only go up.

## Public key vs guest (anonymous) users

These are two different things:

| | Publishable (anon) key | Guest = Supabase anonymous user |
|---|---|---|
| What it is | The project's public API key, shipped inside the game | A real user in `auth.users`, created by PLAY AS GUEST |
| Postgres role | `anon` | `authenticated` (the same role as accounts) |
| Identity | None: no user id | Its own user id (`auth.uid()`), access token and refresh token |
| JWT claim | (none) | `is_anonymous: true` (accounts have `false`) |
| Access to game tables | None | Its own rows only |

Because anyone holding the public key can create a guest, and every guest gets an authenticated identity, a policy written only as `to authenticated` would also let guests in. `supabase/migrations/20261030231844_003_anonymous_user_hardening.sql` and `..._003b_guest_name_checks_auth_users.sql` (applied 2026-10-31) make this explicit:

- Every policy says it is for guests **and** accounts, each limited to their own rows, and requires a real Supabase user token (one carrying `is_anonymous`).
- Guests can't choose a display name: it stays "Guest" until the guest becomes an account, and the name given then is copied to the profile (`auth.users` decides who is a guest, not the token).
- Table privileges the game never uses are removed (`TRUNCATE`, which ignores RLS, `TRIGGER`, `REFERENCES`, and `UPDATE`/`DELETE` on `game_results`).
- Throwaway identities can only store valid level and achievement ids, at most 1000 run-history rows and 100 achievements each (extra rows are skipped, so sync never gets stuck).

The game keeps a guest's **refresh token** (never the password or access token) in PlayerPrefs, so the same guest comes back after a restart. After a guest becomes an account, the game fetches a new token so the database sees `is_anonymous: false` straight away.

**Recommended dashboard settings** (Authentication):
- **Rate Limits → anonymous sign-ins per hour:** keep it low (default 30 per IP). It limits how many guests one machine can create.
- **Attack Protection → Prevent use of leaked passwords:** turn on if the plan allows it (flagged by the Security Advisor).
- CAPTCHA protection is Supabase's strongest defence against mass guest creation, but it needs a CAPTCHA widget in the game, which the desktop build doesn't have yet.

Scores come from the game client, so they are fine for progress but not proof against cheating. For a competitive leaderboard, record results through a Supabase Edge Function that replays or validates the run.

## Setup steps (dashboard access needed)

### Supabase › Authentication › Sign In / Providers
- **Allow anonymous sign-ins:** ON (guest play).
- **Email › Confirm email:** OFF while testing (accounts work right away). Turn it back on for release if you want; the game handles it.

### Supabase › Project Settings › Integrations › GitHub
The `supabase/` folder at the repository root follows the Supabase CLI layout, so the project can be linked to this repository:
1. **Authorize GitHub** and choose the repository `c23-2310-967-ops/Scam-Sense`.
2. **Working directory:** `.` (the `supabase/` folder is at the root).
3. **Production branch:** `main`.
4. **Deploy to production:** OFF. Database changes are applied on purpose, not on every push.
5. Leave **Automatic branching** off (preview branches need a paid plan).
6. **Enable integration.**

The five migration files already carry the versions recorded in the project (Database › Migrations), so they count as applied and nothing would run again. A future change goes in a new file named `<yyyymmddhhmmss>_<name>.sql`.

## Testing checklist
| # | Test | Expected |
|---|---|---|
| 1 | PLAY AS GUEST | Signs in as a guest (needs Allow anonymous sign-ins); the chip shows GUEST PLAYER. |
| 2 | Create account / sign in with email | The chip shows the display name. |
| 3 | Session | Restart the game → the player is signed back in. SIGN OUT → PLAY asks again. |
| 4 | Progress | Finish a level → rows appear in `level_progress` and `game_results` for that user only. |
| 5 | Guest isolation | A second guest can't read or change the first guest's rows (checked 2026-10-31 over the API: 29/29 checks passed). |

> Google and Facebook sign-in were tried and removed (2026-10-20). `supabase/migrations/20261020025802_provider_display_names.sql` stays applied; it only affects users from those providers.
