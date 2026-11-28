using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Rows of the classroom tables exactly as the database returns them (snake_case = column names).
/// See supabase/migrations/20261126074705_classroom_roles_classes_records.sql.
/// </summary>
public static class ClassroomModels
{
    [Serializable] public class ClassInfo { public string id, teacher_id, name, code, created_at; public bool archived; }

    [Serializable] public class MemberRow { public string class_id, student_id, joined_at, removed_at, removed_by; }

    [Serializable] public class ProfileRow { public string id, display_name, created_at; }

    /// <summary>public.game_results: one finished run.</summary>
    [Serializable]
    public class AttemptRow
    {
        public long id;
        public string user_id, run_id, level_id, completed_at, class_id;
        public int score, max_score, stars, correct_answers, incorrect_answers, total_questions, attempt_number;
        public bool passed, completed;
        public float total_answer_seconds;

        public float Percent => max_score > 0 ? (float)score / max_score : 0f;
    }

    /// <summary>public.question_results: one question of one run.</summary>
    [Serializable]
    public class AnswerRow
    {
        public long result_id;
        public string user_id, class_id, level_id, question_id, completed_at;
        public bool correct, recovered, hint_used;
        public float answer_seconds;
    }

    [Serializable] public class ActivityRow { public string level_id, title; public int level_number, question_count, max_score, pass_score; }

    [Serializable] public class QuestionRow { public string id, level_id, course, topic, learning_objective; }

    [Serializable]
    public class ProgressRow
    {
        public string user_id, level_id;
        public bool unlocked, completed, passed, perfect;
        public int best_score, best_stars, times_completed, best_correct, message_count;
        public long last_played_utc;
    }

    [Serializable] public class AchievementRow { public string user_id, achievement_id; public long unlocked_utc; }

    /// <summary>join_class / claim_teacher_role answer.</summary>
    [Serializable] public class ActionResult { public bool ok; public string error, class_id, class_name, teacher_name, role; public int members_removed; }

    [Serializable] public class MyClass { public string class_id, class_name, teacher_name, joined_at; }

    // ---------- helpers ----------

    /// <summary>Parses a Postgres timestamptz ("2026-11-25T09:00:00.123+00:00"). Returns false for null / "".</summary>
    public static bool TryTime(string s, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrEmpty(s)) return false;
        if (!DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t)) return false;
        utc = t.UtcDateTime;
        return true;
    }

    public static string ShortDate(string s) => TryTime(s, out var t) ? t.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : "-";
    public static string DateTimeText(string s) => TryTime(s, out var t) ? t.ToLocalTime().ToString("MMM d, yyyy  HH:mm", CultureInfo.InvariantCulture) : "-";

    public static bool IsActive(MemberRow m) => m != null && string.IsNullOrEmpty(m.removed_at);
}

/// <summary>Everything the teacher portal shows for one class, loaded once and analysed locally.</summary>
public class ClassData
{
    public ClassroomModels.ClassInfo info;
    public List<ClassroomModels.MemberRow> members = new List<ClassroomModels.MemberRow>();          // includes removed students
    public Dictionary<string, string> names = new Dictionary<string, string>();                      // student id -> display name
    public List<ClassroomModels.AttemptRow> attempts = new List<ClassroomModels.AttemptRow>();       // runs recorded in this class
    public List<ClassroomModels.AnswerRow> answers = new List<ClassroomModels.AnswerRow>();          // their per-question rows
    public List<ClassroomModels.ActivityRow> activities = new List<ClassroomModels.ActivityRow>();
    public List<ClassroomModels.QuestionRow> questions = new List<ClassroomModels.QuestionRow>();

    public string NameOf(string studentId) =>
        studentId != null && names.TryGetValue(studentId, out var n) && !string.IsNullOrEmpty(n) ? n : "Student";
}

/// <summary>A student's own records (student portal).</summary>
public class StudentRecords
{
    public List<ClassroomModels.AttemptRow> attempts = new List<ClassroomModels.AttemptRow>();
    public List<ClassroomModels.AnswerRow> answers = new List<ClassroomModels.AnswerRow>();
    public List<ClassroomModels.ActivityRow> activities = new List<ClassroomModels.ActivityRow>();
    public List<ClassroomModels.QuestionRow> questions = new List<ClassroomModels.QuestionRow>();
}
