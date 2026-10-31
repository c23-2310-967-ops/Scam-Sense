using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Scoring rules (ScoringSystem): points, penalties, duplicates, counters and the timer text.
/// Uses a throwaway level, never the real level assets, and saves nothing.
/// </summary>
public class ScoringTests
{
    GameObject go;
    ScoringSystem scoring;
    LevelDefinition level;

    [SetUp]
    public void SetUp()
    {
        level = ScriptableObject.CreateInstance<LevelDefinition>();   // default rules: 100 per correct, -10 per wrong
        go = new GameObject("ScoringTest");
        scoring = go.AddComponent<ScoringSystem>();
        scoring.Configure(level);
        scoring.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(level);
    }

    static ScamMessage Msg(string id, MessageVerdict verdict) =>
        new ScamMessage { id = id, channel = MessageChannel.Email, subject = "Test", body = "Test body", correctVerdict = verdict };

    [Test]
    public void CorrectAnswer_Earns100Points()
    {
        Assert.IsTrue(scoring.TryRecord(Msg("a", MessageVerdict.Scam), MessageVerdict.Scam, 5f, out var r));
        Assert.IsTrue(r.isCorrect);
        Assert.AreEqual(100, r.points);
        Assert.AreEqual(100, scoring.TotalScore);
        Assert.AreEqual(1, scoring.CorrectCount);
    }

    [Test]
    public void WrongAnswer_EarnsNothingAndCostsThePenalty()
    {
        scoring.TryRecord(Msg("a", MessageVerdict.Scam), MessageVerdict.Scam, 5f, out _);
        Assert.IsTrue(scoring.TryRecord(Msg("b", MessageVerdict.Scam), MessageVerdict.Legitimate, 5f, out var r));
        Assert.IsFalse(r.isCorrect);
        Assert.AreEqual(0, r.points);
        Assert.AreEqual(level.scoring.wrongPenalty, r.penalty);
        Assert.AreEqual(100 - level.scoring.wrongPenalty, scoring.TotalScore);
    }

    [Test]
    public void ScoreNeverGoesBelowZero()
    {
        scoring.TryRecord(Msg("a", MessageVerdict.Scam), MessageVerdict.Legitimate, 5f, out _);
        scoring.TryRecord(Msg("b", MessageVerdict.Legitimate), MessageVerdict.Scam, 5f, out _);
        Assert.AreEqual(0, scoring.TotalScore);
    }

    [Test]
    public void SameMessageCannotBeScoredTwice()
    {
        var m = Msg("a", MessageVerdict.Scam);
        Assert.IsTrue(scoring.TryRecord(m, MessageVerdict.Scam, 5f, out _));
        Assert.IsFalse(scoring.TryRecord(m, MessageVerdict.Scam, 5f, out _));
        Assert.AreEqual(100, scoring.TotalScore);
        Assert.AreEqual(1, scoring.Results.Count);
    }

    [Test]
    public void MissedScamsAndFalseAlarms_AreCountedSeparately()
    {
        scoring.TryRecord(Msg("scam", MessageVerdict.Scam), MessageVerdict.Legitimate, 5f, out _);   // trusted a scam
        scoring.TryRecord(Msg("real", MessageVerdict.Legitimate), MessageVerdict.Scam, 5f, out _);   // reported a real one
        Assert.AreEqual(1, scoring.MissedRisks);
        Assert.AreEqual(1, scoring.FalseAlarms);
        Assert.AreEqual(2, scoring.IncorrectCount);
    }

    [Test]
    public void AnsweredEvent_FiresOncePerAnswer()
    {
        int fired = 0;
        scoring.AnswerRecorded += _ => fired++;
        var m = Msg("a", MessageVerdict.Scam);
        scoring.TryRecord(m, MessageVerdict.Scam, 5f, out _);
        scoring.TryRecord(m, MessageVerdict.Scam, 5f, out _);
        Assert.AreEqual(1, fired);
    }

    [Test]
    public void ResetRun_ClearsEverything()
    {
        scoring.TryRecord(Msg("a", MessageVerdict.Scam), MessageVerdict.Scam, 5f, out _);
        scoring.ResetRun();
        Assert.AreEqual(0, scoring.TotalScore);
        Assert.AreEqual(0, scoring.Results.Count);
        Assert.IsFalse(scoring.HasAnswered("a"));
    }

    [Test]
    public void SlowCorrectAnswer_NeverDropsBelowTheMinimum()
    {
        var m = Msg("a", MessageVerdict.Scam);
        int points = scoring.PointsFor(m, true, 10000f);
        Assert.GreaterOrEqual(points, level.scoring.minCorrectPoints);
    }

    [TestCase(0f, "0:00")]
    [TestCase(5f, "0:05")]
    [TestCase(65f, "1:05")]
    [TestCase(4.2f, "0:05")]   // rounds up, so a countdown never shows 0:00 early
    public void Clock_FormatsMinutesAndSeconds(float seconds, string expected)
    {
        Assert.AreEqual(expected, ScoringSystem.Clock(seconds));
    }
}
