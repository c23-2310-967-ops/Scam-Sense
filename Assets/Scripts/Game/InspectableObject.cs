using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A room object the player can look at and press E to read: a case folder, an evidence document or the
/// investigation board. PlayerInteractor (the existing look-and-E script on the camera) shows the prompt and
/// calls <see cref="Interact"/>; the content opens in DocumentReaderUI. Everything is set in the Inspector.
///
/// Needs a non-trigger Collider on this object or a child so the look ray can hit it. The ray stops at the first
/// collider, so walls block it, and <see cref="interactDistance"/> limits the reach.
/// </summary>
public class InspectableObject : MonoBehaviour
{
    public enum Kind { CaseFolder, EvidenceDocument, InvestigationBoard }
    public enum Source
    {
        /// <summary>The pages typed below.</summary>
        CustomPages,
        /// <summary>The level's briefing, case notes and objectives.</summary>
        LevelBriefing,
        /// <summary>The level's Case Files (trusted contacts, orders, policy...).</summary>
        LevelCaseFiles,
        /// <summary>Every message reviewed so far in this level, with the answer and the explanation.</summary>
        ReviewedEvidence,
    }
    public enum Requirement { None, MessagesReviewed }

    [Header("What it is")]
    public Kind kind = Kind.EvidenceDocument;
    [Tooltip("Prompt text, e.g. 'Read case folder'. Empty = a default for the kind.")]
    public string promptLabel = "";
    [Tooltip("Title shown at the top of the reader.")]
    public string title = "Evidence";

    [Header("Content")]
    public Source source = Source.CustomPages;
    [Tooltip("Used when Source is Custom Pages (and as a fallback if the level has no matching content).")]
    public List<CaseFile> pages = new List<CaseFile>();

    [Header("Interaction")]
    [Tooltip("How close the player must be (metres).")]
    public float interactDistance = 2.5f;

    [Header("Availability")]
    public Requirement requirement = Requirement.None;
    [Tooltip("Messages that must be reviewed first (Requirement = Messages Reviewed).")]
    [Min(1)] public int requiredCount = 1;
    [Tooltip("Second prompt line while it is not available yet.")]
    public string lockedHint = "Review a message first";
    [Tooltip("Shown when E is pressed while it is not available yet.")]
    public string lockedMessage = "Nothing to review yet. Read and answer your first message, then come back.";

    [Header("Events")]
    [Tooltip("Raised each time the document is opened (e.g. to tick an objective).")]
    public UnityEvent onOpened;

    GameManager gameManager;
    QuizManager quiz;

    void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning($"{name}: InspectableObject needs a Collider (not a trigger) so the player can look at it.", this);
    }

    public string Prompt => !string.IsNullOrEmpty(promptLabel) ? promptLabel :
        kind == Kind.CaseFolder ? "Read case folder" : kind == Kind.InvestigationBoard ? "Review investigation board" : "Read document";

    public string KindLabel => kind == Kind.CaseFolder ? "Case folder" : kind == Kind.InvestigationBoard ? "Investigation board" : "Evidence document";

    /// <summary>False while a requirement is not met yet (the reason says what to do).</summary>
    public bool IsAvailable(out string reason)
    {
        reason = null;
        if (requirement == Requirement.MessagesReviewed)
        {
            int reviewed = Quiz != null && Quiz.Scoring != null ? Quiz.Scoring.Results.Count : 0;
            if (reviewed < requiredCount) { reason = lockedMessage; return false; }
        }
        return true;
    }

    /// <summary>Opens the reader. Returns false if nothing was opened (not available, no content, another panel open).</summary>
    public bool Interact(LevelHUD hud)
    {
        if (!IsAvailable(out string reason))
        {
            if (hud != null && !string.IsNullOrEmpty(reason)) hud.ShowToast(reason);
            return false;
        }
        var content = BuildPages();
        if (content.Count == 0)
        {
            if (hud != null) hud.ShowToast("There's nothing written here yet.");
            return false;
        }
        if (!DocumentReaderUI.Open(KindLabel, title, content)) return false;
        onOpened?.Invoke();
        return true;
    }

    QuizManager Quiz => quiz != null ? quiz : (quiz = FindFirstObjectByType<QuizManager>());
    LevelDefinition Level
    {
        get
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null && gameManager.Level != null) return gameManager.Level;
            return Quiz != null ? Quiz.Level : null;
        }
    }

    // ---------- Content ----------

    public List<DocumentReaderUI.Page> BuildPages()
    {
        var result = new List<DocumentReaderUI.Page>();
        var level = Level;
        switch (source)
        {
            case Source.LevelBriefing when level != null: AddBriefing(result, level); break;
            case Source.LevelCaseFiles when level != null && level.caseFiles != null && level.caseFiles.Count > 0:
                foreach (var f in level.caseFiles) result.Add(new DocumentReaderUI.Page(f.title, f.body));
                break;
            case Source.ReviewedEvidence: AddEvidence(result, level); break;
        }
        if (result.Count == 0 && pages != null)   // custom pages, or the fallback when the level has no content
            foreach (var p in pages) if (p != null) result.Add(new DocumentReaderUI.Page(p.title, p.body));
        return result;
    }

    static string Bullet(string s) => $"<color={UIKit.HexAccent}>•</color>  {s}";

    static void AddBriefing(List<DocumentReaderUI.Page> result, LevelDefinition level)
    {
        result.Add(new DocumentReaderUI.Page($"Case {level.levelNumber}: {level.displayName}",
            string.IsNullOrEmpty(level.briefingText) ? level.description : level.briefingText));
        if (level.caseNotes != null && level.caseNotes.Length > 0)
        {
            var sb = new StringBuilder();
            foreach (var n in level.caseNotes) sb.AppendLine(Bullet(n));
            result.Add(new DocumentReaderUI.Page("Case notes", sb.ToString().TrimEnd()));
        }
        if (level.objectives != null && level.objectives.Length > 0)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < level.objectives.Length; i++) sb.AppendLine($"<color={UIKit.HexAccent}><b>{i + 1:00}</b></color>   {level.objectives[i]}");
            result.Add(new DocumentReaderUI.Page("Objectives", sb.ToString().TrimEnd()));
        }
    }

    void AddEvidence(List<DocumentReaderUI.Page> result, LevelDefinition level)
    {
        var results = Quiz != null && Quiz.Scoring != null ? Quiz.Scoring.Results : null;
        if (results == null || results.Count == 0) return;
        int correct = 0;
        foreach (var r in results) if (r.isCorrect) correct++;

        var overview = new StringBuilder();
        int total = level != null ? level.MessageCount : results.Count;
        overview.AppendLine($"Messages reviewed: <b>{results.Count} / {total}</b>     Correct: <b>{correct}</b>\n");
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            string verdict = r.message.correctVerdict == MessageVerdict.Scam ? $"<color={UIKit.HexDanger}>SCAM</color>" : $"<color={UIKit.HexSafe}>LEGITIMATE</color>";
            string mark = r.isCorrect ? $"<color={UIKit.HexSafe}>✓</color>" : $"<color={UIKit.HexWarning}>×</color>";
            overview.AppendLine($"{mark}  <b>{i + 1}.</b> {Safe(MessageChannels.Noun(r.message.channel))} from <b>{Safe(r.message.sender)}</b>  —  {verdict}");
        }
        overview.AppendLine("\nOpen the next pages for the clues behind each answer.");
        result.Add(new DocumentReaderUI.Page("Evidence collected", overview.ToString().TrimEnd()));

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var m = r.message;
            var sb = new StringBuilder();
            sb.AppendLine($"<color={UIKit.HexSecondary}>From</color>  {Safe(m.sender)}{(string.IsNullOrEmpty(m.senderDetail) ? "" : "  ·  " + Safe(m.senderDetail))}");
            if (!string.IsNullOrEmpty(m.subject)) sb.AppendLine($"<color={UIKit.HexSecondary}>Subject</color>  {Safe(m.subject)}");
            if (!string.IsNullOrEmpty(m.link)) sb.AppendLine($"<color={UIKit.HexSecondary}>Link</color>  {Safe(m.link)}  <color={UIKit.HexSecondary}>(never opened)</color>");
            string body = m.body ?? "";
            if (body.Length > 260) body = body.Substring(0, 257) + "...";
            sb.AppendLine($"\n<i>\"{Safe(body)}\"</i>\n");
            sb.AppendLine($"Your answer: <b>{UIKit.VerdictWord(r.chosen)}</b>   ·   Correct answer: <b>{UIKit.VerdictWord(m.correctVerdict)}</b>   {(r.isCorrect ? $"<color={UIKit.HexSafe}>✓ correct</color>" : $"<color={UIKit.HexWarning}>× not quite</color>")}");
            sb.AppendLine($"\n<color={UIKit.HexAccent}><b>Why</b></color>\n{m.explanation}");
            result.Add(new DocumentReaderUI.Page($"{i + 1}. {MessageChannels.Noun(m.channel)} — {m.sender}", sb.ToString().TrimEnd()));
        }
    }

    /// <summary>Message text is shown as typed (never parsed as rich-text tags).</summary>
    static string Safe(string s) => string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "") + "</noparse>";
}
