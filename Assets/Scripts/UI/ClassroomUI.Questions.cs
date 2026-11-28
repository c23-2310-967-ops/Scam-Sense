using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using M = ClassroomModels;

/// <summary>
/// QUESTIONS tab: the question library and editor, in two modes decided by the server's roles.
///   Admin   (content_admins): edits public.question_bank directly - SAVE, TURN OFF / ON, DELETE, HISTORY.
///   Teacher (user_roles):     the same editor, but every change is a REQUEST to the admin with a reason:
///                             SUBMIT REQUEST (add / edit, including on / off and moving level) and REQUEST DELETE.
///                             Nothing a teacher does changes what students play until an admin approves it
///                             (the server enforces this; see supabase/migrations/20261128101505_question_requests.sql).
/// Edits cover text, sender, subject, link, answer style, choices, the correct answer, explanation, feedback, hint,
/// level, position, category, difficulty and on / off; every save or request is checked first with the game's own
/// validator. Clickable clues, activities and wrong-answer settings are kept as they are (CHECK offers to repair
/// clues whose words were edited away). Requests and reviews: ClassroomUI.Requests.cs.
/// </summary>
public partial class ClassroomUI
{
    LoadedQuestionBank qbank;
    bool qbankLoading, qAdmin, qSaving;
    string qbankError;
    List<QuestionBankService.Request> myRequests = new List<QuestionBankService.Request>();
    string draftReason = "";
    List<QuestionBankService.HistoryEntry> qHistory;   // admin: the open question's earlier versions (null = not loaded)
    string qFilter = "level_1";          // level id, "reserve", "off" or "all"
    QuestionRecord draft;                // the question being edited (a copy; nothing is saved until SAVE)
    bool draftIsNew;
    List<ContentIssue> draftIssues;
    bool confirmDeleteQuestion;

    const float QLabel = 210f;

    /// <summary>Wraps around: the step after the last item is the first.</summary>
    static int Cycle(int index, int step, int count) => count <= 0 ? 0 : ((index + step) % count + count) % count;

    // ---------- Entry ----------

    void TeacherQuestions()
    {
        if (qbank == null)
        {
            if (!string.IsNullOrEmpty(qbankError))
            {
                var err = UICharts.Card(content, "Question bank");
                Line(err, Esc(qbankError), Body, UIKit.Danger);
                Btn(err, "TRY AGAIN", () => { qbankError = null; LoadQuestionBank(); }, UIKit.HudVariant.Primary, 260);
                return;
            }
            if (!qbankLoading) LoadQuestionBank();
            Line(content, "Loading the question bank…", Body, Muted);
            return;
        }
        if (draft != null) QuestionForm(); else QuestionList();
    }

    async void LoadQuestionBank(Action after = null)
    {
        int v = version;
        qbankLoading = true;
        try
        {
            var bank = await QuestionBankService.LoadAsync();
            if (!bank.Usable) throw new Exception("The server's question bank couldn't be read: " +
                                                  string.Join("; ", bank.issues.Where(i => i.IsError && i.where.Length == 0)));
            qAdmin = portalAdmin = await QuestionBankService.IsAdminAsync();   // same flag the portal uses
            if (!qAdmin)
            {
                try { myRequests = await QuestionBankService.MyRequestsAsync(); }
                catch (SupabaseException) { myRequests = new List<QuestionBankService.Request>(); }   // requests not set up on the server yet
            }
            qbank = bank;
            qbankError = null;
        }
        catch (Exception e) { qbankError = QuestionBankService.Message(e); Debug.LogWarning("ClassroomUI questions: " + e.Message); }
        finally { qbankLoading = false; }
        if (!Current(v) || (tab != Tab.Questions && tab != Tab.Requests)) return;
        after?.Invoke();
        RenderKeepScroll();
    }

    void RenderKeepScroll()
    {
        var pos = content.anchoredPosition;
        Render();
        content.anchoredPosition = pos;
    }

    /// <summary>Leaves the form (ESC or CANCEL). True if there was a form to leave.</summary>
    bool CloseQuestionForm()
    {
        if (draft == null) return false;
        draft = null; draftIssues = null; confirmDeleteQuestion = false; draftReason = ""; qHistory = null;
        Render();
        return true;
    }

    // ---------- Levels ----------

    List<(string id, string label)> QuestionLevels()
    {
        var list = new List<(string, string)>();
        var cat = menu != null ? menu.Catalog : null;
        if (cat != null)
            foreach (var l in cat.levels.Where(l => l != null)) list.Add((l.levelId, $"Level {l.levelNumber}  ·  {l.displayName}"));
        else if (qbank?.data?.levels != null)
            foreach (var l in qbank.data.levels) list.Add((l.levelId, l.levelId.Replace("level_", "Level ")));
        return list;
    }

    string LevelName(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return "Reserve (not in a level)";
        var found = QuestionLevels().FirstOrDefault(l => l.id == levelId);
        return found.label ?? levelId;
    }

    IEnumerable<QuestionRecord> Filtered()
    {
        var qs = qbank.data.questions.Where(q => q != null);
        switch (qFilter)
        {
            case "all": return qs.OrderBy(q => q.levelId == "" ? "zz" : q.levelId).ThenBy(q => q.order);
            case "reserve": return qs.Where(q => q.isActive && string.IsNullOrEmpty(q.levelId)).OrderBy(q => q.order);
            case "off": return qs.Where(q => !q.isActive).OrderBy(q => q.levelId).ThenBy(q => q.order);
            default: return qs.Where(q => q.levelId == qFilter).OrderBy(q => q.isActive ? 0 : 1).ThenBy(q => q.order);
        }
    }

    // ---------- List ----------

    void QuestionList()
    {
        var head = UICharts.Card(content, qAdmin ? "Question bank  ·  admin" : "Question library",
            qAdmin ? "The questions every class plays. Your changes reach students the next time they load a level. Turning a question OFF keeps it (and its history) in the bank."
                   : "The questions every class plays. To add, change or remove one, send a request: the admin reviews it, and students only see it once it's approved.");
        var filters = RowH(head, 56, 10);
        foreach (var (id, _) in QuestionLevels())
            FilterButton(filters, "L" + id.Replace("level_", ""), id);
        FilterButton(filters, "RESERVE", "reserve");
        FilterButton(filters, "OFF", "off");
        FilterButton(filters, "ALL", "all");
        var spacer = UIKit.Rect("Spacer", filters);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        Btn(filters, qAdmin ? "+ SCAM / LEGIT MESSAGE" : "REQUEST NEW: SCAM / LEGIT", () => StartNewQuestion(false), UIKit.HudVariant.Primary, qAdmin ? 360 : 400, 56, 19).name = "NewVerdictQuestion";
        Btn(filters, qAdmin ? "+ MULTIPLE CHOICE" : "REQUEST NEW: CHOICE", () => StartNewQuestion(true), UIKit.HudVariant.Primary, qAdmin ? 300 : 330, 56, 19).name = "NewChoiceQuestion";

        if (qFilter.StartsWith("level_"))
        {
            int active = qbank.data.questions.Count(q => q != null && q.isActive && q.levelId == qFilter);
            bool ok = active >= LevelContentRules.MinQuestionsPerRoom && active <= LevelContentRules.MaxQuestionsPerRoom;
            Line(head, $"<b>{Esc(LevelName(qFilter))}</b>: {active} active question{(active == 1 ? "" : "s")}" +
                       (ok ? "" : $"   <color={UIKit.HexWarning}>(a room is designed for {LevelContentRules.MinQuestionsPerRoom}-{LevelContentRules.MaxQuestionsPerRoom})</color>") +
                       "   ·   students get them in a new random order each attempt.", Small, UIKit.TextSecondary);
        }

        var list = UICharts.Card(content, qFilter.StartsWith("level_") ? "Questions in this level" : qFilter == "reserve" ? "Reserve (not in any level)" : qFilter == "off" ? "Turned off" : "All questions");
        var rows = Filtered().ToList();
        if (rows.Count == 0) { UICharts.Empty(list, "No questions here."); return; }
        var cols = new[] { 120f, -1f, 250f, 200f, 120f, 120f, 330f };
        TableRow(list, cols, true, "ID", "QUESTION", "TYPE", "CATEGORY", "LEVEL", "STATUS", "");
        foreach (var q in rows)
        {
            string snippet = Snip(UIKit.StripRichText(q.message), 90);
            string type = q.HasChoices ? $"Choice ({q.choices.Length})" : (q.isScam ? "Scam" : "Legitimate") + $"  ·  {q.messageType}";
            string status = q.isActive ? $"<color={UIKit.HexSafe}>On</color>" : $"<color={UIKit.HexWarning}>Off</color>";
            if (!qAdmin && PendingRequestFor(q.questionId) != null) status += $"  <color={UIKit.HexAccent}>· requested</color>";
            var r = TableRow(list, cols, false, $"<b>{Esc(q.questionId)}</b>", Esc(snippet), type, Esc(q.category ?? ""),
                             string.IsNullOrEmpty(q.levelId) ? "Reserve" : "L" + q.levelId.Replace("level_", ""), status, null);
            var cell = r.GetChild(r.childCount - 1);
            var h = UIKit.Horizontal((RectTransform)cell, 10, TextAnchor.MiddleRight);
            h.childForceExpandWidth = false;
            string id = q.questionId;
            if (qAdmin)
            {
                Btn(cell, "EDIT", () => StartEdit(id), UIKit.HudVariant.Primary, 130, 46, 18).name = "Edit_" + id;
                Btn(cell, q.isActive ? "TURN OFF" : "TURN ON", () => ToggleQuestion(id), UIKit.HudVariant.Secondary, 170, 46, 18).name = "Toggle_" + id;
            }
            else Btn(cell, "VIEW / REQUEST", () => StartEdit(id), UIKit.HudVariant.Primary, 260, 46, 18).name = "Edit_" + id;
        }
    }

    void FilterButton(Transform parent, string label, string filter)
    {
        bool on = qFilter == filter;
        var b = Btn(parent, label, () => { qFilter = filter; RenderKeepScroll(); }, on ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary,
                    Mathf.Max(90f, 50f + label.Length * 16f), 56, 19);
        b.name = "Filter_" + filter;
    }

    static string Snip(string s, int max)
    {
        s = (s ?? "").Replace("\n", " ").Trim();
        return s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "…";
    }

    void StartEdit(string id)
    {
        var q = qbank.data.questions.FirstOrDefault(x => x != null && x.questionId == id);
        if (q == null) return;
        draft = QuestionEditing.Copy(q);
        draftIsNew = false;
        draftIssues = null; confirmDeleteQuestion = false; draftReason = ""; qHistory = null;
        Render();
    }

    /// <summary>This teacher's pending request for a question, if any (one at a time per question).</summary>
    QuestionBankService.Request PendingRequestFor(string questionId) =>
        myRequests?.FirstOrDefault(r => r.status == "pending" && r.question_id == questionId);

    void StartNewQuestion(bool multipleChoice)
    {
        string level = qFilter.StartsWith("level_") ? qFilter : qFilter == "reserve" ? "" : QuestionLevels().Select(l => l.id).FirstOrDefault() ?? "";
        // Ids in this teacher's own requests are taken too (the server also refuses ids other teachers asked for).
        draft = QuestionEditing.NewQuestion(qbank.data, level, multipleChoice, (myRequests ?? new List<QuestionBankService.Request>()).Select(r => r.question_id));
        draftIsNew = true;
        draftIssues = null; confirmDeleteQuestion = false; draftReason = ""; qHistory = null;
        Render();
    }

    async void ToggleQuestion(string id)
    {
        var q = qbank.data.questions.FirstOrDefault(x => x != null && x.questionId == id);
        if (q == null || qSaving || !qAdmin) return;
        if (!q.isActive)
        {
            // Turning one on: it must pass the same checks as a save.
            var copy = QuestionEditing.Copy(q);
            copy.isActive = true;
            var problems = QuestionEditing.Check(copy, qbank.data, false).Where(i => i.IsError).ToList();
            if (problems.Count > 0) { SetStatus($"{id} can't be turned on yet: {problems[0].message}. Open EDIT to fix it.", true); return; }
        }
        int v = version;
        qSaving = true;
        try
        {
            await QuestionBankService.SetActiveAsync(id, !q.isActive);
            if (!Current(v)) return;
            string msg = $"{id} is {(q.isActive ? "off: students won't see it" : "on")}." + LevelCountNote(q.levelId, q.isActive ? -1 : +1);
            LoadQuestionBank(() => SetStatus(msg, false));
        }
        catch (Exception e) { if (Current(v)) SetStatus(QuestionBankService.Message(e), true); }
        finally { qSaving = false; }
    }

    string LevelCountNote(string levelId, int change)
    {
        if (string.IsNullOrEmpty(levelId)) return "";
        int n = qbank.data.questions.Count(q => q != null && q.isActive && q.levelId == levelId) + change;
        return $" {LevelName(levelId)} now has {n} active questions; its pass score was recalculated. " +
               "Students who are mid-level finish with the old set.";
    }

    // ---------- Form ----------

    void QuestionForm()
    {
        var q = draft;
        string title = qAdmin ? (draftIsNew ? $"New question  ·  {q.questionId}" : $"Edit question  ·  {q.questionId}")
                              : (draftIsNew ? $"Request a new question  ·  {q.questionId}" : $"Request a change  ·  {q.questionId}");
        var card = UICharts.Card(content, title, qAdmin
            ? "Nothing changes for students until you press SAVE. Text may use <b>bold</b> and <i>italic</i> tags."
            : "Make your changes, give a reason, and press SUBMIT REQUEST. The admin reviews it; students only see it once it's approved. Text may use <b>bold</b> and <i>italic</i> tags.");
        var pending = qAdmin ? null : PendingRequestFor(q.questionId);
        if (pending != null)
            Line(card, $"<color={UIKit.HexWarning}>You already have a pending {pending.request_type.ToUpperInvariant()} request for {Esc(q.questionId)} " +
                       $"(sent {M.ShortDate(pending.created_at)}). Withdraw it in MY REQUESTS to send a different one.</color>", Small, UIKit.TextPrimary).name = "PendingNote";

        // Where it plays
        var levels = QuestionLevels().Select(l => l.id).Concat(new[] { "" }).ToList();
        CycleRow(card, "LEVEL", Esc(LevelName(q.levelId)), step => { q.levelId = levels[Cycle(levels.IndexOf(q.levelId ?? ""), step, levels.Count)]; RenderKeepScroll(); }, "Level");
        var statusRow = RowH(card, 56);
        Label(statusRow, "STATUS", QLabel, Small, Muted, true);
        Btn(statusRow, "ON", () => { q.isActive = true; RenderKeepScroll(); }, q.isActive ? UIKit.HudVariant.Safe : UIKit.HudVariant.Secondary, 120, 52, 19).name = "ActiveOn";
        Btn(statusRow, "OFF", () => { q.isActive = false; RenderKeepScroll(); }, !q.isActive ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, 120, 52, 19).name = "ActiveOff";
        Label(statusRow, "   POSITION", 170, Small, Muted, true);
        var pos = Input(statusRow, "OrderInput", "10", 6, 130);
        pos.contentType = TMP_InputField.ContentType.IntegerNumber;
        pos.text = q.order.ToString();
        pos.onValueChanged.AddListener(s => { if (int.TryParse(s, out int o)) q.order = o; });
        Line(statusRow, "lower = earlier (before the shuffle)", 17f, Muted);

        var cats = (qbank.data.categories ?? new string[0]).ToList();
        if (!string.IsNullOrEmpty(q.category) && !cats.Contains(q.category)) cats.Add(q.category);
        if (cats.Count > 0)
            CycleRow(card, "CATEGORY", Esc(string.IsNullOrEmpty(q.category) ? "(none)" : q.category),
                     step => { q.category = cats[Cycle(Mathf.Max(0, cats.IndexOf(q.category)), step, cats.Count)]; RenderKeepScroll(); }, "Category");
        var diffs = QuestionBankValidator.Difficulties;
        CycleRow(card, "DIFFICULTY", q.difficulty ?? "", step => { q.difficulty = diffs[Cycle(Mathf.Max(0, Array.IndexOf(diffs, q.difficulty)), step, diffs.Length)]; RenderKeepScroll(); }, "Difficulty");

        // Answer style
        var style = RowH(card, 56);
        Label(style, "ANSWER STYLE", QLabel, Small, Muted, true);
        Btn(style, "SCAM OR LEGITIMATE", () => { QuestionEditing.SetMultipleChoice(q, false); RenderKeepScroll(); }, !q.HasChoices ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary, 330, 52, 19).name = "StyleVerdict";
        Btn(style, "MULTIPLE CHOICE", () => { QuestionEditing.SetMultipleChoice(q, true); RenderKeepScroll(); }, q.HasChoices ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary, 290, 52, 19).name = "StyleChoice";
        if (!q.HasChoices)
        {
            var types = QuestionEditing.MessageTypes.Where(t => t != "Question").ToArray();
            CycleRow(card, "ARRIVES AS", MessageTypeName(q.messageType),
                     step => { q.messageType = types[Cycle(Mathf.Max(0, Array.IndexOf(types, q.messageType)), step, types.Length)]; RenderKeepScroll(); }, "MessageType");
        }
        else Line(card, "Multiple-choice questions open in the case-question window; the A-D order is shuffled each attempt.", 17f, Muted);

        // The message
        Section(card, "THE MESSAGE");
        TextRow(card, "SENDER", q.sender, false, s => q.sender = s, "e.g. +63 917 555 0142 or Campus IT Helpdesk");
        if (MessageChannels.OnComputer(QuestionMapper.Parse(q.messageType, MessageChannel.SMS)) || q.HasChoices)
            TextRow(card, "SUBJECT", q.subject, false, s => q.subject = s, q.HasChoices ? "shown as the window title, e.g. Multiple Choice" : "required for emails and web pages");
        TextRow(card, q.HasChoices ? "QUESTION" : "MESSAGE", q.message, true, s => q.message = s, "what the player reads");
        if (!q.HasChoices) TextRow(card, "LINK", q.link, false, s => q.link = s, "optional; use .example addresses only - never opened");
        TextRow(card, "BACKGROUND", q.scenarioContext, false, s => q.scenarioContext = s, "optional: what the player already knows (case notes)");

        // The answer
        Section(card, "THE ANSWER");
        if (q.HasChoices)
        {
            for (int i = 0; i < QuestionEditing.MaxChoices; i++)
            {
                int idx = i;
                if (q.choices.Length <= idx) q.choices = q.choices.Concat(Enumerable.Repeat("", idx + 1 - q.choices.Length)).ToArray();
                var row = RowH(card, 60);
                Label(row, "CHOICE " + (char)('A' + idx), QLabel, Small, Muted, true);
                var f = Input(row, "Choice" + idx, idx < 2 ? "required" : "optional (leave empty for 2-3 choices)", 300, -1);
                f.text = q.choices[idx] ?? "";
                f.onValueChanged.AddListener(s => q.choices[idx] = s);
                f.GetComponent<LayoutElement>().flexibleWidth = 1f;
                bool right = q.correctChoice == idx;
                Btn(row, right ? "✓ CORRECT" : "MAKE CORRECT", () => { q.correctChoice = idx; RenderKeepScroll(); },
                    right ? UIKit.HudVariant.Safe : UIKit.HudVariant.Secondary, 240, 54, 18).name = "Correct" + idx;
            }
            Line(card, "Type the choices without A / B / C / D - letters are added on screen after shuffling.", 17f, Muted);
        }
        else
        {
            var row = RowH(card, 56);
            Label(row, "CORRECT ANSWER", QLabel, Small, Muted, true);
            Btn(row, "SCAM", () => { q.isScam = true; RenderKeepScroll(); }, q.isScam ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, 200, 52, 19).name = "AnswerScam";
            Btn(row, "LEGITIMATE", () => { q.isScam = false; RenderKeepScroll(); }, !q.isScam ? UIKit.HudVariant.Safe : UIKit.HudVariant.Secondary, 240, 52, 19).name = "AnswerLegit";
        }
        TextRow(card, "EXPLANATION", q.explanation, true, s => q.explanation = s, "why the answer is right (shown after answering)");
        TextRow(card, "FEEDBACK", q.feedback, false, s => q.feedback = s, "one short line above the explanation");
        TextRow(card, "HINT", q.hint, false, s => q.hint = s, "points to what matters - never gives the answer (max 140)");

        // What the editor doesn't change
        int clues = q.clues?.Count ?? 0;
        bool activity = q.activity != null && !string.IsNullOrEmpty(q.activity.kind);
        if (clues > 0 || activity)
            Line(card, $"Kept as they are: {(clues > 0 ? $"{clues} clickable clue{(clues == 1 ? "" : "s")}" : "")}" +
                       $"{(clues > 0 && activity ? " and " : "")}{(activity ? $"the {q.activity.kind} activity" : "")}. " +
                       "If you edit words a clue points at, CHECK offers to fix it. To edit clues themselves, ask the content admin.", 17f, Muted);

        // Problems
        if (draftIssues != null)
        {
            var errors = draftIssues.Where(i => i.IsError).ToList();
            var warnings = draftIssues.Where(i => !i.IsError).ToList();
            if (errors.Count == 0 && warnings.Count == 0)
                Line(card, $"<color={UIKit.HexSafe}>✓ {(qAdmin ? "Ready to save" : "Ready to send")}: the game will play this question.</color>", Body, UIKit.TextPrimary);
            foreach (var e in errors) Line(card, $"<color={Hex(UIKit.Danger)}>✗ {Esc(e.message)}</color>", Small, UIKit.TextPrimary).name = "Error";
            foreach (var w in warnings) Line(card, $"<color={UIKit.HexWarning}>! {Esc(w.message)}</color>", Small, UIKit.TextPrimary).name = "Warning";
        }

        // Teachers: why (required by the server for every request)
        if (!qAdmin)
        {
            Section(card, "YOUR REQUEST");
            TextRow(card, "REASON", draftReason, true, s => draftReason = s, "why this change is needed - the admin reads this (required)");
        }

        // Actions
        var actions = RowH(card, 62);
        if (qAdmin) Btn(actions, qSaving ? "SAVING…" : "SAVE", SaveDraft, UIKit.HudVariant.Primary, 220).name = "SaveQuestion";
        else Btn(actions, qSaving ? "SENDING…" : "SUBMIT REQUEST", () => SubmitRequest(draftIsNew ? "add" : "edit"), UIKit.HudVariant.Primary, 300).name = "SubmitRequest";
        Btn(actions, "CHECK", () => { RunCheck(); RenderKeepScroll(); }, UIKit.HudVariant.Secondary, 200).name = "CheckQuestion";
        if (draftIssues != null && QuestionEditing.HasBrokenClues(draftIssues))
            Btn(actions, "FIX CLUES", () => { int n = QuestionEditing.RepairClues(draft); RunCheck(); RenderKeepScroll(); SetStatus($"{n} clue setting{(n == 1 ? "" : "s")} adjusted to the new text.", false); },
                UIKit.HudVariant.Secondary, 220).name = "FixClues";
        Btn(actions, "CANCEL", () => CloseQuestionForm(), UIKit.HudVariant.Secondary, 200).name = "CancelQuestion";
        if (!draftIsNew)
        {
            var spacer = UIKit.Rect("Spacer", actions);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (qAdmin)
            {
                Btn(actions, qHistory == null ? "HISTORY" : "HIDE HISTORY", ToggleHistory, UIKit.HudVariant.Secondary, 240).name = "History";
                Btn(actions, confirmDeleteQuestion ? "CONFIRM DELETE" : "DELETE", DeleteDraft,
                    confirmDeleteQuestion ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, confirmDeleteQuestion ? 300 : 200).name = "DeleteQuestion";
            }
            else Btn(actions, confirmDeleteQuestion ? "CONFIRM DELETE REQUEST" : "REQUEST DELETE", () =>
                {
                    if (!confirmDeleteQuestion) { confirmDeleteQuestion = true; RenderKeepScroll(); SetStatus("Give a reason above, then press CONFIRM DELETE REQUEST. The admin decides; the question stays until then.", false); return; }
                    SubmitRequest("delete");
                }, confirmDeleteQuestion ? UIKit.HudVariant.Danger : UIKit.HudVariant.Secondary, confirmDeleteQuestion ? 420 : 300).name = "RequestDelete";
        }

        if (qAdmin && qHistory != null) HistoryCard(q);
    }

    // ---------- Teacher: send the draft as a request ----------

    async void SubmitRequest(string type)
    {
        if (draft == null || qSaving) return;
        if ((draftReason ?? "").Trim().Length < 5) { SetStatus("Write a short reason for the admin first (at least 5 characters).", true); return; }
        if (type != "delete")
        {
            RunCheck();
            if (draftIssues.Any(i => i.IsError)) { RenderKeepScroll(); SetStatus("Fix the problems marked ✗ first.", true); return; }
            if (!draftIsNew)
            {
                var old = qbank.data.questions.FirstOrDefault(x => x != null && x.questionId == draft.questionId);
                if (old != null && QuestionEditing.Diff(old, draft).Count == 0) { SetStatus("Nothing has changed yet - edit the question first.", true); return; }
            }
        }
        var q = draft;
        int v = version;
        qSaving = true;
        RenderKeepScroll();
        try
        {
            var res = await QuestionBankService.SubmitRequestAsync(type, q, q.questionId, draftReason.Trim());
            if (!Current(v)) return;
            if (!res.ok) { SetStatus(res.error, true); return; }
            draft = null; draftIssues = null; draftReason = ""; confirmDeleteQuestion = false;
            string what = type == "add" ? "add" : type == "delete" ? "delete" : "change";
            LoadQuestionBank(() => SetStatus($"Request sent: {what} {q.questionId}. The admin will review it; follow it in MY REQUESTS. Students see no change until it's approved.", false));
        }
        catch (Exception e) { if (Current(v)) SetStatus(QuestionBankService.Message(e), true); }
        finally { qSaving = false; if (Current(v) && draft != null) RenderKeepScroll(); }
    }

    // ---------- Admin: history ----------

    async void ToggleHistory()
    {
        if (qHistory != null) { qHistory = null; RenderKeepScroll(); return; }
        if (draft == null) return;
        int v = version;
        string id = draft.questionId;
        try
        {
            var h = await QuestionBankService.HistoryAsync(id);
            if (!Current(v) || draft == null || draft.questionId != id) return;
            qHistory = h;
            RenderKeepScroll();
        }
        catch (Exception e) { if (Current(v)) SetStatus(QuestionBankService.Message(e), true); }
    }

    void HistoryCard(QuestionRecord q)
    {
        var card = UICharts.Card(content, $"History  ·  {q.questionId}", "Every earlier version is kept. Each line says what that change replaced.");
        if (qHistory.Count == 0) { UICharts.Empty(card, "No changes yet: this is the original version."); return; }
        var saved = qbank.data.questions.FirstOrDefault(x => x != null && x.questionId == q.questionId);
        QuestionRecord newer = saved;   // compare each old version with the one that replaced it
        foreach (var h in qHistory)
        {
            var older = h.old_row != null ? h.old_row.ToRecord(q.questionId) : null;
            string who = string.IsNullOrEmpty(h.changed_by) ? "someone" : Esc(h.changed_by);
            string when = M.ShortDate(h.changed_at);
            string what = h.operation == "DELETE" ? "deleted it" :
                          older != null && newer != null ? string.Join(", ", QuestionEditing.Diff(older, newer).Select(d => d.field)) : "changed it";
            Line(card, $"<b>{when}</b>  ·  {who}  ·  {(h.operation == "DELETE" ? what : "changed: " + (string.IsNullOrEmpty(what) ? "nothing visible" : what))}", Small, UIKit.TextSecondary).name = "HistoryLine";
            if (older != null) newer = older;
        }
    }

    static string MessageTypeName(string t) =>
        t == "SMS" ? "Text message (phone)" : t == "Chat" ? "Chat message (phone)" : t == "Email" ? "Email (computer)"
        : t == "Website" ? "Web page (computer)" : t == "Case" ? "Final case (case terminal)" : t ?? "";

    void Section(Transform parent, string title)
    {
        var t = Line(parent, title, 17f, UIKit.Accent, true);
        t.characterSpacing = 3f;
    }

    void CycleRow(Transform parent, string label, string value, Action<int> step, string name)
    {
        var row = RowH(parent, 56);
        Label(row, label, QLabel, Small, Muted, true);
        Btn(row, "◀", () => step(-1), UIKit.HudVariant.Secondary, 70, 52, 20).name = name + "Prev";
        Label(row, value, 560, Body, UIKit.TextPrimary).alignment = TextAlignmentOptions.Center;
        Btn(row, "▶", () => step(+1), UIKit.HudVariant.Secondary, 70, 52, 20).name = name + "Next";
    }

    void TextRow(Transform parent, string label, string value, bool multiline, Action<string> set, string placeholder)
    {
        var row = RowH(parent, multiline ? 170 : 60);
        var l = Label(row, label, QLabel, Small, Muted, true);
        if (multiline) l.alignment = TextAlignmentOptions.TopLeft;
        var f = Input(row, label.Replace(" ", "") + "Input", placeholder, 4000, -1);
        f.GetComponent<LayoutElement>().flexibleWidth = 1f;
        if (multiline)
        {
            f.lineType = TMP_InputField.LineType.MultiLineNewline;
            UIKit.Size(f, -1, 165);
            f.GetComponent<LayoutElement>().flexibleWidth = 1f;
            f.textComponent.alignment = TextAlignmentOptions.TopLeft;
            if (f.placeholder is TMP_Text ph) ph.alignment = TextAlignmentOptions.TopLeft;
        }
        f.text = value ?? "";
        f.onValueChanged.AddListener(s => set(s));
    }

    void RunCheck()
    {
        if (draft == null) return;
        QuestionEditing.TidyChoices(draft);
        draftIssues = QuestionEditing.Check(draft, qbank.data, draftIsNew);
    }

    async void SaveDraft()
    {
        if (draft == null || qSaving) return;
        if (!qAdmin) { SubmitRequest(draftIsNew ? "add" : "edit"); return; }   // only admins write directly
        RunCheck();
        if (draftIssues.Any(i => i.IsError)) { RenderKeepScroll(); SetStatus("Fix the problems marked ✗ first.", true); return; }
        var q = draft;
        var old = qbank.data.questions.FirstOrDefault(x => x != null && x.questionId == q.questionId);
        int change = (q.isActive && !string.IsNullOrEmpty(q.levelId) ? 1 : 0) -
                     (old != null && old.isActive && old.levelId == q.levelId ? 1 : 0);
        string note = change != 0 ? LevelCountNote(q.levelId, change) : "";
        if (old != null && old.isActive && !string.IsNullOrEmpty(old.levelId) && old.levelId != q.levelId)
            note += $" It left {LevelName(old.levelId)}.";
        int v = version;
        qSaving = true;
        RenderKeepScroll();
        try
        {
            await QuestionBankService.SaveAsync(q, draftIsNew);
            if (!Current(v)) return;
            string msg = $"Saved {q.questionId}. Students get it the next time they load a level." + note;
            draft = null; draftIssues = null;
            if (!string.IsNullOrEmpty(q.levelId)) qFilter = q.levelId;
            LoadQuestionBank(() => SetStatus(msg, false));
        }
        catch (Exception e) { if (Current(v)) { SetStatus(QuestionBankService.Message(e), true); } }
        finally { qSaving = false; if (Current(v) && draft != null) RenderKeepScroll(); }
    }

    async void DeleteDraft()
    {
        if (draft == null || qSaving) return;
        if (!confirmDeleteQuestion) { confirmDeleteQuestion = true; RenderKeepScroll(); SetStatus("Deleting can't be undone from here (the history table keeps a copy). Turning it OFF is usually better.", false); return; }
        string id = draft.questionId;
        int v = version;
        qSaving = true;
        try
        {
            await QuestionBankService.DeleteAsync(id);
            if (!Current(v)) return;
            draft = null; draftIssues = null; confirmDeleteQuestion = false;
            LoadQuestionBank(() => SetStatus($"{id} was deleted.", false));
        }
        catch (Exception e) { if (Current(v)) { confirmDeleteQuestion = false; SetStatus(QuestionBankService.Message(e), true); RenderKeepScroll(); } }
        finally { qSaving = false; }
    }

#if UNITY_EDITOR
    /// <summary>Editor only (layout checks): shows the QUESTIONS tab with a bank passed in instead of the server's.</summary>
    public void EditorPreviewQuestions(QuestionBankFile bank, bool admin, string editId = null, List<QuestionBankService.Request> requests = null)
    {
        qbank = new LoadedQuestionBank { data = bank, source = ContentSource.Bundled, issues = new List<ContentIssue>() };
        qAdmin = admin;
        portalAdmin = admin;
        myRequests = admin ? new List<QuestionBankService.Request>() : requests ?? new List<QuestionBankService.Request>();
        version++;
        previewTeacher = true;
        tab = Tab.Questions;
        draft = null;
        if (!IsOpen) { dim.SetActive(true); panel.gameObject.SetActive(true); IsOpen = true; }
        if (!string.IsNullOrEmpty(editId)) { var q = bank.questions.First(x => x.questionId == editId); qFilter = string.IsNullOrEmpty(q.levelId) ? "reserve" : q.levelId; StartEdit(editId); }
        else Render();
    }

    /// <summary>Editor only: the question being edited (tests drive the form through it).</summary>
    public QuestionRecord EditorDraft => draft;
#endif
}
