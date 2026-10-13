using System;
using UnityEngine;

public enum MessageChannel { SMS, Email }

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

    /// <summary>True if there is anything to show in the Details section.</summary>
    public bool HasDetails =>
        !string.IsNullOrEmpty(senderDetail) || !string.IsNullOrEmpty(requestedAction) ||
        !string.IsNullOrEmpty(linkNote) || (clues != null && clues.Length > 0);
}
