using System;
using UnityEngine;

/// <summary>Which part of a message an inspectable detail lives in.</summary>
public enum InspectField { Sender, SenderDetail, Subject, Body, Link }

/// <summary>
/// A detail of a message the player can click/tap to inspect (part of a ScamMessage's investigation data).
/// <see cref="match"/> is the exact visible words inside <see cref="field"/> that become clickable
/// (empty = the whole field). The message text itself is never changed, so the HUD, inbox and other
/// screens keep showing it exactly as written.
/// </summary>
[Serializable]
public class MessageInspectable
{
    public string id = "";
    public InspectField field = InspectField.Body;
    [Tooltip("Exact words in that field to make clickable. Empty = the whole field.")]
    public string match = "";
    [Tooltip("What the player is looking at, e.g. 'Sender address'.")]
    public string label = "";
    [Tooltip("What the player finds out (for links: where it really goes - it is never opened).")]
    [TextArea(2, 6)] public string finding = "";
    [Tooltip("Links: the full address the link points to (reserved .example domains only).")]
    public string linkAddress = "";
    [Tooltip("Evidence revealed by inspecting this (a MessageEvidence id). Empty = a neutral detail, not evidence.")]
    public string clueId = "";

    public bool IsLink => field == InspectField.Link || !string.IsNullOrEmpty(linkAddress);
}

/// <summary>A clue an inspectable detail can reveal (shown on the finding card and in the feedback).</summary>
[Serializable]
public class MessageEvidence
{
    public string id = "";
    public string title = "";
    [Tooltip("Why this clue matters (shown in the feedback and the wrong-answer card).")]
    [TextArea(2, 4)] public string whyItMatters = "";
    [Tooltip("True if this clue supports the message's correct verdict (expected evidence).")]
    public bool supportsVerdict = true;
}
