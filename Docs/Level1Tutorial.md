# Level 1 Tutorial

Level 1 ("The First Case") is the game's introduction. A guide teaches new players on their first question, while they actually play. Levels 2–5 are unchanged: everything below is switched on only by `LevelDefinition.tutorial`, and only `Level1_TheFirstCase.asset` has it set. The `OnlyLevel1_IsTheTutorial` test enforces this.

## What the player sees

1. **Briefing:** *Welcome, Investigator.* A short story intro: you've joined the Cybercrime Investigation Unit, and this room is your training case.
2. **Welcome card:** your role (cybercrime investigator), why spotting scams matters, the goal (**SCAM** vs **LEGITIMATE**, answer each case question) and the unlock score. The player clicks **LET'S START**.
3. **Controls 1 of 3 · Move and look:** W A S D to walk, the mouse to look, Space to jump. The step finishes on its own once the player has walked about 1.5 m and turned 25°.
4. **Controls 2 of 3 · Interact:** look at the door, a chair, the phone or a computer and press E. E or Space stands you up from a chair. The step finishes when E is pressed.
5. **Controls 3 of 3 · Menus:** Tab shows or hides the task list. Esc or P opens the pause menu, and Esc also closes a computer or card. When a window is open, click things; click the game again to go back to looking around.
6. **Get ready:** "Your first question is on its way", then "Press E to open it" when it arrives.
7. **Step 1 of 5 · Warning signs:** what to look for: sender, links, pressure, requests for passwords, codes, money or personal details, and spelling. One sign alone doesn't prove a scam; weigh all the evidence.
8. **Step 2 of 5 · Underlined clues:** click an underlined word. "Nice find!" explains that a gold ★ marks a key clue.
9. **Step 3 of 5 · Hints:** the **HINT** button is framed. A hint never gives the answer. The player can press it or click **SKIP HINT**.
10. **Step 4 of 5 · Answer:** the answer buttons are framed. Clicking an answer submits it straight away, and the A–D order changes every time.
11. **Step 5 of 5 · Feedback:** the feedback is framed: ✓ / ×, the *Why* section, and the clue list showing what was found and missed.
12. **Case Review:** the case-question sheet has turned into a case report with the correct answer, the clues to notice (✓ found, • missed) and what you learned.
13. **Training complete**, then a one-time **Scam or legitimate?** tip when the first SCAM / LEGITIMATE message appears.

Every card with **NEXT** also continues with **Enter**, because the mouse pointer is hidden while walking around. If the player opens the first question during the controls steps, the guide jumps straight to the question steps.

## Controls taught (from the project's actual bindings)

| Key | Action | Source |
|---|---|---|
| W A S D | Walk | `Input/PlayerInput.inputactions` (OnFoot/Movement) |
| Space | Jump; stand up from a chair | PlayerInput (Jump), `SittableChair.altStandKey` |
| Mouse | Look around | `MouseLook` |
| E | Interact: phone, computers, chairs, doors, case files | `PlayerInteractor`, `QuizManager`, `ComputerTerminal`, `SittableChair` |
| Left click | Buttons and underlined words; return to mouse look | UI, `MouseLook` |
| Tab | Show or hide the task list | `LevelHUD.toggleTasksKey` |
| Esc / P | Pause menu; Esc also closes a computer or card | `PauseMenu.pauseKey` / `altPauseKey`, `InteractionGate` |
| Enter | Begin the briefing; continue a guide card | `BriefingUI`, `TutorialCoach` |

## Case Review (Level 1 only)

In Level 1, the case-question sheet ("Case Review") uses a dark, high-contrast report style:
- **Style:** a navy panel with a light border; a title bar reading *CASE REVIEW · QUESTION n OF 6*; the question type as a cyan heading; *THE CASE* section with near-white 28 pt text. The blue underlined clues are now readable; they were faint on the old white sheet.
- **After answering**, the sheet becomes a case report: a coloured verdict box (✓ SOLVED / × NOT QUITE, with the correct answer and yours), **Clues to notice** (✓ found, • missed, with why each matters) and **What you learned** (the question's learning objective).

Levels 2–5 keep the original light sheet unchanged. The style is chosen by `LevelDefinition.tutorial`.

**How the guide behaves:**
- **SKIP TUTORIAL** on every card ends the guide.
- Cards never cover the answer buttons, and they step aside for the wrong-answer panel and the pause menu.
- The guide runs on every attempt of Level 1. The guided first question is always **Q3 (E03, phishing email)**. The other five questions, and all answer choices, are still shuffled.

## Hint and underline features

- **Underlines** use the existing message-investigation system: blue underlined details, a finding card, a gold ★ for key clues, and the feedback's clue list.
  - In the tutorial room, every message type is investigated: case questions, texts and web pages.
  - Since the interactive activities update, the other rooms also investigate the messages and activities that carry clue data (see `InteractiveActivities.md`). E03, E04 and E05 are now played as an email, a call transcript and a chat. E03 is still the guided question, and its answers are never locked.
- **HINT** (`HintControl`) appears on the phone pop-up and the computer screen, but only for messages with a `hint` (Level 1's six questions).
  - Each hint is recorded once and shown as *Hints* on the results screen.
  - Hints cost no points.
- **Follow-up re-check:** on Level 1's two SCAM / LEGITIMATE messages, a wrong answer also offers the existing follow-up re-check: re-inspect the key clues, use the follow-up HINT, and answer again for recovery points.

## Content

The clue details and hints for E01–E06 are in `TUTORIAL` in `Tools/build_game_questions.py`. The briefing, objectives and guided first question are in `LEVEL1`.

To change them:
1. Edit the script and rerun it.
2. In Unity, run **Tools › Scam Sense › Curriculum › Load Level 1 (Tutorial) Only**. This rewrites only `Level1_TheFirstCase.asset`.

The question wording, choices and answers still come from `Docs/Curriculum_Question_Bank.md` and are not changed.

## Files

| File | Change |
|---|---|
| `Scripts/Game/TutorialCoach.cs` | New. The step-by-step guide: cards, a pulsing frame and a label on the control to use. |
| `Scripts/UI/HintControl.cs` | New. HINT button and hint box, shown only for messages with a hint. |
| `Scripts/Data/LevelDefinition.cs` | Adds `tutorial` and `tutorialFirstQuestion`. |
| `Scripts/Data/CaseData.cs` | Adds `ScamMessage.hint`. |
| `Scripts/Game/QuizManager.cs` | Adds the guide in tutorial rooms; pins the guided question first; evaluates clues for choice answers. |
| `Scripts/Game/QuestionShuffle.cs` | Optional pinned first question (the rest are still Fisher–Yates shuffled). |
| `Scripts/Game/MessageInvestigation.cs` | Non-email investigation in tutorial rooms only; `MarkRich` underlines phrases in case-question text. |
| `Scripts/UI/MessagePopupUI.cs` | HINT control, underlined case-question text, Level 1 Case Review style and case report, and the screen areas the guide can frame (`ITutorialTargets`). |
| `Scripts/Game/ComputerTerminal.cs` | HINT control and the screen areas the guide can frame. |
| `Editor/CurriculumQuestionsImporter.cs` | Imports hints, clue details and tutorial settings; adds a *Level 1 only* menu. |
| `Levels/Level1_TheFirstCase.asset` | Hints, clue details, intro briefing, objectives, tutorial flag. |
