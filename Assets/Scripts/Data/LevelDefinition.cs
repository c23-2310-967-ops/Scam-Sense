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

/// <summary>
/// How one level scores answers (ScoringSystem reads it; nothing else hardcodes points).
/// Correct = base points, plus a small speed bonus if answered well inside the reading allowance, minus a small
/// stepped overtime deduction (capped) once the allowance runs out. Reading time is never wasted time: the
/// allowance grows with the message's length and number of details. Wrong = 0 points and a small deduction.
/// </summary>
[System.Serializable]
public class LevelScoring
{
    [Header("Points")]
    [Tooltip("Points for a correct answer given within the reading allowance.")]
    public int basePoints = 100;
    [Tooltip("Extra points for a quick correct answer (full inside the fast window, fading to 0 at the end of the allowance). 0 = no speed bonus.")]
    public int speedBonus = 0;
    [Tooltip("Share of the reading allowance that earns the full speed bonus.")]
    [Range(0f, 1f)] public float fastWindow = 0.35f;
    [Tooltip("Taken off the total for a wrong answer (once per message; the total never goes below 0).")]
    public int wrongPenalty = 10;
    [Tooltip("Earned when a wrong answer is corrected in its follow-up re-check (half if a hint was used).")]
    public int recoveryPoints = 20;

    [Header("Reading allowance (seconds) = base + words x per word + details x per detail")]
    public float baseReadingSeconds = 25f;
    public float secondsPerWord = 0.6f;
    [Tooltip("Per clickable detail and Details clue: time to inspect it.")]
    public float secondsPerDetail = 4f;
    public float minReadingSeconds = 45f;
    public float maxReadingSeconds = 150f;
    [Tooltip("Added for a multi-evidence final case.")]
    public float caseExtraSeconds = 60f;

    [Header("Overtime (after the allowance) - stepped, capped, never per second")]
    [Tooltip("Every this many seconds past the allowance...")]
    public float overtimeStepSeconds = 15f;
    [Tooltip("...this many points come off a correct answer. 0 = no time penalty.")]
    public int overtimePenaltyPerStep = 0;
    [Tooltip("Most a correct answer can lose to overtime.")]
    public int maxTimePenalty = 0;
    [Tooltip("A correct answer never earns less than this.")]
    public int minCorrectPoints = 100;

    /// <summary>Speed bonus or overtime deductions exist (otherwise there is no time pressure at all).</summary>
    public bool HasTimePressure => speedBonus > 0 || (maxTimePenalty > 0 && overtimePenaltyPerStep > 0);

    public int MaxPointsPerMessage => Mathf.Max(0, basePoints) + Mathf.Max(0, speedBonus);

    /// <summary>One sentence of rules for the briefing and How to Play.</summary>
    public string Summary()
    {
        string s = $"Correct answers earn <b>{basePoints}</b> points" + (speedBonus > 0 ? $" (up to <b>+{speedBonus}</b> for a quick, confident answer)" : "") + ". ";
        s += HasTimePressure && maxTimePenalty > 0
            ? $"Each message gives you time to read it fully - longer messages get more. After that, a little comes off (at most <b>{maxTimePenalty}</b>). "
            : "There is no time pressure - take the time you need to read and investigate. ";
        return s + $"A wrong answer costs <b>{wrongPenalty}</b>.";
    }

    public void Validate()
    {
        basePoints = Mathf.Max(0, basePoints);
        speedBonus = Mathf.Max(0, speedBonus);
        wrongPenalty = Mathf.Max(0, wrongPenalty);
        recoveryPoints = Mathf.Max(0, recoveryPoints);
        baseReadingSeconds = Mathf.Max(0f, baseReadingSeconds);
        secondsPerWord = Mathf.Max(0f, secondsPerWord);
        secondsPerDetail = Mathf.Max(0f, secondsPerDetail);
        minReadingSeconds = Mathf.Max(5f, minReadingSeconds);
        maxReadingSeconds = Mathf.Max(minReadingSeconds, maxReadingSeconds);
        caseExtraSeconds = Mathf.Max(0f, caseExtraSeconds);
        overtimeStepSeconds = Mathf.Max(1f, overtimeStepSeconds);
        overtimePenaltyPerStep = Mathf.Max(0, overtimePenaltyPerStep);
        maxTimePenalty = Mathf.Clamp(maxTimePenalty, 0, basePoints);
        minCorrectPoints = Mathf.Clamp(minCorrectPoints, 0, basePoints);
    }
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

    [Header("Scoring (points, reading time and penalties for this level)")]
    public LevelScoring scoring = new LevelScoring();

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
        if (scoring == null) scoring = new LevelScoring();
        scoring.Validate();
        delayBetweenMessages.x = Mathf.Max(0f, delayBetweenMessages.x);
        delayBetweenMessages.y = Mathf.Max(delayBetweenMessages.x, delayBetweenMessages.y);

        // Each message is scored once by id, so ids must be unique.
        var seen = new HashSet<string>();
        foreach (var m in messages)
            if (m != null && !seen.Add(m.id))
                Debug.LogWarning($"{name}: duplicate message id '{m.id}'. Every message needs a unique id.", this);
    }
}
