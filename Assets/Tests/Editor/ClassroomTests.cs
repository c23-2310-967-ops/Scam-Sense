using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using M = ClassroomModels;
using CA = ClassroomAnalytics;

/// <summary>
/// Classroom mode: class codes, the result records a finished level produces, duplicate handling (retried uploads,
/// replays), the teacher analytics / CSV reports, and that the database migration matches the game's levels.
/// Nothing here talks to the network or changes the player's saved progress.
/// </summary>
public class ClassroomTests
{
    const string MigrationPath = "supabase/migrations/20261126074705_classroom_roles_classes_records.sql";
    static string Migration => File.ReadAllText(MigrationPath);

    // ---------- Class codes ----------

    [TestCase("ab3-k7p", "AB3K7P")]
    [TestCase("  AB3 K7P ", "AB3K7P")]
    [TestCase("ab.3k_7p", "AB3K7P")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void ClassCode_Normalize(string raw, string expected) => Assert.AreEqual(expected, ClassCode.Normalize(raw));

    [TestCase("AB3K7P", true)]
    [TestCase("ab3-k7p", true)]
    [TestCase("AB3K7", false)]      // too short
    [TestCase("AB3K7PQ", false)]    // too long
    [TestCase("AB0K7P", false)]     // 0 is never used
    [TestCase("ABOK7P", false)]     // O is never used
    [TestCase("AB1K7P", false)]     // 1 is never used
    [TestCase("ABIK7P", false)]     // I is never used
    public void ClassCode_IsValid(string code, bool valid) => Assert.AreEqual(valid, ClassCode.IsValid(code));

    [Test]
    public void ClassCode_Problem_ExplainsLookAlikes()
    {
        Assert.IsNull(ClassCode.Problem("AB3-K7P"));
        StringAssert.Contains("O, 0, I or 1", ClassCode.Problem("ABOK7P"));
        StringAssert.Contains("6 characters", ClassCode.Problem("AB3"));
        StringAssert.Contains("class code", ClassCode.Problem(""));
    }

    [Test]
    public void ClassCode_Display_AddsDash() => Assert.AreEqual("AB3-K7P", ClassCode.Display("ab3k7p"));

    [Test]
    public void ClassCode_Generate_AlwaysValid_AndVaried()
    {
        var rng = new System.Random(17);
        var seen = new HashSet<string>();
        for (int i = 0; i < 2000; i++)
        {
            string c = ClassCode.Generate(rng);
            Assert.IsTrue(ClassCode.IsValid(c), c);
            seen.Add(c);
        }
        Assert.Greater(seen.Count, 1990, "codes should almost never repeat (32^6 possibilities)");
    }

    [Test]
    public void ClassCode_AlphabetMatchesDatabase()
    {
        string sql = Migration;
        StringAssert.Contains($"alphabet constant text := '{ClassCode.Alphabet}'", sql, "new_class_code() must use the same alphabet");
        StringAssert.Contains("code ~ '^[A-HJ-NP-Z2-9]{6}$'", sql);
        var check = new Regex("^[A-HJ-NP-Z2-9]$");
        Assert.AreEqual(32, ClassCode.Alphabet.Length);
        Assert.AreEqual(32, ClassCode.Alphabet.Distinct().Count());
        foreach (char c in ClassCode.Alphabet) Assert.IsTrue(check.IsMatch(c.ToString()), c + " is refused by the database check");
        for (char c = 'A'; c <= 'Z'; c++) Assert.AreEqual(check.IsMatch(c.ToString()), ClassCode.Alphabet.IndexOf(c) >= 0, c.ToString());
    }

    // ---------- Result records ----------

    static ScamMessage Msg(string id) => new ScamMessage { id = id };

    static ScoringSystem.AnswerResult Answer(string id, bool correct, float seconds = 5f, bool hint = false) =>
        new ScoringSystem.AnswerResult { message = Msg(id), isCorrect = correct, seconds = seconds, hintUsed = hint, chosenChoice = -1 };

    [Test]
    public void RunRecord_OneRowPerQuestion_FirstAnswerOnly()
    {
        var answers = new[] { Answer("E01", true), Answer("E02", false), Answer("E02", true), Answer("E03", false, -3f) };
        var rows = RunRecord.FromAnswers(answers, id => id == "E02" || id == "E01", id => id == "E03");
        Assert.AreEqual(new[] { "E01", "E02", "E03" }, rows.Select(r => r.question_id).ToArray());
        Assert.IsTrue(rows[0].correct);
        Assert.IsFalse(rows[0].recovered, "a right first answer is never 'recovered'");
        Assert.IsFalse(rows[1].correct, "the first answer counts, not a later one");
        Assert.IsTrue(rows[1].recovered);
        Assert.IsTrue(rows[2].hint_used);
        Assert.AreEqual(0f, rows[2].seconds);
    }

    [Test]
    public void RunRecord_SkipsAnswersWithoutQuestion()
    {
        var rows = RunRecord.FromAnswers(new[] { new ScoringSystem.AnswerResult(), Answer("", true), Answer("M01", true) });
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual(0, RunRecord.FromAnswers(null).Count);
    }

    [Test]
    public void ResultRow_HasEverythingTheClassRecordNeeds()
    {
        var qs = new List<GameResultLog.QuestionAnswer>
        {
            new GameResultLog.QuestionAnswer { question_id = "E01", correct = true },
            new GameResultLog.QuestionAnswer { question_id = "E01", correct = false },   // duplicate: dropped
            new GameResultLog.QuestionAnswer { question_id = "E02", correct = false, recovered = true },
            new GameResultLog.QuestionAnswer { question_id = "E03", correct = true, recovered = true },   // recovered only after a miss
        };
        var when = new DateTime(2026, 11, 25, 9, 30, 0, DateTimeKind.Utc);
        var r = GameResultLog.Make("level_1", 700, 600, 5, 9, 6, true, 42f, qs, true, when);
        Assert.IsTrue(Guid.TryParse(r.run_id, out _), "every run has its own id");
        Assert.AreEqual("level_1", r.level_id);
        Assert.AreEqual(600, r.score, "score is clamped to the level's maximum");
        Assert.AreEqual(3, r.stars);
        Assert.AreEqual(6, r.correct_answers, "correct is clamped to the question count");
        Assert.AreEqual(0, r.incorrect_answers);
        Assert.AreEqual(r.total_questions, r.correct_answers + r.incorrect_answers);
        Assert.IsTrue(r.completed);
        Assert.AreEqual(new[] { "E01", "E02", "E03" }, r.questions.Select(q => q.question_id).ToArray());
        Assert.IsFalse(r.questions[2].recovered);
        Assert.IsTrue(M.TryTime(r.completed_at, out var t) && t == when, "timestamp is ISO 8601 UTC");

        var low = GameResultLog.Make("level_2", -5, 990, 0, 2, 6, false, 10f, null, true, when);
        Assert.AreEqual(0, low.score);
        Assert.AreEqual(4, low.incorrect_answers);
    }

    [Test]
    public void SubmitBody_IsTheRpcArgument()
    {
        var r = GameResultLog.Make("level_3", 500, 1320, 1, 3, 6, false, 30f,
                                   new List<GameResultLog.QuestionAnswer> { new GameResultLog.QuestionAnswer { question_id = "M03", correct = true } }, true, DateTime.UtcNow);
        string body = GameResultLog.SubmitBody(r);
        StringAssert.StartsWith("{\"p_run\":{", body);
        foreach (var field in new[] { "run_id", "level_id", "score", "max_score", "correct_answers", "incorrect_answers", "total_questions",
                                      "passed", "completed", "completed_at", "questions", "question_id" })
            StringAssert.Contains("\"" + field + "\"", body);
    }

    // ---------- Duplicates ----------

    string profile;

    [SetUp] public void NewProfile() => profile = "classroom_test_" + Guid.NewGuid().ToString("N");
    [TearDown] public void RemoveProfile() { GameResultLog.Clear(profile); GameResultLog.Clear(profile + "_b"); }

    static GameResultLog.Row Run(string level = "level_1") => GameResultLog.Make(level, 400, 600, 2, 5, 6, true, 20f, null, true, DateTime.UtcNow);

    [Test]
    public void Replays_AreSeparateAttempts()
    {
        var a = Run(); var b = Run();
        GameResultLog.Enqueue(profile, a);
        GameResultLog.Enqueue(profile, b);
        Assert.AreNotEqual(a.run_id, b.run_id);
        Assert.AreEqual(2, GameResultLog.PendingFor(profile).Count, "every replay stays in the history");
    }

    [Test]
    public void SameRun_IsNeverQueuedTwice()
    {
        var a = Run();
        GameResultLog.Enqueue(profile, a);
        GameResultLog.Enqueue(profile, a);
        Assert.AreEqual(1, GameResultLog.PendingFor(profile).Count);
    }

    [Test]
    public void Upload_RemovesOnlyConfirmedRuns()
    {
        var a = Run(); var b = Run("level_2");
        GameResultLog.Enqueue(profile, a);
        GameResultLog.Enqueue(profile, b);
        GameResultLog.MarkUploaded(profile, new List<GameResultLog.Row> { a });
        var left = GameResultLog.PendingFor(profile);
        Assert.AreEqual(1, left.Count);
        Assert.AreEqual(b.run_id, left[0].run_id);
    }

    [Test]
    public void MovingGuestRuns_DoesNotDuplicate()
    {
        var a = Run();
        GameResultLog.Enqueue(profile, a);
        GameResultLog.Enqueue(profile + "_b", a);
        GameResultLog.MoveAll(profile, profile + "_b");
        Assert.AreEqual(1, GameResultLog.PendingFor(profile + "_b").Count);
        Assert.AreEqual(0, GameResultLog.PendingFor(profile).Count);
    }

    [TestCase("recorded", GameResultLog.UploadOutcome.Stored)]
    [TestCase("duplicate", GameResultLog.UploadOutcome.Stored)]   // a retried upload: already stored, nothing to do
    [TestCase("limit", GameResultLog.UploadOutcome.Stored)]
    [TestCase(null, GameResultLog.UploadOutcome.Retry)]
    [TestCase("", GameResultLog.UploadOutcome.Retry)]
    public void UploadOutcome_FromServerStatus(string status, GameResultLog.UploadOutcome expected) =>
        Assert.AreEqual(expected, GameResultLog.OutcomeForStatus(status));

    [TestCase(0, true, GameResultLog.UploadOutcome.Retry)]       // offline
    [TestCase(503, true, GameResultLog.UploadOutcome.Retry)]
    [TestCase(401, false, GameResultLog.UploadOutcome.Retry)]    // session: sign in again, keep the run
    [TestCase(403, false, GameResultLog.UploadOutcome.Retry)]
    [TestCase(404, false, GameResultLog.UploadOutcome.Retry)]    // migration not applied yet
    [TestCase(429, false, GameResultLog.UploadOutcome.Retry)]
    [TestCase(400, false, GameResultLog.UploadOutcome.Rejected)] // invalid run: would fail forever
    public void UploadOutcome_FromErrors(int status, bool network, GameResultLog.UploadOutcome expected) =>
        Assert.AreEqual(expected, GameResultLog.OutcomeForError(status, network));

    [Test]
    public void Database_ProtectsResults()
    {
        string sql = Migration;
        StringAssert.Contains("unique (result_id, question_id)", sql, "one row per question per run");
        StringAssert.Contains("on conflict (user_id, run_id) do nothing", sql, "a retried upload never adds a second row");
        StringAssert.Contains("'status', 'duplicate'", sql);
        StringAssert.Contains("revoke insert on public.game_results from authenticated", sql, "results only arrive through submit_game_result");
        StringAssert.Contains("create trigger game_results_final before update", sql, "results can't be edited");
        StringAssert.Contains("create trigger question_results_final before update", sql);
        StringAssert.IsMatch(@"grant select on public\.user_roles", sql);
        Assert.IsFalse(Regex.IsMatch(sql, @"grant\s+(insert|update|all)[^;]*user_roles", RegexOptions.IgnoreCase), "players can never write their own role");
        Assert.IsFalse(Regex.IsMatch(sql, @"grant[^;]*teacher_invites[^;]*to authenticated", RegexOptions.IgnoreCase), "invite codes are never readable");
        StringAssert.IsMatch(@"revoke execute on function public\.submit_game_result\(jsonb\)\s+from public, anon", sql, "signed-out requests can't submit");
        StringAssert.IsMatch(@"revoke execute on function public\.create_class\(text\)\s+from public, anon", sql);
        foreach (var fn in new[] { "create_class", "remove_student", "readmit_student", "set_class_archived" })
            StringAssert.IsMatch(@"function public\." + fn + @"\([^)]*\)[^$]*?\$\$[\s\S]*?if not public\.is_teacher\(\) then raise exception", sql, fn + " must refuse students");
    }

    // ---------- The migration's catalog matches the game ----------

    [Test]
    public void MigrationCatalog_MatchesLevels()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset");
        Assert.IsNotNull(catalog);
        string sql = Migration;
        var go = new GameObject("ScoringProbe");
        try
        {
            var scoring = go.AddComponent<ScoringSystem>();
            foreach (var l in catalog.levels)
            {
                scoring.Configure(l);
                string row = $"('{l.levelId}', {l.levelNumber}, '{l.displayName.Replace("'", "''")}', {l.MessageCount}, {scoring.MaxScoreFor(l.MessageCount)}, {l.minScoreToUnlockNext})";
                StringAssert.Contains(row, sql, $"activities row for {l.levelId} is out of date (score rules or questions changed?)");
                foreach (var m in l.messages)
                    StringAssert.IsMatch($@"\('{m.id}', '{l.levelId}', ", sql, $"question {m.id} of {l.levelId} is missing from public.questions");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // ---------- Teacher analytics ----------

    static readonly DateTime Now = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    static string Ts(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

    static ClassData Sample()
    {
        var d = new ClassData { info = new M.ClassInfo { id = "c1", name = "BSIT 1-A", code = "AB3K7P" } };
        d.activities = new List<M.ActivityRow>
        {
            new M.ActivityRow { level_id = "level_1", level_number = 1, title = "One", question_count = 2, max_score = 100, pass_score = 60 },
            new M.ActivityRow { level_id = "level_2", level_number = 2, title = "Two", question_count = 2, max_score = 200, pass_score = 120 },
        };
        d.questions = new List<M.QuestionRow>
        {
            new M.QuestionRow { id = "E01", level_id = "level_1", course = "IAS", topic = "CIA Triad", learning_objective = "Name the goal." },
            new M.QuestionRow { id = "E02", level_id = "level_1", course = "IAS", topic = "Phishing", learning_objective = "Spot phishing." },
            new M.QuestionRow { id = "E03", level_id = "level_2", course = "CS", topic = "Phishing", learning_objective = "Spot fake links." },
            new M.QuestionRow { id = "E04", level_id = "level_2", course = "CS", topic = "OTP", learning_objective = "Never share an OTP." },
        };
        d.members = new List<M.MemberRow>
        {
            new M.MemberRow { class_id = "c1", student_id = "ana", joined_at = Ts(Now.AddDays(-20)) },
            new M.MemberRow { class_id = "c1", student_id = "ben", joined_at = Ts(Now.AddDays(-20)) },
            new M.MemberRow { class_id = "c1", student_id = "cy", joined_at = Ts(Now.AddDays(-20)) },
            new M.MemberRow { class_id = "c1", student_id = "gone", joined_at = Ts(Now.AddDays(-20)), removed_at = Ts(Now.AddDays(-1)), removed_by = "teacher" },
        };
        d.names = new Dictionary<string, string> { { "ana", "Ana" }, { "ben", "=Ben" }, { "cy", "Cy, Jr." }, { "gone", "Gone" } };
        long id = 1;
        M.AttemptRow Att(string who, string level, int score, int max, int correct, int n, DateTime at) => new M.AttemptRow
        {
            id = id++, user_id = who, level_id = level, score = score, max_score = max, correct_answers = correct, incorrect_answers = 2 - correct,
            total_questions = 2, completed = true, passed = score * 10 >= max * 6, attempt_number = n, completed_at = Ts(at), class_id = "c1",
        };
        d.attempts = new List<M.AttemptRow>
        {
            Att("ana", "level_1", 50, 100, 1, 1, Now.AddDays(-10)),
            Att("ana", "level_1", 100, 100, 2, 2, Now.AddDays(-2)),    // replay: best 100%
            Att("ana", "level_2", 150, 200, 1, 1, Now.AddDays(-1)),
            Att("ben", "level_1", 40, 100, 0, 1, Now.AddDays(-9)),
            Att("gone", "level_1", 0, 100, 0, 1, Now.AddDays(-2)),     // removed: left out of every number
        };
        d.answers = new List<M.AnswerRow>();
        void Ans(long result, string who, string level, string q, bool ok, bool recovered = false) =>
            d.answers.Add(new M.AnswerRow { result_id = result, user_id = who, level_id = level, question_id = q, correct = ok, recovered = recovered, class_id = "c1" });
        Ans(1, "ana", "level_1", "E01", true); Ans(1, "ana", "level_1", "E02", false, true);
        Ans(2, "ana", "level_1", "E01", true); Ans(2, "ana", "level_1", "E02", true);
        Ans(3, "ana", "level_2", "E03", false); Ans(3, "ana", "level_2", "E04", true);
        Ans(4, "ben", "level_1", "E01", false); Ans(4, "ben", "level_1", "E02", false);
        Ans(5, "gone", "level_1", "E01", false); Ans(5, "gone", "level_1", "E02", false);
        return d;
    }

    [Test]
    public void Dashboard_CountsOnlyRecordedResults()
    {
        var s = CA.Dashboard(Sample(), Now);
        Assert.AreEqual(3, s.enrolled, "removed students aren't counted");
        Assert.AreEqual(1, s.active, "only Ana played in the last 7 days");
        Assert.AreEqual(1, s.completed, "only Ana finished every level");
        Assert.AreEqual(1, s.notStarted, "Cy has no results");
        Assert.AreEqual(4, s.attempts);
        // Ana: best 100% and 75% -> 87.5%; Ben: 40% -> class average 63.75%
        Assert.AreEqual(0.6375f, s.classAverage, 0.0001f);
        // 3 levels done (Ana 2, Ben 1) out of 3 students x 2 levels
        Assert.AreEqual(0.5f, s.completionRate, 0.0001f);
    }

    [Test]
    public void Dashboard_EmptyClass_HasNoMadeUpNumbers()
    {
        var d = Sample();
        d.attempts.Clear(); d.answers.Clear();
        var s = CA.Dashboard(d, Now);
        Assert.AreEqual(-1f, s.classAverage, "no average until someone has results");
        Assert.AreEqual(0f, s.completionRate);
        Assert.AreEqual(3, s.notStarted);
        Assert.IsTrue(CA.Levels(d).All(l => l.averageBest < 0f && l.studentsCompleted == 0));
    }

    [Test]
    public void Levels_CompletionAndAverages()
    {
        var levels = CA.Levels(Sample());
        Assert.AreEqual(2, levels[0].studentsCompleted);
        Assert.AreEqual(2f / 3f, levels[0].completionRate, 0.0001f);
        Assert.AreEqual(0.7f, levels[0].averageBest, 0.0001f, "best per student: Ana 100%, Ben 40%");
        Assert.AreEqual(3, levels[0].attempts, "Ana's replay is a separate attempt; the removed student's isn't counted");
    }

    [Test]
    public void Questions_And_Topics_ByMissRate()
    {
        var d = Sample();
        var ids = new HashSet<string> { "ana", "ben", "cy" };
        var answers = d.answers.Where(a => ids.Contains(a.user_id)).ToList();
        var q = CA.Questions(answers, d.questions);
        Assert.AreEqual("E03", q[0].questionId, "E03: missed 1 of 1");
        var e02 = q.First(x => x.questionId == "E02");
        Assert.AreEqual(3, e02.answered); Assert.AreEqual(2, e02.wrong); Assert.AreEqual(1, e02.recovered);

        var topics = CA.Topics(answers, d.questions);
        var phishing = topics.First(t => t.topic == "Phishing");
        Assert.AreEqual(4, phishing.answered, "E02 and E03 share the topic");
        Assert.AreEqual(3, phishing.wrong);
        Assert.AreEqual("Phishing", topics[0].topic);
    }

    [Test]
    public void Student_Detail()
    {
        var d = Sample();
        var ana = CA.Student(d, "ana");
        Assert.AreEqual(3, ana.attempts);
        Assert.AreEqual(2, ana.levelsCompleted);
        Assert.AreEqual(4, ana.correct);
        Assert.AreEqual(2, ana.incorrect);
        Assert.AreEqual(Now.AddDays(-1), ana.lastActive);
        Assert.AreEqual("Phishing", ana.weakTopics.Single().topic, "2 of 3 phishing answers wrong; CIA Triad and OTP fine");

        var levels = CA.LevelRecords(d.attempts.Where(a => a.user_id == "ana"), d.activities);
        Assert.AreEqual(2, levels.Count, "one record per level, however many replays");
        Assert.AreEqual(2, levels[0].attempts);
        Assert.AreEqual(100, levels[0].bestScore);
        Assert.AreEqual(100, levels[0].lastScore);
        Assert.AreEqual("Completed", ana.Status(2));
        Assert.AreEqual("Not started", CA.Student(d, "cy").Status(2));
    }

    [Test]
    public void Feedback_ListsMissedObjectives()
    {
        var d = Sample();
        var run = d.attempts[0];
        string fb = CA.Feedback(run, d.answers.Where(a => a.result_id == run.id), d.questions);
        StringAssert.Contains("Spot phishing.", fb);
        Assert.IsFalse(fb.Contains("Name the goal"), "only missed questions are listed");
        StringAssert.StartsWith("Excellent", CA.Feedback(d.attempts[1], d.answers.Where(a => a.result_id == 2), d.questions));
    }

    [Test]
    public void OverTime_AveragesPerDay()
    {
        var pts = CA.OverTime(Sample().attempts);
        Assert.AreEqual(4, pts.Count);
        Assert.IsTrue(pts.Zip(pts.Skip(1), (a, b) => a.day < b.day).All(x => x), "oldest first");
    }

    // ---------- Reports ----------

    [Test]
    public void Csv_Filters_ByStudentAndActivity()
    {
        var d = Sample();
        var all = CA.AttemptsCsv(d, new CA.ReportFilter()).Trim().Split('\n');
        Assert.AreEqual(1 + 4, all.Length, "header + the 4 enrolled students' attempts");
        StringAssert.StartsWith("class,student,level", all[0]);

        var ana = CA.AttemptsCsv(d, new CA.ReportFilter { studentId = "ana" }).Trim().Split('\n');
        Assert.AreEqual(1 + 3, ana.Length);
        var l2 = CA.AttemptsCsv(d, new CA.ReportFilter { levelId = "level_2" }).Trim().Split('\n');
        Assert.AreEqual(1 + 1, l2.Length);

        var answers = CA.AnswersCsv(d, new CA.ReportFilter { studentId = "ben" }).Trim().Split('\n');
        Assert.AreEqual(1 + 2, answers.Length);
        StringAssert.Contains("Phishing", answers[2]);
    }

    [Test]
    public void Csv_EscapesNamesSafely()
    {
        Assert.AreEqual("\"Cy, Jr.\"", CA.CsvCell("Cy, Jr."));
        Assert.AreEqual("\"say \"\"hi\"\"\"", CA.CsvCell("say \"hi\""));
        Assert.AreEqual("'=Ben", CA.CsvCell("=Ben"), "a name can't become a spreadsheet formula");
        Assert.AreEqual("'@x", CA.CsvCell("@x"));
        Assert.AreEqual("-12.5", CA.CsvCell("-12.5"), "negative numbers stay numbers");
        string csv = CA.AttemptsCsv(Sample(), new CA.ReportFilter());
        StringAssert.Contains("'=Ben", csv);
        StringAssert.Contains("\"Cy, Jr.\"", CA.AttemptsCsv(WithCy(), new CA.ReportFilter { studentId = "cy" }));
        Assert.IsFalse(csv.Contains("Gone"), "removed students are left out unless asked for");
    }

    static ClassData WithCy()
    {
        var d = Sample();
        d.attempts.Add(new M.AttemptRow { id = 99, user_id = "cy", level_id = "level_1", score = 60, max_score = 100, correct_answers = 1, incorrect_answers = 1,
                                          total_questions = 2, completed = true, attempt_number = 1, completed_at = Ts(Now) });
        return d;
    }

    [Test]
    public void Timestamps_ParseFromPostgres()
    {
        Assert.IsTrue(M.TryTime("2026-11-25T09:00:00.123456+00:00", out var t));
        Assert.AreEqual(new DateTime(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc), t.AddTicks(-(t.Ticks % TimeSpan.TicksPerSecond)));
        Assert.IsTrue(M.TryTime("2026-11-25T17:00:00+08:00", out var t2));
        Assert.AreEqual(9, t2.Hour);
        Assert.IsFalse(M.TryTime("", out _));
        Assert.IsFalse(M.TryTime(null, out _));
    }
}
