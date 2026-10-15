using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything that makes one room/case different: briefing, messages, timing,
/// star thresholds and the score needed to unlock the next room.
/// Create more with Assets > Create > Scam Sense > Level Definition.
/// </summary>
/// <summary>One page of reference evidence on the case-files computer.</summary>
[System.Serializable]
public class CaseFile
{
    public string title = "Trusted Contacts";
    [TextArea(4, 12)] public string body = "";
}

[CreateAssetMenu(fileName = "LevelDefinition", menuName = "Scam Sense/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique save key, e.g. level_1. Do not change after release or saved progress is lost.")]
    public string levelId = "level_1";
    public int levelNumber = 1;
    public string displayName = "The First Case";
    [Tooltip("Scene that plays this level. Must be in the Build Profile scene list.")]
    public string sceneName = "Game";
    [Tooltip("One or two sentences for the Level Selection card.")]
    [TextArea(2, 4)] public string description = "";
    [Tooltip("Still of the room for the menu's room previews (Tools > Scam Sense > Menu > Capture Level Previews). Optional.")]
    public Sprite previewImage;

    [Header("Briefing")]
    public string briefingTitle = "CASE BRIEFING";
    [TextArea(3, 8)] public string briefingText = "";
    [Tooltip("Facts the player knows before the day starts (e.g. an order they placed).")]
    public string[] caseNotes;
    public string[] objectives;

    [Header("Messages (played in order)")]
    public List<ScamMessage> messages = new List<ScamMessage>();

    [Header("Computer Inbox")]
    [Tooltip("Older, already-read emails shown in the computer inbox for realism. Read-only, never scored.")]
    public List<ScamMessage> inboxExtras = new List<ScamMessage>();

    [Header("Case Files (optional evidence computer)")]
    [Tooltip("Reference pages shown on a CaseFilesTerminal (trusted contacts, orders, policy...). Never state an answer.")]
    public List<CaseFile> caseFiles = new List<CaseFile>();

    [Header("Message Timing (seconds)")]
    [Tooltip("Delay before the first message after the briefing closes.")]
    public float firstMessageDelay = 8f;
    [Tooltip("Random delay range after the previous message has been answered.")]
    public Vector2 delayBetweenMessages = new Vector2(10f, 20f);
    [Tooltip("A message's activity trigger can fire early, but never sooner than this.")]
    public float minimumActivityDelay = 4f;

    [Header("Stars (performance reward only)")]
    public int oneStarScore = 200;
    public int twoStarScore = 300;
    public int threeStarScore = 400;

    [Header("Room Progression (score based, NOT stars)")]
    [Tooltip("Final score needed to unlock the next room.")]
    public int minScoreToUnlockNext = 300;
    [Tooltip("Save key of the next room, e.g. level_2. Leave empty if this is the last room.")]
    public string nextLevelId = "level_2";
    [Tooltip("Scene loaded by Continue. Must be in the Build Profile scene list. Leave empty until the scene exists.")]
    public string nextSceneName = "";

    [Header("Results")]
    [Tooltip("Short lessons shown on the results screen.")]
    public string[] lessons;
    [Tooltip("Optional one-paragraph summary shown above the lessons.")]
    [TextArea(2, 4)] public string learningSummary = "";
    [Tooltip("Optional performance messages (empty = the default Level 1 wording).")]
    public string performanceHigh = "";
    public string performanceModerate = "";
    public string performanceLow = "";

    public int MessageCount => messages != null ? messages.Count : 0;

    void OnValidate()
    {
        // Keep thresholds in order so the star rating can never skip a star.
        oneStarScore = Mathf.Max(0, oneStarScore);
        twoStarScore = Mathf.Max(oneStarScore, twoStarScore);
        threeStarScore = Mathf.Max(twoStarScore, threeStarScore);
        minScoreToUnlockNext = Mathf.Max(0, minScoreToUnlockNext);
        delayBetweenMessages.x = Mathf.Max(0f, delayBetweenMessages.x);
        delayBetweenMessages.y = Mathf.Max(delayBetweenMessages.x, delayBetweenMessages.y);

        // Each message is scored once by id, so ids must be unique.
        var seen = new HashSet<string>();
        foreach (var m in messages)
            if (m != null && !seen.Add(m.id))
                Debug.LogWarning($"{name}: duplicate message id '{m.id}'. Every message needs a unique id.", this);
    }
}
