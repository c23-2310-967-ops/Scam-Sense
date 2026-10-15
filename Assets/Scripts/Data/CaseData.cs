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

    /// <summary>True if the message has details to inspect.</summary>
    public bool HasInvestigation => inspectables != null && inspectables.Count > 0;

    /// <summary>True if there is anything to show in the Details section.</summary>
    public bool HasDetails =>
        !string.IsNullOrEmpty(senderDetail) || !string.IsNullOrEmpty(requestedAction) ||
        !string.IsNullOrEmpty(linkNote) || (clues != null && clues.Length > 0);
}
