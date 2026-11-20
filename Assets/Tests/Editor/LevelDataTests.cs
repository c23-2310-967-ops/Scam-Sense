using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Every room's level data (Assets/Levels): scenes exist in the build, messages are complete, ids are unique
/// and the scores needed for stars and the next room can actually be reached. Read-only.
/// </summary>
public class LevelDataTests
{
    static LevelCatalog Catalog => AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset");

    static IEnumerable<LevelDefinition> Levels()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets/Levels" }))
            yield return AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
    }

    static HashSet<string> BuildScenes() => new HashSet<string>(
        EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)));

    static int MaxScore(LevelDefinition l) => (l.scoring.basePoints + l.scoring.speedBonus) * l.MessageCount;

    [Test]
    public void Catalog_ListsEveryRoomOnce()
    {
        Assert.IsNotNull(Catalog, "Assets/Levels/LevelCatalog.asset is missing");
        Assert.IsFalse(Catalog.levels.Any(l => l == null), "The catalog has an empty slot");
        var ids = Catalog.levels.Select(l => l.levelId).ToList();
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Two rooms share a levelId: " + string.Join(", ", ids));
        foreach (var level in Levels())
            Assert.Contains(level, Catalog.levels, $"{level.name} is not in the level catalog, so the menu cannot open it");
    }

    [Test]
    public void EveryRoom_SceneIsInTheBuild()
    {
        var scenes = BuildScenes();
        foreach (var level in Levels())
        {
            Assert.IsTrue(scenes.Contains(level.sceneName), $"{level.name}: scene '{level.sceneName}' is not in the Build Profile scene list");
            if (!string.IsNullOrEmpty(level.nextSceneName))
                Assert.IsTrue(scenes.Contains(level.nextSceneName), $"{level.name}: next scene '{level.nextSceneName}' is not in the build");
        }
    }

    [Test]
    public void EveryRoom_HasMessages()
    {
        foreach (var level in Levels())
            Assert.Greater(level.MessageCount, 0, $"{level.name} has no messages");
    }

    [Test]
    public void MessageIds_AreUniqueWithinARoom()
    {
        foreach (var level in Levels())
        {
            var ids = level.messages.Concat(level.inboxExtras ?? new List<ScamMessage>()).Select(m => m.id).ToList();
            var dupes = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(dupes, $"{level.name}: duplicate message ids (answers would be mixed up): {string.Join(", ", dupes)}");
        }
    }

    [Test]
    public void EveryMessage_IsComplete()
    {
        var problems = new List<string>();
        foreach (var level in Levels())
            foreach (var m in level.messages)
            {
                string where = $"{level.name} / {m.id}";
                if (string.IsNullOrWhiteSpace(m.id)) problems.Add($"{where}: no id");
                if (string.IsNullOrWhiteSpace(m.body)) problems.Add($"{where}: no body text");
                if (string.IsNullOrWhiteSpace(m.explanation)) problems.Add($"{where}: no explanation for the feedback screen");
                if (MessageChannels.OnComputer(m.channel) && string.IsNullOrWhiteSpace(m.subject)) problems.Add($"{where}: email/web page with no subject");
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void EveryRoom_HasBothScamsAndRealMessages()
    {
        foreach (var level in Levels())
        {
            // Only for rooms judged purely SCAM / LEGITIMATE: in a room with choice questions, answering SCAM
            // every time cannot win anyway.
            if (level.messages.Any(m => m.HasChoices)) continue;
            var cased = level.messages.Where(m => m.channel != MessageChannel.Case).ToList();
            if (cased.Count < 2) continue;
            Assert.IsTrue(cased.Any(m => m.correctVerdict == MessageVerdict.Scam), $"{level.name}: no scam messages");
            Assert.IsTrue(cased.Any(m => m.correctVerdict == MessageVerdict.Legitimate), $"{level.name}: no legitimate messages - answering SCAM every time would win");
        }
    }

    // ---------- Curriculum questions (5 rooms x 6 questions) ----------

    [Test]
    public void EveryRoom_HasSixQuestions()
    {
        foreach (var level in Levels())
            Assert.AreEqual(CurriculumQuestionsImporter.QuestionsPerRoom, level.MessageCount, $"{level.name} must have exactly six questions");
    }

    [Test]
    public void ThirtyQuestions_EachInExactlyOneRoom()
    {
        var ids = Levels().SelectMany(l => l.messages.Select(m => m.id)).ToList();
        Assert.AreEqual(30, ids.Count, "The five rooms must hold 30 questions in total");
        var dupes = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.IsEmpty(dupes, "A question appears in more than one room: " + string.Join(", ", dupes));
    }

    [Test]
    public void Rooms_MatchTheValidatedQuestionBank()
    {
        var data = CurriculumQuestionsImporter.Load();
        Assert.AreEqual(5, data.rooms.Length, "The question data must define five rooms");
        foreach (var room in data.rooms)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(room.levelAsset);
            Assert.IsNotNull(level, room.levelAsset + " is missing");
            CollectionAssert.AreEqual(room.questions.Select(q => q.id).ToList(), level.messages.Select(m => m.id).ToList(),
                $"{level.name} is out of date with {CurriculumQuestionsImporter.JsonPath} - run Tools > Scam Sense > Curriculum > Load Questions into Rooms");
            for (int i = 0; i < room.questions.Length; i++)
            {
                var q = room.questions[i]; var m = level.messages[i];
                Assert.AreEqual(q.body, m.body, $"{level.name} / {m.id}: question text differs from the question bank");
                Assert.AreEqual(q.correctChoice, m.correctChoice, $"{level.name} / {m.id}: correct answer differs from the question bank");
                CollectionAssert.AreEqual(q.choices ?? new string[0], m.choices ?? new string[0], $"{level.name} / {m.id}: choices differ from the question bank");
            }
        }
    }

    [Test]
    public void ChoiceQuestions_AreWellFormed()
    {
        var problems = new List<string>();
        foreach (var level in Levels())
            foreach (var m in level.messages.Where(m => m.HasChoices))
            {
                string where = $"{level.name} / {m.id}";
                if (m.choices.Length != 4) problems.Add($"{where}: {m.choices.Length} choices (expected 4)");
                if (m.correctChoice < 0 || m.correctChoice >= m.choices.Length) problems.Add($"{where}: correct choice {m.correctChoice} is out of range");
                if (m.choices.Any(string.IsNullOrWhiteSpace)) problems.Add($"{where}: an empty choice");
                if (m.choices.Distinct().Count() != m.choices.Length) problems.Add($"{where}: two identical choices");
                // Letters are added on screen by position (the order is shuffled), so the data must not carry them.
                if (m.choices.Any(c => System.Text.RegularExpressions.Regex.IsMatch(c, @"^\s*[A-D][.)]\s"))) problems.Add($"{where}: a choice starts with a letter label");
                if (m.channel != MessageChannel.Question) problems.Add($"{where}: choice questions use the Question channel");
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void StarAndUnlockScores_AreReachable()
    {
        foreach (var level in Levels())
        {
            int max = MaxScore(level);
            Assert.LessOrEqual(level.oneStarScore, level.twoStarScore, $"{level.name}: 1-star score is above 2-star");
            Assert.LessOrEqual(level.twoStarScore, level.threeStarScore, $"{level.name}: 2-star score is above 3-star");
            Assert.LessOrEqual(level.threeStarScore, max, $"{level.name}: 3 stars needs {level.threeStarScore} but the best possible is {max}");
            Assert.LessOrEqual(level.minScoreToUnlockNext, max, $"{level.name}: the next room needs {level.minScoreToUnlockNext} but the best possible is {max}");
        }
    }
}
