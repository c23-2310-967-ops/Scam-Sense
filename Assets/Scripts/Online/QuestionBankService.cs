using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Teacher / admin access to the managed question bank (supabase/migrations/20261128004018_question_bank.sql).
/// Every call goes through the player's own sign-in, so the server decides what is allowed (never a service key):
/// admins (content_admins) write question_bank directly and review requests; teachers only send ADD / EDIT / DELETE
/// requests (supabase/migrations/20261128101505_question_requests.sql); students can do neither. The server also
/// re-checks each active question and keeps the old version in question_bank_history.
/// </summary>
public static class QuestionBankService
{
    static CloudAccountService Svc => CloudAccountService.Instance;

    static SupabaseClient Client
    {
        get
        {
            if (!Svc.SignedIn) throw new SupabaseException("Sign in to edit questions.", 401, "session_not_found");
            return Svc.Client;
        }
    }

    /// <summary>The whole bank as editors see it (inactive and reserve questions included).</summary>
    public static async Task<LoadedQuestionBank> LoadAsync()
    {
        string json = await Client.RpcAsync(CloudContentSync.RpcName);
        return QuestionBankRepository.Parse(json, ContentSource.CloudCache, "supabase:" + CloudContentSync.RpcName);
    }

    /// <summary>True for content admins (may delete questions).</summary>
    public static async Task<bool> IsAdminAsync()
    {
        try { return (await Client.RpcAsync("is_content_admin")).Trim() == "true"; }
        catch (SupabaseException) { return false; }
    }

    /// <summary>Saves a question: a new row, or an update of the existing one (same id).</summary>
    public static async Task SaveAsync(QuestionRecord q, bool isNew)
    {
        if (isNew) await Client.UpsertAsync("question_bank", "[" + QuestionEditing.ToRowJson(q) + "]", "question_id", ignoreDuplicates: false);
        else await Client.UpdateAsync("question_bank?question_id=eq." + Uri.EscapeDataString(q.questionId), QuestionEditing.ToRowJson(q, includeId: false));
        AfterChange();
    }

    /// <summary>Turns a question on or off (kept in the bank either way).</summary>
    public static async Task SetActiveAsync(string questionId, bool active)
    {
        await Client.UpdateAsync("question_bank?question_id=eq." + Uri.EscapeDataString(questionId), "{\"is_active\":" + (active ? "true" : "false") + "}");
        AfterChange();
    }

    /// <summary>Content admins only (the server refuses everyone else). Prefer turning a question off.</summary>
    public static async Task DeleteAsync(string questionId)
    {
        string filter = "question_bank?question_id=eq." + Uri.EscapeDataString(questionId);
        await Client.DeleteAsync(filter);
        // Row Level Security skips rows it doesn't allow without an error, so check that the row is really gone.
        string left = await Client.SelectAsync(filter + "&select=question_id");
        if (!string.IsNullOrEmpty(left) && left.Contains(questionId))
            throw new SupabaseException("Only content admins can delete questions. Turn it off instead: it stays in the bank but is never shown.", 403, "42501");
        AfterChange();
    }

    // ---------- Teacher requests (supabase/migrations/20261128101505_question_requests.sql) ----------

    /// <summary>A question_bank row as the server stores it (management columns + the record).</summary>
    [Serializable] public class RowData
    {
        public string level_id, category, difficulty, question_type, message_type;
        public int sort_order;
        public bool is_active = true;
        public QuestionRecord content;

        /// <summary>The full record for <paramref name="questionId"/>, columns applied.</summary>
        public QuestionRecord ToRecord(string questionId)
        {
            var q = content != null ? QuestionEditing.Copy(content) : new QuestionRecord();
            q.questionId = questionId; q.levelId = level_id ?? ""; q.order = sort_order; q.isActive = is_active;
            q.category = category ?? ""; q.difficulty = difficulty ?? ""; q.questionType = question_type ?? ""; q.messageType = message_type ?? "SMS";
            return q;
        }
    }

    /// <summary>One teacher request with what the reviewer needs to decide.</summary>
    [Serializable] public class Request
    {
        public string id, teacher_id, teacher_name, request_type, question_id, reason, status, admin_remarks, reviewer_name, created_at, reviewed_at;
        public bool changed_since;
        public RowData proposed, current;
        public bool HasProposed => proposed != null && proposed.content != null && !string.IsNullOrEmpty(proposed.message_type);
        public bool HasCurrent => current != null && current.content != null && !string.IsNullOrEmpty(current.message_type);
    }

    [Serializable] public class Result { public bool ok; public string error, request_id, status; }

    /// <summary>Teacher: asks the admin to ADD, EDIT or DELETE a question. Nothing changes until it is approved.</summary>
    public static async Task<Result> SubmitRequestAsync(string type, QuestionRecord q, string questionId, string reason)
    {
        string args = "{\"p_type\":" + QuestionEditing.Str(type) + ",\"p_question\":" + QuestionEditing.Str(questionId) +
                      ",\"p_proposed\":" + (type == "delete" || q == null ? "null" : QuestionEditing.ToRowJson(q, includeId: false)) +
                      ",\"p_reason\":" + QuestionEditing.Str(reason ?? "") + "}";
        return Parse(await Client.RpcAsync("submit_question_request", args));
    }

    public static async Task<Result> WithdrawRequestAsync(string requestId) =>
        Parse(await Client.RpcAsync("withdraw_question_request", "{\"p_request\":" + QuestionEditing.Str(requestId) + "}"));

    /// <summary>Teacher: their own requests, newest first (pending on top).</summary>
    public static async Task<System.Collections.Generic.List<Request>> MyRequestsAsync() =>
        SupabaseClient.ParseArray<Request>(await Client.RpcAsync("my_question_requests"));

    /// <summary>Admin: every request with the given status ("" = all).</summary>
    public static async Task<System.Collections.Generic.List<Request>> AdminRequestsAsync(string status) =>
        SupabaseClient.ParseArray<Request>(await Client.RpcAsync("admin_question_requests", "{\"p_status\":" + QuestionEditing.Str(status ?? "") + "}"));

    /// <summary>Admin: approve (applies the change in the same step) or reject (remarks required).</summary>
    public static async Task<Result> ReviewAsync(string requestId, bool approve, string remarks)
    {
        var r = Parse(await Client.RpcAsync("review_question_request",
            "{\"p_request\":" + QuestionEditing.Str(requestId) + ",\"p_approve\":" + (approve ? "true" : "false") + ",\"p_remarks\":" + QuestionEditing.Str(remarks ?? "") + "}"));
        if (r.ok && approve) AfterChange();
        return r;
    }

    [Serializable] public class HistoryEntry { public string operation, changed_at, changed_by; public RowData old_row; }

    /// <summary>Admin: earlier versions of a question (newest first).</summary>
    public static async Task<System.Collections.Generic.List<HistoryEntry>> HistoryAsync(string questionId) =>
        SupabaseClient.ParseArray<HistoryEntry>(await Client.RpcAsync("question_history", "{\"p_question\":" + QuestionEditing.Str(questionId) + "}"));

    static Result Parse(string json) => (string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Result>(json)) ?? new Result { ok = false, error = "The server didn't answer." };

    /// <summary>The game downloads the new content now, so the next level played on this PC uses it.</summary>
    static void AfterChange() => CloudContentSync.RequestSync();

    /// <summary>A message for the teacher. The server's own checks ("Question T01: explanation is empty") are shown as they are.</summary>
    public static string Message(Exception e)
    {
        if (e is SupabaseException se)
        {
            if (se.Status == 404 || (se.Code ?? "").Contains("PGRST202") || (se.Code ?? "").Contains("PGRST205"))
                return "The question bank isn't set up on the server yet.";
            if (se.Code == "42501" || (se.Message ?? "").Contains("row-level security"))
                return "Your account isn't allowed to do that. Only admins change questions directly; teachers send requests for the admin to approve.";
            if (se.Code == "23505") return "Another question already uses this id.";
            if (!se.IsNetwork && !string.IsNullOrEmpty(se.Message) && (se.Status == 400 || se.Code == "22023")) return se.Message;
        }
        return CloudAccountService.FriendlyError(e);
    }
}
