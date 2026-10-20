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
| `Supabase/migrations/*.sql` | Database schema and security, already applied to the project. |
| `Supabase/SupabaseSettings.example.json` | Template for the settings file. |

**Inspector:** nothing to assign. The new fields and buttons are saved in `Menu.unity`, and the code also adds them if they are missing.

## Database (applied)
- `profiles`: display name, last progress reset.
- `level_progress`: one row per player per level.
- `achievements`
- `game_results`: run history; can't be edited or deleted from the game.

Every table has **Row Level Security**: a signed-in player (guest or account) can only read and write rows where the user id is their own (`auth.uid()`). Signed-out requests can read nothing. A trigger also stops progress from going down on the server: unlocks stay unlocked and best scores only go up.

Scores come from the game client, so they are fine for progress but not proof against cheating. For a competitive leaderboard, record results through a Supabase Edge Function that replays or validates the run.

## Setup steps (dashboard access needed)

### Supabase › Authentication › Sign In / Providers
- **Allow anonymous sign-ins:** ON (guest play).
- **Email › Confirm email:** OFF while testing (accounts work right away). Turn it back on for release if you want; the game handles it.

## Testing checklist
| # | Test | Expected |
|---|---|---|
| 1 | PLAY AS GUEST | Signs in as a guest (needs Allow anonymous sign-ins); the chip shows GUEST PLAYER. |
| 2 | Create account / sign in with email | The chip shows the display name. |
| 3 | Session | Restart the game → the player is signed back in. SIGN OUT → PLAY asks again. |
| 4 | Progress | Finish a level → rows appear in `level_progress` and `game_results` for that user only. |

> Google and Facebook sign-in were tried and removed (2026-10-20). `Supabase/migrations/002_provider_display_names.sql` stays applied; it only affects users from those providers.
