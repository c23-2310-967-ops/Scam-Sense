# Level 3 — The Digital Trap

Room 3 is a cybersecurity operations room. The player investigates six convincing messages: five emails on the main computer and one text message on the pocket phone. A new **case-files computer** holds the evidence needed to judge them: trusted domains, order history, account activity, the IT policy and notes. Nothing reveals an answer; the player compares and decides.

Level 3 reuses the existing systems:
- QuizManager, ScoringSystem and GameManager;
- LocalDataManager and Cloud Save;
- ComputerTerminal, MessagePopupUI (with the PocketPhone animation), BriefingUI, LevelHUD (Tab), ResultsUI, PauseMenu and LevelAudio.

It adds **no** second game manager, score system, save system, input handler or EventSystem.

## Files

### New
| Path | What it is |
|---|---|
| `Assets/Scenes/Room3_DigitalTrap.unity` | The operations room. It's a copy of Room 2, so the Player (kid character), EventSystem, managers (`Level3`), email `Workstation`, sittable `DeskChair` and `Phone` are the same working objects. The environment is new. |
| `Assets/Levels/Level3_TheDigitalTrap.asset` | All Level 3 content. It's the former `Level3_AdvancedThreats` placeholder, renamed with the same GUID and `level_3` save key, so the Level Catalog still points to it. |
| `Assets/Scripts/Game/CaseFilesTerminal.cs` | The evidence computer: look at it and press E, the camera eases in, you click tabs, then press Esc or EXIT. |
| `Assets/Scripts/Game/OpsWallDisplay.cs` | The live wall dashboard: messages reviewed, accuracy and status. |
| `Assets/Scripts/Game/RackHum.cs` | A soft 3D server-fan hum, generated with no audio file. It plays through the same mixer group as the level ambience, so your volume sliders control it. |
| `Assets/Editor/Room3Builder.cs` | Menu items **Tools › Scam Sense › Level 3 › Create Room 3 Scene** and **Rebuild Room 3 Environment**. |
| `Assets/Editor/Level3Content.cs` | The six messages, case files and texts, used once to fill the asset. After that, edit the **asset**. |
| `Assets/Materials/R3_*.mat` | 46 operations-room materials. |

### Modified (originals in `Backups/2026-10-14_before_level3/`)
| Path | Change |
|---|---|
| `Data/LevelDefinition.cs` | New optional `caseFiles` list (title and body pages) and the `CaseFile` class. Other levels leave it empty. |
| `Levels/Level2_TheInvestigation.asset` | `nextSceneName` = `Room3_DigitalTrap`, so NEXT ROOM in Level 2 now opens Room 3 once it's unlocked. |
| `UI/MainMenuUI.cs` | Level Selection fits three cards. With three rooms, "Level 4 unlocked · coming in a future update" appears as a note on the Level 3 card instead of a fourth card that wouldn't fit. |
| `ExitConfirmation.cs` | Null-safe. The earlier menu rebuild removed its old panel; the new menu has its own QUIT dialog. It used to throw an error every frame in the Menu. |
| Build Settings | Menu (0), Game (1), Room2_InvestigationOffice (2), Room3_DigitalTrap (3). |

Room 1, Room 2 and the Main Menu scene were **not** changed.

## Room layout (12 × 9 m, 3.4 m ceiling)
| Area | What's there |
|---|---|
| North (main station) | The email `Workstation` (ComputerTerminal), the sittable `DeskChair`, desk lamp, mug and notebook. A **Trusted Contacts card** stands on the desk. Above it is the **THREAT ANALYSIS wall display** (live), framed by the room's only cyan accent strip. The `Phone` side table is to the right. |
| West | The **case-files station**: desk, monitor, keyboard, case folders, a mug and an analyst badge. Also the **digital case wall** with six pattern cards and orange pins and string. It never names a message or an answer. |
| East | Four **server racks** with status LEDs, one amber; a cable tray; a SERVER STATUS screen; and the fan hum. |
| Centre | The **analyst desk**: two dashboards (traffic and alert queue) facing the entrance, printed notes, a badge and a mug. |
| South | The entrance door with a badge reader (spawn point), metal shelving with binders, a network switch and a storage box, a coffee point, a THINK BEFORE YOU CLICK poster and a wall clock. |

### Lighting (Built-in RP, unchanged)
- **Ceiling:** four cool downlights under the ceiling panels. Three cast soft shadows.
- **Task and screen lights:** a warm desk lamp (soft shadows), a warm spotlight on the case wall (soft shadows), and soft cyan glows from the monitors, wall display and racks. These glows cast no shadows.
- **Ambient and reflections:** cool trilight ambient and one box-projected reflection probe.
- **Contact shadows:** under every piece of furniture, as the stand-in for ambient occlusion.
- **Not used:** no flicker, fog or bloom.

The desk and chair models from earlier rooms are recoloured to the operations palette in **this scene only**: grey desks, navy seats. The model files and other rooms are unchanged.

## Hierarchy (Room3_DigitalTrap)
```
Player                [CharacterController, PlayerMotor, InputManager]   (spawn by the door)
  Main Camera         [MouseLook, PlayerInteractor, FloatingHand, SeatedHeadLook]
  KidHoodie           [PlayerBody]
EventSystem
Level3                [ScoringSystem, LevelAudio, LevelHUD, MessagePopupUI, BriefingUI, ResultsUI, QuizManager, GameManager, PauseMenu]
Environment
  Workstation         [ComputerTerminal, MessageDevice(Email)]   (kept from Room 2)
  Desk, Keyboard, Mouse, DeskLamp, DeskChair [SittableChair], PhoneTable, Phone [MessageDevice(SMS)]   (kept)
  Structure           (floor, walls, ceiling, panels, door, vents, lights panels)
  Furniture
    ServerRack_1..4, CableTray, RackHum [AudioSource, RackHum]
    CaseFilesStation
      CaseDesk
      CaseFilesTerminal  [CaseFilesTerminal]
        Base, Stand, Body (BoxCollider), Screen, ViewPoint
        CaseFilesUI     [Canvas (World Space), GraphicRaycaster]
          Header (Title, Status), Tabs (Tab_1..Tab_5), NewMessageBanner, ExitButton, Page (PageTitle, PageBody)
    AnalystDesk, Shelving, CoffeeCabinet, SpareChair(s)
  Screens
    WallDisplay       [OpsWallDisplay]  -> WallDisplayUI canvas
    RackStatusScreen, DigitalCaseWall, Dashboard_1, Dashboard_2
  Props               (Trusted Contacts card, mugs, notebooks, badges, poster, clock, plants)
  Lighting            (all lights + ReflectionProbe_Ops)
  ContactShadows
```

## Inspector reference
- **Level3 › GameManager:** Level = `Level3_TheDigitalTrap`, Main Menu Scene = `Menu`.
- **CaseFilesTerminal:**
  - Screen Canvas = `CaseFilesUI`, View Point = `ViewPoint`;
  - Tab Buttons = `Tab_1`…`Tab_5`;
  - Page Title / Page Body / Status Text = the matching texts;
  - New Message Banner = `NewMessageBanner`, Exit Button = `ExitButton`.

  Pages come from the level asset's **Case Files**. **Fallback Pages** is used only if the asset has none.
- **Buttons (persistent onClick, visible in the Inspector):**
  - `Tab_1`…`Tab_5` → `CaseFilesTerminal.ShowPage(0…4)`;
  - `ExitButton` → `CaseFilesTerminal.Exit()`.

  Tab labels are filled from the case-file titles at runtime, and unused tabs hide. These are your HUD-style buttons with the Digital Light Sweep.
- **WallDisplay › OpsWallDisplay:** Reviewed / Accuracy / Status texts and the progress bar `Fill`.
- **RackHum:** Output Group = the level's music mixer group (assigned by the builder), Volume 0.12.

## Level 3 asset
- **Briefing:** your briefing text and the four objectives. Case notes cover OPS-WS3 sign-in, order ED-48213, the password change and where the case files are.
- **Messages:** six, as specified, with ids `l3_signin_alert`, `l3_lookalike_bank`, `l3_it_password`, `l3_delivery_sms`, `l3_exec_giftcards` and `l3_password_confirm`. Each has a sender address, the requested action, a link note and neutral clues.
- **Fictional domains (reserved `.example` suffix, never opened):**
  - genuine: `examplecloud.example`, `examplebank.example`, `exampledelivery.example`, `yourcompany.example`;
  - lookalikes: `examplebank-protect.example`, `yourcompany-exec.example`.
- **Old inbox emails:** a welcome from the security team, a bank statement from the genuine bank domain, and the ED-48213 order confirmation.
- **Timing:** first message after 10 s, then 10–20 s after each answer. The activity trigger can bring a message in early, after at least 5 s.
- **Scoring:** the existing rules. Points per correct answer by speed are 100 / 80 / 60 / 40 / 20, wrong = 0, maximum 600.
- **Stars:** 1 = 1+, 2 = 435+, 3 = 520+. They're shown and saved, and never unlock anything.
- **Unlock:** `minScoreToUnlockNext` = 435 (about 73%) and `nextLevelId` = `level_4`. `nextSceneName` is empty, so the results show **NEXT ROOM: COMING SOON**. When Level 4 exists:
  - set its scene name here;
  - add its LevelDefinition to the Level Catalog;
  - widen the Level Selection panel, or make the cards narrower, because it fits three cards side by side.

## Tested (Editor Play Mode, buttons driven by script through their real onClick)
- **Briefing and HUD:** Room 3 starts with the briefing (title, your text, case notes, 4 objectives, controls, max 600, passing score 435). BEGIN INVESTIGATION starts the level. One EventSystem.
- **Case-files computer:**
  - the "E Open case files" prompt appears when looking at it;
  - the camera eases in and the cursor is freed;
  - a tab button switches the page;
  - a pointer raycast at the tab and EXIT screen positions hits those buttons;
  - EXIT restores the camera, FOV, mouse-look, movement and cursor lock;
  - the "NEW MESSAGE WAITING" banner shows while a message waits.
- **Message order:** messages arrive in order 1 → 6.
- **Emails:** they open on the main computer with the old inbox emails listed. A double-click on an answer scored once.
- **Text message:** it opened with the pocket phone (the phone comes out in a hoodie sleeve, the SMS is on the phone screen). The answer was scored, and CONTINUE put the phone away and restored control.
- **Run result:** 5 correct + 1 deliberately wrong = **420 / 600**, 2 stars.
- **Wall display:** updated live (1/6 → 6/6, accuracy, "INVESTIGATION COMPLETE").
- **Results screen:** correct/incorrect, stars, response time, the Level 3 performance message, "Room unlocked", the learning summary, lessons, and **NEXT ROOM: COMING SOON** (disabled), REPLAY LEVEL and MAIN MENU.
- **Save:** Level 3 completed and passed with best 420, and `level_4` unlocked.
- **Replay** reloaded the room with the score at 0 and kept the best score of 420.
- **Main Menu:** Level Selection shows 3 cards, with Level 3 "The Digital Trap" best 420, 2/3 stars and the Level 4 note. PLAY on the Level 3 card went through the account gate and loaded Room 3.
- **Console:** no errors from Room 3.

## Not tested
- **Real mouse and keyboard play:** walking with WASD, pressing E yourself, scrolling, and sitting in the desk chair in this room.
- **Level 2 → Room 3 through NEXT ROOM:** the setting was changed, and the button code is the same one tested in Level 2.
- **Resolutions and builds:** a 1920×1080 and 1366×768 check of the screens, and a standalone build.
- **Baked lighting:** not baked. The room uses realtime lights, like Rooms 1 and 2.

## Test data note
- **Local progress:** backed up before testing and restored afterwards (`Backups/2026-10-14_before_level3/progress_backup.txt`).
- **Cloud:** testing PLAY from the menu used **Play as guest**, which synced that guest player (`wXHpmikQzxJj7ZX32Risl9t2LQi7`) to Cloud Save with the test Level 3 result (420, Level 4 unlocked). The local copy is restored. Because cloud data merges by "best of", playing as that guest again will bring the test result back. To clear it, delete the `progress_v1` Cloud Save entry for that player, or delete that anonymous player, in the Unity Dashboard.

## Common problems
| Symptom | Fix |
|---|---|
| Level 3 card says "Scene missing from build" | Add `Assets/Scenes/Room3_DigitalTrap.unity` in File › Build Profiles › Scene List. |
| Room 3 plays nothing / Console says "assign a LevelDefinition" | `Level3 › GameManager › Level` must be `Level3_TheDigitalTrap`. Running **Rebuild Room 3 Environment** also re-assigns it. |
| E does nothing at the case-files monitor | Look straight at the monitor within 3 m. `CaseFilesTerminal/Body` needs its BoxCollider. |
| Changed text in `Level3Content.cs` but the game didn't change | That file only seeds the asset once. Edit `Assets/Levels/Level3_TheDigitalTrap.asset` in the Inspector. |
| Rebuild Room 3 Environment removed my decor edits | Rebuild regenerates everything under Environment except the gameplay objects (Workstation, Desk, Keyboard, Mouse, DeskLamp, DeskChair, PhoneTable, Phone). Make decor edits after rebuilding, or don't rebuild. |
