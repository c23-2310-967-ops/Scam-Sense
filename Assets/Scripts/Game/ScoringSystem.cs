using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scores answers with the current level's rules (LevelDefinition.scoring - the one place points are configured).
/// A correct answer earns the level's base points while the player is inside the message's reading allowance
/// (worked out from its length and details, so reading carefully is never punished), a small speed bonus when
/// clearly quick, and a small stepped overtime deduction (capped) after the allowance. A wrong answer earns 0 and
/// costs the level's wrong-answer penalty. Each message is scored once per run; a wrong answer can be recovered once.
/// The case evaluation (accuracy, evidence found, hints, grade) is tracked here too; all of it is per run.
/// Stars and room unlocking are calculated from the final score by the static helpers at the bottom.
/// </summary>
public class ScoringSystem : MonoBehaviour
{
    public struct AnswerResult
    {
        public ScamMessage message;
        public MessageVerdict chosen;
        public bool isCorrect;
        public float seconds;
        /// <summary>Reading allowance this message had (seconds).</summary>
        public float allowance;
        /// <summary>Points earned for this answer (base + speed bonus - overtime for a correct answer).</summary>
        public int points;
        public int speedBonus;
        /// <summary>Overtime deduction already taken off <see cref="points"/>.</summary>
        public int timePenalty;
        /// <summary>Points taken off for this wrong answer (already subtracted from the total).</summary>
        public int penalty;
        /// <summary>A follow-up answer after a mistake (points = recovery points), not a first answer.</summary>
        public bool recovery;
        public bool hintUsed;
    }

    /// <summary>The level's rules (Level 1 defaults until <see cref="Configure"/> is called).</summary>
    public LevelScoring Config { get; private set; } = new LevelScoring();

    /// <summary>Uses this level's scoring rules. Called by GameManager / QuizManager before the run starts.</summary>
    public void Configure(LevelDefinition level)
    {
        Config = level != null && level.scoring != null ? level.scoring : new LevelScoring();
        Config.Validate();
    }

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
    public int RecoveryPointsFor(bool hintUsed) => hintUsed ? Config.recoveryPoints / 2 : Config.recoveryPoints;
    public int IncorrectPenalty => Config.wrongPenalty;
    public bool HasTimePressure => Config.HasTimePressure;

    /// <summary>Best possible points for a single message (base + full speed bonus).</summary>
    public int MaxPointsPerMessage => Config.MaxPointsPerMessage;

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

    // ---------- Reading allowance and points ----------

    /// <summary>
    /// Seconds a player gets to read and investigate this message before overtime starts: the message's own
    /// readingSeconds if set, else base + words x per word + details x per detail (+ extra for a final case), clamped.
    /// </summary>
    public float ReadingAllowance(ScamMessage m)
    {
        var c = Config;
        if (m == null) return c.minReadingSeconds;
        if (m.readingSeconds > 0f) return m.readingSeconds;
        int words = Words(m.sender) + Words(m.senderDetail) + Words(m.subject) + Words(m.body) + Words(m.link);
        int details = (m.inspectables != null ? m.inspectables.Count : 0) + (m.clues != null ? m.clues.Length : 0);
        float t = c.baseReadingSeconds + words * c.secondsPerWord + details * c.secondsPerDetail;
        t = Mathf.Clamp(t, c.minReadingSeconds, c.maxReadingSeconds);
        if (m.channel == MessageChannel.Case) t += c.caseExtraSeconds;
        return Mathf.Round(t);
    }

    static int Words(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int n = 0;
        bool inWord = false;
        foreach (char ch in s)
        {
            bool w = !char.IsWhiteSpace(ch);
            if (w && !inWord) n++;
            inWord = w;
        }
        return n;
    }

    /// <summary>Points for an answer at this response time, with the speed bonus and overtime parts.</summary>
    public int PointsFor(ScamMessage m, bool correct, float seconds, out int bonus, out int timePenalty, out float allowance)
    {
        bonus = timePenalty = 0;
        allowance = ReadingAllowance(m);
        if (!correct) return 0;   // fast and wrong never beats careful and right
        var c = Config;
        seconds = Mathf.Max(0f, seconds);
        if (c.speedBonus > 0)
        {
            // Full bonus inside the fast window, fading to 0 at the end of the allowance - a small extra, never the main score.
            float fast = allowance * c.fastWindow;
            float k = seconds <= fast ? 1f : 1f - Mathf.InverseLerp(fast, allowance, seconds);
            bonus = Mathf.RoundToInt(c.speedBonus * Mathf.Clamp01(k));
        }
        if (seconds > allowance && c.overtimePenaltyPerStep > 0 && c.maxTimePenalty > 0)
        {
            // Stepped and capped: nothing during the first step past the allowance, never a per-second drain.
            int steps = Mathf.FloorToInt((seconds - allowance) / c.overtimeStepSeconds);
            timePenalty = Mathf.Min(c.maxTimePenalty, steps * c.overtimePenaltyPerStep, Mathf.Max(0, c.basePoints - c.minCorrectPoints));
        }
        return Mathf.Max(c.minCorrectPoints, c.basePoints - timePenalty) + bonus;
    }

    /// <summary>Points a correct answer would earn now (used by the presenters' timer line).</summary>
    public int PointsFor(ScamMessage m, bool correct, float seconds) => PointsFor(m, correct, seconds, out _, out _, out _);

    /// <summary>"0:42" - remaining or elapsed time for the timer displays.</summary>
    public static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
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
        int points = PointsFor(message, correct, seconds, out int bonus, out int timePenalty, out float allowance);
        result = new AnswerResult
        {
            message = message,
            chosen = chosen,
            isCorrect = correct,
            seconds = seconds,
            allowance = allowance,
            points = points,
            speedBonus = bonus,
            timePenalty = timePenalty
        };

        answeredIds.Add(message.id);
        earned += result.points;
        if (!correct)
        {
            // Small and once per message: one mistake costs a little, it doesn't sink the case.
            result.penalty = Config.wrongPenalty;
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

    /// <summary>"+165 points (+15 speed bonus)", "+0 points  -25 penalty", "+40 recovery points" - the points part of a verdict line.</summary>
    public static string PointsSummary(AnswerResult r)
    {
        string pts = $"<color={UIKit.HexAccent}><b>+{r.points} {(r.recovery ? "recovery points" : "points")}</b></color>";
        if (r.speedBonus > 0) pts += $" <color={UIKit.HexSecondary}>(+{r.speedBonus} speed bonus)</color>";
        if (r.timePenalty > 0) pts += $" <color={UIKit.HexSecondary}>(-{r.timePenalty} overtime)</color>";
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
}
