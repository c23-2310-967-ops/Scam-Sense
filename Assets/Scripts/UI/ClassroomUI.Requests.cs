using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using M = ClassroomModels;

/// <summary>
/// REQUESTS tab - the approval workflow for question changes (supabase/migrations/20261128101505_question_requests.sql).
///   Teacher: MY REQUESTS - every ADD / EDIT / DELETE request they sent, its status (pending, approved, rejected,
///            withdrawn), the admin's remarks; VIEW shows what was asked, WITHDRAW takes back a pending one.
///   Admin:   REQUESTS - pending first, filters for approved / rejected / all (the approval history), and a review
///            page with the teacher's reason and a field-by-field CURRENT vs PROPOSED comparison. APPROVE applies the
///            change on the server in the same step; REJECT needs a remark the teacher will read.
/// Permissions are enforced by the server functions, not by which buttons are shown.
/// </summary>
public partial class ClassroomUI
{
    bool portalAdmin;                          // set from the server (is_content_admin) when the panel loads
    List<QuestionBankService.Request> adminRequests;
    bool requestsLoading;
    string requestsError;
    string requestFilter = "pending";          // admin: pending / approved / rejected / "" (all)
    string openRequestId;
    string reviewRemarks = "";
    bool reviewBusy;

    // ---------- Entry ----------

    void QuestionRequests()
    {
        if (!string.IsNullOrEmpty(requestsError))
        {
            var err = UICharts.Card(content, portalAdmin ? "Teacher requests" : "My requests");
            Line(err, Esc(requestsError), Body, UIKit.Danger);
            Btn(err, "TRY AGAIN", () => { requestsError = null; LoadRequests(); }, UIKit.HudVariant.Primary, 260);
            return;
        }
        var list = portalAdmin ? adminRequests : (qbank != null ? myRequests : null);
        if (list == null)
        {
            if (!requestsLoading) LoadRequests();
            Line(content, "Loading requests…", Body, Muted);
            return;
        }
        var open = list.FirstOrDefault(r => r.id == openRequestId);
        if (open != null) RequestDetail(open);
        else RequestList(list);
    }

    async void LoadRequests(Action after = null)
    {
        int v = version;
        requestsLoading = true;
        try
        {
            if (qbank == null)
            {
                var bank = await QuestionBankService.LoadAsync();
                if (bank.Usable) qbank = bank;
                qAdmin = portalAdmin;   // the QUESTIONS tab reuses this bank, so it must know the role too
            }
            if (portalAdmin) adminRequests = await QuestionBankService.AdminRequestsAsync(requestFilter);
            else myRequests = await QuestionBankService.MyRequestsAsync();
            requestsError = null;
        }
        catch (SupabaseException se) when (se.Status == 404 || (se.Code ?? "").Contains("PGRST202"))
        {
            requestsError = "Question requests aren't set up on the server yet (developer: apply supabase/migrations/20261128101505_question_requests.sql).";
        }
        catch (Exception e) { requestsError = QuestionBankService.Message(e); }
        finally { requestsLoading = false; }
        if (!Current(v) || tab != Tab.Requests) return;
        after?.Invoke();
        RenderKeepScroll();
    }

    // ---------- List ----------

    void RequestList(List<QuestionBankService.Request> list)
    {
        if (portalAdmin)
        {
            var head = UICharts.Card(content, "Teacher requests",
                "Teachers can't change questions themselves. Review each request: APPROVE applies it right away, REJECT sends your remarks back to the teacher.");
            var filters = RowH(head, 56, 10);
            RequestFilter(filters, "PENDING", "pending");
            RequestFilter(filters, "APPROVED", "approved");
            RequestFilter(filters, "REJECTED", "rejected");
            RequestFilter(filters, "ALL (HISTORY)", "");
        }
        else
        {
            int pending = list.Count(r => r.status == "pending");
            UICharts.Card(content, "My requests",
                $"{pending} waiting for the admin. Approved changes reach students the next time they load a level. To request a change, use QUESTIONS > VIEW / REQUEST.");
        }

        var card = UICharts.Card(content, portalAdmin ? (requestFilter == "" ? "All requests" : char.ToUpper(requestFilter[0]) + requestFilter.Substring(1)) : "Sent requests");
        if (list.Count == 0) { UICharts.Empty(card, portalAdmin && requestFilter == "pending" ? "No requests waiting. All caught up." : "No requests here yet."); return; }
        var cols = portalAdmin ? new[] { 170f, 230f, 120f, 120f, -1f, 160f, 200f } : new[] { 170f, 120f, 120f, -1f, 160f, 330f };
        if (portalAdmin) TableRow(card, cols, true, "SENT", "TEACHER", "TYPE", "QUESTION", "REASON", "STATUS", "");
        else TableRow(card, cols, true, "SENT", "TYPE", "QUESTION", "REASON / ADMIN REMARKS", "STATUS", "");
        foreach (var r in list)
        {
            string reason = Snip(r.status != "pending" && !portalAdmin && !string.IsNullOrEmpty(r.admin_remarks) ? "Admin: " + r.admin_remarks : r.reason, 90);
            var cells = portalAdmin
                ? new[] { M.ShortDate(r.created_at), Esc(r.teacher_name), TypeLabel(r.request_type), $"<b>{Esc(r.question_id)}</b>", Esc(reason), StatusLabel(r.status), null }
                : new[] { M.ShortDate(r.created_at), TypeLabel(r.request_type), $"<b>{Esc(r.question_id)}</b>", Esc(reason), StatusLabel(r.status), null };
            var row = TableRow(card, cols, false, cells);
            var cell = row.GetChild(row.childCount - 1);
            var h = UIKit.Horizontal((RectTransform)cell, 10, TextAnchor.MiddleRight);
            h.childForceExpandWidth = false;
            string id = r.id;
            Btn(cell, portalAdmin && r.status == "pending" ? "REVIEW" : "VIEW", () => { openRequestId = id; reviewRemarks = ""; Render(); },
                portalAdmin && r.status == "pending" ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary, 160, 46, 18).name = "Open_" + r.question_id;
            if (!portalAdmin && r.status == "pending")
                Btn(cell, "WITHDRAW", () => Withdraw(id), UIKit.HudVariant.Secondary, 160, 46, 18).name = "Withdraw_" + r.question_id;
        }
    }

    void RequestFilter(Transform parent, string label, string filter)
    {
        bool on = requestFilter == filter;
        Btn(parent, label, () => { if (requestFilter == filter) return; requestFilter = filter; adminRequests = null; openRequestId = null; Render(); },
            on ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary, Mathf.Max(150f, 50f + label.Length * 16f), 56, 19).name = "ReqFilter_" + (filter == "" ? "all" : filter);
    }

    static string TypeLabel(string t) =>
        t == "add" ? $"<color={UIKit.HexSafe}>ADD</color>" : t == "delete" ? $"<color=#F87171>DELETE</color>" : $"<color={UIKit.HexAccent}>EDIT</color>";

    static string StatusLabel(string s) =>
        s == "approved" ? $"<color={UIKit.HexSafe}>Approved</color>" : s == "rejected" ? "<color=#F87171>Rejected</color>"
        : s == "withdrawn" ? "<color=#94A3B8>Withdrawn</color>" : $"<color={UIKit.HexWarning}>Pending</color>";

    // ---------- One request ----------

    void RequestDetail(QuestionBankService.Request r)
    {
        var head = UICharts.Card(content, $"{r.request_type.ToUpperInvariant()} request  ·  {r.question_id}",
            $"Sent {M.ShortDate(r.created_at)} by {Esc(r.teacher_name)}   ·   {StatusLabel(r.status)}" +
            (r.status != "pending" && !string.IsNullOrEmpty(r.reviewed_at) ? $"   ·   decided {M.ShortDate(r.reviewed_at)}" + (string.IsNullOrEmpty(r.reviewer_name) ? "" : $" by {Esc(r.reviewer_name)}") : ""));
        Line(head, $"<b>Reason:</b> {Esc(r.reason)}", Body, UIKit.TextPrimary).name = "RequestReason";
        if (!string.IsNullOrEmpty(r.admin_remarks)) Line(head, $"<b>Admin remarks:</b> {Esc(r.admin_remarks)}", Body, UIKit.TextPrimary).name = "AdminRemarks";
        if (r.status == "pending" && r.changed_since)
            Line(head, $"<color={UIKit.HexWarning}>! This question was changed after the request was sent. Approving replaces the current version with the proposal below.</color>", Small, UIKit.TextPrimary).name = "ChangedSince";

        // What would change
        var current = r.HasCurrent ? r.current.ToRecord(r.question_id) : null;
        var proposed = r.HasProposed ? r.proposed.ToRecord(r.question_id) : null;
        var cmp = UICharts.Card(content, r.request_type == "add" ? "Proposed new question" : r.request_type == "delete" ? "Question to delete" : "Changes (current → proposed)");
        if (r.request_type == "edit" && current != null && proposed != null)
        {
            var diff = QuestionEditing.Diff(current, proposed);
            if (diff.Count == 0) UICharts.Empty(cmp, "The proposal is the same as the current version.");
            else CompareRows(cmp, diff.Select(d => (d.field, d.before, d.after)).ToList(), "CURRENT", "PROPOSED");
            int same = QuestionEditing.Describe(proposed).Count - diff.Count;
            if (same > 0) Line(cmp, $"{same} other field{(same == 1 ? "" : "s")} unchanged.", 17f, Muted);
        }
        else
        {
            var show = proposed ?? current;
            if (show == null) UICharts.Empty(cmp, r.request_type == "delete" ? "The question no longer exists." : "No proposal attached.");
            else CompareRows(cmp, QuestionEditing.Describe(show).Where(f => f.value.Length > 0).Select(f => (f.field, "", f.value)).ToList(), null, "VALUE");
            if (r.request_type == "add" && proposed != null)
                foreach (var i in QuestionEditing.Check(proposed, qbank?.data, false).Where(i => !i.IsError))
                    Line(cmp, $"<color={UIKit.HexWarning}>! {Esc(i.message)}</color>", 17f, UIKit.TextPrimary);
        }

        // Decision
        var act = UICharts.Card(content, r.status == "pending" ? (portalAdmin ? "Your decision" : "Waiting for the admin") : "Decision");
        if (portalAdmin && r.status == "pending")
        {
            var row = RowH(act, 120);
            var l = Label(row, "REMARKS", 210, Small, Muted, true);
            l.alignment = TextAlignmentOptions.TopLeft;
            var f = Input(row, "RemarksInput", "what the teacher should know (required to reject)", 1000, -1);
            f.lineType = TMP_InputField.LineType.MultiLineNewline;
            UIKit.Size(f, -1, 115);
            f.GetComponent<LayoutElement>().flexibleWidth = 1f;
            f.textComponent.alignment = TextAlignmentOptions.TopLeft;
            if (f.placeholder is TMP_Text ph) ph.alignment = TextAlignmentOptions.TopLeft;
            f.text = reviewRemarks;
            f.onValueChanged.AddListener(s => reviewRemarks = s);
            var buttons = RowH(act, 62);
            Btn(buttons, reviewBusy ? "WORKING…" : "APPROVE", () => Review(r, true), UIKit.HudVariant.Safe, 240).name = "Approve";
            Btn(buttons, "REJECT", () => Review(r, false), UIKit.HudVariant.Danger, 220).name = "Reject";
            Btn(buttons, "BACK", () => { openRequestId = null; Render(); }, UIKit.HudVariant.Secondary, 180).name = "BackToRequests";
            if (r.request_type == "delete") Line(act, "Approving deletes the question from the bank (its history keeps a copy). Turning it off instead keeps it: reject with that suggestion.", 17f, Muted);
        }
        else
        {
            if (r.status == "pending") Line(act, "The admin hasn't decided yet. Nothing changes for students until it's approved.", Body, UIKit.TextSecondary);
            var buttons = RowH(act, 62);
            if (!portalAdmin && r.status == "pending") Btn(buttons, "WITHDRAW", () => Withdraw(r.id), UIKit.HudVariant.Secondary, 220).name = "WithdrawDetail";
            Btn(buttons, "BACK", () => { openRequestId = null; Render(); }, UIKit.HudVariant.Secondary, 180).name = "BackToRequests";
        }
    }

    void CompareRows(Transform parent, List<(string field, string before, string after)> rows, string beforeHeader, string afterHeader)
    {
        var cols = beforeHeader == null ? new[] { 230f, -1f } : new[] { 230f, -1f, -1f };
        if (beforeHeader == null) TableRow(parent, cols, true, "FIELD", afterHeader);
        else TableRow(parent, cols, true, "FIELD", beforeHeader, afterHeader);
        foreach (var (field, before, after) in rows)
        {
            // Long text wraps: a plain card row (TableRow cells don't wrap).
            var row = UIKit.Rect("Compare", parent);
            UIKit.AddImage(row, new Color(0.08f, 0.12f, 0.2f, 1f), rounded: true).raycastTarget = false;
            var h = UIKit.Horizontal(row, 14, TextAnchor.UpperLeft);
            h.padding = new RectOffset(16, 12, 10, 10);
            h.childForceExpandWidth = false; h.childControlHeight = true; h.childForceExpandHeight = false;   // the card's layout sizes the row to its tallest text
            Label(row, field, 230, Small, Muted, true).textWrappingMode = TextWrappingModes.Normal;
            if (beforeHeader != null)
            {
                var b = Line(row, string.IsNullOrEmpty(before) ? "<i>(empty)</i>" : Esc(before), Small, Muted);
                b.GetComponent<LayoutElement>().flexibleWidth = 1f; b.GetComponent<LayoutElement>().preferredWidth = 400;
            }
            var a = Line(row, string.IsNullOrEmpty(after) ? "<i>(empty)</i>" : Esc(after), Small, UIKit.TextPrimary);
            a.GetComponent<LayoutElement>().flexibleWidth = 1f; a.GetComponent<LayoutElement>().preferredWidth = 400;
        }
    }

    // ---------- Actions ----------

    async void Review(QuestionBankService.Request r, bool approve)
    {
        if (reviewBusy) return;
        if (!approve && (reviewRemarks ?? "").Trim().Length < 3) { SetStatus("Add a remark so the teacher knows why it was rejected.", true); return; }
        if (approve && r.request_type != "delete" && r.HasProposed)
        {
            // The game's own checks first: the server re-checks the core rules, the game also checks clues.
            var errors = QuestionEditing.Check(r.proposed.ToRecord(r.question_id), qbank?.data, false).Where(i => i.IsError).ToList();
            if (errors.Count > 0) { SetStatus($"Can't approve: {errors[0].message}. Reject it with that remark so the teacher can fix it.", true); return; }
        }
        int v = version;
        reviewBusy = true;
        RenderKeepScroll();
        try
        {
            var res = await QuestionBankService.ReviewAsync(r.id, approve, reviewRemarks?.Trim());
            if (!Current(v)) return;
            if (!res.ok) { SetStatus(res.error, true); return; }
            openRequestId = null; reviewRemarks = "";
            string msg = approve ? $"Approved: {r.request_type} {r.question_id}. Students get it the next time they load a level." : $"Rejected {r.question_id}. The teacher will see your remarks.";
            qbank = null; adminRequests = null;   // reload both: the bank changed
            LoadRequests(() => SetStatus(msg, false));
        }
        catch (Exception e) { if (Current(v)) SetStatus(QuestionBankService.Message(e), true); }
        finally { reviewBusy = false; if (Current(v)) RenderKeepScroll(); }
    }

    async void Withdraw(string requestId)
    {
        int v = version;
        try
        {
            var res = await QuestionBankService.WithdrawRequestAsync(requestId);
            if (!Current(v)) return;
            if (!res.ok) { SetStatus(res.error, true); return; }
            openRequestId = null;
            LoadRequests(() => SetStatus("Request withdrawn.", false));
        }
        catch (Exception e) { if (Current(v)) SetStatus(QuestionBankService.Message(e), true); }
    }

#if UNITY_EDITOR
    /// <summary>Editor only (layout checks): shows the REQUESTS tab with rows passed in instead of the server's.</summary>
    public void EditorPreviewRequests(QuestionBankFile bank, bool admin, List<QuestionBankService.Request> requests, string openId = null)
    {
        qbank = new LoadedQuestionBank { data = bank, source = ContentSource.Bundled, issues = new List<ContentIssue>() };
        qAdmin = portalAdmin = admin;
        if (admin) adminRequests = requests; else myRequests = requests;
        version++;
        previewTeacher = true;
        tab = Tab.Requests;
        openRequestId = openId;
        if (!IsOpen) { dim.SetActive(true); panel.gameObject.SetActive(true); IsOpen = true; }
        Render();
    }
#endif
}
