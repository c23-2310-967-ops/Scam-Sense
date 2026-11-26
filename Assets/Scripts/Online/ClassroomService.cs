using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using M = ClassroomModels;

/// <summary>
/// Classroom features over the signed-in Supabase session (the same one CloudAccountService uses):
/// the player's role, joining / leaving a class, the student's own records, and the teacher's classes and class data.
/// Every write goes through a database function that checks the role on the server
/// (supabase/migrations/20261126074705_classroom_roles_classes_records.sql); Row Level Security limits every read.
/// Nothing here is cached across players: the cache is cleared whenever the signed-in user changes.
/// </summary>
public static class ClassroomService
{
    public enum Role { Unknown, Student, Teacher }

    static string cacheUser;
    static Role role = Role.Unknown;
    static List<M.ActivityRow> activities;
    static List<M.QuestionRow> questions;

    /// <summary>Set when the classroom tables / functions are missing (the migration isn't applied yet).</summary>
    public static bool NotInstalled { get; private set; }

    static CloudAccountService Svc => CloudAccountService.Instance;

    static SupabaseClient Client
    {
        get
        {
            if (!Svc.SignedIn) throw new SupabaseException("Sign in to use the classroom.", 401, "session_not_found");
            if (Svc.UserId != cacheUser) { cacheUser = Svc.UserId; role = Role.Unknown; }
            return Svc.Client;
        }
    }

    /// <summary>The role read last for the signed-in user (Unknown until <see cref="LoadRoleAsync"/>).</summary>
    public static Role CurrentRole => Svc.SignedIn && Svc.UserId == cacheUser ? role : Role.Unknown;

    // ---------- Errors ----------

    /// <summary>A message for the player. The classroom functions' own messages (bad code, not a teacher...) are shown as they are.</summary>
    public static string Message(Exception e)
    {
        if (e is SupabaseException se)
        {
            if (IsMissing(se)) return "The classroom isn't set up on the server yet. (Developer: apply supabase/migrations/20261126074705_classroom_roles_classes_records.sql.)";
            if (!se.IsNetwork && !string.IsNullOrEmpty(se.Message) && (se.Status == 400 || se.Code == "42501" || se.Code == "22023") &&
                !se.Message.Contains("row-level security"))
                return se.Message;
        }
        return CloudAccountService.FriendlyError(e);
    }

    static bool IsMissing(SupabaseException se) =>
        se.Status == 404 || (se.Code ?? "").Contains("PGRST202") || (se.Code ?? "").Contains("PGRST205") || (se.Code ?? "") == "42P01" || (se.Code ?? "") == "42883";

    static async Task<string> Rpc(string fn, string args = null)
    {
        try
        {
            string r = await Client.RpcAsync(fn, args);
            NotInstalled = false;
            return r;
        }
        catch (SupabaseException se) when (IsMissing(se)) { NotInstalled = true; throw; }
    }

    static string Arg(string name, string value) => "{\"" + name + "\":" + Json(value) + "}";
    static string Json(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
    static string Unquote(string json) => string.IsNullOrEmpty(json) ? "" : json.Trim().Trim('"');
    static bool IsNull(string json) => string.IsNullOrWhiteSpace(json) || json.Trim() == "null";

    // ---------- Role ----------

    public static async Task<Role> LoadRoleAsync()
    {
        string r = Unquote(await Rpc("my_role"));
        role = r == "teacher" ? Role.Teacher : Role.Student;
        return role;
    }

    /// <summary>TEACHER CODE: an account becomes a teacher with an invite code made by the school's admin.</summary>
    public static async Task<M.ActionResult> ClaimTeacherAsync(string code)
    {
        var res = JsonUtility.FromJson<M.ActionResult>(await Rpc("claim_teacher_role", Arg("p_code", (code ?? "").Trim())));
        if (res != null && res.ok) role = Role.Teacher;
        return res ?? new M.ActionResult { ok = false, error = "The server didn't answer." };
    }

    // ---------- Students ----------

    public static async Task<M.MyClass> MyClassAsync()
    {
        string json = await Rpc("my_class");
        return IsNull(json) ? null : JsonUtility.FromJson<M.MyClass>(json);
    }

    public static async Task<M.ActionResult> JoinClassAsync(string code)
    {
        string problem = ClassCode.Problem(code);
        if (problem != null) return new M.ActionResult { ok = false, error = problem };
        var res = JsonUtility.FromJson<M.ActionResult>(await Rpc("join_class", Arg("p_code", ClassCode.Normalize(code))));
        return res ?? new M.ActionResult { ok = false, error = "The server didn't answer." };
    }

    public static Task LeaveClassAsync() => Rpc("leave_class");

    /// <summary>The signed-in player's own runs and per-question rows (Row Level Security returns only theirs).</summary>
    public static async Task<StudentRecords> LoadMyRecordsAsync()
    {
        string uid = Client == null ? null : Svc.UserId;
        var r = new StudentRecords();
        await LoadCatalogAsync();
        r.activities = activities;
        r.questions = questions;
        r.attempts = await Client.SelectAllAsync<M.AttemptRow>($"game_results?user_id=eq.{uid}&select=*&order=completed_at.asc,id.asc");
        try { r.answers = await Client.SelectAllAsync<M.AnswerRow>($"question_results?user_id=eq.{uid}&select=*&order=result_id.asc,question_id.asc"); }
        catch (SupabaseException se) when (IsMissing(se)) { NotInstalled = true; r.answers = new List<M.AnswerRow>(); }
        return r;
    }

    static async Task LoadCatalogAsync()
    {
        if (activities != null && questions != null && activities.Count > 0) return;
        try
        {
            activities = await Client.SelectAllAsync<M.ActivityRow>("activities?select=*&order=level_number.asc");
            questions = await Client.SelectAllAsync<M.QuestionRow>("questions?select=*&order=id.asc");
            NotInstalled = false;
        }
        catch (SupabaseException se) when (IsMissing(se)) { NotInstalled = true; throw; }
    }

    // ---------- Teachers ----------

    public static async Task<List<M.ClassInfo>> ListClassesAsync()
    {
        string uid = Client == null ? null : Svc.UserId;
        return await Client.SelectAllAsync<M.ClassInfo>($"classes?teacher_id=eq.{uid}&select=*&order=archived.asc,created_at.desc");
    }

    public static async Task<M.ClassInfo> CreateClassAsync(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length < 2 || name.Length > 60) throw new SupabaseException("Class names are 2 to 60 characters long.", 400, "22023");
        return JsonUtility.FromJson<M.ClassInfo>(await Rpc("create_class", Arg("p_name", name)));
    }

    public static Task SetArchivedAsync(string classId, bool archived) =>
        Rpc("set_class_archived", "{\"p_class\":" + Json(classId) + ",\"p_archived\":" + (archived ? "true" : "false") + "}");

    public static Task RemoveStudentAsync(string classId, string studentId) =>
        Rpc("remove_student", "{\"p_class\":" + Json(classId) + ",\"p_student\":" + Json(studentId) + "}");

    public static Task ReadmitStudentAsync(string classId, string studentId) =>
        Rpc("readmit_student", "{\"p_class\":" + Json(classId) + ",\"p_student\":" + Json(studentId) + "}");

    /// <summary>Everything about one class the teacher may see: roster (with display names), results and per-question rows.</summary>
    public static async Task<ClassData> LoadClassAsync(M.ClassInfo info)
    {
        var c = Client;
        await LoadCatalogAsync();
        var d = new ClassData { info = info, activities = activities, questions = questions };
        d.members = await c.SelectAllAsync<M.MemberRow>($"class_members?class_id=eq.{info.id}&select=*&order=joined_at.asc,student_id.asc");
        var ids = d.members.Select(m => m.student_id).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        for (int i = 0; i < ids.Count; i += 80)   // keep the URL short
        {
            string list = string.Join(",", ids.Skip(i).Take(80));
            foreach (var p in await c.SelectAllAsync<M.ProfileRow>($"profiles?id=in.({list})&select=id,display_name,created_at&order=id.asc"))
                d.names[p.id] = p.display_name;
        }
        d.attempts = await c.SelectAllAsync<M.AttemptRow>($"game_results?class_id=eq.{info.id}&select=*&order=completed_at.asc,id.asc");
        d.answers = await c.SelectAllAsync<M.AnswerRow>($"question_results?class_id=eq.{info.id}&select=*&order=result_id.asc,question_id.asc");
        return d;
    }

    /// <summary>A student's saved progress and achievements (only while enrolled in one of the teacher's classes).</summary>
    public static async Task<(List<M.ProgressRow> progress, List<M.AchievementRow> achievements)> LoadStudentProgressAsync(string studentId)
    {
        var c = Client;
        var p = await c.SelectAllAsync<M.ProgressRow>($"level_progress?user_id=eq.{studentId}&select=*&order=level_id.asc");
        var a = await c.SelectAllAsync<M.AchievementRow>($"achievements?user_id=eq.{studentId}&select=*&order=unlocked_utc.asc");
        return (p, a);
    }
}
