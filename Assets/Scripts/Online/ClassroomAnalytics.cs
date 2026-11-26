using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using A = ClassroomModels;

/// <summary>
/// Teacher dashboard numbers, analytics and CSV reports, worked out from the class's recorded rows only
/// (nothing is estimated or made up: an empty class shows zeros and "no data yet").
/// Pure functions with no Unity or network calls, so they are covered by ClassroomTests.
///
/// Definitions (also in Docs/ClassroomGuide.md):
///   enrolled   = currently in the class (removed students are left out of every number)
///   active     = finished at least one level in this class in the last 7 days
///   completed  = has a finished run of every level in this class
///   class average = mean, over students with results, of their average best score (% of the level's maximum)
///   completion rate = levels completed / (enrolled students x levels)
///   miss rate  = wrong first answers / answers (a recovered answer still counts as a miss)
/// </summary>
public static class ClassroomAnalytics
{
    public const int ActiveDays = 7;

    public class Summary
    {
        public int enrolled, active, completed, notStarted, attempts;
        /// <summary>0..1, or -1 when nobody has results yet.</summary>
        public float classAverage = -1f;
        public float completionRate;
    }

    public class LevelStat
    {
        public string levelId, title;
        public int number, studentsCompleted, studentsPlayed, attempts;
        public float completionRate;          // of enrolled students
        public float averageBest = -1f;       // mean best % of students who played it (-1 = nobody)
    }

    public class QuestionStat
    {
        public string questionId, levelId, topic, objective;
        public int answered, wrong, recovered;
        public float MissRate => answered > 0 ? (float)wrong / answered : 0f;
    }

    public class TopicStat
    {
        public string topic, course;
        public int answered, wrong;
        public List<string> questionIds = new List<string>();
        public float MissRate => answered > 0 ? (float)wrong / answered : 0f;
    }

    public class StudentStat
    {
        public string id, name, joinedAt;
        public int attempts, levelsCompleted, correct, incorrect;
        public float averageBest = -1f;
        public DateTime? lastActive;
        public List<TopicStat> weakTopics = new List<TopicStat>();
        public string Status(int levelCount) => attempts == 0 ? "Not started" : levelsCompleted >= levelCount && levelCount > 0 ? "Completed" : "In progress";
    }

    public class LevelRecord
    {
        public string levelId, title;
        public int number, attempts, bestScore = -1, lastScore = -1, maxScore;
        public bool completed, passed;
        public string lastPlayed;
    }

    public class ProgressPoint { public string label; public DateTime day; public float average; public int attempts; }

    // ---------- Students ----------

    public static List<A.MemberRow> Enrolled(ClassData d) => d.members.Where(A.IsActive).ToList();

    static HashSet<string> EnrolledIds(ClassData d) => new HashSet<string>(Enrolled(d).Select(m => m.student_id));

    /// <summary>Attempts of currently enrolled students (removed students' rows are left out).</summary>
    public static List<A.AttemptRow> EnrolledAttempts(ClassData d)
    {
        var ids = EnrolledIds(d);
        return d.attempts.Where(a => ids.Contains(a.user_id)).ToList();
    }

    /// <summary>Best score % per level for one student's attempts (finished runs only).</summary>
    static Dictionary<string, float> BestByLevel(IEnumerable<A.AttemptRow> attempts)
    {
        var best = new Dictionary<string, float>();
        foreach (var a in attempts)
        {
            if (!a.completed) continue;
            float p = a.Percent;
            if (!best.TryGetValue(a.level_id, out var b) || p > b) best[a.level_id] = p;
        }
        return best;
    }

    static float Mean(IEnumerable<float> values)
    {
        float sum = 0f; int n = 0;
        foreach (var v in values) { sum += v; n++; }
        return n > 0 ? sum / n : -1f;
    }

    public static Summary Dashboard(ClassData d, DateTime nowUtc)
    {
        var s = new Summary();
        var enrolled = Enrolled(d);
        s.enrolled = enrolled.Count;
        var attempts = EnrolledAttempts(d);
        s.attempts = attempts.Count;
        int levels = d.activities.Count;
        var byStudent = attempts.GroupBy(a => a.user_id).ToDictionary(g => g.Key, g => g.ToList());

        var averages = new List<float>();
        int levelsDone = 0;
        foreach (var m in enrolled)
        {
            if (!byStudent.TryGetValue(m.student_id, out var mine) || mine.Count == 0) { s.notStarted++; continue; }
            if (mine.Any(a => A.TryTime(a.completed_at, out var t) && (nowUtc - t).TotalDays <= ActiveDays)) s.active++;
            var best = BestByLevel(mine);
            if (best.Count > 0) averages.Add(Mean(best.Values));
            int done = d.activities.Count(act => best.ContainsKey(act.level_id));
            levelsDone += done;
            if (levels > 0 && done >= levels) s.completed++;
        }
        s.classAverage = averages.Count > 0 ? Mean(averages) : -1f;
        s.completionRate = s.enrolled > 0 && levels > 0 ? (float)levelsDone / (s.enrolled * levels) : 0f;
        return s;
    }

    public static List<LevelStat> Levels(ClassData d)
    {
        var attempts = EnrolledAttempts(d);
        int enrolled = Enrolled(d).Count;
        var list = new List<LevelStat>();
        foreach (var act in d.activities.OrderBy(a => a.level_number))
        {
            var mine = attempts.Where(a => a.level_id == act.level_id).ToList();
            var bestPerStudent = mine.Where(a => a.completed).GroupBy(a => a.user_id).Select(g => g.Max(a => a.Percent)).ToList();
            list.Add(new LevelStat
            {
                levelId = act.level_id, title = act.title, number = act.level_number,
                attempts = mine.Count,
                studentsPlayed = mine.Select(a => a.user_id).Distinct().Count(),
                studentsCompleted = bestPerStudent.Count,
                completionRate = enrolled > 0 ? (float)bestPerStudent.Count / enrolled : 0f,
                averageBest = bestPerStudent.Count > 0 ? bestPerStudent.Average() : -1f,
            });
        }
        return list;
    }

    // ---------- Questions and topics ----------

    public static List<QuestionStat> Questions(IEnumerable<A.AnswerRow> answers, IEnumerable<A.QuestionRow> catalog)
    {
        var byId = catalog.ToDictionary(q => q.id, q => q);
        var stats = new Dictionary<string, QuestionStat>();
        foreach (var a in answers)
        {
            if (string.IsNullOrEmpty(a.question_id)) continue;
            if (!stats.TryGetValue(a.question_id, out var s))
            {
                byId.TryGetValue(a.question_id, out var q);
                s = stats[a.question_id] = new QuestionStat
                {
                    questionId = a.question_id, levelId = q != null ? q.level_id : a.level_id,
                    topic = q != null ? q.topic : a.question_id, objective = q != null ? q.learning_objective : "",
                };
            }
            s.answered++;
            if (!a.correct) s.wrong++;
            if (a.recovered) s.recovered++;
        }
        return stats.Values.OrderByDescending(s => s.MissRate).ThenByDescending(s => s.wrong).ThenBy(s => s.questionId, StringComparer.Ordinal).ToList();
    }

    public static List<TopicStat> Topics(IEnumerable<A.AnswerRow> answers, IEnumerable<A.QuestionRow> catalog)
    {
        var byId = catalog.ToDictionary(q => q.id, q => q);
        var stats = new Dictionary<string, TopicStat>();
        foreach (var a in answers)
        {
            if (string.IsNullOrEmpty(a.question_id) || !byId.TryGetValue(a.question_id, out var q)) continue;
            if (!stats.TryGetValue(q.topic, out var s)) s = stats[q.topic] = new TopicStat { topic = q.topic, course = q.course };
            s.answered++;
            if (!a.correct) s.wrong++;
            if (!s.questionIds.Contains(q.id)) s.questionIds.Add(q.id);
        }
        return stats.Values.OrderByDescending(s => s.MissRate).ThenByDescending(s => s.wrong).ThenBy(s => s.topic, StringComparer.Ordinal).ToList();
    }

    /// <summary>Topics a learner struggles with: missed at least once and at least a third of the time (worst first).</summary>
    public static List<TopicStat> WeakTopics(IEnumerable<A.AnswerRow> answers, IEnumerable<A.QuestionRow> catalog, int max = 3) =>
        Topics(answers, catalog).Where(t => t.wrong > 0 && t.MissRate >= 1f / 3f).Take(max).ToList();

    // ---------- Students ----------

    public static List<StudentStat> Students(ClassData d, bool includeRemoved = false)
    {
        var list = new List<StudentStat>();
        foreach (var m in d.members)
        {
            if (!includeRemoved && !A.IsActive(m)) continue;
            list.Add(Student(d, m.student_id));
        }
        return list.OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static StudentStat Student(ClassData d, string studentId)
    {
        var m = d.members.FirstOrDefault(x => x.student_id == studentId);
        var mine = d.attempts.Where(a => a.user_id == studentId).ToList();
        var best = BestByLevel(mine);
        var s = new StudentStat
        {
            id = studentId, name = d.NameOf(studentId), joinedAt = m != null ? m.joined_at : null,
            attempts = mine.Count,
            levelsCompleted = d.activities.Count(act => best.ContainsKey(act.level_id)),
            correct = mine.Sum(a => a.correct_answers),
            incorrect = mine.Sum(a => a.incorrect_answers),
            averageBest = best.Count > 0 ? Mean(best.Values) : -1f,
        };
        foreach (var a in mine)
            if (A.TryTime(a.completed_at, out var t) && (s.lastActive == null || t > s.lastActive)) s.lastActive = t;
        s.weakTopics = WeakTopics(d.answers.Where(x => x.user_id == studentId), d.questions);
        return s;
    }

    /// <summary>Per-level record for one learner (student portal and student detail).</summary>
    public static List<LevelRecord> LevelRecords(IEnumerable<A.AttemptRow> attempts, IEnumerable<A.ActivityRow> activities)
    {
        var all = attempts.ToList();
        var list = new List<LevelRecord>();
        foreach (var act in activities.OrderBy(a => a.level_number))
        {
            var mine = all.Where(a => a.level_id == act.level_id).OrderBy(a => a.attempt_number).ThenBy(a => a.completed_at, StringComparer.Ordinal).ToList();
            var r = new LevelRecord { levelId = act.level_id, title = act.title, number = act.level_number, maxScore = act.max_score, attempts = mine.Count };
            if (mine.Count > 0)
            {
                var finished = mine.Where(a => a.completed).ToList();
                r.completed = finished.Count > 0;
                r.passed = mine.Any(a => a.passed);
                r.bestScore = finished.Count > 0 ? finished.Max(a => a.score) : mine.Max(a => a.score);
                var last = mine.OrderBy(a => a.completed_at, StringComparer.Ordinal).Last();
                r.lastScore = last.score;
                r.lastPlayed = last.completed_at;
            }
            list.Add(r);
        }
        return list;
    }

    // ---------- Over time ----------

    /// <summary>Average score per day (local calendar day), the most recent <paramref name="maxPoints"/> days that have results.</summary>
    public static List<ProgressPoint> OverTime(IEnumerable<A.AttemptRow> attempts, int maxPoints = 12)
    {
        var points = new SortedDictionary<DateTime, List<float>>();
        foreach (var a in attempts)
        {
            if (!A.TryTime(a.completed_at, out var t)) continue;
            var day = t.ToLocalTime().Date;
            if (!points.TryGetValue(day, out var l)) points[day] = l = new List<float>();
            l.Add(a.Percent);
        }
        return points.Skip(Math.Max(0, points.Count - maxPoints))
                     .Select(p => new ProgressPoint { day = p.Key, label = p.Key.ToString("MMM d", CultureInfo.InvariantCulture), average = p.Value.Average(), attempts = p.Value.Count })
                     .ToList();
    }

    // ---------- Feedback ----------

    /// <summary>Feedback for one run: how it went, then the learning objectives of the questions that were missed.</summary>
    public static string Feedback(A.AttemptRow run, IEnumerable<A.AnswerRow> runAnswers, IEnumerable<A.QuestionRow> catalog)
    {
        var sb = new StringBuilder();
        int total = run.total_questions;
        if (total > 0 && run.correct_answers >= total) sb.Append("Excellent! You identified every message correctly.");
        else if (total > 0 && run.correct_answers >= (int)Math.Ceiling(total * 0.6)) sb.Append("Good work! Review the warning signs to improve your judgment.");
        else sb.Append("Keep learning. Look carefully at the sender, message context, urgency, and links.");
        var byId = catalog.ToDictionary(q => q.id, q => q);
        var missed = runAnswers.Where(a => !a.correct).Select(a => byId.TryGetValue(a.question_id, out var q) ? q.learning_objective : null)
                               .Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
        if (missed.Count > 0) sb.Append(" Review: ").Append(string.Join(" ", missed.Select(o => o.TrimEnd('.') + ".")));
        return sb.ToString();
    }

    // ---------- Reports (CSV) ----------

    public class ReportFilter
    {
        public string studentId;   // null = every enrolled student
        public string levelId;     // null = every level
        public bool includeRemoved;
    }

    public static List<A.AttemptRow> FilterAttempts(ClassData d, ReportFilter f)
    {
        var ids = f != null && f.includeRemoved ? new HashSet<string>(d.members.Select(m => m.student_id)) : EnrolledIds(d);
        return d.attempts.Where(a => ids.Contains(a.user_id)
                                     && (f == null || string.IsNullOrEmpty(f.studentId) || a.user_id == f.studentId)
                                     && (f == null || string.IsNullOrEmpty(f.levelId) || a.level_id == f.levelId))
                         .OrderBy(a => d.NameOf(a.user_id), StringComparer.OrdinalIgnoreCase).ThenBy(a => a.level_id, StringComparer.Ordinal)
                         .ThenBy(a => a.attempt_number).ToList();
    }

    public static string AttemptsCsv(ClassData d, ReportFilter f)
    {
        var titles = d.activities.ToDictionary(a => a.level_id, a => a.title);
        var sb = new StringBuilder();
        Line(sb, "class", "student", "level", "level_title", "attempt", "score", "max_score", "percent", "correct", "incorrect",
             "total_questions", "passed", "completed", "stars", "answer_seconds", "finished_at_utc");
        foreach (var a in FilterAttempts(d, f))
            Line(sb, d.info != null ? d.info.name : "", d.NameOf(a.user_id), a.level_id, titles.TryGetValue(a.level_id, out var t) ? t : "",
                 Num(a.attempt_number), Num(a.score), Num(a.max_score), Num(Math.Round(a.Percent * 100.0, 1)), Num(a.correct_answers),
                 Num(a.incorrect_answers), Num(a.total_questions), a.passed ? "yes" : "no", a.completed ? "yes" : "no", Num(a.stars),
                 Num(Math.Round(a.total_answer_seconds, 1)), Iso(a.completed_at));
        return sb.ToString();
    }

    public static string AnswersCsv(ClassData d, ReportFilter f)
    {
        var runs = FilterAttempts(d, f).ToDictionary(a => a.id, a => a);
        var byId = d.questions.ToDictionary(q => q.id, q => q);
        var sb = new StringBuilder();
        Line(sb, "class", "student", "level", "attempt", "question", "course", "topic", "learning_objective", "first_answer_correct",
             "recovered", "hint_used", "answer_seconds", "finished_at_utc");
        foreach (var x in d.answers.Where(x => runs.ContainsKey(x.result_id))
                                   .OrderBy(x => d.NameOf(x.user_id), StringComparer.OrdinalIgnoreCase).ThenBy(x => x.level_id, StringComparer.Ordinal)
                                   .ThenBy(x => runs[x.result_id].attempt_number).ThenBy(x => x.question_id, StringComparer.Ordinal))
        {
            byId.TryGetValue(x.question_id, out var q);
            Line(sb, d.info != null ? d.info.name : "", d.NameOf(x.user_id), x.level_id, Num(runs[x.result_id].attempt_number), x.question_id,
                 q != null ? q.course : "", q != null ? q.topic : "", q != null ? q.learning_objective : "",
                 x.correct ? "yes" : "no", x.recovered ? "yes" : "no", x.hint_used ? "yes" : "no", Num(Math.Round(x.answer_seconds, 1)), Iso(x.completed_at));
        }
        return sb.ToString();
    }

    static string Num(double v) => v.ToString(CultureInfo.InvariantCulture);
    static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);
    static string Iso(string ts) => A.TryTime(ts, out var t) ? t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "";

    static void Line(StringBuilder sb, params string[] cells)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(CsvCell(cells[i]));
        }
        sb.Append("\r\n");
    }

    /// <summary>
    /// One CSV cell: quoted when it holds a comma, quote or line break, and a leading = + - @ (which a spreadsheet would
    /// run as a formula - display names are typed by players) is made harmless with an apostrophe.
    /// </summary>
    public static string CsvCell(string v)
    {
        if (v == null) return "";
        if (v.Length > 0 && "=+-@\t\r".IndexOf(v[0]) >= 0 && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) v = "'" + v;
        bool quote = v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (v.Contains("\"")) v = v.Replace("\"", "\"\"");
        return quote ? "\"" + v + "\"" : v;
    }
}
