using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Admin / Teacher / Student question management (supabase/migrations/20261128101505_question_requests.sql):
/// teachers can only send requests, admins alone change the published questions, students can do neither -
/// checked in the SQL (the real enforcement) and in what the game sends and reads.
/// </summary>
public class QuestionRequestTests
{
    const string Path = "supabase/migrations/20261128101505_question_requests.sql";
    static string Sql => File.ReadAllText(Path);
    static string Fn(string name) => Regex.Match(Sql, @"function public\." + name + @"\([\s\S]*?end \$\$;").Value;
    static QuestionBankFile Bank() => JsonUtility.FromJson<QuestionBankFile>(File.ReadAllText(QuestionBankTools.BankPath));

    // ---------- Server rules ----------

    [Test]
    public void PublishedQuestions_OnlyAdminsWrite()
    {
        string sql = Sql;
        StringAssert.Contains("drop policy if exists \"question_bank: editors insert\"", sql, "the teacher insert policy is removed");
        StringAssert.Contains("drop policy if exists \"question_bank: editors update\"", sql, "the teacher update policy is removed");
        StringAssert.IsMatch(@"""question_bank: admins insert"" on public\.question_bank for insert to authenticated\s+with check \(public\.is_content_admin\(\)\)", sql);
        StringAssert.IsMatch(@"""question_bank: admins update"" on public\.question_bank for update to authenticated\s+using \(public\.is_content_admin\(\)\)", sql);
        StringAssert.IsMatch(@"""content_levels: admins update""[\s\S]*?is_content_admin", sql);
    }

    [Test]
    public void Teachers_OnlySubmitRequests()
    {
        string submit = Fn("submit_question_request");
        StringAssert.Contains("not public.is_teacher()", submit, "students and guests are refused");
        StringAssert.Contains("is_anonymous", submit);
        StringAssert.Contains("insert into public.question_requests", submit, "a request is stored, not applied");
        StringAssert.DoesNotContain("question_bank (", submit, "submitting never writes the published questions");
        StringAssert.DoesNotContain("update public.question_bank", submit);
        StringAssert.Contains("char_length(btrim(coalesce(p_reason, ''))) < 5", submit, "a reason is required");
        StringAssert.Contains("question_row_problem(p_proposed)", submit, "proposals are checked when sent");
        Assert.IsFalse(Regex.IsMatch(Sql, @"grant\s+(insert|update|delete|all)[^;]*question_requests", RegexOptions.IgnoreCase), "rows change only through the functions");
        StringAssert.Contains("where status = 'pending'", Sql, "one pending request per teacher per question");
    }

    [Test]
    public void OnlyAdmins_ReviewAndReadHistory()
    {
        foreach (var name in new[] { "review_question_request", "admin_question_requests", "question_history" })
            StringAssert.IsMatch(@"if not public\.is_content_admin\(\) then raise exception", Fn(name), name + " must refuse non-admins");
        string review = Fn("review_question_request");
        StringAssert.Contains("Add a remark so the teacher knows why it was rejected", review, "rejections need remarks");
        StringAssert.Contains("apply_question_row(r.question_id, r.proposed, true)", review, "approving an add publishes it");
        StringAssert.Contains("delete from public.question_bank where question_id = r.question_id", review, "approving a delete removes it");
        StringAssert.Contains("if r.status <> 'pending'", review, "a request is decided once");
        StringAssert.IsMatch(@"revoke execute on function public\.apply_question_row\(text, jsonb, boolean\)\s+from public, anon, authenticated", Sql,
            "the apply helper can't be called from the game directly");
    }

    [Test]
    public void SignedOutCallers_CantReachAnything()
    {
        foreach (var sig in new[] { "submit_question_request(text, text, jsonb, text)", "withdraw_question_request(uuid)", "my_question_requests()",
                                    "admin_question_requests(text)", "review_question_request(uuid, boolean, text)", "question_history(text)" })
            StringAssert.IsMatch(@"revoke execute on function public\." + Regex.Escape(sig) + @"\s+from public, anon", Sql, sig);
    }

    // ---------- What the game sends and reads ----------

    [Test]
    public void ARequest_RoundTripsTheProposedQuestion()
    {
        var q = Bank().questions.Single(x => x.questionId == "E07");
        q.correctChoice = 2;
        string proposed = QuestionEditing.ToRowJson(q, includeId: false);
        string json = "[{\"id\":\"r1\",\"teacher_name\":\"Ms. Santos\",\"request_type\":\"edit\",\"question_id\":\"E07\",\"reason\":\"Choice C is the better answer\"," +
                      "\"status\":\"pending\",\"admin_remarks\":\"\",\"changed_since\":true,\"proposed\":" + proposed + ",\"current\":null}," +
                      "{\"id\":\"r2\",\"request_type\":\"delete\",\"question_id\":\"M06\",\"reason\":\"Outdated\",\"status\":\"rejected\",\"admin_remarks\":\"Still in the syllabus\",\"proposed\":null,\"current\":null}]";
        var list = SupabaseClient.ParseArray<QuestionBankService.Request>(json);
        Assert.AreEqual(2, list.Count);
        Assert.IsTrue(list[0].HasProposed);
        Assert.IsTrue(list[0].changed_since);
        Assert.AreEqual(JsonUtility.ToJson(q), JsonUtility.ToJson(list[0].proposed.ToRecord("E07")), "the admin reviews exactly what the teacher sent");
        Assert.IsFalse(list[1].HasProposed, "a delete request carries no proposal");
        Assert.AreEqual("Still in the syllabus", list[1].admin_remarks);
    }

    [Test]
    public void Review_ShowsWhatChanged()
    {
        var before = Bank().questions.Single(x => x.questionId == "E08");
        var after = QuestionEditing.Copy(before);
        Assert.IsEmpty(QuestionEditing.Diff(before, after), "no change, nothing listed");
        after.correctChoice = 2;
        after.hint = "New hint";
        var d = QuestionEditing.Diff(before, after);
        CollectionAssert.AreEquivalent(new[] { "Correct answer", "Hint" }, d.Select(x => x.field));
        StringAssert.StartsWith("C. ", d.Single(x => x.field == "Correct answer").after);

        var withClues = QuestionEditing.Copy(Bank().questions.Single(x => x.questionId == "E02"));
        var edited = QuestionEditing.Copy(withClues);
        edited.clues[0].finding = "Different wording";
        Assert.AreEqual("Clues and activity", QuestionEditing.Diff(withClues, edited).Single().field, "clue edits are flagged even when the counts match");
    }

    [Test]
    public void TheEditor_SendsRequestsForTeachers()
    {
        string ui = File.ReadAllText("Assets/Scripts/UI/ClassroomUI.Questions.cs");
        StringAssert.Contains("if (!qAdmin) { SubmitRequest(", ui, "a teacher's SAVE becomes a request");
        StringAssert.Contains("QuestionBankService.SubmitRequestAsync(", ui);
        StringAssert.Contains("if (q == null || qSaving || !qAdmin) return;", ui, "TURN OFF / ON is admin-only");
        string reqUi = File.ReadAllText("Assets/Scripts/UI/ClassroomUI.Requests.cs");
        StringAssert.Contains("QuestionBankService.ReviewAsync(", reqUi);
        string main = File.ReadAllText("Assets/Scripts/UI/ClassroomUI.cs");
        StringAssert.Contains("portalAdmin = await QuestionBankService.IsAdminAsync()", main, "the admin role comes from the server");
        StringAssert.Contains("qAdmin = portalAdmin;", main, "every tab uses the portal's admin answer");
        StringAssert.Contains("qAdmin = portalAdmin;", reqUi, "the REQUESTS tab's bank carries the role to QUESTIONS (the admin saw request buttons before)");
    }
}
