# Interactive Investigation Activities

The 30 curriculum questions are now played as cybersecurity investigations rather than a plain quiz: the player inspects a simulated email, browser, chat, call, form, Wi-Fi list, file explorer or evidence folder, pins key clues on an evidence board, and then answers. **What** each question teaches is unchanged. Only **how** it is answered changed.

## What did not change

- **Validated content:** question wording, answer choices, correct answers, explanations, learning objectives, curriculum references, difficulty and IDs all still come from `Docs/Curriculum_Question_Bank.md`. The generator compares every one of these fields against the previous build; there were 0 differences.
- **Scoring:** points, penalties, speed bonus, unlock and star scores, progression and saves work exactly as before (`ScoringSystem.TryRecordChoice` / `TryRecord`).
- **Shuffling:** questions and answer choices are still shuffled each attempt. Level 1 still starts with the guided question (E03).
- **Hints:** HINT is still a Level 1 tutorial feature only.

## Activities

| Activity | How it works | Questions |
|---|---|---|
| **Email Analysis** | A mail client: From, Subject, body, attachments, warning bars and buttons. Click underlined details. | E03, M10, H01 (+ case file), H03 (+ authentication results), H06 (raw headers) |
| **Website Inspection** | A browser: address bar, padlock, certificate note, page content, permission screens. | M02, M03, M05, H02, H10 |
| **Chat / Social Engineering** | Chat bubbles, file attachments and system notices. | E05 (file + antivirus request), E10 (blocked seller), M04 (Taglish "new number" texts) |
| **Call Transcript** | The caller's lines and the code received. | E04, M09 |
| **Form Inspection** | Form fields: which ones go too far for the purpose? | E07 |
| **Network Investigation** | A Wi-Fi list with lookalike networks and a sign-in page. | M07 |
| **File Investigation** | A file explorer with `.locked` files, a ransom note, network and power status. | H08 |
| **Evidence Board** | An evidence folder: which items can investigators trust? | H05 |
| **Message / Page Inspection** (SCAM or LEGITIMATE) | Clickable clues on the existing simulated text message, email or web page. | E09, M01, M08, H07, H09 (E02 and E06 already had them from the tutorial) |
| **Plain case sheet** (unchanged) | Concept questions with no scenario to investigate. | E01, E08, M06, H04 |

**Decision Challenges:** the "What Should You Do?" and decision-type scenario questions show a **WHAT HAPPENS NEXT** line after answering. The line describes the consequence of the choice that was picked, in green or red. This applies to E04, E05, E10, M05, M07, M09, H02, H03, H05, H08 and H10.

Formats from the brief that were **not** used: Drag and Drop, Match, Sequence and True/False. Each would change the question's answer format, and the validated questions are single-answer multiple choice.

## How an activity plays

1. **Open:** the case window shows the original question text, then the simulated app below it with a **TASK** line and the note *simulated · nothing is opened or sent*.
2. **Investigate:** underlined details (cyan) are clickable. Clicking one shows the finding on the **Evidence Board** under the app:
   - **★ KEY CLUE FOUND** (green) pins the clue to the board, and the detail turns gold with a ★.
   - **✓ CHECKED** (grey) means the detail is not evidence on its own.
   - Links show their simulated destination and are never opened.
3. **Answers locked:** the answer buttons stay locked until the minimum number of key clues is on the board. The card says how many are still needed. The player can press **ANSWER WITHOUT MORE EVIDENCE** to answer early. When every key clue is found, the board shows *INVESTIGATION COMPLETE*.
4. **Answer:** feedback works as before. In addition:
   - Decision questions show *What happens next*.
   - The board marks missed clues in red (*✗ … (missed)*), and every detail can still be clicked to review it.
   - In Level 1 the Case Review report replaces the app, as before.

**Clues needed to unlock (gradual difficulty):**

| Room | Clues needed |
|---|---|
| Room 1 | 1 clue (the guided question E03 is never locked) |
| Room 2 | 1 clue |
| Room 3 | 1 clue, except M04 (2) |
| Room 4 | 2 clues |
| Room 5 | 2 clues |

**Safety:**
- All addresses are fictional `.example` domains.
- Codes and passwords are masked (`••••••`).
- Nothing can be typed, and no real site is ever opened. A test enforces the address rule.

## Content that needs validation

The validated questions are untouched. The activity text, however, is new supporting content in `Tools/activity_content.py`, and the adviser or validator should review it:
- the simulated app items
- the clue findings, written from each question's own explanation
- the **What happens next** lines

Simulated details added beyond the question text, all neutral and fictional:
- **Sender/caller labels:** "Unknown player", "Unknown number", "Caller".
- **E10:** a chat line ("Paid na po…"), a receipt and a profile row.
- **E04:** the masked OTP text.
- **E07:** the form fields "Full name" and "Mobile number". The question text only names the ID photo, the birthdate and the missing privacy notice.
- **M03:** a certificate note ("checks that the requester controls this domain").
- **Domains:** `company-portal.login-verify.example` (M05) and `student-portal-login.example` (H10).
- **File names and student IDs:** H08 and H10.

## Files

| File | Change |
|---|---|
| `Scripts/Data/CaseData.cs` | Adds `MessageActivity`, `ActivityKind`, `ActivityRole` and `ActivityItem`, plus `ScamMessage.activity` / `HasActivity`. |
| `Scripts/Data/MessageInvestigationData.cs` | Adds `InspectField.Activity` and `MessageInspectable.part` (the activity item a clue sits in). |
| `Scripts/UI/ActivityView.cs` | **New.** A reusable view that draws any activity kind and its evidence board. |
| `Scripts/UI/MessagePopupUI.cs` | Hosts the activity in the case window; answer lock and skip button; *What happens next*; auto-fits the decision card to the screen; the case window grows downward. |
| `Scripts/Game/MessageInvestigation.cs` | Investigates any message that has clue data; `MarkActivity`; findings can go to the presenter's evidence board (`IInvestigationInline`). |
| `Scripts/Game/InvestigationSession.cs` | Validates activity clues; `KeyFound` / `KeyTotal`. |
| `Scripts/Game/IMessagePresenter.cs` | Adds the `IInvestigationInline` interface. |
| `Scripts/Game/ScoringSystem.cs` | Reading time also counts the activity's text. |
| `Editor/CurriculumQuestionsImporter.cs` | Imports activities, outcomes and clue parts. |
| `Tools/activity_content.py` | **New.** All activity content. |
| `Tools/build_game_questions.py` | Merges the activities and checks them: clue phrases exist, every clue can be found, one outcome per choice. |
| `Tests/Editor/LevelDataTests.cs` | New tests: all 30 questions present once; activities valid and fair (unlock count, outcomes, `.example` addresses); at least three investigations per room; gentle unlocking in Level 1. |
| `Levels/Level1-5 *.asset` | Re-imported with the activity data. |

## Changing activity content

1. Edit `Tools/activity_content.py`.
2. Run: `python Tools/build_game_questions.py Docs/Curriculum_Question_Bank.md Assets/Editor/CurriculumQuestions.json`. The script stops with a message if a clue phrase is missing or a clue can never be found.
3. In Unity, run **Tools › Scam Sense › Curriculum › Load Questions into Rooms**.

No other manual setup is needed: everything is built at runtime, with no prefabs or scene changes.
