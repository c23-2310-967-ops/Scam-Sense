# Scam Sense — Readability, Chairs, Computer Room, Usable Monitors, In-Game Menu

Unity 6000.0.38f1 · new Input System · uGUI + TextMeshPro · Built-in render pipeline · offline.

## 1. What I found before changing anything
| Area | Existing state |
|---|---|
| Scenes | `Menu` (build 0), `Game` (build 1). Old `Capstone20/30/40%` scenes in `Assets/` (unused). |
| Controller | `PlayerMotor` + `InputManager` (generated `PlayerInput` actions) on Player; `MouseLook`, `PlayerInteractor`, `FloatingHand`, `SeatedHeadLook` on Main Camera. |
| Interaction | Doors: `PlayerInteractor` (raycast, IMGUI label). Chairs: `SittableChair` (distance-only, IMGUI label, root-position seat points, duplicate desk chair). `PlayerSitInteractor`/`ChairInteractable` exist but are unused. Level 1: `QuizManager` + `MessageDevice` + `InteractionGate`. |
| Second floor | Open loft (≈12×14 m, floor y≈2.46) reached by the stairs + landing door; only a small table, two chairs, phone/papers/pen. |
| UI | uGUI + TMP. Level UI built at runtime (`UIKit`). In-game prompts were IMGUI `OnGUI` default 22 px white text without background. Display font (Goldrops) used for some titles; tips on loading screen used it too. |
| TMP | Installed; Liberation Sans default; Roboto-Bold in TMP examples; **Inter** available inside Unity's `com.unity.dt.app-ui` package (OFL). |
| Audio | `MainMixer` (Master / Music / SFX, exposed `MasterVol` / `MusicVol` / `SFXVol`). Main menu `SettingsMenu` saves `set_master/set_music/set_sfx/set_sens` in PlayerPrefs. Game scene had no music source. |
| Scene loading | `SceneLoader.LoadScene()` (loading screen, DontDestroyOnLoad). |
| Pause / cursor | `PauseMenu.cs` existed but was not in any scene. Cursor was set by `MouseLook` (Esc free / click lock) and by `InteractionGate`. |

## 2. Changes

### New files
| File | Responsibility |
|---|---|
| `Assets/Fonts/Inter-Regular.ttf`, `Inter-SemiBold.ttf`, `Inter-LICENSE.txt` | Readable UI font (OFL), copied from Unity's app-ui package. |
| `Assets/Fonts/Resources/Inter-Regular SDF.asset`, `Inter-SemiBold SDF.asset` | Dynamic TMP font assets (ASCII + common punctuation pre-baked). `<b>`/Bold maps to SemiBold; fallback Liberation Sans. |
| `Assets/Scripts/UI/InteractionPrompt.cs` | One readable prompt for everything (“[E] Sit”, “[E] Use computer”, “[E] Open door”), dark pill + cyan key cap, highest priority wins, hidden while any panel is open. Auto-created. |
| `Assets/Scripts/Game/ComputerTerminal.cs` | Usable computer: World Space canvas fitted to the monitor’s **screen sub-mesh** (follows its tilt, sits 4 mm in front of the glass), desktop + Mail app (inbox, reading pane, timer, SCAM/LEGITIMATE, feedback, review of answered/old mail). E → camera eases to a view point facing the screen, cursor freed, HUD hidden; Esc / “Exit computer” returns. Implements `IMessagePresenter`. |
| `Assets/Scripts/Game/IMessagePresenter.cs` | Interface so the phone pop-up and the computer screens display the **same** QuizManager messages/scoring. |
| `Assets/Scripts/GameAudioSettings.cs` | Shared keys, mixer params and dB conversion for Settings (main menu + in-game). |
| `Assets/Materials/CR_*.mat` | Simple Standard materials for the computer room (rack, LEDs, cork, cables, screens). |

### Rewritten / modified
| File | Change |
|---|---|
| `UI/UIKit.cs` | New palette (navy panels, near-white text, cyan accent, green/red verdict colours), Inter fonts, type scale (body 27, small 23, buttons 28 — reference 1920×1080), panels with borders, accessible buttons (hover/press feedback, auto-size labels, dim when disabled), badges, Canvas Scaler **Match Height** so tall panels always fit. |
| `UI/LevelHUD.cs` | Bigger, opaque HUD; notifications show *how* to open them (“Press [E] to read” / “Read it at a computer”); prompt moved to `InteractionPrompt`. |
| `UI/MessagePopupUI.cs` | Restyled phone/email view; SMS body 28 px; implements `IMessagePresenter`; ✓/× + words for verdicts (not colour alone). |
| `UI/BriefingUI.cs`, `UI/ResultsUI.cs` | Restyled, larger text; results panel widened so lessons don’t wrap; unlock box has ✓/× icon. |
| `Game/InteractionGate.cs` | Now **reference-counted**: any panel `Acquire(owner)`/`Release(owner)`; the player unfreezes and the cursor re-locks only when the last owner releases. Esc handler stack. Drops owners from unloaded scenes and resets timeScale. |
| `Game/QuizManager.cs` | Uses presenters; emails open on a computer when the scene has one (E elsewhere shows a hint toast), SMS on the phone pop-up; raises `StateChanged`; uses the shared prompt. |
| `Game/GameManager.cs` | New gate API. |
| `Game/LevelAudio.cs` | Background/ambience loop → **Music** group (so the Music slider is audible in-game); effects → SFX. Optional music clip slot. |
| `Data/LevelDefinition.cs` | `inboxExtras` (older read-only emails for a realistic inbox; never scored). |
| `Sittablechair.cs` | Rewritten (same class, scene refs kept): look-or-nearest targeting with one focused chair per frame, E to sit, **E or Space** to stand, SeatPoint = eye position/facing, ExitPoint = floor point with free-space check + fallbacks, CharacterController/movement off while seated, `SeatedHeadLook` for head movement, camera/FOV restored on stand, cooldown + busy lock, `TrySit()`/`TryStand()` API, shared prompt. |
| `PauseMenu.cs` | Rewritten: MENU button (top-right) + Esc/P; Resume / Settings (Master, Music & ambience, SFX, Mouse sensitivity) / Return to Main Menu (confirm) / Exit Game (confirm; Editor shows a message instead of force-stopping). timeScale 0 while open, holds the gate, Esc goes back one level. |
| `Mouselook.cs` | Esc no longer fights the pause menu; doesn’t re-lock the cursor while a panel is open; applies saved mouse sensitivity. |
| `PlayerInteractor.cs` | Shared prompt instead of IMGUI; claims E. |
| `FloatingHand.cs` | `SetHandsVisible(bool)` (hidden at the computer). |
| `SettingsMenu.cs` | Uses `GameAudioSettings` (same keys/behaviour). |
| `Sceneloader.cs` | Loading tips + progress use Inter (title keeps the display font). |

### Scene changes (`Game.unity`, saved)
- **Level1**: + `PauseMenu` (mixer + LevelAudio assigned); `LevelAudio.musicGroup` = Music.
- **Chairs** (`SittableChair` + generated `SeatPoint`/`ExitPoint`): downstairs desk chair (component moved from child `chair` to `chairDesk`; old markers replaced; looks at `ScreenFocus`), 4 dining chairs, 2 loft chairs, `loungeSofaLong`, plus the 2 new computer-room chairs. `chairDesk (1)` (exact duplicate) **disabled and renamed**, not deleted.
- **ComputerRoom** (new root, second floor): main desk against the north wall between the windows, 1.2× monitor (**Workstation** with `ComputerTerminal` + `MessageDevice`), keyboard, mouse, desk lamp, speaker, books, sittable desk chair, cables, server rack with LEDs, evidence board (existing `papers`/`CaseFolder` art), corner desk with laptop + second (flickering) screen + sittable chair, two bookcases with books, storage cabinet, archive boxes, rug, plants, floor lamp, bin; lights: desk lamp, monitor bias light, desk spot, room fill, rack glow. Clear path landing → desk. Existing loft table/chairs kept.
- **Furnitures/Workstation_Downstairs** (new, unscaled): groups `Computersad` + `Computersad (1)`; `ComputerTerminal` fitted to `computerScreen (1)`’s screen; `Computersad`’s renderer disabled (it sat in front of the screen; collider kept for aiming).
- Level asset: 3 `inboxExtras`.

## 3. How it plays
- Text message → phone banner → **E** anywhere → phone view.
- Email → desktop toast “Read it at a computer” → walk to the downstairs desk **or** the upstairs computer room → look at the monitor → **E** → click the NEW email → timer starts → SCAM / LEGITIMATE → feedback → **Back to inbox** → **Exit computer** (or Esc).
- Chairs: look at a chair (or stand next to it facing it) → **E Sit**; **E / Space** stand. You can use the computer while seated.
- **Esc** (or MENU button / P): menu. Esc inside Settings/confirm goes back one step. Esc at the computer first exits the computer; while an email is being answered, Esc opens the menu (and the timer pauses).

## 4. Inspector configuration
- **ComputerTerminal**: `Screen Surface` = monitor model, `Screen Submesh` = 1 (computerScreen.fbx) — or a thin quad with `-1`; `Front Hint` = the chair’s SeatPoint; `View Fill` (how much of the view the screen fills); `Canvas Width` (UI resolution, default 1280).
- **SittableChair**: `Seat Point` (eyes, blue arrow = facing), `Exit Point` (floor), `Look Target`, `Sit Label`, distances, `Seated Pitch`, `Seated Field Of View` (0 = unchanged).
- **PauseMenu**: `Mixer` (MainMixer), `Level Audio`, `Main Menu Scene` (“Menu” — must be in the Build Profile), keys.
- **LevelAudio**: `Sfx Group`, `Music Group`, optional `Ambience Loop` (assign real background music here if wanted).
- No button OnClick wiring is needed — all runtime UI wires itself.

### Adding another usable computer
1. Empty, **unscaled** parent containing the monitor model (add a BoxCollider so it can be looked at).
2. Add `ComputerTerminal`: Screen Surface = monitor, Screen Submesh = 1, Front Hint = a point in front of the screen (e.g. the chair SeatPoint).
3. Optional `MessageDevice` (Email) on the parent so “near computer” triggers work.

### Adding another sittable chair
Add `SittableChair` + a collider; create children `SeatPoint` (at eye height ≈ seat + 0.8 m in this house’s scale, Z facing forward) and `ExitPoint` (clear floor). Select the chair to see the gizmos.

## 5. Testing — what was verified vs not
**Verified in Play Mode (1366×768)**
- Briefing, HUD, phone banner, email toast, prompt pill and both computer screens render readably at 1366×768 (screenshots).
- Live playthrough (by the person playing in the Editor): walked upstairs, sat in the computer-room chair, used the upstairs computer (inbox with NEW/OLD tags); answered emails on the downstairs computer; after exiting, cursor re-locked and movement restored; scoring continued (3 messages scored, 1×100, 2×0 for wrong answers).
- Opening a text message froze movement + look and freed the cursor (scripted check).
- Both terminal screens align inside the monitor bezel (rendered from their view points).
- Pause: timeScale 0, cursor free; Settings opens; Music slider 0.25 → MusicVol −12.04 dB and saved to `set_music`; Back returns to the pause panel; Resume → timeScale 1, cursor locked, movement + look restored (scripted).
- Downstairs desk chair: sit puts the camera exactly on SeatPoint, CharacterController/PlayerMotor off, SeatedHeadLook on, a second sit request is rejected; stand → feet on the floor, controls + FOV restored (scripted).
- No errors from project scripts in the Console (only MCP-plugin warnings and the pre-existing `wallCorner (1)` negative-scale warning).

**Implemented but not yet verified by me in Play Mode**
- Return to Main Menu and Exit Game from the pause menu, Esc routing while at the computer, sit/stand on the other chairs and the sofa, the results screen in the new style, 1920×1080 screenshots, Exit in a standalone build.

**Suggested test pass**
1. 1920×1080 and 1366×768 Game view: read briefing, HUD, notifications, prompts, phone, computer, feedback, results, menu.
2. Sit/stand on every chair + sofa; check eye height, no clipping, standing spot clear, double-tapping E does nothing odd.
3. Walk stairs → landing door → computer room → sit → use computer → answer → exit → stand.
4. Esc in each state (walking, seated, computer idle, answering, phone message, results, settings, confirm).
5. Settings: drag Music to 0 (background hum stops), SFX to 0 (notifications silent), Master to 0 (everything); reopen game → values kept; main menu Settings shows the same values.
6. Return to Main Menu → menu works with a visible cursor; New Case → fresh run.
7. Build & run → Exit Game closes the app.

## 6. Troubleshooting
| Symptom | Fix |
|---|---|
| Computer UI misaligned / behind the bezel | Check `Screen Submesh` = 1 for computerScreen.fbx. For other models use a thin quad over the glass with `-1`. |
| UI appears mirrored | `Front Hint` must be on the viewer’s side of the screen. |
| Prompt never shows for a chair/computer | The object needs a Collider; check distances; another collider in front blocks the look ray. |
| Seated too high/low | Move the chair’s `SeatPoint` (it is the eye position). |
| Stuck after standing | Move `ExitPoint` to clear floor (a warning is logged if no free spot was found). |
| Music slider does nothing in-game | Assign `LevelAudio > Music Group`; the game scene’s only “music” is the ambience loop unless you assign a music clip. |
| Return to Main Menu shows an error | `PauseMenu > Main Menu Scene` must match a scene in File > Build Profiles. |
| Text shows boxes | A glyph missing from Inter + Liberation Sans; avoid it or add it to the Inter font asset (dynamic atlas adds glyphs automatically when the source font has them). |
| Cursor locked inside a menu | Something called `Cursor.lockState` directly — use `InteractionGate.Acquire/Release` instead. |

## 7. Known limitations / notes
- The house furniture is large relative to the player, so “seated” eye height (≈1.35 m above floor) is only a little lower than standing (≈1.43 m); this keeps the eyes above table tops.
- The main menu scene’s own layout/fonts were not changed (only the shared Settings keys).
- `PlayerSitInteractor.cs` / `ChairInteractables.cs` remain in the project but are unused.
