using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The teacher question editor's rules (QuestionEditing) and the rows it sends to Supabase (QuestionBankService).
/// Every case ends by asking the game itself (validator + LevelContentResolver) whether the edited question plays.
/// </summary>
public class QuestionEditorTests
{
    static QuestionBankFile Bank() => JsonUtility.FromJson<QuestionBankFile>(File.ReadAllText(QuestionBankTools.BankPath));
    static LevelDefinition Asset(string id) =>
        AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset").levels.Single(l => l.levelId == id);

    [Test]
    public void NewQuestion_NeedsItsText_ThenPlaysInTheLevel()
    {
        var bank = Bank();
        var q = QuestionEditing.NewQuestion(bank, "level_2", false);
        Assert.AreEqual("T01", q.questionId, "first teacher-made id");
        Assert.AreEqual("level_2", q.levelId);
        Assert.Greater(q.order, bank.questions.Where(x => x.levelId == "level_2").Max(x => x.order), "added after the level's last question");
        Assert.IsTrue(QuestionEditing.Check(q, bank, true).Any(i => i.IsError), "an empty template can't be saved");

        q.sender = "+63 900 000 0000";
        q.message = "Your account wins a free phone. Claim it now at the link.";
        q.explanation = "You didn't join any promo; free prizes with a rush are bait.";
        q.feedback = "Scam!"; q.hint = "Did you join a promo?";
        Assert.IsFalse(QuestionEditing.Check(q, bank, true).Any(i => i.IsError), string.Join("\n", QuestionEditing.Check(q, bank, true)));

        bank.questions.Add(q);
        var r = LevelContentResolver.Resolve(Asset("level_2"), new LoadedQuestionBank { data = bank, source = ContentSource.CloudCache, issues = QuestionBankValidator.Validate(bank) });
        try { Assert.IsTrue(r.level.messages.Any(m => m.id == "T01")); Assert.AreEqual(7, r.level.MessageCount); }
        finally { Object.DestroyImmediate(r.level); }
    }

    [Test]
    public void NewIds_AreNeverReused()
    {
        var bank = Bank();
        bank.questions.Add(new QuestionRecord { questionId = "T01" });
        bank.questions.Add(new QuestionRecord { questionId = "t02" });
        Assert.AreEqual("T03", QuestionEditing.NextFreeId(bank), "ids are compared without case");
        var q = QuestionEditing.NewQuestion(bank, "level_1", true);
        q.questionId = "E02";
        Assert.IsTrue(QuestionEditing.Check(q, bank, true).Any(i => i.IsError && i.message.Contains("already used")));
    }

    [Test]
    public void SwitchingAnswerStyle_KeepsTheQuestionValid()
    {
        var bank = Bank();
        var q = QuestionEditing.Copy(bank.questions.Single(x => x.questionId == "E09"));   // a SCAM / LEGIT web page
        QuestionEditing.SetMultipleChoice(q, true);
        Assert.AreEqual("Question", q.messageType, "choice questions use the case-question window");
        Assert.AreEqual(4, q.choices.Length);
        q.choices = new[] { "Close the tab", "Call the number", "", "" };
        q.correctChoice = 0;
        QuestionEditing.TidyChoices(q);
        Assert.AreEqual(2, q.choices.Length, "empty choices are dropped");
        Assert.IsFalse(QuestionEditing.Check(q, bank, false).Any(i => i.IsError), string.Join("\n", QuestionEditing.Check(q, bank, false)));

        var a = QuestionEditing.Copy(bank.questions.Single(x => x.questionId == "E03"));    // a choice question with an Email activity
        QuestionEditing.SetMultipleChoice(a, false);
        Assert.AreEqual("SMS", a.messageType);
        Assert.IsTrue(string.IsNullOrEmpty(a.activity.kind), "activities need choices, so it is removed");
        Assert.AreEqual(0, a.choices.Length);
    }

    [Test]
    public void RemovingAnEmptyChoice_NeverMovesTheRightAnswer()
    {
        var q = new QuestionRecord { messageType = "Question", choices = new[] { "Alpha", "", "Charlie", "Delta" }, correctChoice = 2 };
        QuestionEditing.TidyChoices(q);
        CollectionAssert.AreEqual(new[] { "Alpha", "Charlie", "Delta" }, q.choices);
        Assert.AreEqual("Charlie", q.choices[q.correctChoice]);
    }

    [Test]
    public void EditingWordsAClueUses_IsCaught_AndCanBeRepaired()
    {
        var bank = Bank();
        var q = QuestionEditing.Copy(bank.questions.Single(x => x.questionId == "E02"));
        q.message = q.message.Replace("within 12 hours", "by tonight");
        var issues = QuestionEditing.Check(q, bank, false);
        Assert.IsTrue(QuestionEditing.HasBrokenClues(issues), string.Join("\n", issues));
        Assert.Greater(QuestionEditing.RepairClues(q), 0);
        Assert.IsFalse(QuestionEditing.Check(q, bank, false).Any(i => i.IsError), string.Join("\n", QuestionEditing.Check(q, bank, false)));
        Assert.AreEqual(5, q.clues.Count, "the clue is kept (whole field), not deleted");
    }

    [Test]
    public void ChangingTheCorrectAnswer_InTheEditor_ChangesScoring()
    {
        var bank = Bank();
        var q = QuestionEditing.Copy(bank.questions.Single(x => x.questionId == "M06"));
        int before = q.correctChoice;
        q.correctChoice = (before + 1) % q.choices.Length;
        var go = new GameObject("Scoring");
        try
        {
            var scoring = go.AddComponent<ScoringSystem>();
            scoring.Configure(Asset("level_3")); scoring.ResetRun();
            var m = QuestionMapper.ToMessage(q, false);
            Assert.IsTrue(scoring.TryRecordChoice(m, q.correctChoice, 5f, out var r));
            Assert.IsTrue(r.isCorrect, "the newly marked choice scores as right");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ---------- The row sent to Supabase ----------

    [Test]
    public void RowJson_HasTheColumns_AndTheRecordAsContent()
    {
        var q = QuestionEditing.Copy(Bank().questions.Single(x => x.questionId == "E07"));
        string row = QuestionEditing.ToRowJson(q);
        StringAssert.Contains("\"question_id\":\"E07\"", row);
        StringAssert.Contains("\"level_id\":\"level_2\"", row);
        StringAssert.Contains("\"is_active\":true", row);
        StringAssert.Contains("\"message_type\":\"Question\"", row);
        StringAssert.DoesNotContain("\"question_id\"", QuestionEditing.ToRowJson(q, includeId: false), "updates never change the id");

        // What the server does in get_content_bundle(): content + the columns. It must give the same record back.
        string content = QuestionEditing.ContentJson(q);
        StringAssert.DoesNotContain("\"questionId\"", content);
        StringAssert.DoesNotContain("\"isActive\"", content);
        var back = JsonUtility.FromJson<QuestionRecord>(content);
        back.questionId = q.questionId; back.levelId = q.levelId; back.order = q.order; back.isActive = q.isActive;
        back.category = q.category; back.difficulty = q.difficulty; back.questionType = q.questionType; back.messageType = q.messageType;
        Assert.AreEqual(JsonUtility.ToJson(q), JsonUtility.ToJson(back));
    }

    [Test]
    public void RowJson_ReserveQuestionsHaveNoLevel_AndTextIsEscaped()
    {
        var q = new QuestionRecord { questionId = "T09", levelId = "", messageType = "SMS", message = "Line 1\nShe said \"pay\" \\ now\t!", explanation = "x" };
        string row = QuestionEditing.ToRowJson(q);
        StringAssert.Contains("\"level_id\":null", row, "a reserve question has no level (the column is a foreign key)");
        var parsed = JsonUtility.FromJson<QuestionRecord>(Regex.Match(row, "\"content\":(\\{.*\\})\\}$").Groups[1].Value);
        Assert.AreEqual(q.message, parsed.message);
        Assert.AreEqual("\"a\\\"b\\\\c\\n\"", QuestionEditing.Str("a\"b\\c\n"));
    }

    [Test]
    public void Editor_SourcesUseRlsNotAServiceKey()
    {
        string svc = File.ReadAllText("Assets/Scripts/Online/QuestionBankService.cs");
        StringAssert.Contains("Svc.Client", svc, "writes use the player's own sign-in");
        Assert.IsFalse(Regex.IsMatch(svc, "service_role|serviceKey|secret", RegexOptions.IgnoreCase), "no admin key in the game");
        string ui = File.ReadAllText("Assets/Scripts/UI/ClassroomUI.Questions.cs");
        StringAssert.Contains("QuestionEditing.Check(", ui, "the editor checks with the game's validator before saving");
        StringAssert.IsMatch(@"if \(qAdmin\)\s*\{[^}]*""DeleteQuestion""", ui, "DELETE is offered to content admins only");
    }
}
