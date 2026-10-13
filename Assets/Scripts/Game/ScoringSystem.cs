using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Speed-based scoring. A correct answer earns points from the bracket its response time falls in;
/// a wrong answer earns incorrectPoints (0). Each message can only be scored once per run.
/// Brackets are edited in the Inspector. Stars and room unlocking are calculated from the
/// final score by the static helpers at the bottom - they never change the score itself.
/// </summary>
public class ScoringSystem : MonoBehaviour
{
    [Serializable]
    public class SpeedBracket
    {
        [Tooltip("Answers at or under this many seconds earn these points.")]
        public float maxSeconds;
        public int points;

        public SpeedBracket(float maxSeconds, int points)
        {
            this.maxSeconds = maxSeconds;
            this.points = points;
        }
    }

    public struct AnswerResult
    {
        public ScamMessage message;
        public MessageVerdict chosen;
        public bool isCorrect;
        public float seconds;
        public int points;
    }

    [Header("Speed Brackets (correct answers)")]
    [SerializeField] private List<SpeedBracket> brackets = new List<SpeedBracket>
    {
        new SpeedBracket(5f, 100),
        new SpeedBracket(10f, 80),
        new SpeedBracket(15f, 60),
        new SpeedBracket(20f, 40),
    };
    [Tooltip("Correct answers slower than the last bracket.")]
    [SerializeField] private int slowestCorrectPoints = 20;
    [Tooltip("Points for a wrong answer, whatever the time. Never below 0.")]
    [SerializeField] private int incorrectPoints = 0;

    private readonly List<AnswerResult> results = new List<AnswerResult>();
    private readonly HashSet<string> answeredIds = new HashSet<string>();

    public event Action<AnswerResult> AnswerRecorded;

    public IReadOnlyList<AnswerResult> Results => results;
    public int TotalScore { get; private set; }
    public int CorrectCount { get; private set; }
    public int IncorrectCount { get; private set; }
    public float TotalAnswerTime { get; private set; }

    /// <summary>Best possible points for a single message.</summary>
    public int MaxPointsPerMessage
    {
        get
        {
            int best = Mathf.Max(0, slowestCorrectPoints);
            foreach (var b in brackets) best = Mathf.Max(best, b.points);
            return best;
        }
    }

    public int MaxScoreFor(int messageCount) => MaxPointsPerMessage * Mathf.Max(0, messageCount);

    public void ResetRun()
    {
        results.Clear();
        answeredIds.Clear();
        TotalScore = 0;
        CorrectCount = 0;
        IncorrectCount = 0;
        TotalAnswerTime = 0f;
    }

    public bool HasAnswered(string messageId) => answeredIds.Contains(messageId);

    /// <summary>Points a correct answer would earn at this response time (used by the HUD timer).</summary>
    public int PointsFor(bool correct, float seconds)
    {
        if (!correct) return Mathf.Max(0, incorrectPoints);
        foreach (var b in brackets)
            if (seconds <= b.maxSeconds) return Mathf.Max(0, b.points);
        return Mathf.Max(0, slowestCorrectPoints);
    }

    /// <summary>
    /// Scores one answer. Returns false (and scores nothing) if this message was already answered.
    /// </summary>
    public bool TryRecord(ScamMessage message, MessageVerdict chosen, float seconds, out AnswerResult result)
    {
        result = default;
        if (message == null || answeredIds.Contains(message.id)) return false;

        seconds = Mathf.Max(0f, seconds);
        bool correct = chosen == message.correctVerdict;
        result = new AnswerResult
        {
            message = message,
            chosen = chosen,
            isCorrect = correct,
            seconds = seconds,
            points = PointsFor(correct, seconds)
        };

        answeredIds.Add(message.id);
        results.Add(result);
        TotalScore += result.points;
        TotalAnswerTime += seconds;
        if (correct) CorrectCount++; else IncorrectCount++;

        AnswerRecorded?.Invoke(result);
        return true;
    }

    // ---------- Stars and unlocking (separate from scoring on purpose) ----------

    /// <summary>0-3 stars. Below the one-star score is 0 stars.</summary>
    public static int StarsFor(int score, int oneStar, int twoStar, int threeStar)
    {
        if (score >= threeStar) return 3;
        if (score >= twoStar) return 2;
        if (score >= oneStar) return 1;
        return 0;
    }

    public static int StarsFor(int score, LevelDefinition level) =>
        StarsFor(score, level.oneStarScore, level.twoStarScore, level.threeStarScore);

    /// <summary>The next room unlocks on score alone - stars are never checked.</summary>
    public static bool MeetsUnlockScore(int score, int requiredScore) => score >= requiredScore;

    void OnValidate()
    {
        brackets.Sort((a, b) => a.maxSeconds.CompareTo(b.maxSeconds));
        incorrectPoints = Mathf.Max(0, incorrectPoints);
        slowestCorrectPoints = Mathf.Max(0, slowestCorrectPoints);
        foreach (var b in brackets)
        {
            b.maxSeconds = Mathf.Max(0f, b.maxSeconds);
            b.points = Mathf.Max(0, b.points);
        }
    }
}
