using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Data-driven content: the question bank (Assets/Resources/Content/question_bank.json) is what the game plays.
/// Every change below is made to bank DATA only - no gameplay script is edited - and the test checks the level the
/// game would build (LevelContentResolver, the same call GameManager makes) or how ScoringSystem scores it.
/// Nothing is saved: edits are made to in-memory copies of the bank.
/// </summary>
public class QuestionBankTests
{
    static LevelCatalog Catalog => AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset");
    static LevelDefinition Asset(string id) => Catalog.levels.Single(l => l.levelId == id);

    /// <summary>A fresh, editable copy of the bundled bank.</summary>
    static QuestionBankFile Bank() => JsonUtility.FromJson<QuestionBankFile>(File.ReadAllText(QuestionBankTools.BankPath));

    static LoadedQuestionBank Loaded(QuestionBankFile f) =>
        new LoadedQuestionBank { data = f, source = ContentSource.Bundled, issues = QuestionBankValidator.Validate(f) };

    readonly List<Object> made = new List<Object>();

    ResolvedLevel Play(QuestionBankFile bank, string levelId) => Play(Loaded(bank), Asset(levelId));

    ResolvedLevel Play(LoadedQuestionBank bank, LevelDefinition asset)
    {
        var r = LevelContentResolver.Resolve(asset, bank);
        if (r.level != null) made.Add(r.level);
        return r;
    }

    [TearDown]
    public void CleanUp()
    {
        foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
        made.Clear();
        QuestionBankRepository.UseForTesting((LoadedQuestionBank)null);
    }

    static QuestionRecord NewVerdictQuestion(string id, string levelId) => new QuestionRecord
    {
        questionId = id, levelId = levelId, order = 999, isActive = true, category = "Smishing", difficulty = "Easy",
        questionType = "Scam or Legitimate", messageType = "SMS", sender = "+63 900 000 0000",
        message = "Congratulations! You won a phone. Claim it at the link today.", link = "http://prize-claim.example/now",
        isScam = true, explanation = "You didn't join any raffle, and real prizes never need a link and a deadline.",
        feedback = "Scam! An unexpected prize with a rush is bait.", hint = "Did you join any raffle?",
    };

    // ---------- The bundled bank ----------

    [Test]
    public void BundledBank_IsValid()
    {
        var known = Catalog.levels.Select(l => l.levelId).ToList();
        var issues = QuestionBankValidator.Validate(Bank(), known);
        Assert.IsEmpty(issues.Where(i => i.IsError).Select(i => i.ToString()), "the bundled question bank has errors");
        Assert.IsEmpty(issues.Where(i => !i.message.StartsWith("(inactive)")).Select(i => i.ToString()), "the bundled question bank has warnings");
    }

    [Test]
    public void BundledBank_IsWhatTheGameLoads()
    {
        var bank = QuestionBankRepository.Parse(Resources.Load<TextAsset>(QuestionBankRepository.BundledResource).text, ContentSource.Bundled);
        Assert.IsTrue(bank.Usable);
        Assert.AreEqual(QuestionBankFile.CurrentSchemaVersion, bank.data.schemaVersion);
    }

    [Test]
    public void ExistingLevels_PlayExactlyAsBefore()
    {
        // The bank resolves to the same messages, texts and scores as the level assets (the built-in fallback).
        foreach (var asset in Catalog.levels)
        {
            var r = Play(Bank(), asset.levelId);
            Assert.AreEqual(ContentSource.Bundled, r.source, asset.levelId);
            var l = r.level;
            Assert.AreEqual(Json(asset.messages), Json(l.messages), $"{asset.levelId}: messages differ from the level asset - run Tools > Scam Sense > Content > Bake Fallback Content into Levels");
            Assert.AreEqual(Json(asset.inboxExtras), Json(l.inboxExtras), $"{asset.levelId}: inbox differs");
            Assert.AreEqual(asset.briefingText, l.briefingText, asset.levelId);
            CollectionAssert.AreEqual(asset.objectives, l.objectives, asset.levelId);
            CollectionAssert.AreEqual(asset.lessons, l.lessons, asset.levelId);
            CollectionAssert.AreEqual(asset.caseFiles.Select(f => f.title + f.body), l.caseFiles.Select(f => f.title + f.body), asset.levelId);
            Assert.AreEqual(new[] { asset.oneStarScore, asset.twoStarScore, asset.threeStarScore, asset.minScoreToUnlockNext },
                            new[] { l.oneStarScore, l.twoStarScore, l.threeStarScore, l.minScoreToUnlockNext }, asset.levelId + " scores");
            Assert.AreEqual(asset.tutorial, l.tutorial);
            Assert.AreEqual(asset.tutorialFirstQuestion, l.tutorialFirstQuestion);
            // Identity, scene, scoring rules and unlock chain are never touched.
            Assert.AreEqual(asset.sceneName, l.sceneName);
            Assert.AreEqual(asset.nextLevelId, l.nextLevelId);
            Assert.AreEqual(JsonUtility.ToJson(asset.scoring), JsonUtility.ToJson(l.scoring));
        }
    }

    [Test]
    public void Resolving_NeverChangesTheLevelAsset()
    {
        var bank = Bank();
        bank.questions.RemoveAll(q => q.levelId == "level_2" && q.questionId != "E07");
        string before = JsonUtility.ToJson(Asset("level_2"));
        var r = Play(bank, "level_2");
        Assert.AreEqual(1, r.level.MessageCount);
        Assert.AreNotSame(Asset("level_2"), r.level);
        Assert.AreEqual(before, JsonUtility.ToJson(Asset("level_2")));
    }

    // ---------- Migration kept everything ----------

    [Test]
    public void Migration_KeptEveryLegacyQuestion()
    {
        var bank = Bank();
        var legacy = CurriculumQuestionsImporter.Load();
        foreach (var room in legacy.rooms)
            foreach (var q in room.questions)
                AssertSame(q, bank, room.tutorial, Catalog.levels.Single(l => AssetDatabase.GetAssetPath(l) == room.levelAsset).levelId);
        foreach (var q in legacy.reserve) AssertSame(q, bank, false, "");
        Assert.AreEqual(4, bank.questions.Count(q => q.isActive && q.levelId == ""), "4 validated reserve questions");
    }

    static void AssertSame(CurriculumQuestionsImporter.QuestionData q, QuestionBankFile bank, bool tutorial, string levelId)
    {
        var rec = bank.questions.SingleOrDefault(x => x.questionId == q.id);
        Assert.IsNotNull(rec, q.id + " is missing from the bank");
        Assert.AreEqual(levelId, rec.levelId, q.id + " level");
        Assert.IsTrue(rec.isActive, q.id);
        Assert.AreEqual(q.difficulty, rec.difficulty, q.id);
        Assert.AreEqual(q.learningObjective, rec.learningObjective, q.id);
        Assert.IsFalse(string.IsNullOrEmpty(rec.category), q.id + " has no category");
        Assert.AreEqual(JsonUtility.ToJson(CurriculumQuestionsImporter.ToMessage(q, tutorial)), JsonUtility.ToJson(QuestionMapper.ToMessage(rec, tutorial)), q.id + " plays differently");
    }

    [Test]
    public void Migration_KeptTheOldBuilderMessages_Inactive()
    {
        var bank = Bank();
        foreach (var (levelId, fill) in new (string, System.Action<LevelDefinition>)[] { ("level_3", Level3Content.Fill), ("level_4", Level4Content.Fill), ("level_5", Level5Content.Fill) })
        {
            var temp = ScriptableObject.CreateInstance<LevelDefinition>();
            made.Add(temp);
            fill(temp);
            foreach (var m in temp.messages)
            {
                var rec = bank.questions.SingleOrDefault(q => q.questionId == m.id);
                Assert.IsNotNull(rec, m.id + " from the old " + levelId + " builder is missing");
                Assert.IsFalse(rec.isActive, m.id + " must stay inactive until reviewed");
                Assert.AreEqual(JsonUtility.ToJson(m), JsonUtility.ToJson(QuestionMapper.ToMessage(rec, false)), m.id);
            }
        }
    }

    [Test]
    public void Mapper_RoundTripsEveryMessage()
    {
        foreach (var l in Catalog.levels)
            foreach (var m in l.messages.Concat(l.inboxExtras))
            {
                var back = QuestionMapper.ToMessage(QuestionMapper.ToRecord(m, l.levelId, 0, l.tutorial), l.tutorial);
                Assert.AreEqual(JsonUtility.ToJson(m), JsonUtility.ToJson(back), $"{l.levelId}/{m.id}");
            }
    }

    // ---------- The required behaviours (data edits only) ----------

    [Test]
    public void AddingAQuestion_MakesItAppearInTheGame()
    {
        var bank = Bank();
        bank.questions.Add(NewVerdictQuestion("T01", "level_2"));
        var l = Play(bank, "level_2").level;
        Assert.AreEqual(7, l.MessageCount);
        var m = l.messages.Single(x => x.id == "T01");
        Assert.AreEqual("Congratulations! You won a phone. Claim it at the link today.", m.body);
        Assert.AreEqual(MessageTrigger.NearPhone, m.trigger, "default rule: phone messages arrive near the phone");
        // Scores, briefing and the classroom count follow the new size.
        LevelContentRules.ApplyThresholds(ScriptableObjectCopy("level_2", out var expected), 7);
        Assert.AreEqual(expected.minScoreToUnlockNext, l.minScoreToUnlockNext);
        Assert.Greater(l.minScoreToUnlockNext, Asset("level_2").minScoreToUnlockNext);
        StringAssert.Contains("7 questions", l.briefingText);
        StringAssert.Contains("Answer all 7 questions", l.objectives[0]);
        QuestionBankRepository.UseForTesting(bank);
        Assert.AreEqual(7, LevelContentResolver.QuestionCount(Asset("level_2")));
    }

    LevelDefinition ScriptableObjectCopy(string levelId, out LevelDefinition copy)
    {
        copy = Object.Instantiate(Asset(levelId));
        made.Add(copy);
        return copy;
    }

    [Test]
    public void EditingAQuestion_ChangesWhatIsShown()
    {
        var bank = Bank();
        var q = bank.questions.Single(x => x.questionId == "E09");
        q.message = "EDITED: Your laptop has 3 viruses. Call 1-800-555-0100.";
        q.explanation = "EDITED explanation.";
        q.feedback = "EDITED feedback.";
        q.hint = "EDITED hint.";
        // A clue that pointed at the old words must be updated with them (the validator checks this).
        foreach (var c in q.clues.Where(c => c.field == "Body")) c.match = "";
        var m = Play(bank, "level_2").level.messages.Single(x => x.id == "E09");
        Assert.AreEqual(q.message, m.body);
        Assert.AreEqual("EDITED explanation.", m.explanation);
        Assert.AreEqual("EDITED feedback.", m.feedback);
        Assert.AreEqual("EDITED hint.", m.hint);
    }

    [Test]
    public void ChangingTheCorrectAnswer_ChangesScoring()
    {
        var go = new GameObject("ScoringProbe");
        try
        {
            var scoring = go.AddComponent<ScoringSystem>();

            // Choice question: E08's right answer moves to another choice.
            var bank = Bank();
            var q = bank.questions.Single(x => x.questionId == "E08");
            int oldRight = q.correctChoice, newRight = (q.correctChoice + 1) % q.choices.Length;
            var before = Play(bank, "level_2").level;
            q.correctChoice = newRight;
            var after = Play(bank, "level_2").level;

            scoring.Configure(before); scoring.ResetRun();
            Assert.IsTrue(scoring.TryRecordChoice(before.messages.Single(m => m.id == "E08"), oldRight, 5f, out var r1));
            Assert.IsTrue(r1.isCorrect, "before the edit the original answer is right");
            scoring.ResetRun();
            Assert.IsTrue(scoring.TryRecordChoice(after.messages.Single(m => m.id == "E08"), oldRight, 5f, out var r2));
            Assert.IsFalse(r2.isCorrect, "after the edit the original answer is wrong");
            scoring.ResetRun();
            Assert.IsTrue(scoring.TryRecordChoice(after.messages.Single(m => m.id == "E08"), newRight, 5f, out var r3));
            Assert.IsTrue(r3.isCorrect, "after the edit the new answer is right");
            Assert.Greater(r3.points, r2.points);

            // SCAM / LEGITIMATE question: L01 (a real notice) flipped to a scam.
            var b2 = Bank();
            b2.questions.Single(x => x.questionId == "L01").isScam = true;
            var l1 = Play(b2, "level_1").level;
            scoring.Configure(l1); scoring.ResetRun();
            Assert.IsTrue(scoring.TryRecord(l1.messages.Single(m => m.id == "L01"), MessageVerdict.Scam, 5f, out var r4));
            Assert.IsTrue(r4.isCorrect);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void DeactivatingAQuestion_StopsItAppearing()
    {
        var bank = Bank();
        bank.questions.Single(x => x.questionId == "M06").isActive = false;
        var l = Play(bank, "level_3").level;
        Assert.AreEqual(5, l.MessageCount);
        Assert.IsFalse(l.messages.Any(m => m.id == "M06"));
        StringAssert.Contains("5 questions", l.briefingText);
    }

    [Test]
    public void Questions_CanBeAssignedToDifferentLevels()
    {
        var bank = Bank();
        bank.questions.Single(x => x.questionId == "M08").levelId = "level_5";   // move a question
        bank.questions.Single(x => x.questionId == "E04").levelId = "level_2";   // use a reserve question
        var l3 = Play(bank, "level_3").level;
        var l5 = Play(bank, "level_5").level;
        var l2 = Play(bank, "level_2").level;
        Assert.IsFalse(l3.messages.Any(m => m.id == "M08"));
        Assert.IsTrue(l5.messages.Any(m => m.id == "M08"));
        Assert.AreEqual(7, l5.MessageCount);
        Assert.IsTrue(l2.messages.Any(m => m.id == "E04"));
        // The play order follows "order" before the per-attempt shuffle.
        var b2 = Bank();
        b2.questions.Single(x => x.questionId == "M02").order = -1;
        Assert.AreEqual("M02", Play(b2, "level_2").level.messages[0].id);
    }

    [Test]
    public void TutorialSettings_ComeFromTheBank()
    {
        var bank = Bank();
        bank.levels.Single(l => l.levelId == "level_1").firstQuestion = "E02";
        Assert.AreEqual("E02", Play(bank, "level_1").level.tutorialFirstQuestion);
        bank.questions.Single(x => x.questionId == "E02").isActive = false;
        Assert.AreEqual("", Play(bank, "level_1").level.tutorialFirstQuestion, "a guided question that isn't playable is dropped, not crashed on");
    }

    // ---------- Invalid or missing data ----------

    [Test]
    public void Supabase_IsTheFirstContentSource()
    {
        var order = System.Enum.GetValues(typeof(ContentSource)).Cast<ContentSource>().ToList();
        Assert.Less(order.IndexOf(ContentSource.CloudCache), order.IndexOf(ContentSource.LocalOverride));
        Assert.Less(order.IndexOf(ContentSource.LocalOverride), order.IndexOf(ContentSource.Bundled), "the bundled copy is the offline fallback");
        string code = File.ReadAllText("Assets/Scripts/Content/QuestionBankRepository.cs");
        Assert.Less(code.IndexOf("FromFile(CloudCachePath"), code.IndexOf("FromFile(OverridePath"), "the Supabase download is read first");
        Assert.Less(code.IndexOf("FromFile(OverridePath"), code.IndexOf("Resources.Load<TextAsset>(BundledResource)"));
    }

    [Test]
    public void CloudBundle_ParsesLikeTheFile()
    {
        // What get_content_bundle() returns: the file's shape, management columns merged into each question.
        var bank = Bank();
        string bundle = JsonUtility.ToJson(new QuestionBankFile
        {
            schemaVersion = 2, contentVersion = "2026-11-28T09:00:00.000000Z", source = "supabase", categories = bank.categories,
            levels = bank.levels, questions = bank.questions.Where(q => q.isActive && q.levelId != "").ToList(),
        });
        var cloud = QuestionBankRepository.Parse(bundle, ContentSource.CloudCache, "supabase:" + CloudContentSync.RpcName);
        Assert.IsTrue(cloud.Usable, string.Join("\n", cloud.issues));
        foreach (var asset in Catalog.levels)
        {
            var r = Play(cloud, asset);
            Assert.AreEqual(ContentSource.CloudCache, r.source);
            Assert.AreEqual(Json(asset.messages), Json(r.level.messages), asset.levelId);
        }
    }

    [Test]
    public void BrokenFiles_AreRejected_NotFatal()
    {
        Assert.IsFalse(QuestionBankRepository.Parse("{ not json", ContentSource.LocalOverride).Usable);
        Assert.IsFalse(QuestionBankRepository.Parse("", ContentSource.LocalOverride).Usable);
        Assert.IsFalse(QuestionBankRepository.Parse("{\"schemaVersion\":99,\"levels\":[],\"questions\":[]}", ContentSource.LocalOverride).Usable, "a newer format is refused");
        // A rejected bank -> the level plays its built-in questions.
        var broken = QuestionBankRepository.Parse("{ not json", ContentSource.LocalOverride);
        var r = Play(broken, Asset("level_2"));
        Assert.AreEqual(ContentSource.LevelAsset, r.source);
        Assert.AreEqual(Asset("level_2").MessageCount, r.level.MessageCount);
        Assert.IsTrue(r.Playable);
    }

    [Test]
    public void AnInvalidQuestion_IsLeftOut_AndTheRestPlays()
    {
        var bank = Bank();
        bank.questions.Single(x => x.questionId == "E07").correctChoice = 9;
        bank.questions.Single(x => x.questionId == "E08").messageType = "Carrier pigeon";
        bank.questions.Single(x => x.questionId == "E10").explanation = "";
        var r = Play(bank, "level_2");
        Assert.AreEqual(3, r.level.MessageCount);
        Assert.IsTrue(r.issues.Any(i => i.IsError && i.where == "E07" && i.message.Contains("correctChoice")));
        Assert.IsTrue(r.issues.Any(i => i.IsError && i.where == "E08" && i.message.Contains("messageType")));
        Assert.IsTrue(r.issues.Any(i => i.IsError && i.where == "E10" && i.message.Contains("explanation")));
    }

    [Test]
    public void ALevelWithNoPlayableQuestion_UsesItsBuiltInQuestions()
    {
        var bank = Bank();
        foreach (var q in bank.questions.Where(x => x.levelId == "level_4")) q.isActive = false;
        var r = Play(bank, "level_4");
        Assert.AreEqual(ContentSource.LevelAsset, r.source);
        Assert.AreEqual(Json(Asset("level_4").messages), Json(r.level.messages));
        var b2 = Bank();
        b2.levels.RemoveAll(l => l.levelId == "level_4");
        Assert.AreEqual(ContentSource.LevelAsset, Play(b2, "level_4").source, "a level missing from the bank keeps its built-in content");
    }

    [Test]
    public void NoQuestionsAnywhere_IsNotPlayable_SoTheErrorScreenShows()
    {
        var empty = ScriptableObject.CreateInstance<LevelDefinition>();
        made.Add(empty);
        empty.levelId = "level_1";
        empty.messages = new List<ScamMessage>();
        var bank = Bank();
        foreach (var q in bank.questions.Where(x => x.levelId == "level_1")) q.message = "";   // all invalid
        var r = Play(Loaded(bank), empty);
        Assert.IsFalse(r.Playable);
        Assert.IsTrue(r.issues.Any(i => i.IsError && i.message.Contains("no questions")));
    }

    [Test]
    public void ErrorScreen_Builds()
    {
        var go = new GameObject("Owner");
        try
        {
            bool clicked = false;
            var s = ContentErrorScreen.Show(go.transform, "The Investigation", "ERROR E07: correctChoice 9 is out of range", () => clicked = true);
            Assert.IsNotNull(s);
            var texts = s.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Select(t => t.text).ToList();
            Assert.IsTrue(texts.Contains(ContentErrorScreen.Title));
            var button = s.GetComponentInChildren<UnityEngine.UI.Button>(true);
            button.onClick.Invoke();
            Assert.IsTrue(clicked, "MAIN MENU leaves the level");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void MissingFields_UseSafeDefaults()
    {
        // A hand-written question with only the basics: active by default, verdict question, default trigger.
        string json = "{\"schemaVersion\":2,\"levels\":[{\"levelId\":\"level_2\",\"intro\":\"x\"}],\"questions\":[" +
                      "{\"questionId\":\"T02\",\"levelId\":\"level_2\",\"messageType\":\"Email\",\"sender\":\"a@b.example\",\"subject\":\"Hi\"," +
                      "\"message\":\"Hello\",\"isScam\":false,\"explanation\":\"A normal note.\"}]}";
        var bank = QuestionBankRepository.Parse(json, ContentSource.LocalOverride);
        Assert.IsTrue(bank.Usable, string.Join("\n", bank.issues));
        var l = Play(bank, Asset("level_2")).level;
        Assert.AreEqual(1, l.MessageCount);
        var m = l.messages[0];
        Assert.AreEqual(MessageVerdict.Legitimate, m.correctVerdict);
        Assert.AreEqual(MessageTrigger.NearComputer, m.trigger);
        Assert.IsFalse(m.HasChoices);
        Assert.AreEqual(ActivityKind.None, m.activity.kind);
    }

    [Test]
    public void Validator_CatchesAuthoringMistakes()
    {
        var q = NewVerdictQuestion("T03", "level_2");
        Assert.IsTrue(QuestionBankValidator.IsPlayable(q));
        q.choices = new[] { "Only one" };
        Assert.IsFalse(QuestionBankValidator.IsPlayable(q), "one choice");
        q.choices = new[] { "Yes", "Yes" };
        Assert.IsFalse(QuestionBankValidator.IsPlayable(q), "duplicate choices / not a Question type");
        q = NewVerdictQuestion("T03", "level_2");
        q.clues.Add(new ClueRecord { id = "c", field = "Body", match = "not in the text", indicatorId = "x" });
        Assert.IsFalse(QuestionBankValidator.IsPlayable(q), "a clue on words that aren't there");
        q = NewVerdictQuestion("bad id!", "level_2");
        Assert.IsFalse(QuestionBankValidator.IsPlayable(q), "bad id");
        var bank = Bank();
        bank.questions.Add(NewVerdictQuestion("E02", "level_2"));
        Assert.IsTrue(QuestionBankValidator.Validate(bank).Any(i => i.IsError && i.message.Contains("used twice")), "duplicate ids");
        var b3 = Bank();
        b3.questions.Single(x => x.questionId == "E02").hint = "This is a SCAM.";
        Assert.IsTrue(QuestionBankValidator.Validate(b3).Any(i => i.where == "E02" && i.message.Contains("hint")), "a hint giving the answer away");
    }

    // ---------- No gameplay-script edits ----------

    [Test]
    public void GameManager_PlaysTheBankContent()
    {
        var bank = Bank();
        bank.questions.Single(x => x.questionId == "M01").message = "Changed only in the data.";
        bank.questions.Single(x => x.questionId == "M01").clues.ForEach(c => { if (c.field == "Body") c.match = ""; });
        QuestionBankRepository.UseForTesting(bank);
        var go = new GameObject("GM");
        try
        {
            var gm = go.AddComponent<GameManager>();
            var so = new SerializedObject(gm);
            so.FindProperty("level").objectReferenceValue = Asset("level_2");
            so.ApplyModifiedPropertiesWithoutUndo();
            made.Add(gm.Level);
            Assert.AreEqual("Changed only in the data.", gm.Level.messages.Single(m => m.id == "M01").body);
            Assert.AreEqual(ContentSource.LocalOverride, gm.Content.source);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void NoGameplayScript_HardCodesQuestionContent()
    {
        var ids = Bank().questions.Where(q => q.isActive).Select(q => q.questionId).ToList();
        var offenders = new List<string>();
        foreach (var path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            foreach (var id in ids)
                if (Regex.IsMatch(text, "\"" + Regex.Escape(id) + "\"")) offenders.Add($"{path}: \"{id}\"");
        }
        Assert.IsEmpty(offenders, "runtime scripts must not name questions; put the behaviour in the bank data");
    }

    // ---------- Supabase seed ----------

    [Test]
    public void SupabaseSeed_IsUpToDate()
    {
        Assert.IsTrue(File.Exists(QuestionBankTools.SeedPath), QuestionBankTools.SeedPath + " is missing");
        string expected = QuestionBankTools.BuildSeedSql(Bank(), Catalog);
        Assert.AreEqual(expected.Replace("\r", ""), File.ReadAllText(QuestionBankTools.SeedPath).Replace("\r", ""),
            "the Supabase seed is out of date - run Tools > Scam Sense > Content > Export Supabase Seed");
        StringAssert.Contains("on conflict (question_id) do nothing", expected, "the seed never overwrites server edits");
        // The seed drops empty members; every record must read back exactly the same.
        foreach (var q in Bank().questions)
        {
            string full = JsonUtility.ToJson(q);
            Assert.AreEqual(full, JsonUtility.ToJson(JsonUtility.FromJson<QuestionRecord>(QuestionBankTools.Compact(full))), q.questionId);
        }
        foreach (var l in Bank().levels)
        {
            string full = JsonUtility.ToJson(l);
            Assert.AreEqual(full, JsonUtility.ToJson(JsonUtility.FromJson<LevelContentRecord>(QuestionBankTools.Compact(full))), l.levelId);
        }
    }

    [Test]
    public void SupabaseMigration_KeepsRulesInStepWithTheGame()
    {
        string sql = File.ReadAllText(QuestionBankTools.SchemaPath);
        StringAssert.Contains("create or replace function public.get_content_bundle() returns jsonb", sql);
        StringAssert.Contains("'schemaVersion', 2", sql);
        StringAssert.Contains("function public." + CloudContentSync.RpcName + "()", sql, "the game calls this function");
        StringAssert.Contains("ceil(n * 0.6)", sql, "unlock rule must match LevelContentRules.CorrectNeeded");
        StringAssert.IsMatch(@"revoke execute on function public\.get_content_bundle\(\)\s+from public, anon", sql, "signed-out requests get nothing");
        Assert.IsFalse(Regex.IsMatch(sql, @"grant[^;]*(insert|update|all)[^;]*content_admins", RegexOptions.IgnoreCase), "nobody can make themselves an admin from the game");
        // Roles: teachers and admins edit through RLS, only admins delete or manage categories, guests never edit.
        StringAssert.IsMatch(@"function public\.can_edit_content\(\)[\s\S]*?public\.is_content_admin\(\)[\s\S]*?public\.is_teacher\(\)", sql, "teachers and admins may edit");
        StringAssert.IsMatch(@"function public\.is_content_admin\(\)[\s\S]*?is_anonymous", sql, "guests are never admins");
        StringAssert.IsMatch(@"on public\.question_bank for delete to authenticated\s+using \(public\.is_content_admin\(\)\)", sql, "only admins hard-delete questions");
        StringAssert.IsMatch(@"on public\.question_bank for update to authenticated\s+using \(public\.can_edit_content\(\)\)", sql, "teachers can edit questions");
        StringAssert.IsMatch(@"on public\.content_categories for all to authenticated\s+using \(public\.is_content_admin\(\)\)", sql, "only admins manage categories");
        Assert.IsFalse(Regex.IsMatch(sql, @"for (all|insert|update|delete) to (anon|public)", RegexOptions.IgnoreCase), "no write policy for signed-out users");
        foreach (var t in QuestionBankValidator.Difficulties) StringAssert.Contains($"'{t}'", sql);
        foreach (var c in System.Enum.GetNames(typeof(MessageChannel))) StringAssert.Contains($"'{c}'", sql, "message_type check lists every channel");
    }

    static string Json(List<ScamMessage> list) => string.Join("\n", (list ?? new List<ScamMessage>()).Select(m => JsonUtility.ToJson(m)));
}
