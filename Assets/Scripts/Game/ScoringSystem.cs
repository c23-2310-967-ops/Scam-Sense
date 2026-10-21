using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Speed-based scoring. A correct answer earns points from the bracket its response time falls in;
/// a wrong answer earns incorrectPoints (0) and costs a small penalty. Each message can only be scored once per run.
/// A wrong answer can be recovered once (follow-up re-check answered correctly) for a few recovery points.
/// The case evaluation (accuracy, evidence found, hints, grade) is tracked here too; all of it is per run.
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
        /// <summary>Points taken off for this wrong answer (already subtracted from the total).</summary>
        public int penalty;
        /// <summary>A follow-up answer after a mistake (points = recovery points), not a first answer.</summary>
        public bool recovery;
        public bool hintUsed;
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

    [Header("Mistakes & Recovery")]
    [Tooltip("Taken off the total for each wrong answer (once per message; the shown total never goes below 0).")]
    [SerializeField] private int incorrectPenalty = 10;
    [Tooltip("Earned when a wrong answer is corrected in its follow-up re-check (once per message).")]
    [SerializeField] private int recoveryPoints = 20;
    [Tooltip("Recovery points when a hint was used during the re-check.")]
    [SerializeField] private int hintRecoveryPoints = 10;

    private readonly List<AnswerResult> results = new List<AnswerResult>();
    private readonly HashSet<string> answeredIds = new HashSet<string>();
    private readonly HashSet<string> recoveryAttempts = new HashSet<string>();
    private readonly HashSet<string> recoveredIds = new HashSet<string>();
    private readonly HashSet<string> hintIds = new HashSet<string>();
    private readonly Dictionary<string, int> evidenceById = new Dictionary<string, int>();

    public event Action<AnswerResult> AnswerRecorded;

    public IReadOnlyList<AnswerResult> Results => results;
    /// <summary>Points earned (answers + recoveries) minus penalties, never below 0.</summary>
    public int TotalScore => Mathf.Max(0, earned - PenaltyPoints);
    private int earned;
    public int CorrectCount { get; private set; }
    public int IncorrectCount { get; private set; }
    public float TotalAnswerTime { get; private set; }

    // ---------- Case evaluation ----------
    public int PenaltyPoints { get; private set; }
    public int RecoveryPointsEarned { get; private set; }
    public int RecoveredCount => recoveredIds.Count;
    public int HintsUsed => hintIds.Count;
    /// <summary>Scams the player trusted (wrong answers on scam messages).</summary>
    public int MissedRisks { get; private set; }
    /// <summary>Genuine messages the player reported as scams.</summary>
    public int FalseAlarms { get; private set; }
    /// <summary>Key clues discovered in investigated messages (best count per message).</summary>
    public int EvidenceFound { get; private set; }
    /// <summary>First-answer accuracy, 0..1.</summary>
    public float Accuracy => results.Count > 0 ? (float)CorrectCount / results.Count : 0f;
    public int RecoveryPointsFor(bool hintUsed) => Mathf.Max(0, hintUsed ? hintRecoveryPoints : recoveryPoints);
    public int IncorrectPenalty => Mathf.Max(0, incorrectPenalty);

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
        earned = 0;
        CorrectCount = 0;
        IncorrectCount = 0;
        TotalAnswerTime = 0f;
        recoveryAttempts.Clear();
        recoveredIds.Clear();
        hintIds.Clear();
        evidenceById.Clear();
        PenaltyPoints = RecoveryPointsEarned = MissedRisks = FalseAlarms = EvidenceFound = 0;
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
        earned += result.points;
        if (!correct)
        {
            // Small and once per message: one mistake costs a little, it doesn't sink the case.
            result.penalty = Mathf.Max(0, incorrectPenalty);
            PenaltyPoints += result.penalty;
            if (message.correctVerdict == MessageVerdict.Scam) MissedRisks++; else FalseAlarms++;
        }
        results.Add(result);
        TotalAnswerTime += seconds;
        if (correct) CorrectCount++; else IncorrectCount++;

        AnswerRecorded?.Invoke(result);
        return true;
    }

    // ---------- Recovery, hints and evidence ----------

    /// <summary>True if this message was answered wrongly and its one recovery attempt is still unused.</summary>
    public bool CanRecover(string messageId) =>
        answeredIds.Contains(messageId) && !recoveryAttempts.Contains(messageId) && !FirstAnswerCorrect(messageId);

    public bool WasRecovered(string messageId) => recoveredIds.Contains(messageId);

    bool FirstAnswerCorrect(string messageId)
    {
        foreach (var r in results) if (r.message != null && r.message.id == messageId) return r.isCorrect;
        return false;
    }

    /// <summary>Uses up the recovery attempt without answering (the player skipped the follow-up).</summary>
    public void ForfeitRecovery(string messageId) { if (!string.IsNullOrEmpty(messageId)) recoveryAttempts.Add(messageId); }

    /// <summary>
    /// Scores the follow-up answer to a wrongly answered message. Once per message: returns false (and scores
    /// nothing) if there is no recovery attempt left. The first answer's result is never changed.
    /// </summary>
    public bool TryRecordRecovery(ScamMessage message, MessageVerdict chosen, float seconds, bool hintUsed, out AnswerResult result)
    {
        result = default;
        if (message == null || !CanRecover(message.id)) return false;
        recoveryAttempts.Add(message.id);
        bool correct = chosen == message.correctVerdict;
        result = new AnswerResult
        {
            message = message, chosen = chosen, isCorrect = correct, seconds = Mathf.Max(0f, seconds),
            points = correct ? RecoveryPointsFor(hintUsed) : 0, recovery = true, hintUsed = hintUsed
        };
        if (correct)
        {
            recoveredIds.Add(message.id);
            earned += result.points;
            RecoveryPointsEarned += result.points;
        }
        AnswerRecorded?.Invoke(result);
        return true;
    }

    /// <summary>Counts a hint for this message (once per message). Returns true the first time.</summary>
    public bool RecordHint(string messageId) => !string.IsNullOrEmpty(messageId) && hintIds.Add(messageId);
    public bool HintUsedFor(string messageId) => hintIds.Contains(messageId);

    /// <summary>Key clues found while investigating a message; repeated reports keep the best count (no double counting).</summary>
    public void RecordEvidence(string messageId, int keyCluesFound)
    {
        if (string.IsNullOrEmpty(messageId) || keyCluesFound <= 0) return;
        evidenceById.TryGetValue(messageId, out int before);
        if (keyCluesFound <= before) return;
        evidenceById[messageId] = keyCluesFound;
        EvidenceFound += keyCluesFound - before;
    }

    /// <summary>"+80 points", "+0 points  −10 penalty", "+20 recovery points" - the points part of a verdict line.</summary>
    public static string PointsSummary(AnswerResult r)
    {
        string pts = $"<color={UIKit.HexAccent}><b>+{r.points} {(r.recovery ? "recovery points" : "points")}</b></color>";
        if (r.penalty > 0) pts += $"  <color={UIKit.HexDanger}><b>-{r.penalty} penalty</b></color>";
        return pts;
    }

    /// <summary>Case grade A-F: half from the score, half from accuracy (a recovered mistake counts half).</summary>
    public string CaseGrade(int maxScore)
    {
        if (results.Count == 0) return "-";
        float scorePart = maxScore > 0 ? Mathf.Clamp01((float)TotalScore / maxScore) : 0f;
        float accuracyPart = Mathf.Clamp01((CorrectCount + 0.5f * RecoveredCount) / results.Count);
        float k = 0.5f * scorePart + 0.5f * accuracyPart;
        return k >= 0.9f ? "A" : k >= 0.78f ? "B" : k >= 0.65f ? "C" : k >= 0.5f ? "D" : "F";
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
        incorrectPenalty = Mathf.Max(0, incorrectPenalty);
        recoveryPoints = Mathf.Max(0, recoveryPoints);
        hintRecoveryPoints = Mathf.Clamp(hintRecoveryPoints, 0, recoveryPoints);
        slowestCorrectPoints = Mathf.Max(0, slowestCorrectPoints);
        foreach (var b in brackets)
        {
            b.maxSeconds = Mathf.Max(0f, b.maxSeconds);
            b.points = Mathf.Max(0, b.points);
        }
    }
}
