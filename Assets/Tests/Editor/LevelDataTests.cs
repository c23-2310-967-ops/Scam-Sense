using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

    // ---------- Curriculum questions (layout: CurriculumRoomLayout) ----------

    [Test]
    public void EveryRoom_HasAReasonableNumberOfQuestions()
    {
        foreach (var level in Levels())
        {
            Assert.GreaterOrEqual(level.MessageCount, CurriculumQuestionsImporter.MinQuestionsPerRoom, $"{level.name} has too few questions");
            Assert.LessOrEqual(level.MessageCount, CurriculumQuestionsImporter.MaxQuestionsPerRoom, $"{level.name} is too long");
        }
    }

    [TestCase(2, 1, 1)]
    [TestCase(3, 1, 2)]
    [TestCase(4, 2, 3)]
    [TestCase(6, 3, 4)]
    [TestCase(8, 4, 5)]
    public void CorrectAnswersNeeded_ScaleWithRoomSize(int questions, int oneStar, int unlock)
    {
        var (k1, k2) = CurriculumQuestionsImporter.CorrectNeeded(questions);
        Assert.AreEqual(oneStar, k1, "1 star");
        Assert.AreEqual(unlock, k2, "unlock: about 60%, never every question");
    }

    [Test]
    public void EveryQuestion_IsInAtMostOneRoom()
    {
        var ids = Levels().SelectMany(l => l.messages.Select(m => m.id)).ToList();
        var dupes = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.IsEmpty(dupes, "A question appears in more than one room: " + string.Join(", ", dupes));
    }

    [Test]
    public void Level1_IsAShortIntroduction_TwoScamsTwoReal()
    {
        var level = Levels().Single(l => l.levelId == "level_1");
        Assert.AreEqual(4, level.MessageCount, "Level 1 has 4 messages");
        Assert.AreEqual(2, level.messages.Count(m => m.correctVerdict == MessageVerdict.Legitimate && !m.HasChoices), "2 legitimate messages");
        Assert.AreEqual(2, level.messages.Count(m => m.correctVerdict == MessageVerdict.Scam), "2 scams");
        Assert.IsTrue(level.messages.All(m => !string.IsNullOrWhiteSpace(m.explanation) && !string.IsNullOrWhiteSpace(m.feedback)), "every message explains its answer");
        Assert.IsFalse(level.inboxExtras.Any(m => m.subject != null && m.subject.Contains("desk lamp")),
                       "the old receipt contradicted the case note 'You have not ordered anything this month'");
        foreach (var l in Levels())
            Assert.AreEqual(l.lessons.Length, l.lessons.Distinct().Count(), $"{l.name}: 'What you learned' lists the same lesson twice");
        var sizes = Levels().Where(l => l.levelId != "level_1").Select(l => l.MessageCount);
        Assert.IsTrue(sizes.All(n => n > level.MessageCount), "the introduction is the shortest room");
    }

    [Test]
    public void Rooms_MatchTheValidatedQuestionBank()
    {
        var data = CurriculumQuestionsImporter.Load();
        Assert.AreEqual(Catalog.levels.Count, data.rooms.Length, "The question data must define one room per level in the catalog");
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

    // ---------- Level 1 tutorial (must never leak into other rooms) ----------

    [Test]
    public void OnlyLevel1_IsTheTutorial()
    {
        foreach (var level in Levels())
        {
            bool first = level.levelId == "level_1";
            Assert.AreEqual(first, level.tutorial, $"{level.name}: only Level 1 may run the tutorial");
            if (first) continue;
            Assert.IsEmpty(level.tutorialFirstQuestion ?? "", $"{level.name}: a guided first question belongs to Level 1 only");
        }
    }

    // ---------- Beginner support ----------

    [Test]
    public void EveryQuestion_HasAShortHint_ThatNeverGivesTheAnswer()
    {
        var problems = new List<string>();
        foreach (var level in Levels())
            foreach (var m in level.messages)
            {
                string where = $"{level.name} / {m.id}";
                if (!m.HasHint) { problems.Add($"{where}: no hint"); continue; }
                if (m.hint.Length > 140) problems.Add($"{where}: hint is {m.hint.Length} characters (keep it to one short line)");
                if (m.HasChoices && m.choices.Any(c => m.hint.IndexOf(UIKit.StripRichText(c), System.StringComparison.OrdinalIgnoreCase) >= 0))
                    problems.Add($"{where}: the hint quotes an answer choice");
                foreach (var word in new[] { "SCAM", "LEGITIMATE", "correct answer", "the answer is" })
                    if (m.hint.IndexOf(word, System.StringComparison.Ordinal) >= 0) problems.Add($"{where}: the hint says '{word}'");
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void Difficulty_RisesGently()
    {
        var rooms = Catalog.levels;
        var data = CurriculumQuestionsImporter.Load();
        float Level(string d) => d == "Hard" ? 3f : d == "Medium" ? 2f : 1f;
        float prevAvg = 0f;
        for (int i = 0; i < rooms.Count; i++)
        {
            var l = rooms[i];
            if (i > 0)
            {
                Assert.GreaterOrEqual(l.scoring.wrongPenalty, rooms[i - 1].scoring.wrongPenalty, $"{l.name}: wrong answers cost less than in the room before");
                Assert.GreaterOrEqual(l.scoring.basePoints, rooms[i - 1].scoring.basePoints, $"{l.name}: fewer points than the room before");
            }
            if (i < 2) Assert.IsFalse(l.scoring.HasTimePressure, $"{l.name}: the first two rooms have no time pressure");
            Assert.LessOrEqual(l.scoring.wrongPenalty, l.scoring.basePoints / 5, $"{l.name}: a mistake shouldn't cost more than a fifth of a right answer");
            var room = data.rooms.First(r => r.levelAsset == AssetDatabase.GetAssetPath(l));
            float avg = room.questions.Average(q => Level(q.difficulty));
            Assert.GreaterOrEqual(avg, prevAvg, $"{l.name}: easier on average than the room before");
            prevAvg = avg;
            Assert.IsFalse(string.IsNullOrWhiteSpace(room.intro), $"{l.name}: the briefing must say what the room teaches");
        }
    }

    [Test]
    public void Level1_EveryQuestionHasAHintAndValidClues()
    {
        var level = Levels().Single(l => l.levelId == "level_1");
        Assert.IsTrue(level.messages.Any(m => m.id == level.tutorialFirstQuestion), "the guided first question is missing from Level 1");
        var problems = new List<string>();
        foreach (var m in level.messages)
        {
            if (!m.HasHint) problems.Add($"{m.id}: no hint");
            if (!m.HasInvestigation) problems.Add($"{m.id}: no underlined clues");
            problems.AddRange(InvestigationSession.Validate(m).Select(p => $"{m.id}: {p}"));
            if (m.HasChoices && m.HasHint && m.choices.Any(c => m.hint.IndexOf(c, System.StringComparison.OrdinalIgnoreCase) >= 0))
                problems.Add($"{m.id}: the hint quotes an answer choice");
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    // ---------- Interactive activities ----------

    static readonly string[] CurriculumIds =
        Enumerable.Range(1, 10).Select(i => $"E{i:00}").Concat(Enumerable.Range(1, 10).Select(i => $"M{i:00}"))
                  .Concat(Enumerable.Range(1, 10).Select(i => $"H{i:00}")).ToArray();

    static List<ScamMessage> CurriculumMessages() =>
        Levels().SelectMany(l => l.messages).Where(m => CurriculumIds.Contains(m.id)).ToList();

    [Test]
    public void All30CurriculumQuestions_AreInARoomOrTheReserve_Once()
    {
        var reserve = (CurriculumQuestionsImporter.Load().reserve ?? new CurriculumQuestionsImporter.QuestionData[0]).Select(q => q.id);
        var ids = CurriculumMessages().Select(m => m.id).Concat(reserve).ToList();
        CollectionAssert.AreEquivalent(CurriculumIds, ids, "a validated question is missing, or is both in a room and in the reserve");
    }

    [Test]
    public void Activities_AreValidAndFair()
    {
        var problems = new List<string>();
        int count = 0;
        foreach (var m in CurriculumMessages())
        {
            problems.AddRange(InvestigationSession.Validate(m).Select(p => $"{m.id}: {p}"));
            if (!m.HasActivity) continue;
            count++;
            var a = m.activity;
            if (!m.HasChoices) problems.Add($"{m.id}: activities are shown with the case-question window (choice questions only)");
            if (!MessageInvestigation.Investigates(m)) problems.Add($"{m.id}: activity has nothing to inspect");
            if (a.items.Select(i => i.key).Distinct().Count() != a.items.Count) problems.Add($"{m.id}: repeated item key");
            int keyClues = m.evidence.Count(e => e.supportsVerdict);
            if (a.minClues > keyClues) problems.Add($"{m.id}: needs {a.minClues} key clues but only has {keyClues}");
            if (a.minClues > 2) problems.Add($"{m.id}: more than 2 clues to unlock is too strict");
            if (a.HasOutcomes && a.outcomes.Length != m.choices.Length) problems.Add($"{m.id}: one outcome per choice is required");
            if (a.HasOutcomes && a.outcomes.Any(string.IsNullOrWhiteSpace)) problems.Add($"{m.id}: empty outcome");
            foreach (var i in m.inspectables.Where(i => i.field == InspectField.Activity))
                if (a.Item(i.part) == null) problems.Add($"{m.id}: clue '{i.id}' points to missing item '{i.part}'");
            // Simulated only: fictional .example addresses, never a real password field.
            foreach (var item in a.items)
                foreach (var word in item.text.Split(' ', '<', '>', '"', '(', ')'))
                    if ((word.Contains("://") || word.Contains("@") || System.Text.RegularExpressions.Regex.IsMatch(word, @"\.(com|net|org|ph|edu)\b"))
                        && !word.Contains(".example"))
                        problems.Add($"{m.id}/{item.key}: '{word}' is not a fictional .example address");
        }
        Assert.GreaterOrEqual(count, 15, "most scenario questions should be played as activities");
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void Activities_AreSpreadAcrossEveryRoom()
    {
        foreach (var level in Levels().Where(l => l.messages.Any(m => CurriculumIds.Contains(m.id))))
            Assert.GreaterOrEqual(level.messages.Count(m => m.HasActivity || (m.HasInvestigation && !m.HasChoices)), Mathf.Min(3, (level.MessageCount + 1) / 2),
                $"{level.name}: at least half of a room's questions (up to three) should be interactive investigations");
    }

    [Test]
    public void Activities_UnlockGentlyInEarlyRooms()
    {
        var level1 = Levels().Single(l => l.levelId == "level_1");
        foreach (var m in level1.messages.Where(m => m.HasActivity))
            Assert.LessOrEqual(m.activity.minClues, 1, $"{m.id}: Level 1 is for beginners - at most one clue to unlock");
        var guided = level1.messages.Single(m => m.id == level1.tutorialFirstQuestion);
        Assert.AreEqual(0, guided.HasActivity ? guided.activity.minClues : 0, "the guided tutorial question must never lock its answers");
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
