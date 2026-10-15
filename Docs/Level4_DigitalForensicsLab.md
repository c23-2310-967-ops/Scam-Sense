# Level 4 — Digital Forensics Lab

Room 4 is a bright, clinical forensics lab. It's deliberately unlike Room 3's dark operations room: light walls, a concrete floor with a yellow safety walkway, a tiled ceiling, white desks and black chairs. The player classifies **seven reported communications** (emails, texts and a captured web page). They inspect evidence at two stations, then close **one final multi-evidence case** at a dedicated terminal.

Level 4 reuses the existing systems:
- QuizManager, ScoringSystem and GameManager;
- LocalDataManager and Cloud Save;
- ComputerTerminal, MessagePopupUI (with the PocketPhone), CaseFilesTerminal, OpsWallDisplay, BriefingUI, LevelHUD, ResultsUI, PauseMenu and LevelAudio.

It adds **no** second game manager, score system, save system, input handler or EventSystem.

## Files

### New
| Path | What it is |
|---|---|
| `Assets/Scenes/Room4_DigitalForensicsLab.unity` | The lab. It's a copy of Room 3, so the Player, EventSystem, managers (`Level4`), `Workstation`, `DeskChair` and `Phone` are the same working objects. The environment is new. |
| `Assets/Levels/Level4_DigitalForensicsLab.asset` | All Level 4 content (`level_4`). It's listed in the Level Catalog. |
| `Assets/Scripts/Game/EvidenceTracker.cs` | Remembers which evidence pages the player actually opened. |
| `Assets/Scripts/Game/FinalCaseTerminal.cs` | The final case station: locked checklist, then the case, SCAM / LEGITIMATE, **SUBMIT FINDINGS** and feedback. |
| `Assets/Editor/Room4Builder.cs` | Menu items **Tools › Scam Sense › Level 4 › Create Room 4 Scene** and **Rebuild Room 4 Environment**. |
| `Assets/Editor/Level4Content.cs` | The eight scenarios, evidence pages and texts. They're written to the asset once; after that, edit the **asset**. |
| `Assets/Materials/R4_*.mat` | 43 lab materials. |

### Modified (originals in `Backups/2026-10-15_before_level4/`)
| Path | Change |
|---|---|
| `Data/CaseData.cs` | `MessageChannel` gains `Website` and `Case`, appended so the saved values of SMS and Email don't change. Adds the `MessageChannels` helper. |
| `Game/QuizManager.cs` | Routes Website items to the computer and Case items to the final case terminal. Adds a notification hint for each. |
| `Game/ComputerTerminal.cs` | The inbox also lists web pages. These show "Site:", "Address bar:" and a "captured page (training copy)" label. |
| `Game/CaseFilesTerminal.cs` | Optional own page set (`useOwnPages`), used by the network station. Reports viewed pages to the EvidenceTracker, but only while the player is actually using it. |
| `UI/UIKit.cs` | Channel labels for web pages and cases. |
| `UI/MainMenuUI.cs` | Level cards scroll sideways (mouse wheel or drag), so four or more rooms fit without shrinking the cards. A hint appears when there are more than three. |
| `Editor/Room3Builder.cs` | Shared builder helpers made `internal` so Room 4 can reuse them. Room 3's own output is unchanged. |
| `Levels/Level3_TheDigitalTrap.asset` | `nextSceneName` = `Room4_DigitalForensicsLab`. |
| `Levels/LevelCatalog.asset` | Level 4 added. |
| Build Settings | Room4_DigitalForensicsLab added after Room 3. |

Rooms 1–3 and their scenes were not rebuilt.

## Room layout (12 × 9 m, 3.2 m ceiling)
| Zone | What's there |
|---|---|
| A · Entrance (south) | Steel door, access panel and the **DIGITAL FORENSICS LAB** sign. The player spawns here. A yellow walkway leads into the room. |
| B · Main workstation (north) | The email/web `Workstation` with tower PC and desk chair. Above it is the live **case dashboard**. The phone table is beside it. |
| C · Evidence wall (west) | The **EvidenceWallTerminal** with the four case files (*Initial Email, Domain Record, Follow-up Message, Case Notes*), plus printed evidence cards joined by red string. |
| D · Network analysis (east) | A side desk with the **NetworkAnalysisTerminal** (*Verified Domains, Account & Order Records, Message Timeline, Relationship Map*) and a traffic monitor. Above it is a window into the evidence server room. |
| E · Final case terminal (centre) | A pedestal with a tilted screen showing the **checklist**. It opens only when the case is ready. |
| Props | Lockers, a workbench, an equipment rack (soft fan hum), a backlit light table with evidence photos and bags, and a timeline whiteboard. |

## Scenarios
| # | id | Channel | Answer | Key clue |
|---|---|---|---|---|
| 1 | `l4_account_verification` | Email | SCAM | `novacloud-verify.example` isn't NovaCloud's verified domain; 24-hour threat. |
| 2 | `l4_security_notification` | Email | LEGIT | From `security@novacloud.example`; asks for nothing; says to open the app yourself. |
| 3 | `l4_payment_recovery` | Text | SCAM | "BrightPay" fee to recover money; you have no BrightPay account. |
| 4 | `l4_impersonated_announcement` | Email | SCAM | Look-alike Vertexa domain pushing a "security tool" download. |
| 5 | `l4_order_update` | Text | LEGIT | Matches your real order BW-55120; no link or payment request. |
| 6 | `l4_prize_claim` | Email | SCAM | Prize you never entered; asks for a "delivery fee". |
| 7 | `l4_suspicious_website` | Web page | SCAM | `novacl0ud-login.example` (zero for "o"), with an urgent banner asking for card details. |
| 8 | `l4_final_case` | Case | SCAM | Operation Billing Storm: unverified sender, a 3-day-old domain with a hidden owner on a shared server, and the same link in the follow-up text. |

All organisations, people and domains are fictional; every link uses `.example`.

## Flow and rules
1. Briefing → BEGIN. Messages 1–7 arrive one at a time. Emails and web pages arrive on the workstation; texts arrive on the phone.
2. The final case terminal shows a checklist from the start:
   - classify the reported communications (n/7);
   - inspect **Initial Email**, **Domain Record** and **Follow-up Message**.
   
   A page counts only when the player opens it at a station. Looking at the printed cards doesn't count.
3. When message 8 is pending **and** all three pieces of evidence have been inspected, the status turns green: READY · PRESS E. Until then E does nothing, and the screen says what's missing.
4. Opening the case starts its response timer. Pick SCAM or LEGITIMATE, then **SUBMIT FINDINGS**. The answer is submitted once and can't be changed. While answering, EXIT is disabled and Esc opens the pause menu.
5. Feedback → **FINISH CASE** → the results screen.

**Scoring** is the standard ScoringSystem: 100/80/60/40/20 points by speed, each message scored once. The maximum is 800.
- Stars: 1 star for finishing, 2 stars at 600, 3 stars at 680.
- **Unlocking the next room needs 600 points (75%).** Stars are only a reward.
- `nextLevelId` = `level_5` and `nextSceneName` is empty. The results screen shows **NEXT ROOM: COMING SOON**, and the unlock is still saved. Level Select doesn't add a fake Level 5 card; it shows a note on the Level 4 card.

## Editing
- **Text, answers, evidence, scoring:** select `Level4_DigitalForensicsLab.asset`. Keep the evidence titles the same as `FinalCaseTerminal → Required Evidence`, which is set on the terminal in the scene.
- **Network station pages:** these are on the `NetworkAnalysisTerminal` component (`Fallback Pages`, with *Use Own Pages* ticked).
- **Rebuild the environment:** use **Tools › Scam Sense › Level 4 › Rebuild Room 4 Environment**. It keeps the Workstation, Desk, chair, phone, player and managers.

## Tested (Play Mode, 2026-10-15)
- Menu → Room 4: one EventSystem and one camera; level assigned; briefing correct; the checklist is filled in during the briefing.
- All 8 scenarios played:
  - correct answers score by speed;
  - a deliberately wrong answer on #6 scores 0 and shows the right answer;
  - the web page shows "Is this web page a scam or legitimate?" and the address bar.
- Evidence: nothing counted before opening a station. Each page counts once. The terminal stayed locked with 2/3 evidence and unlocked at 3/3.
- Final case:
  - SUBMIT does nothing without a choice;
  - it scores once (re-submitting or changing the choice afterwards does nothing);
  - EXIT is blocked while answering.
- Results: 620/800, 7/8 correct, 2 stars, unlock message, NEXT ROOM: COMING SOON.
- Save: `level_4` completed and `level_5` unlocked; Levels 1–3 unchanged.
- Console: no errors. The only warnings were 4 existing Menu warnings (NewCaseTransition fields, menu video).
- Level Select: 4 cards in a scrolling row with the hint.

PlayerPrefs were backed up before testing and restored afterwards, and cloud sync was turned off during the tests.

## Limitations
- Navigation, looking and the E prompts were driven by test commands, not by walking and aiming with keyboard and mouse. The room should get one real walkthrough with keyboard and mouse.
- There is no Level 5. The unlock is stored, ready for when it's built.
