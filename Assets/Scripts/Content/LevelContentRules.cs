using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a level's question list turns into its star / unlock scores, briefing, objectives and case-file pages.
/// Used at runtime (LevelContentResolver) and by the editor import that bakes fallback content into the level
/// assets, so both always agree. Depends only on the level's own scoring rules and how many questions it has.
/// </summary>
public static class LevelContentRules
{
    /// <summary>A normal room has 2-8 questions. Fewer still plays (warning); none falls back.</summary>
    public const int MinQuestionsPerRoom = 2, MaxQuestionsPerRoom = 8;

    /// <summary>
    /// Correct answers needed for 1 star and for 2 stars / unlocking, out of <paramref name="n"/> questions:
    /// about half for 1 star, about 60% (never all of them) to unlock. 3 questions: 1 / 2. 6 questions: 3 / 4.
    /// </summary>
    public static (int oneStar, int unlock) CorrectNeeded(int n)
    {
        int unlock = Mathf.Clamp(Mathf.CeilToInt(n * 0.6f), 1, Mathf.Max(1, n - 1));
        int one = Mathf.Clamp(n / 2, 1, unlock);
        return (one, unlock);
    }

    /// <summary>
    /// Star and unlock scores for <paramref name="n"/> questions. About 60% right always unlocks, even if every
    /// right answer ran over its reading time (each still earns at least minCorrectPoints) and the misses cost points.
    /// </summary>
    public static void ApplyThresholds(LevelDefinition level, int n)
    {
        var s = level.scoring ?? new LevelScoring();
        int b = s.basePoints, p = s.wrongPenalty, slow = Mathf.Min(b, Mathf.Max(0, s.minCorrectPoints));
        var (k1, k2) = CorrectNeeded(Mathf.Max(1, n));
        level.oneStarScore = Mathf.Max(1, k1 * slow - (n - k1) * p);
        level.twoStarScore = Mathf.Max(level.oneStarScore, k2 * slow - (n - k2) * p);
        level.threeStarScore = Mathf.Max(level.twoStarScore, n * b);
        level.minScoreToUnlockNext = level.twoStarScore;
    }

    /// <summary>Briefing, objectives, case notes, lessons and case-file pages for the level's current messages.</summary>
    public static void ApplyTexts(LevelDefinition level, LevelContentRecord room)
    {
        int n = level.MessageCount;
        int choiceCount = 0;
        foreach (var m in level.messages) if (m != null && m.HasChoices) choiceCount++;
        int verdictCount = n - choiceCount;
        string mix = verdictCount == 0 ? $"{n} questions with answer choices (A-D)"
                   : choiceCount == 0 ? $"{n} messages to judge as SCAM or LEGITIMATE"
                   : $"{n} questions: {choiceCount} with answer choices (A-D) and {verdictCount} to judge as SCAM or LEGITIMATE";
        level.briefingText = $"{room.intro}\nYou'll get {mix}. Take your time and read each one carefully.";
        bool last = string.IsNullOrEmpty(level.nextLevelId);
        level.objectives = new[]
        {
            $"Answer all {n} questions.",
            "Read the whole situation before you choose. Stuck? Press HINT.",
            last ? $"Score {level.minScoreToUnlockNext}+ to finish the investigation."
                 : $"Score {level.minScoreToUnlockNext}+ to unlock the next room."
        };
        if (!string.IsNullOrEmpty(room.briefingTitle)) level.briefingTitle = room.briefingTitle;
        if (!string.IsNullOrEmpty(room.briefing)) level.briefingText = room.briefing;
        if (room.objectives != null && room.objectives.Length > 0)
        {
            level.objectives = new string[room.objectives.Length];
            for (int i = 0; i < room.objectives.Length; i++)
                level.objectives[i] = (room.objectives[i] ?? "").Replace("{unlock}", level.minScoreToUnlockNext.ToString()).Replace("{count}", n.ToString());
        }
        level.caseNotes = room.caseNotes ?? new string[0];
        level.lessons = room.lessons ?? new string[0];
        level.learningSummary = room.learningSummary ?? "";

        string notes = level.caseNotes.Length > 0 ? "• " + string.Join("\n• ", level.caseNotes)
                     : "No extra records are needed in this room. Everything you need is in each question.";
        level.caseFiles = new List<CaseFile>
        {
            new CaseFile { title = "Case Notes", body = notes },
            new CaseFile { title = "How This Room Works", body =
                $"1. {n} questions arrive one at a time, in a new order each attempt.\n\n" +
                "2. Case questions: pick the safest, most accurate answer (A-D).\n\n" +
                "3. Messages and web pages: choose SCAM or LEGITIMATE.\n\n" +
                "4. Not sure? Press HINT to see what to look at. After each answer you'll see why it was right or wrong.\n\n" +
                $"Goal: score {level.minScoreToUnlockNext}+ to {(last ? "finish the investigation" : "unlock the next room")}." }
        };
    }
}
