using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Server score check: the server recalculates each run from the answers the game sends, using its own copy of the
/// rules and answer key (supabase/migrations/*_scoring_key.sql). These tests keep that copy in step with the Level
/// assets and check the answers are sent in the shape submit_game_result expects. They never contact Supabase.
/// </summary>
public class ServerScoringTests
{
    GameObject go;
    ScoringSystem scoring;
    LevelDefinition level;

    [SetUp]
    public void SetUp()
    {
        level = ScriptableObject.CreateInstance<LevelDefinition>();
        go = new GameObject("ServerScoringTest");
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
    public void LatestScoringKey_MatchesLevelAssets()
    {
        string latest = ServerScoringKey.LatestKeyFile();
        Assert.IsNotNull(latest, "No supabase/migrations/*_scoring_key.sql - run Tools > Scam Sense > Export Server Scoring Key");
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ServerScoringKey.CatalogPath);
        Assert.AreEqual(ServerScoringKey.Body(ServerScoringKey.Generate(catalog)), ServerScoringKey.Body(File.ReadAllText(latest)),
            "A level's messages or scoring changed: run Tools > Scam Sense > Export Server Scoring Key and apply the new migration, " +
            "or the server will score runs with the old rules");
    }

    [Test]
    public void AnswersFrom_ListsEachFirstAnswerAndItsRecheck()
    {
        var a = Msg("a", MessageVerdict.Scam);
        var b = Msg("b", MessageVerdict.Legitimate);
        scoring.TryRecord(a, MessageVerdict.Scam, 12.5f, out _);
        scoring.TryRecord(b, MessageVerdict.Scam, 30f, out _);
        scoring.TryRecordRecovery(b, MessageVerdict.Legitimate, 4f, true, out _);

        var answers = GameResultLog.AnswersFrom(scoring);
        Assert.AreEqual(2, answers.Count);
        Assert.AreEqual("a", answers[0].id);
        Assert.AreEqual("scam", answers[0].verdict);
        Assert.AreEqual(12.5f, answers[0].seconds);
        Assert.AreEqual("", answers[0].recovery_verdict, "A correct answer has no re-check");
        Assert.AreEqual("scam", answers[1].verdict);
        Assert.AreEqual("legit", answers[1].recovery_verdict);
        Assert.IsTrue(answers[1].recovery_hint);
    }

    [Test]
    public void SubmitJson_UsesTheFunctionArgumentNames()
    {
        scoring.TryRecord(Msg("a", MessageVerdict.Scam), MessageVerdict.Legitimate, 3f, out _);
        var row = new GameResultLog.Row
        {
            run_id = "00000000-0000-0000-0000-000000000001", level_id = "level_1",
            completed_at = "2026-11-04T00:00:00Z", answers = GameResultLog.AnswersFrom(scoring)
        };
        string json = GameResultLog.SubmitJson(row);
        StringAssert.Contains("\"p_run_id\":\"00000000-0000-0000-0000-000000000001\"", json);
        StringAssert.Contains("\"p_level_id\":\"level_1\"", json);
        StringAssert.Contains("\"p_answers\":[{\"id\":\"a\",\"verdict\":\"legit\",\"seconds\":3.0", json);
        StringAssert.Contains("\"p_completed_at\":\"2026-11-04T00:00:00Z\"", json);
        StringAssert.DoesNotContain("\"score\"", json, "The game must not send its own score");
    }
}
