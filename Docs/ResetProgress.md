# Reset Progress

**Main Menu › Settings › Saved progress › RESET PROGRESS** (red). It opens a confirmation:

> Are you sure you want to reset your game progress? Your completed levels, scores, stars, and unlocked levels will be reset. This action cannot be undone.

The confirmation has **CANCEL** and **RESET EVERYTHING**, and a line saying which player is affected and what happens to their cloud save.
- **CANCEL** (or × / Esc) changes nothing.
- **RESET EVERYTHING** resets the progress, then shows either **PROGRESS RESET SUCCESSFULLY** or **RESET FAILED**.

## What is reset and what is kept
| Reset (active player only) | Kept |
|---|---|
| Completed rooms, best/last scores, stars, times completed, "passed", unlocked rooms | Audio, display, graphics and mouse settings (`set_*` keys) |
| | Account, sign-in state, player name and session |
| | Other players' saves on this PC and the offline save |

After a reset, Level 1 is playable through `LevelCatalog`'s own rule (the first room is always unlocked), and Rooms 2–4 are locked. CONTINUE is hidden, and the footer says "No saved progress yet".

## How it works
- `LocalDataManager.ResetProgress(out error)` overwrites **only** the active profile's key (`scamsense_progress_v1[_<playerId>]`) with an empty save. Then it reads the key back to check it.
  - If writing fails, the previous save is put back unchanged, and the menu shows RESET FAILED instead of success.
  - Nothing in memory changes unless the write succeeded.
- **Cloud Save.** Normal merges never lower progress, so a plain wipe would come back from the cloud. To prevent that:
  - The save now records `resetUtc`, the time of the last reset, and each level entry records `changedUtc`.
  - Every merge ignores entries from before the reset. That covers merges from Cloud Save, from the guest profile when signing in, and from the old offline save.
  - The reset save is marked pending and uploads through the normal sync, so the player's cloud copy becomes the reset save. If they're offline, the upload happens on the next sync.
  - A newer reset found in the player's **own** cloud save, for example from another PC, is applied locally. Another player's reset never is.
- Progress made after the reset, on this PC or another, merges normally.

## Notes
- **Clocks.** Reset times use each PC's clock. A PC whose clock is far behind could have runs made shortly after a reset counted as "before" it.
- **Existing scenes.** The Menu scene built before this feature gets the section and panels at runtime. Rebuilding the menu (**Tools › Scam Sense › Build Menu UI In Scene**) bakes them into the scene with Inspector-visible events. No manual Inspector setup is needed either way.
- **Testing hook.** `LocalDataManager.FailNextResetForTesting` (Editor/development builds only) makes the next reset fail, for testing the error path.
