using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// The editing rules behind the teacher's question editor (no UI, no network, so they are unit-tested):
/// new-question templates, switching the answer style, keeping clues valid after a text edit, picking a free id,
/// and turning a record into the row Supabase stores (management columns + the record as jsonb).
/// </summary>
public static class QuestionEditing
{
    public const int MaxChoices = 4;
    /// <summary>Where a question can be delivered, in the order the editor cycles through them.</summary>
    public static readonly string[] MessageTypes = { "SMS", "Chat", "Email", "Website", "Question" };

    public static QuestionRecord Copy(QuestionRecord q) => JsonUtility.FromJson<QuestionRecord>(JsonUtility.ToJson(q));

    /// <summary>A new, valid-shaped question for a level (the teacher fills in the text).</summary>
    public static QuestionRecord NewQuestion(QuestionBankFile bank, string levelId, bool multipleChoice, IEnumerable<string> alsoTaken = null)
    {
        var q = new QuestionRecord
        {
            questionId = NextFreeId(bank, alsoTaken),
            levelId = levelId ?? "",
            order = NextOrder(bank, levelId),
            isActive = true,
            category = bank?.categories != null && bank.categories.Length > 0 ? bank.categories[0] : "",
            difficulty = "Easy",
            questionType = multipleChoice ? "Multiple Choice" : "Scam or Legitimate",
            messageType = multipleChoice ? "Question" : "SMS",
            sender = multipleChoice ? "Case Review" : "",
            subject = multipleChoice ? "Multiple Choice" : "",
            isScam = true,
            choices = multipleChoice ? new[] { "", "", "", "" } : new string[0],
            notes = "Added in the teacher question editor.",
        };
        return q;
    }

    /// <summary>T01, T02... (teacher-made questions), the first one not used in the bank.</summary>
    public static string NextFreeId(QuestionBankFile bank, IEnumerable<string> alsoTaken = null)
    {
        var used = new HashSet<string>((bank?.questions ?? new List<QuestionRecord>()).Where(x => x != null).Select(x => x.questionId), StringComparer.OrdinalIgnoreCase);
        if (alsoTaken != null) used.UnionWith(alsoTaken.Where(x => !string.IsNullOrEmpty(x)));
        for (int i = 1; i < 10000; i++)
        {
            string id = "T" + i.ToString("00");
            if (!used.Contains(id)) return id;
        }
        return "T" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    /// <summary>After the level's last question (orders go up in tens).</summary>
    public static int NextOrder(QuestionBankFile bank, string levelId)
    {
        var inLevel = (bank?.questions ?? new List<QuestionRecord>()).Where(x => x != null && x.levelId == (levelId ?? "")).ToList();
        return inLevel.Count == 0 ? 10 : (inLevel.Max(x => x.order) / 10 + 1) * 10;
    }

    /// <summary>Switches between SCAM / LEGITIMATE and multiple choice, keeping what still makes sense.</summary>
    public static void SetMultipleChoice(QuestionRecord q, bool multipleChoice)
    {
        if (multipleChoice == q.HasChoices) return;
        if (multipleChoice)
        {
            q.choices = new[] { "", "", "", "" };
            q.correctChoice = 0;
            q.messageType = "Question";
            if (string.IsNullOrEmpty(q.questionType) || q.questionType.StartsWith("Scam or Legitimate")) q.questionType = "Multiple Choice";
        }
        else
        {
            q.choices = new string[0];
            q.correctChoice = 0;
            if (q.messageType == "Question") q.messageType = "SMS";
            if (q.activity != null && !string.IsNullOrEmpty(q.activity.kind)) q.activity = new ActivityRecord();   // activities need choices
            if (string.IsNullOrEmpty(q.questionType) || !q.questionType.StartsWith("Scam or Legitimate")) q.questionType = "Scam or Legitimate";
        }
    }

    /// <summary>
    /// Choices as typed: blank trailing ones are dropped (2-4 kept). The correct answer follows its text, so removing
    /// an empty box above it never moves the right answer to another choice.
    /// </summary>
    public static void TidyChoices(QuestionRecord q)
    {
        if (q.choices == null || q.choices.Length == 0) return;
        string right = q.correctChoice >= 0 && q.correctChoice < q.choices.Length ? q.choices[q.correctChoice] : null;
        var kept = q.choices.Select(c => (c ?? "").Trim()).Where(c => c.Length > 0).ToList();
        int idx = right != null ? kept.IndexOf(right.Trim()) : -1;
        if (kept.Count < 2) { while (kept.Count < 2) kept.Add(""); }   // still a choice question; the validator says what's missing
        q.choices = kept.ToArray();
        q.correctChoice = idx >= 0 ? idx : Mathf.Clamp(q.correctChoice, 0, q.choices.Length - 1);
        if (q.activity != null && q.activity.outcomes != null && q.activity.outcomes.Length > 0 && q.activity.outcomes.Length != q.choices.Length)
            q.activity.outcomes = new string[0];   // outcomes are per choice; they no longer line up
    }

    /// <summary>
    /// Clues whose words are no longer in their field (after the teacher edited the text). Fixing: the clue is kept but
    /// points at the whole field when the field still exists, otherwise it is removed. Returns how many were changed.
    /// </summary>
    public static int RepairClues(QuestionRecord q)
    {
        if (q.clues == null || q.clues.Count == 0) return 0;
        var message = QuestionMapper.ToMessage(q, false);
        int changed = 0;
        for (int i = q.clues.Count - 1; i >= 0; i--)
        {
            var c = q.clues[i];
            if (c == null) { q.clues.RemoveAt(i); changed++; continue; }
            var field = QuestionMapper.Parse(c.field, InspectField.Body);
            string text = InvestigationSession.FieldText(message, field, c.part);
            if (string.IsNullOrEmpty(text)) { q.clues.RemoveAt(i); changed++; continue; }
            if (!string.IsNullOrEmpty(c.match) && !text.Contains(c.match)) { c.match = ""; changed++; }
        }
        // Indicators nobody can discover any more are dropped too (they would never show).
        if (q.indicators != null)
            changed += q.indicators.RemoveAll(e => e != null && !q.clues.Any(c => c.indicatorId == e.id));
        if (q.activity != null && !string.IsNullOrEmpty(q.activity.kind))
            q.activity.minClues = Mathf.Min(q.activity.minClues, (q.indicators ?? new List<IndicatorRecord>()).Count(e => e.isKeyClue));
        return changed;
    }

    /// <summary>Clue problems a text edit can cause (so the editor can offer to repair them).</summary>
    public static bool HasBrokenClues(IEnumerable<ContentIssue> issues) =>
        issues.Any(i => i.IsError && (i.message.Contains("is not in the") || i.message.Contains("field is empty") || i.message.Contains("is missing or empty")));

    /// <summary>Errors and warnings for the editor, exactly as the game will judge the question.</summary>
    public static List<ContentIssue> Check(QuestionRecord q, QuestionBankFile bank, bool isNew)
    {
        var level = bank?.levels?.FirstOrDefault(l => l != null && l.levelId == q.levelId);
        var issues = QuestionBankValidator.ValidateQuestion(q, level != null && level.tutorial);
        if (isNew && bank?.questions != null && bank.questions.Any(x => x != null && string.Equals(x.questionId, q.questionId, StringComparison.OrdinalIgnoreCase)))
            issues.Add(new ContentIssue(ContentSeverity.Error, q.questionId, "this id is already used"));
        if (q.isActive && !string.IsNullOrEmpty(q.levelId))
        {
            int count = (bank?.questions ?? new List<QuestionRecord>()).Count(x => x != null && x.isActive && x.levelId == q.levelId && x.questionId != q.questionId) + 1;
            if (count > LevelContentRules.MaxQuestionsPerRoom)
                issues.Add(new ContentIssue(ContentSeverity.Warning, q.levelId, $"this level would have {count} active questions (a room is designed for up to {LevelContentRules.MaxQuestionsPerRoom})"));
        }
        return issues;
    }

    // ---------- Comparing versions (admin review, history) ----------

    /// <summary>What a reviewer reads for each field, in this order. Clues etc. are compared as one item.</summary>
    public static List<(string field, string value)> Describe(QuestionRecord q)
    {
        var list = new List<(string, string)>();
        if (q == null) return list;
        list.Add(("Level", string.IsNullOrEmpty(q.levelId) ? "Reserve (no level)" : q.levelId.Replace("level_", "Level ")));
        list.Add(("Status", q.isActive ? "On" : "Off"));
        list.Add(("Position", q.order.ToString()));
        list.Add(("Category", q.category ?? ""));
        list.Add(("Difficulty", q.difficulty ?? ""));
        list.Add(("Answer style", q.HasChoices ? "Multiple choice" : "Scam or legitimate"));
        list.Add(("Arrives as", q.messageType ?? ""));
        list.Add(("Sender", q.sender ?? ""));
        list.Add(("Subject", q.subject ?? ""));
        list.Add(("Message", q.message ?? ""));
        list.Add(("Link", q.link ?? ""));
        list.Add(("Background", q.scenarioContext ?? ""));
        list.Add(("Choices", q.HasChoices ? string.Join("\n", q.choices.Select((c, i) => $"{(char)('A' + i)}. {c}")) : ""));
        list.Add(("Correct answer", q.HasChoices
            ? (q.correctChoice >= 0 && q.correctChoice < q.choices.Length ? $"{(char)('A' + q.correctChoice)}. {q.choices[q.correctChoice]}" : "(none)")
            : q.isScam ? "SCAM" : "LEGITIMATE"));
        list.Add(("Explanation", q.explanation ?? ""));
        list.Add(("Feedback", q.feedback ?? ""));
        list.Add(("Hint", q.hint ?? ""));
        list.Add(("Clues and activity", $"{q.clues?.Count ?? 0} clues, {q.indicators?.Count ?? 0} indicators" +
                                        (q.activity != null && !string.IsNullOrEmpty(q.activity.kind) ? $", {q.activity.kind} activity" : "")));
        return list;
    }

    [Serializable] class Extras { public List<ClueRecord> clues; public List<IndicatorRecord> indicators; public ActivityRecord activity; public ConsequenceRecord consequence; }

    static string ExtrasJson(QuestionRecord q) =>
        q == null ? "" : JsonUtility.ToJson(new Extras { clues = q.clues, indicators = q.indicators, activity = q.activity, consequence = q.consequence });

    /// <summary>The fields that differ between two versions: (field, before, after).</summary>
    public static List<(string field, string before, string after)> Diff(QuestionRecord before, QuestionRecord after)
    {
        var a = Describe(before);
        var b = Describe(after);
        var diff = new List<(string, string, string)>();
        for (int i = 0; i < Math.Max(a.Count, b.Count); i++)
        {
            string field = i < b.Count ? b[i].field : a[i].field;
            string x = i < a.Count ? a[i].value : "", y = i < b.Count ? b[i].value : "";
            if (x != y) diff.Add((field, x, y));
            else if (field == "Clues and activity" && ExtrasJson(before) != ExtrasJson(after))
                diff.Add((field, x, y + " (details changed)"));   // same counts, different wording / settings
        }
        return diff;
    }

    // ---------- Supabase row ----------

    static readonly string[] ColumnFields = { "questionId", "levelId", "order", "isActive", "category", "difficulty", "questionType", "messageType" };

    /// <summary>
    /// The public.question_bank row for a record: management columns plus the record itself as content (the columns
    /// win when the game downloads it, so the copies inside content are left out).
    /// </summary>
    public static string ToRowJson(QuestionRecord q, bool includeId = true)
    {
        var sb = new StringBuilder("{");
        if (includeId) sb.Append("\"question_id\":").Append(Str(q.questionId)).Append(',');
        sb.Append("\"level_id\":").Append(string.IsNullOrEmpty(q.levelId) ? "null" : Str(q.levelId)).Append(',');
        sb.Append("\"sort_order\":").Append(q.order).Append(',');
        sb.Append("\"is_active\":").Append(q.isActive ? "true" : "false").Append(',');
        sb.Append("\"category\":").Append(Str(q.category ?? "")).Append(',');
        sb.Append("\"difficulty\":").Append(Str(q.difficulty ?? "")).Append(',');
        sb.Append("\"question_type\":").Append(Str(q.questionType ?? "")).Append(',');
        sb.Append("\"message_type\":").Append(Str(q.messageType ?? "SMS")).Append(',');
        sb.Append("\"content\":").Append(ContentJson(q));
        return sb.Append('}').ToString();
    }

    /// <summary>The record without its column fields (they live in the row's columns).</summary>
    public static string ContentJson(QuestionRecord q)
    {
        string json = JsonUtility.ToJson(q);
        foreach (var f in ColumnFields)
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"" + f + "\":(\"(?:[^\"\\\\]|\\\\.)*\"|-?\\d+|true|false),?", "");
        return json.Replace(",}", "}");
    }

    /// <summary>A JSON string literal.</summary>
    public static string Str(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char ch in s ?? "")
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4")); else sb.Append(ch);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
