# Scam Sense — Level 1 "The First Case" + Speed Scoring, Stars, Room Progression

Unity 6000.0.38f1 · new Input System · uGUI + TextMeshPro · fully offline.

## What changed

### New scripts
| File | Responsibility |
|---|---|
| `Assets/Scripts/Data/CaseData.cs` | `ScamMessage` data class + enums `MessageChannel` (SMS/Email), `MessageVerdict` (Scam/Legitimate), `MessageTrigger` (TimeOnly/PlayerWalked/NearComputer/NearPhone). |
| `Assets/Scripts/Data/LevelDefinition.cs` | ScriptableObject per room: briefing, case notes, objectives, messages, timing, star thresholds, **min score to unlock next room**, next room id/scene, lessons. |
| `Assets/Scripts/Data/LocalDataManager.cs` | Static offline save (JSON in PlayerPrefs key `scamsense_progress_v1`): best score, best stars, completed, unlocked rooms. Only ever raises best values; never re-locks. |
| `Assets/Scripts/Game/ScoringSystem.cs` | Speed brackets (Inspector), per-message duplicate protection, totals. Static `StarsFor()` and `MeetsUnlockScore()` — stars and unlocking are separate from scoring. |
| `Assets/Scripts/Game/QuizManager.cs` | Message sequence: wait (time or activity) → notification → open (E / click / device) → response timer → score once → feedback → next. Runs at execution order −100. |
| `Assets/Scripts/Game/GameManager.cs` | Lifecycle: briefing → play → results. Computes stars + unlock, saves, Continue / Replay / Main Menu via the existing `SceneLoader`. |
| `Assets/Scripts/Game/InteractionGate.cs` | Shared E-key gate (so E doesn't open a message **and** a door/chair) and player lock/unlock while panels are open. |
| `Assets/Scripts/Game/MessageDevice.cs` | Marks the phone (SMS) / computer (Email) as look-at interaction points and as "near device" trigger locations. |
| `Assets/Scripts/Game/LevelAudio.cs` | Notification / answer / star sounds and soft room tone. Empty clip slots use generated sounds. Routed to MainMixer **SFX** group. |
| `Assets/Scripts/UI/UIKit.cs` | Runtime UI builder + Scam Sense palette (same approach as `PauseMenu` / `SceneLoader`). |
| `Assets/Scripts/UI/LevelHUD.cs` | Level name, objectives, case notes, "Messages Reviewed x/5", score, phone banner / email toast notifications, look prompt, crosshair. |
| `Assets/Scripts/UI/MessagePopupUI.cs` | Smartphone SMS view / desktop mail view + decision card (live response timer, SCAM / LEGITIMATE) → feedback card (points, time, explanation, Continue). |
| `Assets/Scripts/UI/BriefingUI.cs` | Mission briefing panel (text, case notes, objectives, controls, unlock rule). |
| `Assets/Scripts/UI/ResultsUI.cs` | Results: stars reveal, score/max, correct/incorrect, total answering time, best score, unlock status, lessons, Continue/Replay/Main Menu. |

### Modified existing scripts (minimal)
- `PlayerInteractor.cs` — door toggle skipped when `InteractionGate.InteractBlocked`; prompt hidden while a panel is open.
- `Sittablechair.cs` — `InteractPressed()` returns false when blocked; prompt hidden while a panel is open.
- `PlayerSitInteractor.cs` — same gate in `InteractPressed()` (script isn't in the scene, but kept consistent).

### New asset
- `Assets/Levels/Level1_TheFirstCase.asset` — the five Level 1 messages exactly as specified, fictional brands, reserved `.example` placeholder links (never opened).

### Scene changes (`Assets/Scenes/Game.unity`, saved)
- New root object **Level1** with: `ScoringSystem`, `LevelAudio`, `LevelHUD`, `MessagePopupUI`, `BriefingUI`, `ResultsUI`, `QuizManager`, `GameManager`. All references assigned (level asset, player, camera, menu button sprites, SFX mixer group).
- `Furnitures/Computersad` and `Furnitures/Computersad (1)` (the two monitors): `MessageDevice` (Email).
- **Phone_LivingRoom** — a copy of the existing `Phone` model on the living-room table (`tableRound`) with a `BoxCollider` + `MessageDevice` (SMS).
- Menu scene, settings, `SceneLoader`, and the player prefab are untouched.

## How it plays
1. New Case → Game scene loads → briefing (player frozen, cursor free). **Begin Investigation** or Enter.
2. Explore. First message after ~8 s, or after ~4 s once the player walks 6 m. Emails arrive early when near the computer.
3. Notification appears (phone banner top-centre / mail toast bottom-right) with sound; re-nudges every 20 s if ignored.
4. **E** (anywhere) or looking at the phone/computer + E opens it. Clicking the notification also works when the cursor is free (after Esc).
5. Timer starts when the full message appears and keeps running while reading. There is no close button, so closing and reopening can't reset it.
6. Answer → buttons disable instantly → scored once → feedback → Continue → next message after 10–20 s (or earlier on activity).
7. After 5 answers → results. Replay reloads the scene (clean run); saved progress is kept.

Controls are unchanged: WASD (`InputManager`), mouse look (`MouseLook`), E (doors/chairs/messages), mouse for buttons.

## Inspector configuration
- **ScoringSystem** (on Level1): `Brackets` list (max seconds → points; default ≤5:100, ≤10:80, ≤15:60, ≤20:40), `Slowest Correct Points` (20), `Incorrect Points` (0). Max per message = highest bracket.
- **Level1_TheFirstCase.asset**: star thresholds (200/300/400), `Min Score To Unlock Next` (300), `Next Level Id` (`level_2`), `Next Scene Name` (empty), message timing, messages, lessons.
- **QuizManager**: interact key, look distance, walk distance trigger, near-device radius, reminder interval.
- **LevelAudio**: drop in your own clips; leave empty for generated sounds; toggle/volume room tone.
- **GameManager**: `Main Menu Scene` (`Menu`), results delay.

## Connecting room 2 later
1. Build the room scene, add it to **File > Build Profiles > Scene List**.
2. Create its asset: *Assets > Create > Scam Sense > Level Definition* — set `levelId = level_2`, its own messages, thresholds, `minScoreToUnlockNext`, `nextLevelId = level_3`.
3. In the scene, duplicate the **Level1** object setup (or copy it from Game.unity) and assign the new asset to `GameManager.Level`. Add `MessageDevice` to its phone/computer.
4. In `Level1_TheFirstCase.asset` set `Next Scene Name` to the new scene name. Continue then becomes active when level_2 is unlocked.
5. (Optional) In the main menu, gate a room-select button with `LocalDataManager.IsUnlocked("level_2")`.

If the next scene is missing or not in the build, Continue shows "NEXT CASE SOON" (disabled) and the unlock is still saved.

## Play-test checklist
1. Open `Menu`, press Play, New Case. Briefing appears, player can't move. Begin → WASD/mouse work.
2. Walk around → SMS banner + sound. Press E → phone view, timer counting, movement/look frozen.
3. Answer within 5 s correctly → +100. Wait ~7 s on another → +80. Answer wrong → +0 and an explanation.
4. Double-click an answer button rapidly → only one score change.
5. Look at a monitor with no message → "Computer – no new emails". With a pending email → "Press [E] to read the new email".
6. Stand near a door while a message is pending and press E → message opens, door doesn't move.
7. Sit at the desk, get an email, press E → message opens (you stay seated); after Continue you're still seated and can stand with E.
8. Finish → results. ≥300 → green "Room unlocked!"; <300 → red "Room locked. You scored X..." message.
9. Replay → fresh briefing, score 0/500. Results "Best score" keeps the higher value.
10. Main Menu → menu loads with a visible cursor.

To reset progress while testing, run in a debug script or the Immediate window: `LocalDataManager.ResetAll();` (doesn't touch settings).

## Common errors & fixes
| Symptom | Fix |
|---|---|
| "GameManager: assign a LevelDefinition…" | Select **Level1**, drag `Assets/Levels/Level1_TheFirstCase.asset` into GameManager → Level. |
| No messages ever arrive | Check `QuizManager` is enabled and the level asset has messages; console for errors. |
| E opens a door and the message together | `QuizManager` must keep `[DefaultExecutionOrder(-100)]`; door/chair scripts must include the `InteractionGate` check. |
| Look prompt never shows on phone/computer | The object needs a Collider and `MessageDevice`; `Look Distance` (3 m) may be too short. |
| Player can't move after closing a panel | Something disabled player scripts outside the gate. `InteractionGate.UnlockPlayer()` only re-enables what it disabled. |
| Continue disabled after unlocking | Expected until `Next Scene Name` is set and that scene is in the Build Profile. |
| Sounds too loud/quiet | Settings SFX slider (LevelAudio routes to MainMixer SFX) or `LevelAudio` volumes. |
| Strange glyph in titles | The Goldrops display font lacks some glyphs — use it for words only (titles with numbers use the body font). |

## Known pre-existing issues (not changed)
- `Furnitures/chairDesk` and `Furnitures/chairDesk (1)` are identical duplicates at the same position, each with a `SittableChair` — one E press starts two sit routines. Consider deleting the duplicate.
- `chairModernCushion (4)` has `SittableChair` but no SeatPoint/ExitPoint children (console warning).
- `SettingsMenu` saves sensitivity (`set_sens`) but `MouseLook` doesn't read it.
