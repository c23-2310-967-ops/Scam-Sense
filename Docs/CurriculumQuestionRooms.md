# Curriculum Questions in the Five Rooms

The five rooms now ask the 30 validated curriculum questions, six per room, in a new random order on every attempt.

| Room | Level asset | Questions |
|---|---|---|
| 1 | `Level1_TheFirstCase` | Q1–Q6 (E01–E06) |
| 2 | `Level2_TheInvestigation` | Q7–Q12 (E07–E10, M01–M02) |
| 3 | `Level3_TheDigitalTrap` | Q13–Q18 (M03–M08) |
| 4 | `Level4_DigitalForensicsLab` | Q19–Q24 (M09–M10, H01–H04) |
| 5 | `Level5_MacLab` | Q25–Q30 (H05–H10) |

## How a question is asked

- **Four-choice questions (23):** they use the new `Question` channel. They open on the message pop-up: the scenario is on the left, and the four answer buttons are on the decision card.
- **Scam or Legitimate questions (7):** E02, E06, E09, M01, M08, H07 and H09 are normal messages (text, email or web page) with the SCAM / LEGITIMATE buttons, as before.

## Randomization (`QuestionShuffle`)

- **When it shuffles:** `QuizManager.Begin` starts every attempt (entering a room, Replay). It shuffles the room's questions with Fisher–Yates, and it shuffles each four-choice question's answers too.
- **Repeats:** every question appears exactly once per attempt. A new attempt never repeats the room's previous order.
- **Fixed during an attempt:** the order is set once and does not change while the player clicks or answers.
- **Letters:** A–D are added on screen by position. The data stores the choices without letters.
- **Answer checking:** answers are checked by the choice itself, not its letter. The pop-up reports the original choice index, and `ScoringSystem.TryRecordChoice` compares it with `correctChoice`.
- **Scoring and saves:** they use the question ID (`E01`…`H10`) as the message ID, as before. A wrong choice costs the room's normal penalty. It counts as an "answer to review", not as a missed scam or a false alarm.

## Changing the questions (for example, after validation)

1. Edit `Docs/Curriculum_Question_Bank.md`. It is the single source for the questions.
2. Regenerate the game data:
   ```
   python Tools/build_game_questions.py Docs/Curriculum_Question_Bank.md Assets/Editor/CurriculumQuestions.json
   ```
   The script stops if a room doesn't have exactly six questions, an answer key doesn't match its choices, or an ID is duplicated.
   - **Scam or Legitimate items are an exception.** Their message fields (sender, subject, body, link, case note) are set in `VERDICT_ITEMS` inside the script. Update them there if their wording changes.
3. In Unity, run **Tools > Scam Sense > Curriculum > Load Questions into Rooms**. It replaces each room's questions, briefing, objectives, case notes, case files and lessons. It also sets the star and unlock scores from the room's own scoring rules:

   | Score | Rule |
   |---|---|
   | 1 star | 3 correct |
   | 2 stars and unlock | 4 correct, allowing 2 wrong-answer penalties |
   | 3 stars | 6 correct |

4. Run the edit-mode tests. `LevelDataTests.Rooms_MatchTheValidatedQuestionBank` fails if a room asset is out of date with the data.

**Warning:** the old menu items **Tools > Scam Sense > Level 3/4/5 > …** (Level3Content, Level4Content, Level5Content) would write back the rooms' previous messages. Don't run them.

## Text notes

- Bold and italics in the bank become TextMeshPro tags in four-choice questions.
- Scam or Legitimate messages are shown as plain text, so they carry no tags.
- The ✨ and ⚠ symbols aren't in the game font, so they are left out. ₱ renders correctly.
