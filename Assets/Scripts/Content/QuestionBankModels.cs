using System;
using System.Collections.Generic;

// =====================================================================================================
// Question bank data (schema version 2). Plain serializable records, read with JsonUtility.
// One file / one Supabase bundle holds every level's texts and every question. Gameplay scripts never read
// these records: LevelContentResolver turns them into the ScamMessage / LevelDefinition objects the game
// already uses, so content can change without touching any gameplay code.
//
// Enum-like values are strings so a bad value is reported by QuestionBankValidator instead of breaking the
// whole file. Empty strings mean "use the default rule" (documented per field and in Docs/ContentManagement.md).
// =====================================================================================================

/// <summary>The whole question bank (Assets/Resources/Content/question_bank.json, or the Supabase bundle).</summary>
[Serializable]
public class QuestionBankFile
{
    public const int CurrentSchemaVersion = 2;

    public int schemaVersion = CurrentSchemaVersion;
    /// <summary>Free text that changes on every edit, e.g. "2026-11-28.1". Shown in logs; cloud bundles use the last edit time.</summary>
    public string contentVersion = "";
    public string source = "";
    /// <summary>Allowed category names. A question with another category still plays (validator warning).</summary>
    public string[] categories = new string[0];
    public List<LevelContentRecord> levels = new List<LevelContentRecord>();
    public List<QuestionRecord> questions = new List<QuestionRecord>();
}

/// <summary>Per-level texts. Identity, scene, scoring rules and unlock chain stay in the LevelDefinition asset.</summary>
[Serializable]
public class LevelContentRecord
{
    /// <summary>Must match a LevelDefinition.levelId, e.g. "level_2".</summary>
    public string levelId = "";
    /// <summary>One line on what the room teaches (start of the generated briefing).</summary>
    public string intro = "";
    /// <summary>Optional: replaces the generated briefing title / text / objectives ({unlock} and {count} are filled in).</summary>
    public string briefingTitle = "";
    public string briefing = "";
    public string[] objectives = new string[0];
    /// <summary>Facts the player knows before the day starts (case notes and the case-files computer).</summary>
    public string[] caseNotes = new string[0];
    /// <summary>"What you learned" lines on the results screen.</summary>
    public string[] lessons = new string[0];
    public string learningSummary = "";
    /// <summary>Runs the step-by-step guide. Only the first room should set this.</summary>
    public bool tutorial;
    /// <summary>Tutorial rooms: the question asked first (must be active in this level).</summary>
    public string firstQuestion = "";
    /// <summary>Older, already-read emails in the computer inbox (never scored). Empty list = keep the asset's.</summary>
    public List<QuestionRecord> inbox = new List<QuestionRecord>();
}

/// <summary>
/// One question / scam message / scenario. Two answer styles:
///   * Verdict: no choices; the player answers SCAM or LEGITIMATE and <see cref="isScam"/> is the right answer.
///   * Choice: two or more <see cref="choices"/>; <see cref="correctChoice"/> (0-based, original order) is right.
/// </summary>
[Serializable]
public class QuestionRecord
{
    // ---- Identity and management ----
    public string questionId = "";
    /// <summary>The level that asks it, e.g. "level_3". Empty = not used in any level (reserve).</summary>
    public string levelId = "";
    /// <summary>Position inside the level before the per-attempt shuffle (lower first).</summary>
    public int order;
    /// <summary>false = kept in the bank but never shown.</summary>
    public bool isActive = true;
    public string category = "";
    /// <summary>Easy, Medium or Hard.</summary>
    public string difficulty = "Easy";
    /// <summary>Label only, e.g. "Scam or Legitimate", "Multiple Choice", "What Should You Do?".</summary>
    public string questionType = "";

    // ---- The message ----
    /// <summary>SMS, Chat (phone) · Email, Website (computer) · Question (case-question window) · Case (final case terminal).</summary>
    public string messageType = "SMS";
    public string sender = "";
    public string senderDetail = "";
    public string subject = "";
    /// <summary>Message body / question text (TextMeshPro rich text allowed).</summary>
    public string message = "";
    /// <summary>Shown link (never opened). Use reserved .example domains only.</summary>
    public string link = "";
    /// <summary>Background the player knows (case notes), not shown on the message.</summary>
    public string scenarioContext = "";

    // ---- The answer ----
    public bool isScam = true;
    public string[] choices = new string[0];
    public int correctChoice;
    public string explanation = "";
    /// <summary>Short line above the explanation after answering.</summary>
    public string feedback = "";
    /// <summary>HINT text. Never states the answer. Empty = no HINT button.</summary>
    public string hint = "";
    public string learningObjective = "";
    /// <summary>"Course — Topic" from the validated curriculum.</summary>
    public string curriculumRef = "";

    // ---- Arrival and timing ----
    /// <summary>"" = by type (phone messages near the phone, computer ones near a computer). TimeOnly, PlayerWalked, NearComputer, NearPhone.</summary>
    public string trigger = "";
    /// <summary>Reading allowance in seconds. 0 = worked out from length and details.</summary>
    public float readingSeconds;
    /// <summary>Email / web page: the workstation (ComputerTerminal Station Id). Empty = any computer.</summary>
    public string stationId = "";

    // ---- Details panel ----
    public string requestedAction = "";
    public string linkNote = "";
    /// <summary>Neutral observations listed under Details. Must not state the answer.</summary>
    public string[] detailNotes = new string[0];

    // ---- Investigation ----
    /// <summary>Clickable spans. A clue whose indicator is a key clue lights up gold when found.</summary>
    public List<ClueRecord> clues = new List<ClueRecord>();
    /// <summary>What the clues reveal (red flags, or signs of a real message).</summary>
    public List<IndicatorRecord> indicators = new List<IndicatorRecord>();
    /// <summary>Optional simulated app (email client, browser, chat, call, Wi-Fi list, files, evidence board...).</summary>
    public ActivityRecord activity = new ActivityRecord();

    // ---- Wrong answer ----
    public ConsequenceRecord consequence = new ConsequenceRecord();

    /// <summary>Author's notes (not shown in game).</summary>
    public string notes = "";

    public bool HasChoices => choices != null && choices.Length >= 2;
}

/// <summary>A clickable detail inside a message (an "inspectable").</summary>
[Serializable]
public class ClueRecord
{
    public string id = "";
    /// <summary>Sender, SenderDetail, Subject, Body, Link or Activity.</summary>
    public string field = "Body";
    /// <summary>Exact visible words that become clickable. Empty = the whole field.</summary>
    public string match = "";
    public string label = "";
    /// <summary>What the player finds out.</summary>
    public string finding = "";
    /// <summary>Links: the real address (reserved .example domains only, never opened).</summary>
    public string linkAddress = "";
    /// <summary>The indicator this reveals (an IndicatorRecord id). Empty = neutral detail.</summary>
    public string indicatorId = "";
    /// <summary>Activity clues: the key of the activity item the words are in.</summary>
    public string part = "";
}

/// <summary>A red flag (or sign of legitimacy) that clues reveal.</summary>
[Serializable]
public class IndicatorRecord
{
    public string id = "";
    public string title = "";
    public string whyItMatters = "";
    /// <summary>true = expected evidence for the right answer (a gold key clue).</summary>
    public bool isKeyClue = true;
}

[Serializable]
public class ActivityRecord
{
    /// <summary>"" / None = no activity. Email, Browser, Chat, Call, Social, Form, Network, Files, Evidence.</summary>
    public string kind = "";
    public string title = "";
    public string instruction = "";
    public List<ActivityItemRecord> items = new List<ActivityItemRecord>();
    /// <summary>Key clues to find before the answers unlock.</summary>
    public int minClues = 1;
    /// <summary>Decision outcome per choice (original order). Empty = none.</summary>
    public string[] outcomes = new string[0];
}

[Serializable]
public class ActivityItemRecord
{
    public string key = "";
    /// <summary>Field, Text, Incoming, Outgoing, File, Banner, Row, Button, Note.</summary>
    public string role = "Text";
    public string label = "";
    public string text = "";
}

/// <summary>What a wrong answer sets off. Empty values = the default rule.</summary>
[Serializable]
public class ConsequenceRecord
{
    /// <summary>"" = choice questions FeedbackOnly, others Auto. Auto, SecurityIncident, AccuracyWarning, MissedClue, FeedbackOnly.</summary>
    public string kind = "";
    public string alertTitle = "";
    public string alertBody = "";
    public string overlookedClue = "";
    /// <summary>"" = re-check offered for SCAM/LEGITIMATE messages in the tutorial room only. Auto or None.</summary>
    public string followUp = "";
    /// <summary>Clue ids to re-check in the follow-up. Empty = picked automatically.</summary>
    public string[] recheckClues = new string[0];
}

/// <summary>Where the content the game is playing came from.</summary>
public enum ContentSource { None, CloudCache, LocalOverride, Bundled, LevelAsset }

public enum ContentSeverity { Warning, Error }

/// <summary>One validation finding. Errors on a question drop that question; errors on the file reject the file.</summary>
public class ContentIssue
{
    public ContentSeverity severity;
    /// <summary>Question id, level id, or "" for the whole file.</summary>
    public string where;
    public string message;

    public ContentIssue(ContentSeverity severity, string where, string message)
    {
        this.severity = severity;
        this.where = where ?? "";
        this.message = message;
    }

    public bool IsError => severity == ContentSeverity.Error;
    public override string ToString() => $"{severity.ToString().ToUpperInvariant()} {(where.Length > 0 ? where + ": " : "")}{message}";
}
