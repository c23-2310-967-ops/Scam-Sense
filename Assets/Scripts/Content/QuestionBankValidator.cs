using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Checks question-bank data before the game uses it. Never throws.
///   * An Error on a question means that question is left out (the rest of its level still plays).
///   * An Error on the file (where = "") means the whole file is rejected and the next source is used.
///   * Warnings never block anything; they are logged and shown by the editor's Validate menu.
/// </summary>
public static class QuestionBankValidator
{
    public static readonly string[] Difficulties = { "Easy", "Medium", "Hard" };
    static readonly Regex IdPattern = new Regex(@"^[A-Za-z0-9_-]{1,40}$");
    static readonly Regex LetterLabel = new Regex(@"^\s*[A-D][.)]\s");
    const int MaxHintLength = 140;

    /// <summary>File-level checks plus every question and level. <paramref name="knownLevelIds"/> null = don't check level ids.</summary>
    public static List<ContentIssue> Validate(QuestionBankFile bank, ICollection<string> knownLevelIds = null)
    {
        var issues = new List<ContentIssue>();
        if (bank == null) { issues.Add(Err("", "the file is empty or is not valid JSON")); return issues; }
        if (bank.schemaVersion <= 0) issues.Add(Err("", "schemaVersion is missing (expected " + QuestionBankFile.CurrentSchemaVersion + ")"));
        else if (bank.schemaVersion > QuestionBankFile.CurrentSchemaVersion)
            issues.Add(Err("", $"schemaVersion {bank.schemaVersion} is newer than this game understands ({QuestionBankFile.CurrentSchemaVersion}); update the game"));
        if (bank.questions == null) { issues.Add(Err("", "the file has no \"questions\" list")); return issues; }
        if (bank.levels == null) { issues.Add(Err("", "the file has no \"levels\" list")); return issues; }

        var levelIds = new HashSet<string>();
        foreach (var l in bank.levels)
        {
            if (l == null || string.IsNullOrWhiteSpace(l.levelId)) { issues.Add(Err("", "a level has no levelId")); continue; }
            if (!levelIds.Add(l.levelId)) issues.Add(Err("", $"level '{l.levelId}' is listed twice"));
            if (knownLevelIds != null && !knownLevelIds.Contains(l.levelId))
                issues.Add(Warn(l.levelId, "no level in the game has this levelId, so its texts are never used"));
        }

        var seen = new HashSet<string>();
        var categories = new HashSet<string>(bank.categories ?? new string[0], StringComparer.OrdinalIgnoreCase);
        foreach (var q in bank.questions)
        {
            if (q == null) { issues.Add(Err("", "an empty entry in \"questions\"")); continue; }
            if (!string.IsNullOrEmpty(q.questionId) && !seen.Add(q.questionId))
            {
                issues.Add(Err(q.questionId, "questionId is used twice; only the first one is kept"));
                continue;
            }
            var level = bank.levels.FirstOrDefault(l => l != null && l.levelId == q.levelId);
            // Inactive questions are never played: only their errors are reported (as warnings, to fix before turning them on).
            var found = ValidateQuestion(q, level != null && level.tutorial);
            issues.AddRange(q.isActive ? found : found.Where(i => i.IsError).Select(i => Warn(i.where, "(inactive) " + i.message)));
            if (q.isActive && categories.Count > 0 && !string.IsNullOrEmpty(q.category) && !categories.Contains(q.category))
                issues.Add(Warn(q.questionId, $"category '{q.category}' is not in the categories list"));
            if (q.isActive && !string.IsNullOrEmpty(q.levelId) && !levelIds.Contains(q.levelId) &&
                (knownLevelIds == null || !knownLevelIds.Contains(q.levelId)))
                issues.Add(Warn(q.questionId, $"level '{q.levelId}' does not exist, so this question is never asked"));
        }

        foreach (var l in bank.levels.Where(l => l != null && !string.IsNullOrWhiteSpace(l.levelId)))
        {
            var active = bank.questions.Where(q => q != null && q.isActive && q.levelId == l.levelId).ToList();
            if (active.Count == 0) issues.Add(Warn(l.levelId, "no active questions: the level's built-in fallback questions will be used"));
            else if (active.Count < LevelContentRules.MinQuestionsPerRoom || active.Count > LevelContentRules.MaxQuestionsPerRoom)
                issues.Add(Warn(l.levelId, $"{active.Count} active questions (a room is designed for {LevelContentRules.MinQuestionsPerRoom}-{LevelContentRules.MaxQuestionsPerRoom})"));
            if (l.tutorial && !string.IsNullOrEmpty(l.firstQuestion) && !active.Any(q => q.questionId == l.firstQuestion))
                issues.Add(Warn(l.levelId, $"firstQuestion '{l.firstQuestion}' is not an active question of this level; the order is fully shuffled"));
            if (l.inbox != null)
                foreach (var m in l.inbox)
                {
                    if (m == null || string.IsNullOrWhiteSpace(m.questionId)) issues.Add(Err(l.levelId, "an inbox email has no questionId"));
                    else if (!QuestionMapper.TryParse(m.messageType, out MessageChannel _)) issues.Add(Err(l.levelId + "/" + m.questionId, $"messageType '{m.messageType}' is not valid"));
                }
        }
        return issues;
    }

    /// <summary>Checks one question. Any Error means the question is not playable.</summary>
    public static List<ContentIssue> ValidateQuestion(QuestionRecord q, bool tutorialRoom = false)
    {
        var issues = new List<ContentIssue>();
        if (q == null) { issues.Add(Err("", "empty question")); return issues; }
        string id = string.IsNullOrEmpty(q.questionId) ? "(no id)" : q.questionId;

        if (string.IsNullOrWhiteSpace(q.questionId)) issues.Add(Err(id, "questionId is empty"));
        else if (!IdPattern.IsMatch(q.questionId)) issues.Add(Err(id, "questionId may only use letters, digits, '-' and '_' (max 40)"));
        if (!QuestionMapper.TryParse(q.messageType, out MessageChannel channel))
            issues.Add(Err(id, $"messageType '{q.messageType}' is not one of {string.Join(", ", Enum.GetNames(typeof(MessageChannel)))}"));
        if (string.IsNullOrWhiteSpace(q.message)) issues.Add(Err(id, "message (the question text) is empty"));
        if (string.IsNullOrWhiteSpace(q.explanation)) issues.Add(Err(id, "explanation is empty (the feedback screen needs it)"));
        if (MessageChannels.OnComputer(channel) && string.IsNullOrWhiteSpace(q.subject)) issues.Add(Err(id, "emails and web pages need a subject"));
        if (string.IsNullOrWhiteSpace(q.feedback)) issues.Add(Warn(id, "feedback is empty"));
        if (!Difficulties.Contains(q.difficulty ?? "")) issues.Add(Warn(id, $"difficulty '{q.difficulty}' should be Easy, Medium or Hard"));
        if (string.IsNullOrWhiteSpace(q.category)) issues.Add(Warn(id, "category is empty"));
        if (!string.IsNullOrEmpty(q.trigger) && !QuestionMapper.TryParse(q.trigger, out MessageTrigger _))
            issues.Add(Err(id, $"trigger '{q.trigger}' is not valid"));
        if (q.readingSeconds < 0) issues.Add(Err(id, "readingSeconds can't be negative"));

        // Answer
        int n = q.choices != null ? q.choices.Length : 0;
        if (n == 1) issues.Add(Err(id, "a choice question needs at least 2 choices (or none for SCAM / LEGITIMATE)"));
        if (n >= 2)
        {
            if (q.correctChoice < 0 || q.correctChoice >= n) issues.Add(Err(id, $"correctChoice {q.correctChoice} is out of range (0-{n - 1})"));
            if (q.choices.Any(string.IsNullOrWhiteSpace)) issues.Add(Err(id, "a choice is empty"));
            if (q.choices.Distinct().Count() != n) issues.Add(Err(id, "two choices are identical"));
            if (q.choices.Any(c => c != null && LetterLabel.IsMatch(c))) issues.Add(Warn(id, "choices must not start with 'A.' etc. (letters are added on screen after shuffling)"));
            if (channel != MessageChannel.Question) issues.Add(Err(id, "choice questions must use messageType Question"));
        }

        // Hint
        if (!string.IsNullOrEmpty(q.hint))
        {
            if (q.hint.Length > MaxHintLength) issues.Add(Warn(id, $"hint is {q.hint.Length} characters (keep it under {MaxHintLength})"));
            foreach (var word in new[] { "SCAM", "LEGITIMATE", "correct answer", "the answer is" })
                if (q.hint.IndexOf(word, StringComparison.Ordinal) >= 0) issues.Add(Warn(id, $"the hint says '{word}'"));
            if (n >= 2 && q.choices.Any(c => !string.IsNullOrEmpty(c) && q.hint.IndexOf(StripTags(c), StringComparison.OrdinalIgnoreCase) >= 0))
                issues.Add(Warn(id, "the hint quotes an answer choice"));
        }
        else issues.Add(Warn(id, "no hint"));

        // Investigation, activity, consequence
        var indicatorIds = new HashSet<string>();
        foreach (var e in q.indicators ?? new List<IndicatorRecord>())
            if (e == null || string.IsNullOrEmpty(e.id) || !indicatorIds.Add(e.id)) issues.Add(Err(id, $"indicator id '{e?.id}' is empty or repeated"));
        foreach (var c in q.clues ?? new List<ClueRecord>())
            if (c != null && !QuestionMapper.TryParse(c.field, out InspectField _))
                issues.Add(Err(id, $"clue '{c.id}': field '{c.field}' is not one of {string.Join(", ", Enum.GetNames(typeof(InspectField)))}"));
        var a = q.activity;
        bool hasActivity = a != null && !string.IsNullOrEmpty(a.kind) && !a.kind.Equals("None", StringComparison.OrdinalIgnoreCase);
        if (hasActivity)
        {
            if (!QuestionMapper.TryParse(a.kind, out ActivityKind _)) issues.Add(Err(id, $"activity kind '{a.kind}' is not valid"));
            if (a.items == null || a.items.Count == 0) issues.Add(Err(id, "the activity has no items"));
            else
            {
                foreach (var i in a.items)
                    if (i != null && !string.IsNullOrEmpty(i.role) && !QuestionMapper.TryParse(i.role, out ActivityRole _))
                        issues.Add(Err(id, $"activity item '{i.key}': role '{i.role}' is not valid"));
                if (a.items.Where(i => i != null).Select(i => i.key).Distinct().Count() != a.items.Count(i => i != null))
                    issues.Add(Err(id, "two activity items share a key"));
            }
            if (n < 2) issues.Add(Err(id, "activities are shown in the case-question window, so they need answer choices"));
            int keyClues = (q.indicators ?? new List<IndicatorRecord>()).Count(e => e != null && e.isKeyClue);
            if (a.minClues > keyClues) issues.Add(Err(id, $"the activity needs {a.minClues} key clues but only {keyClues} exist"));
            if (a.outcomes != null && a.outcomes.Length > 0 && a.outcomes.Length != n) issues.Add(Err(id, "the activity needs one outcome per choice"));
        }
        var con = q.consequence;
        if (con != null)
        {
            if (!string.IsNullOrEmpty(con.kind) && !QuestionMapper.TryParse(con.kind, out ConsequenceKind _)) issues.Add(Err(id, $"consequence kind '{con.kind}' is not valid"));
            if (!string.IsNullOrEmpty(con.followUp) && !QuestionMapper.TryParse(con.followUp, out FollowUpMode _)) issues.Add(Err(id, $"followUp '{con.followUp}' must be Auto or None"));
            foreach (var r in con.recheckClues ?? new string[0])
                if (!(q.clues ?? new List<ClueRecord>()).Any(c => c != null && c.id == r)) issues.Add(Warn(id, $"recheckClues lists '{r}', which is not a clue"));
        }

        // Clickable spans must point at real words / indicators (same check the game's authoring tools use).
        if (!issues.Any(i => i.IsError))
            foreach (var p in InvestigationSession.Validate(QuestionMapper.ToMessage(q, tutorialRoom)))
                issues.Add(p.Contains("can never be discovered") ? Warn(id, p) : Err(id, p));
        return issues;
    }

    public static bool IsPlayable(QuestionRecord q, bool tutorialRoom = false) => !ValidateQuestion(q, tutorialRoom).Any(i => i.IsError);

    static string StripTags(string s) => Regex.Replace(s ?? "", "<.*?>", "");
    static ContentIssue Err(string where, string msg) => new ContentIssue(ContentSeverity.Error, where, msg);
    static ContentIssue Warn(string where, string msg) => new ContentIssue(ContentSeverity.Warning, where, msg);
}
