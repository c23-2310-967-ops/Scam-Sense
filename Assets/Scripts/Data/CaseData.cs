using System;
using UnityEngine;

/// <summary>
/// Where a message is investigated. New values are only ever appended (assets store the number).
///   SMS     - the phone (pocket phone pop-up)
///   Email   - a computer's Mail app
///   Website - a captured web page, reviewed on a computer like an email
///   Case    - a multi-evidence final case, submitted at a FinalCaseTerminal
///   Chat    - a work-chat conversation, read on the phone like a text message
/// </summary>
public enum MessageChannel { SMS, Email, Website, Case, Chat }

public static class MessageChannels
{
    /// <summary>Read and answered on a ComputerTerminal (when the scene has one).</summary>
    public static bool OnComputer(MessageChannel c) => c == MessageChannel.Email || c == MessageChannel.Website;

    /// <summary>"email", "text message", "web page", "case" - for feedback sentences.</summary>
    public static string Noun(MessageChannel c) =>
        c == MessageChannel.SMS ? "text message" : c == MessageChannel.Website ? "web page" : c == MessageChannel.Case ? "case"
        : c == MessageChannel.Chat ? "chat message" : "email";

    /// <summary>Read on the phone (text messages and chats).</summary>
    public static bool OnPhone(MessageChannel c) => c == MessageChannel.SMS || c == MessageChannel.Chat;
}

public enum MessageVerdict { Scam, Legitimate }

/// <summary>What can make a message arrive before its timer runs out.</summary>
public enum MessageTrigger
{
    TimeOnly,       // arrives when the delay runs out
    PlayerWalked,   // arrives early once the player has walked around a bit
    NearComputer,   // arrives early when the player is near the computer desk
    NearPhone       // arrives early when the player is near the phone
}

/// <summary>
/// The reaction to a wrong answer (WrongAnswerConsequences). Auto picks it from the verdicts:
///   trusted a scam            -> SecurityIncident (simulated security alert, "security risk missed")
///   flagged a genuine message -> AccuracyWarning (which details support legitimacy, verify first)
///   wrong final case          -> MissedClue (the evidence that was overlooked)
/// FeedbackOnly = the educational explanation without a simulated alert. New values are only appended.
/// </summary>
public enum ConsequenceKind { Auto, SecurityIncident, AccuracyWarning, MissedClue, FeedbackOnly }

/// <summary>Auto = a follow-up re-check when the message has investigation data; None = never.</summary>
public enum FollowUpMode { Auto, None }

/// <summary>Optional per-message settings for the wrong-answer reaction. Empty texts are generated from the message.</summary>
[Serializable]
public class WrongAnswerConsequence
{
    public ConsequenceKind kind = ConsequenceKind.Auto;
    [Tooltip("Headline of the simulated alert. Empty = generated from the message. Fictional content only.")]
    public string alertTitle = "";
    [Tooltip("Body of the simulated alert. Empty = generated from the message.")]
    [TextArea(2, 5)] public string alertBody = "";
    [Tooltip("The clue the player most likely overlooked. Empty = taken from the investigation data or the Details clues.")]
    [TextArea(2, 4)] public string overlookedClue = "";
    [Tooltip("Follow-up investigation: re-check the key details, then answer again for recovery points.")]
    public FollowUpMode followUp = FollowUpMode.Auto;
    [Tooltip("Inspectable ids to re-check in the follow-up. Empty = up to three details that reveal key clues (links and senders first).")]
    public string[] recheckDetails;
}

/// <summary>One incoming text message or email that the player must classify.</summary>
[Serializable]
public class ScamMessage
{
    public string id = "msg_1";
    public MessageChannel channel = MessageChannel.SMS;

    [Header("Content")]
    public string sender = "Unknown Number";
    [Tooltip("Optional second line under the sender, e.g. a phone number.")]
    public string senderDetail = "";
    [Tooltip("Emails only.")]
    public string subject = "";
    [TextArea(3, 8)] public string body = "";
    [Tooltip("Optional link shown in the message. Use reserved .example domains only - it is never opened.")]
    public string link = "";

    [Header("Answer")]
    public MessageVerdict correctVerdict = MessageVerdict.Scam;
    [Tooltip("Background the player knows (shown in the case notes, not on the message).")]
    [TextArea(2, 4)] public string scenarioContext = "";
    [TextArea(3, 8)] public string explanation = "";

    [Header("Arrival")]
    public MessageTrigger trigger = MessageTrigger.TimeOnly;
    [Tooltip("Reading allowance in seconds for this message. 0 = worked out from its length and details (Level Definition > Scoring).")]
    public float readingSeconds = 0f;

    [Header("Investigation (shown under Details before answering)")]
    [Tooltip("What the message asks you to do, in plain words.")]
    public string requestedAction = "";
    [Tooltip("Safe description of any link destination (never opened).")]
    public string linkNote = "";
    [Tooltip("Neutral observations the player can inspect. Must not state the answer.")]
    public string[] clues;

    [Header("Investigation (optional): clickable details and Evidence Notebook clues")]
    [Tooltip("Details the player can click/tap to inspect. Leave empty for a message without investigation.")]
    public System.Collections.Generic.List<MessageInspectable> inspectables = new System.Collections.Generic.List<MessageInspectable>();
    [Tooltip("Clues the inspectables can reveal; supportsVerdict marks the expected evidence.")]
    public System.Collections.Generic.List<MessageEvidence> evidence = new System.Collections.Generic.List<MessageEvidence>();

    [Header("Wrong answer (optional): what a mistake on this message sets off")]
    public WrongAnswerConsequence consequence = new WrongAnswerConsequence();

    /// <summary>True if the message has details to inspect.</summary>
    public bool HasInvestigation => inspectables != null && inspectables.Count > 0;

    /// <summary>True if there is anything to show in the Details section.</summary>
    public bool HasDetails =>
        !string.IsNullOrEmpty(senderDetail) || !string.IsNullOrEmpty(requestedAction) ||
        !string.IsNullOrEmpty(linkNote) || (clues != null && clues.Length > 0);
}
