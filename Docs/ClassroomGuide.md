# Classroom guide (teachers and students)

Scam Sense has a **CLASSROOM** button on the main menu. It opens one of two portals, depending on the role stored on the server for your account:

| Role | How you get it | What you see |
|---|---|---|
| Student | Every account starts as a student. | Your class, your levels, every attempt with feedback, topics to review, achievements. |
| Teacher | Sign in, open CLASSROOM, and enter the **teacher code** from your school under *Are you a teacher?* | Dashboard, students, analytics, reports (CSV), and class management. |

Guests and offline play work as before. Guests can see their own results, but joining a class needs an account: **CREATE ACCOUNT (KEEP PROGRESS)** turns the guest into an account without losing anything.

## For students

1. **Sign in or create an account.** Use the menu's account chip, or CLASSROOM > SIGN IN / CREATE ACCOUNT.
2. **Join your class.** In CLASSROOM > OVERVIEW, type the 6-character class code your teacher gives you (for example `AB3-K7P`), then press **JOIN CLASS**. Codes never use O, 0, I or 1, and the dash is optional.
3. **Play.** Press **PLAY** in the portal or on the menu. Every level you finish is added to your class record automatically: your score, correct and incorrect answers, the attempt number, and which questions you missed. You don't have to submit anything.
4. **Check your progress:**
   - **OVERVIEW:** levels completed, best scores, attempts and achievements.
   - **MY RESULTS:** every attempt, newest first, with feedback that lists what to review. Replays add a new attempt and never overwrite an old one.
   - **TOPICS TO REVIEW:** topics where you missed at least a third of your first answers, with their learning objectives and which level to replay.
   - **ACHIEVEMENTS:** what you've unlocked.
5. **No internet?** Keep playing. Results wait on your PC and upload when you're back online ("uploading" in MY RESULTS).

**LEAVE CLASS** (press it twice to confirm) keeps all your progress. Joining another class moves you there; you can only be in one class at a time.

## For teachers

1. **Get teacher access.** Create an account (or sign in), open CLASSROOM, enter your teacher code under *Are you a teacher?* and press **ACTIVATE TEACHER PORTAL**. A teacher account can't join classes as a student.
2. **CLASSES:** type a class name and press **CREATE CLASS**. Each class gets a unique code. Write it on the board. **ARCHIVE** (press twice) stops new students joining and keeps the records. **RESTORE** reopens the class. **OPEN** switches to that class. **◀ / NEXT CLASS ▶** also switch between classes.
   - **Archived classes are hidden** from the list. **SHOW ARCHIVED (n)** brings them back, and **HIDE ARCHIVED** hides them again.
   - **DELETE** (press twice: DELETE, then CONFIRM DELETE) removes a class with **no recorded results**, such as a test class or a typo. Its code stops working, and any students in it are released so they can join another class.
   - A class that already has results can't be deleted, because student records are never removed. Archive it instead.
   - Deleting needs `supabase/migrations/20261128044325_delete_class.sql` on the server.
3. **DASHBOARD:**
   - the class code;
   - students enrolled, active (played in the last 7 days), completed (finished every level) and not started;
   - class average (average best score) and completion rate;
   - level completion bars;
   - a *Needs attention* list.
4. **STUDENTS:** the roster with status, levels, attempts, average best score, right/wrong answers and last activity.
   - **VIEW** opens the student detail: levels with attempts and best/last scores, struggling topics, achievements, and the full history with the questions missed on the first try.
   - **REMOVE** (press twice) takes the student out of the class. Their data stays, but it leaves your numbers and you no longer see it. **RE-ADMIT** brings them back.
5. **ANALYTICS:**
   - class average by level;
   - level completion;
   - results by student (lowest first);
   - progress over time (average score per day);
   - hardest questions;
   - most-missed topics.
6. **REPORTS:** pick a student (or all), an activity (or all), and one row per attempt or one per question answer, then press **EXPORT CSV**. Files are saved in `Documents\Scam Sense Reports\`. **OPEN FOLDER** shows them. The CSV opens in Excel or Google Sheets.

7. **QUESTIONS and MY REQUESTS:** browse every question in the game.
   - To add, change, move, turn off or delete one, open it, make the change, give a **reason**, and press **SUBMIT REQUEST** (or **REQUEST DELETE**).
   - An admin reviews it. Students only see the change once it's approved.
   - Follow your requests, and the admin's remarks, in **MY REQUESTS**.
   - Admins edit directly and approve requests in **REQUESTS**.
   - Details: [ContentManagement.md](ContentManagement.md).

**REFRESH** reloads everything from the server.

### What the numbers mean
- Every number comes from runs **recorded in this class**. Nothing is estimated, and an empty class shows zeros and "no data yet". Levels a student finished before joining appear in their detail as "Done before joining", but they don't count toward the class numbers.
- **Average best** is each student's best finished run per level, as a % of that level's maximum score.
- **Miss rate** is wrong *first* answers divided by answers. A mistake fixed in the follow-up re-check still counts as a miss, marked "recovered".
- **Topics** come from each question's curriculum reference (course and week/topic). The learning objective is shown next to it.

## Privacy and security (what the server enforces)
- **Roles** live in `user_roles` on the server. Players can't write them; only a valid teacher invite code can grant teacher access.
- **Row Level Security:**
  - students read only their own rows;
  - teachers read only students currently enrolled in one of their own classes, and only the results recorded in that class.
- Teacher functions (create class, remove student, …) check the role on the server, so a student calling them gets "Only teachers…".
- **Results** are written only by `submit_game_result`, which:
  - checks the level, question count, maximum score, pass flag and that every question belongs to that level;
  - stamps the class and attempt number;
  - can't overwrite anything (a retried upload answers "duplicate").

  Stored results can't be edited by anyone through the API.
- **Data kept** is limited to the display name, class membership, results and answers. Teachers never see email addresses.
- **What isn't covered:** scores still come from the game, so a modified game could submit a fake run within the allowed ranges. That's fine for classroom practice, but don't use these results for a high-stakes grade.

## Setup (developers / admin)

1. **Apply the migration.** Open Supabase Dashboard > SQL Editor, paste `supabase/migrations/20261126074705_classroom_roles_classes_records.sql`, and run it. It's safe to run again. Until it is applied, the game keeps uploading results the old way and CLASSROOM says the classroom isn't set up yet.
2. **Create teacher invite codes** in the SQL editor. Codes are stored hashed and are at least 10 characters. Arguments: code, label, max uses, days valid.
   ```sql
   select public.add_teacher_invite('SCAMSENSE-TEACH-2026', 'Ms. Santos', 1, 30);
   ```
   Give the code to the teacher privately. To make someone a teacher directly:
   ```sql
   insert into public.user_roles (user_id, role) values ('<auth user id>', 'teacher')
   on conflict (user_id) do update set role = 'teacher';
   ```
3. **If the game's scoring or questions change**, update the `activities` / `questions` rows in a new migration. `ClassroomTests.MigrationCatalog_MatchesLevels` fails until they match.

## End-to-end check (after the migration is applied)
| # | Step | Expected |
|---|---|---|
| 1 | Teacher account: CLASSROOM > teacher code > ACTIVATE | The portal switches to TEACHER PORTAL > CLASSES. |
| 2 | CREATE CLASS "Test 1-A" | The dashboard shows the new code; 0 students; class average "-". |
| 3 | Student account on another PC (or after SIGN OUT): CLASSROOM > JOIN CLASS with the code | "You joined Test 1-A…" and OVERVIEW shows the class. |
| 4 | Student plays Level 1 to the end | MY RESULTS shows Attempt 1 with feedback. |
| 5 | Teacher: REFRESH | Dashboard 1 student, 1 active; STUDENTS > VIEW shows the attempt, right/wrong answers and missed questions. |
| 6 | Student replays Level 1 | Attempt 2 is added; the teacher sees 2 attempts and one Level 1 progress row. |
| 7 | Teacher: REPORTS > EXPORT CSV | The CSV in `Documents\Scam Sense Reports` has both attempts. |
| 8 | Teacher: REMOVE student, then RE-ADMIT | The student leaves the numbers, then comes back. |
| 9 | Student tries the code `ABOK7P` | "Class codes never use O, 0, I or 1…" |
