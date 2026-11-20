# Scam Sense: Validation Form Revision Summary (2026-11-20)

## 1. Source check

The source was `ScamSense_Question_Validation_Form.pdf` (33 pages). I compared all 30 questions field by field against the PDF: subject and lesson, question or scenario, choices, correct answer, explanation, learning objective, game feedback, curriculum connection, difficulty and question type.

- **No questions were replaced, added, removed or reordered.** All 30 keep their numbers (1–30) and IDs (E01–H10).
- **The originals are backed up** in `Backups/2026-11-20_validation_form_before_rooms/`: the old PDF, DOCX and question bank.

## 2. Changes to question content (5 small fixes)

| Question | Field | Before | After | Why |
|---|---|---|---|---|
| Q6 (E06) | Scenario | `…your birth month! 👇"` | `…your birth month!"` | The 👇 emoji doesn't exist in Times New Roman, so it printed as a broken box. |
| Q24 (H04) | Explanation | "**B** is very likely to be exploited, because the password is already public…" | "**The admin account** is very likely to be exploited, because **its** password is already public…" | The choices will be shuffled, so "B" may no longer be the admin-account option. |
| Q24 (H04) | Explanation | "Training **(D)** matters…" | "**Staff training** matters…" | Same reason. |
| Q24 (H04) | Game Feedback | "Fix **B** first: …" | "Fix **the admin account** first: …" | Same reason. Players would see a letter that doesn't match their screen. |
| Q26 (H06) | Explanation | "…so **C** proves nothing." | "…so **the display name** proves nothing." | Same reason. |

I didn't change any other wording. I checked every question for references to an option letter, for choices like "all of the above", and for grammar. Nothing else needed fixing.

## 3. Changes to the form (design kept)

The letterhead, fonts, question page layout, rating scale, criteria, Retain/Revise/Replace options, comments box and overall evaluation are all the same as before. The form went from 33 to 34 pages.

1. **New page, "III. Room Distribution and Question Order".** It shows which six questions are in each room, their difficulty and subjects, and how the game will present them: shuffled each attempt, choices shuffled, no repeats.
2. **Each question page shows its room** above the question number, e.g., "Room 5: The Compromised Campus Network". The room names are the actual level names in the game.
3. **Difficulty Level is now listed as a criterion.** It has a Yes/No tick, because the 1–4 relevance/clarity scale doesn't fit "is the level right?"
4. **The Summary of Ratings is grouped by room.** It also has a Difficulty OK (Y/N) column.
5. **The later parts are renumbered:** IV is now Summary of Ratings and V is Overall Evaluation.

## 4. Room distribution

| Room | Game room | Questions | Difficulty |
|---|---|---|---|
| 1 | The First Case | Q1–Q6 (E01–E06) | 6 Easy |
| 2 | The Investigation | Q7–Q12 (E07–E10, M01–M02) | 4 Easy, 2 Medium |
| 3 | The Digital Trap | Q13–Q18 (M03–M08) | 6 Medium |
| 4 | Digital Forensics Lab | Q19–Q24 (M09–M10, H01–H04) | 2 Medium, 4 Hard |
| 5 | The Compromised Campus Network | Q25–Q30 (H05–H10) | 6 Hard |

That's 5 rooms × 6 questions = 30. Each question appears once, with no duplicates; the build script checks this.

The same information, with every question listed, is in `ScamSense_Room_Question_Summary.docx/.pdf`.

## 5. Curriculum connection review

I checked every Subject / Lesson against the five syllabi you provided: BIT1101, BIT1225, BIT2226, BIT2105 and BIT4233. Every course, topic and week number in the form exists in those syllabi. Items to review:

- **Weak links:**
  - Q4 (E04): the OTP question relies on the "Authentication" half of "Authentication and Access Control".
  - Q23 (H03): an *Adaptation* that reuses zero trust for verifying payments.
  - Q7 (E07): the data-privacy question is only indirectly about scams.
- **Level mismatch:** Q13 (M03) is Medium but cites a 4th-year course (BIT4233). Computer Security (BIT1225) Cryptography would also support it.
- **Labeling:** none of the syllabi name "phishing" or "scam". The form already labels those questions as *Application*, so don't describe them as direct syllabus content.
- **Extra subject:** the approval letter lists **Information Security Policy and Governance**, but there's no syllabus for it and no question uses it.
- **Syllabus errors:** the BIT4233 and BIT2105 syllabi contain course descriptions from other courses. Only their topic tables were used.

## 6. Other items needing review

- **Q14 (M04)** is in Taglish. Validators who don't read Filipino may need a translation.
- **Q6 (E06)** calls a data-harvesting post a "Scam". A validator may prefer "Unsafe".
- **Q18 (M08)** says banks no longer send links by text. Have a source ready, such as a BSP or bank advisory.
- **Q28 (H08)** advises leaving an infected PC on. Some response plans say to power it off; the explanation already says to follow your organization's plan.
- **Forensics questions** (Q10, Q18, Q25, Q26) don't land in Room 4 "Digital Forensics Lab" under the 1–6 / 7–12 numbering. This is fine if the room theme doesn't need to match the questions.

## 7. Consistency with the current game (important)

*Updated 2026-11-20 (later the same day): the game now matches the form.* The details are in `Docs/CurriculumQuestionRooms.md`.

1. **The 30 questions are the room content, six per room.** The five rooms ask exactly the questions in this form, in the same room order.
2. **Question order is shuffled once per attempt.** A Fisher–Yates shuffle runs each time the player enters a room or replays it. Each question appears exactly once, and a new attempt never repeats the room's previous order.
3. **Choices are shuffled too.** The 23 four-option questions shuffle their choices once per attempt. The other 7 are Scam / Legitimate messages. *Correction: an earlier version of this summary said 16 questions needed a four-option mode. The right number is 23.*
4. **Answers are checked by the choice, not the letter.** Scoring, room unlocking, stars and saved results all key on the question ID (E01–H10).

**In-game wording.** The question text, choices, answers, explanations and feedback come straight from the question bank. Two kinds of display change were made:

- **Scam or Legitimate items:** E02, E06, E09, M01, M08, H07 and H09 are shown as real messages. Their wording is arranged into sender, subject, body and link, and their case notes appear in the room's case notes.
- **Symbols:** the ✨ and ⚠ symbols are dropped, because the game font has no glyph for them.

**Room scores were recalculated for six questions:**

| Room | Unlock | Stars (1 / 2 / 3) |
|---|---|---|
| 1 | 380 | 270 / 380 / 600 |
| 2 | 550 | 375 / 550 / 900 |
| 3 | 720 | 480 / 720 / 1200 |
| 4 | 900 | 600 / 900 / 1500 |
| 5 | 1080 | 720 / 1080 / 1800 |

**When validators send revisions:** edit the question bank, regenerate the data with `Tools/build_game_questions.py`, then run **Tools > Scam Sense > Curriculum > Load Questions into Rooms** in Unity.
