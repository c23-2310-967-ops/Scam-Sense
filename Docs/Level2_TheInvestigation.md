# Level 2 — The Investigation

Room 2 is a cybersecurity investigation office. The player reviews six flagged messages: emails on the desk computer, texts on the phone. They classify each one as SCAM or LEGITIMATE and are scored on accuracy and speed. Room 3 unlocks **only** when the score reaches the passing score.

Level 2 reuses the existing systems: QuizManager, ScoringSystem, GameManager, LocalDataManager, ComputerTerminal, MessagePopupUI, BriefingUI, ResultsUI, PauseMenu and MainMenuUI. It adds **no** second scoring manager, save manager or EventSystem.

## Files

### New
| Path | What it is |
|---|---|
| `Assets/Scenes/Room2_InvestigationOffice.unity` | The office scene. Its Player, EventSystem, `Level2` and `Phone` objects were copied from `Game.unity`; the environment was built new. |
| `Assets/Levels/Level2_TheInvestigation.asset` | All Level 2 content: briefing, case notes, objectives, the six messages with investigation details, timing, star thresholds, passing score, lessons and result texts. |
| `Assets/Levels/Level3_AdvancedThreats.asset` | Placeholder card for Room 3. `sceneName` is empty, so the menu shows "Coming soon". |
| `Assets/Materials/R2_*.mat` | Office materials: floor, walls, ceiling, cyan trim, cabinets, handles and red string. |

### Modified (originals in `Backups/2026-10-14_before_level2/`)
| Path | Change |
|---|---|
| `Data/CaseData.cs` | Each message gained `requestedAction`, `linkNote` and `clues[]`, plus a `HasDetails` property. |
| `Data/LevelDefinition.cs` | Added `learningSummary`, `performanceHigh`, `performanceModerate` and `performanceLow`. |
| `Data/LocalDataManager.cs` | Added a `passed` flag; `RecordResult(..., passed)` sets it and never clears it. |
| `Game/GameManager.cs` | The result now includes the total message count, average answer time and learning summary. The unlock text talks about score only. |
| `Game/ComputerTerminal.cs` | Added the DETAILS/MESSAGE toggle. Long text now shrinks to fit instead of overlapping the buttons. Sender addresses display correctly. |
| `UI/MessagePopupUI.cs` | Added SHOW/HIDE DETAILS with a scrollable box of capped height. Sender addresses display correctly. |
| `UI/ResultsUI.cs` | Shows correct/incorrect out of N, response time, passing score and learning summary. NEXT ROOM reads LOCKED or COMING SOON when it can't be used. |
| `UI/UIKit.cs` | Added the `HudVariant` enum (Primary, Secondary, Danger, Safe) and `MessageDetailsText()`. |
| `UI/BriefingUI.cs`, `UI/MainMenuUI.cs` | Generic control text; three level cards fit side by side. |
| `Levels/Level1_TheFirstCase.asset` | `nextSceneName` = `Room2_InvestigationOffice`. |
| `Levels/LevelCatalog.asset` | Levels = Level 1, Level 2, Level 3. |
| Build Settings | Menu (0), Game (1), Room2_InvestigationOffice (2). |

## Scene layout (Room2_InvestigationOffice)
- **Environment**:
  - a 9 × 7 m room with a 3.6 m ceiling and a thin cyan trim line;
  - a door near the spawn point;
  - a desk with the **Workstation**: a ComputerTerminal and Email MessageDevice on a monitor (screen sub-mesh 1), a side monitor, keyboard, mouse and desk lamp;
  - a **DeskChair** (SittableChair with SeatPoint and ExitPoint);
  - the **Phone** (SMS MessageDevice) on a side table;
  - an **evidence board** with fictional case notes on the west wall;
  - filing cabinets, a bookshelf, a wall screen, a lounge chair, plants and a rug;
  - ceiling lights, a desk lamp, a spotlight on the board, a monitor glow and flat cool ambient light.
- **Level2**:
  - GameManager has `level` set to Level2_TheInvestigation.asset;
  - QuizManager has `player` and `playerCamera` set to this scene's Player.
- Ambient audio and music come from the existing **LevelAudio** component, which uses the same clips as Level 1.

## Inspector reference (if you need to rebuild or change anything)
- **Workstation › ComputerTerminal**: Screen Surface = `Workstation/Monitor`, Screen Submesh = `1`, Front Hint = `DeskChair/SeatPoint`, Quiz = `Level2 (QuizManager)`.
- **DeskChair › SittableChair**: Seat Point = `SeatPoint`, Exit Point = `ExitPoint`, Look Target = `Workstation/ScreenFocus`.
- **Level2 › GameManager**: Level = `Level2_TheInvestigation`, Main Menu Scene = `Menu`.
- **Level2_TheInvestigation.asset**:
  - `minScoreToUnlockNext` = 300 (passing score);
  - `oneStarScore` = 1, `twoStarScore` = 300, `threeStarScore` = 480;
  - `delayBetweenMessages` = (10, 20);
  - `nextLevelId` = `level_3`;
  - `nextSceneName` = empty. Set it to Room 3's scene name once that scene exists and is in Build Settings.

## How scoring, stars and unlocking work
- **Points per message** (ScoringSystem, unchanged):
  - correct within 5 s = 100, within 10 s = 80, within 15 s = 60, within 20 s = 40, slower = 20;
  - wrong = 0;
  - maximum = 600.
- **Timer**:
  - starts when the message opens (`QuizManager.OpenPending`);
  - uses scaled time, so the pause menu (timeScale 0) pauses it;
  - each message id is scored once (`ScoringSystem.TryRecord`), and both answer buttons disable on the first click.
- **Stars** (`LevelDefinition`): 3 = 480–600, 2 = 300–479, 1 = 1–299, 0 = 0. Stars are shown and saved, but **never** unlock anything.
- **Unlock**: `GameManager.EvaluateAndSave` unlocks `nextLevelId` only when `score >= minScoreToUnlockNext`.

## Save data (PlayerPrefs `scamsense_progress_v1`, JSON)
Each level entry stores `unlocked`, `completed`, `bestScore`, `bestStars`, `lastScore`, `lastStars`, `timesCompleted` and the new `passed` flag. Older saves load fine; a missing `passed` reads as false.

Note: Level 1 entries saved before this change show `passed: false` even if they were passed. Nothing reads `passed` to gate progress (unlocking uses `unlocked`), so this is cosmetic.

## Testing

### Tested in Play Mode (by script driving the real buttons' onClick in the Editor)
- Room 2 loads from the Level Selection card through the normal menu fade and SceneLoader. It has one EventSystem and its briefing reads "LEVEL 2: THE INVESTIGATION".
- The briefing shows the case notes, the 4 objectives, max 600 and the passing score 300. BEGIN INVESTIGATION starts the level.
- Messages arrive in order (1 → 6). Emails open on the desk computer and texts on the phone popup.
- DETAILS works on the computer and the phone. The phone's details scroll. Nothing reveals the verdict before answering.
- Scoring brackets seen: 0 s → 100, 14.5 s → 60, 44–46 s → 20, wrong → 0.
- Double-clicking an answer scores only once.
- Pausing for over 5 s kept the timer at 6.5 s; it resumed afterwards.
- **Run below the passing score**: 160 / 600, 2 / 6 correct, 1 star. The result said "Room locked", and the save showed `passed: false` with **no** Level 3 unlock.
- **Replay** reloaded the room with the score reset to 0 and kept the saved best score.
- **Passing run**: 600 / 600, 6 / 6 correct, 3 stars. The save showed `passed: true` and **Level 3 unlocked**. NEXT ROOM showed "NEXT ROOM: COMING SOON" (disabled) because Level 3 has no scene.
- **Main Menu** returned to the menu. The Level Selection cards showed Level 1 Completed, Level 2 Completed (600, 3/3) and Level 3 Unlocked with "Coming soon".
- **Level 1 regression**: Game loads, its briefing starts, and an SMS and an email open and answer normally. Its computer screen layout is intact.
- No game errors in the Console.
- Your progress was backed up before testing and restored afterwards.

### Not tested
- Real mouse and keyboard play: walking, pressing E at the phone or monitor, scrolling the details with the mouse wheel, sitting in the chair.
- The "NEXT ROOM: LOCKED" label and the new unlock sentence after the last edit (the logic was tested; those two strings were changed afterwards).
- The 2-star band (300–479) and the 40-point bracket (15–20 s), as exact values.
- A full standalone build.

## Common problems and fixes
| Symptom | Cause / fix |
|---|---|
| Level 2 card says "Scene missing from build" | Add `Assets/Scenes/Room2_InvestigationOffice.unity` in File › Build Profiles › Scene List. |
| Room 2 plays Level 1's messages | `Level2 › GameManager › Level` must be `Level2_TheInvestigation`. |
| Emails say "read it at a computer" but E does nothing | Look straight at the monitor within 3 m. Check that `Workstation/Monitor` has its BoxCollider and that ComputerTerminal's Quiz field is set. |
| Monitor UI is offset or the wrong size | Screen Submesh must be `1`. Enable Read/Write on `computerScreen.fbx` (it is enabled now). |
| Details text is tiny on the monitor | The text auto-shrinks to fit (minimum 15). Shorter clues or fewer case notes make it larger. |
| Room 3 never unlocks | This is intended below 300 points. Check `minScoreToUnlockNext` and `nextLevelId = level_3` on the Level 2 asset. |
| NEXT ROOM says COMING SOON | Level 3 has no scene yet. Set `nextSceneName` on Level 2 (and `sceneName` on Level 3) once the Room 3 scene is in Build Settings. |
| Clicks don't register in the Game view | Use Free Aspect or a resolution that fits the Game view window. |
| Reset progress for testing | There is no in-game reset button. Use Edit › Clear All PlayerPrefs, which also clears your audio settings, or call `LocalDataManager.ResetAll()`. |
