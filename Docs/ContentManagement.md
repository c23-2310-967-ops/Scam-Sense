# Content Management: the Question Bank

```
Teacher / Administrator  ──►  Supabase database  ──►  Unity (Scam Sense)  ──►  Students
manages questions and         stores questions,       retrieves the bank        play the scenarios
learning content (RLS)        answers, explanations,   (get_content_bundle)      and get feedback
                              levels, categories       + bundled offline copy
```

All learning content in Scam Sense lives in **data**, not in gameplay scripts. **Supabase is the content source.** The bundled JSON is the offline and fallback copy that ships with every build. That covers the questions, scam messages, scenarios, answers, choices, explanations, feedback, hints, clues and indicators, difficulty, category and level assignment. Anyone who can edit a JSON file (or, once the migration is applied, a Supabase table) can add, change, move, switch off or delete a question **without touching C#**.

| Where | What |
|---|---|
| Supabase `question_bank`, `content_levels`, `content_categories` | **The managed question bank.** Teachers and admins edit it (after the migration is applied). |
| `Assets/Resources/Content/question_bank.json` | The bundled copy: first run, offline, and fallback. It seeds Supabase. Developers edit it for releases. |
| `Assets/Levels/*.asset` | Level identity, scene, scoring rules, timing and unlock chain (unchanged). Also a baked copy of the questions, used only as the last-resort fallback. |
| `supabase/migrations/20261128004018_question_bank.sql` | Supabase tables, roles/RLS and `get_content_bundle()`. **Applied to the live project 2026-11-28** (recorded as migration `20261128004018 question_bank`). |
| `supabase/migrations/20261128004100_question_bank_seed.sql` | Generated: loads the bank into those tables (fills empty tables only). **Loaded live 2026-11-28** in per-level chunks through the SQL editor, so it isn't in the migration history; running it again changes nothing. |
| Tools > Scam Sense > Content | Validate, Bake fallback, Export Supabase seed, one-time migration. |

---

## 1. Architecture

```
 Supabase question_bank ──get_content_bundle()──► cloud cache file ─┐  1st
 (teachers + admins write via RLS)  CloudContentSync: sign-in and    │
                                    each scene load (≤ 1/min)        ▼
 local override file ───────────────────────────────► QuestionBankRepository ── first usable bank  2nd
 bundled question_bank.json (Resources) ─────────────►   (parse + QuestionBankValidator)           3rd
                                                                     │
                              LevelDefinition asset (scene, scoring, │
                              unlock chain, built-in fallback) ──────┤
                                                                     ▼
                                                     LevelContentResolver.Resolve(level)
                                        runtime copy: active questions for this level, in order,
                                        briefing/objectives/case files, star + unlock scores
                                                                     │
                                                                     ▼
                         GameManager.Level ──► QuizManager, ScoringSystem, popups, computers,
                                               phone, case files, results, career (unchanged)
```

* **One entry point.** `GameManager` swaps its level asset for `LevelContentResolver.Resolve(asset).level` before anything reads it. Every gameplay system already got its level from `GameManager.Level` / `QuizManager.Level`, so none of them changed.
* **The asset is never modified at runtime.** The resolver works on a copy (`Object.Instantiate`).
* **Sources, first usable wins.** (1) the last Supabase download, `{persistentDataPath}/content/question_bank_cloud.json`, (2) a local override, `{persistentDataPath}/content/question_bank_override.json`, used only on a PC that has never downloaded, (3) bundled `Resources/Content/question_bank.json`. The game checks Supabase at sign-in and on each scene load (at most once a minute). A new download is used from the next level that loads; a level in progress never changes. A file that is missing, isn't valid JSON, or has a newer `schemaVersion` is skipped and logged.
* **Fallbacks, per level.** No usable bank, no record for the level, or no playable question → the questions baked into the `LevelDefinition` asset. Still none → a safe **"THIS CASE CAN'T START"** screen with a MAIN MENU button (`ContentErrorScreen`). Saved progress is never touched.
* **Bad questions don't break a level.** A question with an error (see §5) is left out, and the rest of the level plays. Scores and the briefing adjust to the new count.
* **Scores follow the content.** Star and unlock scores are worked out from the level's own scoring rules and its question count (`LevelContentRules`, also mirrored in SQL). Adding a question raises the unlock score proportionally, and removing one lowers it.

### Code (all in `Assets/Scripts/Content/` unless noted)

| File | Responsibility |
|---|---|
| `QuestionBankModels.cs` | Serializable records (schema v2), `ContentSource`, `ContentIssue`. |
| `QuestionMapper.cs` | Record ⇄ `ScamMessage`. Default rules for empty fields. |
| `QuestionBankValidator.cs` | File-level and per-question checks (errors vs warnings). |
| `QuestionBankRepository.cs` | Finds, parses and caches the bank (sources above). |
| `LevelContentResolver.cs` | Builds the playable level and handles the fallbacks. |
| `LevelContentRules.cs` | Star and unlock scores, briefing, objectives and case-file pages. |
| `CloudContentSync.cs` | Downloads the Supabase bundle once per session after sign-in. |
| `Assets/Scripts/UI/ContentErrorScreen.cs` | The safe error screen. |
| `Assets/Editor/QuestionBankTools.cs` | Editor menu: validate, bake, export seed, migrate. |

---

## 2. JSON file vs Supabase table (and the choice made)

| | Bundled JSON file | Supabase tables |
|---|---|---|
| Works offline / in the lab with no internet | ✅ always | Only after a download (cached) |
| Change content without a new build | ❌ needs a rebuild (or the local override file on one PC) | ✅ next level load after sign-in |
| Who can edit | Developers (Git, Unity) | Authorized editors through the dashboard / API, with RLS |
| Review and history | Git diff | `question_bank_history` table (every old version) |
| Validation | Editor menu + automated tests | DB trigger (core rules) + the game's validator |
| Risk | Low | A bad edit reaches every class: needs editor-only access |

**Decision: Supabase is the content source, with the bundled JSON as the offline and fallback copy.** Teachers and admins manage content in Supabase under RLS and roles. The game downloads it and caches it locally. The bundled JSON seeds the database, is tested on every run, and is what plays on first launch, offline, or if the server's content can't be read. That last case matters for school labs with unreliable internet. No dashboard is built yet: teachers and admins use the Supabase Table Editor or the API.

---

## 3. Schema (version 2)

### File

```jsonc
{
  "schemaVersion": 2,                 // the game refuses newer versions it doesn't understand
  "contentVersion": "2026-11-28.1",   // change on every edit (shown in logs)
  "source": "...",
  "categories": ["Phishing", "Smishing", "Vishing", "Fake Websites", "Online Scams", "Social Engineering",
                 "Malware", "Account Security", "Data Privacy", "Network Security", "Incident Response"],
  "levels":    [ /* LevelContentRecord, one per level */ ],
  "questions": [ /* QuestionRecord */ ]
}
```

### Level record (`levels[]`)

| Field | Meaning |
|---|---|
| `levelId` | Must match the `LevelDefinition.levelId` (`level_1` … `level_5`). |
| `intro` | One line on what the room teaches (start of the generated briefing). |
| `briefingTitle`, `briefing`, `objectives[]` | Optional overrides. `{unlock}` and `{count}` are filled in. Empty = generated. |
| `caseNotes[]` | Facts the player knows (case notes and the case-files computer). |
| `lessons[]`, `learningSummary` | Results screen. |
| `tutorial`, `firstQuestion` | Runs the guide (Level 1 only). `firstQuestion` is asked first. |
| `inbox[]` | Older already-read emails in the computer inbox (question records, never scored). |

### Question record (`questions[]`), based on the sample (`questionId, level, category, difficulty, messageType, sender, subject, message, isScam, explanation, indicators[], isActive`)

| Field | Meaning | Default when empty |
|---|---|---|
| `questionId` | Unique. Letters, digits, `-`, `_` (max 40). Don't rename once released (results refer to it). | required |
| `levelId` | Level that asks it. `""` = reserve (not used). | reserve |
| `order` | Position before the per-attempt shuffle (lower first). | 0 |
| `isActive` | `false` = never shown, kept in the bank. | `true` |
| `category` | One of `categories` (otherwise a warning). | — |
| `difficulty` | `Easy`, `Medium`, `Hard`. | — |
| `questionType` | Label only ("Scam or Legitimate", "Multiple Choice", "What Should You Do?", "Scenario-Based"…). | — |
| `messageType` | **Where it's delivered:** `SMS` / `Chat` → phone · `Email` / `Website` → computer · `Question` → case-question window · `Case` → final-case terminal. | required |
| `sender`, `senderDetail`, `subject`, `message`, `link` | The message. Rich text allowed. Links use `.example` domains only. | — |
| `scenarioContext` | Background the player knows (case notes). | — |
| `isScam` | The answer for SCAM / LEGITIMATE questions. | `true` |
| `choices[]`, `correctChoice` | 2+ choices = a choice question. `correctChoice` is 0-based in the **original** order (the game shuffles them). No "A." prefixes. Needs `messageType: "Question"`. | none |
| `explanation`, `feedback`, `hint` | Feedback screen. `hint` must not give the answer (≤140 chars). | — |
| `learningObjective`, `curriculumRef` | Curriculum link (`"Course — Topic"`). Used by teacher records. | — |
| `trigger` | `TimeOnly`, `PlayerWalked`, `NearComputer`, `NearPhone`. | by type |
| `readingSeconds` | Reading allowance. `0` = from length. | 0 |
| `stationId` | Email/web: which computer. | any |
| `requestedAction`, `linkNote`, `detailNotes[]` | The **Details panel** (neutral observations). | — |
| `clues[]` | **Clickable spans:** `{id, field (Sender/SenderDetail/Subject/Body/Link/Activity), match (exact words, "" = whole field), label, finding, linkAddress, indicatorId, part (activity item key)}`. | — |
| `indicators[]` | What clues reveal: `{id, title, whyItMatters, isKeyClue}`. **Key clues glow gold** and count for the evidence score. | — |
| `activity` | Simulated app (`kind`: Email, Browser, Chat, Call, Social, Form, Network, Files, Evidence), `items[] {key, role, label, text}`, `minClues` to unlock answers, `outcomes[]` (one per choice). | none |
| `consequence` | Wrong answer: `kind` (Auto / SecurityIncident / AccuracyWarning / MissedClue / FeedbackOnly), `alertTitle`, `alertBody`, `overlookedClue`, `followUp` (Auto / None = **follow-up re-check**), `recheckClues[]`. | choice → FeedbackOnly; re-check only for SCAM/LEGIT in the tutorial room |
| `notes` | Author's notes (not shown). | — |

Fields you leave out take their defaults, so a minimal question is enough to play:

```json
{ "questionId": "T01", "levelId": "level_2", "order": 70, "category": "Smishing", "difficulty": "Easy",
  "messageType": "SMS", "sender": "+63 900 000 0000",
  "message": "Congratulations! You won a phone. Claim it at the link today.",
  "link": "http://prize-claim.example/now", "isScam": true,
  "explanation": "You didn't join any raffle, and real prizes never need a link and a deadline.",
  "feedback": "Scam! An unexpected prize with a rush is bait.", "hint": "Did you join any raffle?" }
```

---

## 4. How to…

### In the game: CLASSROOM (admins and teachers)

Who can do what is decided by the server, not by the buttons (see §6). Sign in, open **CLASSROOM** on the main menu, and the panel shows your portal:

| Signed in as | Portal | Question tabs |
|---|---|---|
| Admin (not a teacher) | **ADMIN PORTAL** | REQUESTS, QUESTIONS |
| Teacher | **TEACHER PORTAL** | QUESTIONS, MY REQUESTS (plus the class tabs) |
| Teacher who is also an admin | **TEACHER PORTAL · ADMIN** | QUESTIONS, REQUESTS (plus the class tabs) |
| Student / guest | CLASSROOM (join a class, results) | none |

**QUESTIONS: browsing (everyone with access).** L1–L5 show each level's questions (ID, text, type, category, on/off). RESERVE shows validated questions not in a level, OFF shows switched-off ones, ALL shows everything. For teachers, a question with your pending request is marked *· requested*.

**The editor.** Open a question (EDIT for admins, VIEW / REQUEST for teachers) or start a new one with the two NEW buttons. The editor covers:
* the level (or reserve), position, on/off, category and difficulty
* the answer style (SCAM or LEGITIMATE / multiple choice) and how the message arrives (text, chat, email, web page)
* the sender, subject, message or question, link and background
* the choices and **which one is correct** (MAKE CORRECT), or SCAM / LEGITIMATE
* the explanation, feedback and hint

**CHECK** runs the game's own validator (✗ blocks, ! is advice). The editor keeps clickable clues, activities and wrong-answer settings as they are. If an edit removes words a clue points at, CHECK offers **FIX CLUES**.

**Admins** change the published questions directly:
* **SAVE** writes to Supabase. The server checks the question again, keeps the old version in history, and updates the level's pass score. Students get the change the next time they load a level.
* **TURN OFF / ON** hides or restores a question (it stays in the bank).
* **DELETE** (press twice) removes a question. The history keeps a copy.
* **HISTORY** lists every earlier version: when, who, and which fields changed.

**Teachers** never change the published questions. Every change is a request:
* Make the change in the editor, write a **REASON** (required), and press **SUBMIT REQUEST**. This covers new questions, edits, moving a question to another level, and turning it on or off.
* **REQUEST DELETE** (press twice) asks for a question to be removed.
* **MY REQUESTS** lists each request with its status (Pending, Approved, Rejected, Withdrawn) and the admin's remarks. **VIEW** shows what you asked for, and **WITHDRAW** takes back a pending one.
* One pending request per question at a time. To change a pending request, withdraw it and send a new one.

**Admins: REQUESTS (the approval queue).**
* PENDING comes first. APPROVED, REJECTED and ALL (HISTORY) show past decisions.
* **REVIEW** opens a request: the teacher's reason, and a field-by-field **CURRENT → PROPOSED** comparison (the whole proposal for an add, the question for a delete). A warning appears if the question changed after the request was sent.
* **APPROVE** applies the change on the server in the same step. The game's checks run first, and an add whose id has been taken is refused.
* **REJECT** needs a remark, which the teacher reads in MY REQUESTS.

**Timing.** A change to a level's question count (an add, a delete, or a question turned on or off) takes effect at once. Approve those between class sessions: a student mid-level finishes with the old set, and that run may then be rejected when it uploads.

Code:
* `Assets/Scripts/UI/ClassroomUI.Questions.cs` (library and editor)
* `Assets/Scripts/UI/ClassroomUI.Requests.cs` (requests, review)
* `Assets/Scripts/Content/QuestionEditing.cs` (rules and the comparison, unit-tested)
* `Assets/Scripts/Online/QuestionBankService.cs` (Supabase calls under the user's own sign-in)
### Developers: the bundled file

Edit `Assets/Resources/Content/question_bank.json`, bump `contentVersion`, then run **Tools > Scam Sense > Content > Validate Question Bank** (it lists errors and shows what each level will play). Play the room to check it.

* **Add a question.** Append a record to `questions` with a new `questionId` and the `levelId` / `order` you want. To add clickable clues, give each clue a `match` that appears word for word in its field, and an `indicatorId` from `indicators`.
* **Edit a question.** Change the text fields. If you change words that a clue's `match` points at, update the `match` too (the validator reports it).
* **Change the correct answer.** Set `correctChoice` (0-based, original order) or `isScam`. Update `explanation`, `feedback` and any activity `outcomes` to match.
* **Turn a question off / on.** Set `isActive` to `false` or `true`.
* **Move a question to another level.** Change `levelId` (and `order`). `""` puts it in the reserve.
* **Delete a question.** Prefer `isActive: false` (it keeps classroom history readable). To really delete, remove the record. Never reuse its id.
* **Add a category.** Add the name to `categories`, then use it.
* **Add a new scenario room's texts.** Edit that level's record in `levels`. A brand-new level still needs its `LevelDefinition` asset and scene (gameplay), then a `levels` record and questions.

Then, before committing:

1. **Tools > Scam Sense > Content > Bake Fallback Content into Levels**. This keeps the built-in fallback in the level assets in step (a test checks it).
2. **Tools > Scam Sense > Content > Export Supabase Seed**. This keeps the seed in step (a test checks it).
3. **Tools > Scam Sense > Run All Tests**.

**Testing an edit in a build without rebuilding:** copy the edited file to `%USERPROFILE%\AppData\LocalLow\<company>\<product>\content\question_bank_override.json`. It is used when that PC has no Supabase download (`question_bank_cloud.json` in the same folder); delete the download to test offline. Delete the override to go back.

**Publishing bundled edits to Supabase:** the seed only fills empty tables. Once the server has content, make the same change in the Table Editor (or upsert the row), so the server stays the source.

### With Supabase (live since 2026-11-28)

* **Admins** edit `question_bank` directly, in the game (CLASSROOM > QUESTIONS) or the Table Editor. **Teachers** send requests that an admin approves (§4). Make someone an admin: see §6, *Creating the first admin*. Edits are global: every class plays the same bank.
* **Edit** rows in `question_bank` (Table Editor). Management columns: `level_id`, `sort_order`, `is_active`, `category`, `difficulty`, `question_type`, `message_type`. Everything else lives in `content` (jsonb, same field names as the file). Level texts are in `content_levels.content`.
* The database refuses an **active** question that breaks a core rule (empty text or explanation, bad `correctChoice`, duplicate or empty choices, a missing email subject…). It also keeps every previous version in `question_bank_history` and updates the classroom catalog (`activities.question_count / max_score / pass_score`, `questions`), so `submit_game_result` keeps accepting runs.
* Players get the new content at the next level load after they sign in (`CloudContentSync`). Offline they keep the last download or the bundled file.
* Clickable-span checks (a clue's `match` must exist in its field) run only in the game. Validate in the editor before publishing big edits: a question the game drops would make the server's question count differ from the run.

---

## 5. Validation rules

**Errors**, which leave the question out (or reject the file when file-level):
* File: not JSON, `schemaVersion` missing or newer, no `questions` / `levels`, a level listed twice.
* Question: empty or duplicate or badly formed id · unknown `messageType`, `trigger`, clue `field`, activity `kind` / `role`, consequence `kind` / `followUp` · empty `message` or `explanation` · email or web page without `subject` · 1 choice · `correctChoice` out of range · empty or duplicate choices · choices on a non-`Question` type · activity without choices or items · `minClues` above the number of key clues · outcomes not one per choice · a clue pointing at a missing indicator or activity item, or at words that aren't in the field.

**Warnings**, which never block anything: no hint / feedback / category, unknown category, a hint that gives the answer away or is too long, choices with "A." labels, a level with 0 or more than 8 active questions, `firstQuestion` not active in the tutorial level, a question assigned to a level that doesn't exist. Inactive questions report only their errors, as warnings.

---

## 6. Roles and permissions

| Role | How it's granted | Questions (enforced by the server) |
|---|---|---|
| **Student / guest** | default | Plays the published (active, assigned) questions through `get_content_bundle()`. Can't read requests, submit requests, or change any question. Answers are recorded per question in `question_results` (through `submit_game_result`). |
| **Teacher** | `user_roles.role = 'teacher'` (teacher invite code) | Reads every question, including inactive and reserve ones. Submits ADD / EDIT / DELETE requests with a reason, reads their own requests, and withdraws pending ones. **No direct writes**: inserting is refused, and updates or deletes affect 0 rows. |
| **Admin** | a row in `public.content_admins`, added in the Supabase SQL editor only | Adds, edits, deletes, turns on or off and moves questions directly. Reviews every teacher request (approve applies it; reject needs a remark), reads the change history, and manages categories and level texts. |

* Guests are never teachers or admins (`is_anonymous` is checked).
* Every call uses the user's own sign-in. The game never holds a service or admin key.
* The Supabase dashboard bypasses RLS, so give dashboard access to developers only.

**Database objects**

| Spec | In Scam Sense |
|---|---|
| users, role | `auth.users` + `profiles` (name) + `user_roles` (teacher) + `content_admins` (admin) |
| questions | `question_bank`: id, level, position, active, category, difficulty, type and message type, plus the full question as `content` |
| question history | `question_bank_history`: every previous version, who changed it and when |
| question_requests | `question_requests`: teacher, type, question, proposed row, reason, status, admin remarks, reviewer and times |
| student_answers | `question_results`: one row per question per finished run (first answer right or wrong, recovered, hint used, seconds). The chosen option itself isn't stored. |

Migrations: `20261128004018_question_bank.sql`, `20261128004100_question_bank_seed.sql`, `20261128101505_question_requests.sql`.

### Creating the first admin

1. Have the person create an account in the game (CLASSROOM, then sign up). Guests can't be admins.
2. In Supabase, open **Authentication > Users** and copy their user id.
3. In the **SQL Editor**, run:

   ```sql
   insert into public.content_admins (user_id, label) values ('<their user id>', 'Their name');
   ```

4. They sign in again (or press REFRESH in CLASSROOM). The panel shows **ADMIN PORTAL**, or **TEACHER PORTAL · ADMIN** if they're also a teacher.

To remove an admin: `delete from public.content_admins where user_id = '<id>';`. To make someone a teacher, use the teacher invite code flow (`Docs/ClassroomGuide.md`).

### Checked on the live project (2026-11-28, all inside transactions that were rolled back)

**Before the approval workflow** (the editor migration):
* A teacher could edit directly.
* The catalog moved 6 → 7 → 6 questions (pass 450 → 570 → 450).
* An invalid answer was refused.
* A student could neither update nor insert.

**With `20261128101505_question_requests.sql`:**
* **Teacher:**
  * A direct insert was refused, and a direct update affected 0 rows.
  * Add, edit and delete requests were accepted. A duplicate request and an invalid answer were refused. Withdraw worked.
  * The admin request list and approving were refused.
* **Student:**
  * Submitting, approving, reading requests and updating questions were all refused or saw 0 rows.
  * The student still plays all 28 questions.
* **Admin:**
  * Saw 3 pending requests.
  * Approving the add published T98, and Level 2 became 7 questions (pass 570).
  * Approving the edit changed E08's answer.
  * A rejection without a remark was refused; a rejection with a remark went through, and M06 was kept.
  * Reviewing the same request twice was refused, and the history recorded the change.
* **Teacher (after review):** saw each decision with the admin's remarks.

### Limitations

* **Images and attachments:** a question can't carry an image file. Attachments are shown as text, either in the message or as a File row in an activity (e.g. `Grades_Final.docm`). Adding images would need image storage and a change to the message window.
* **Clickable clues, activities and wrong-answer settings** are kept as they are by the editor and in requests. Only the admin (or a developer) edits them, in the Supabase Table Editor or `question_bank.json`.
* **The bundled offline copy** (`question_bank.json`) doesn't update itself from Supabase. Refresh it before each release.
* **A changed question count** applies at once; students mid-level finish with the old set.

---
## 7. Migrated content

* 28 room questions (Level 1: E02, E03, L01, L02 · Level 2: E07–E10, M01, M02 · Level 3: M03–M08 · Level 4: M09, M10, H01–H04 · Level 5: H05–H10), with all their clues, indicators, activities, hints and feedback, each given a category.
* 4 validated reserve questions (E01, E04, E05, E06): active, `levelId: ""`.
* 9 inbox emails (Levels 2–5) and each room's texts (intro, tutorial briefing and objectives, case notes, lessons).
* 20 pre-curriculum messages from the old `Level3Content` / `Level4Content` / `Level5Content` builders, which the game no longer showed: kept with `isActive: false` and a note, so nothing is lost.
* Checked by tests: every level resolves to exactly the same messages, texts, star and unlock scores as before the refactor. Every legacy question maps to the same game message, and every message round-trips through the mapper unchanged.

**Legacy sources (don't edit them for the game):** `Assets/Editor/CurriculumQuestions.json`, `Tools/build_game_questions.py`, `Tools/activity_content.py`, `Tools/apply_plain_language.ps1` / `plain_language_edits.json`, `Docs/Curriculum_Question_Bank.md` (the validated wording, still the reference document), and `Assets/Editor/Level3Content.cs`–`Level5Content.cs`, `MessageInvestigationContent.cs`. Their "load into rooms" menu items were removed. The scene builders `Room3Builder` / `Room4Builder` / `Room5Builder` still call the old `LevelXContent.Fill` on the level asset. That doesn't change what is played, since the bank wins, but it does overwrite the built-in fallback. After rebuilding a room, run **Bake Fallback Content into Levels** (the tests fail until you do). **Tools > Scam Sense > Content > Migrate Legacy Content to Bank** rebuilds the bank from them, but it **overwrites** edits made in the bank since.
