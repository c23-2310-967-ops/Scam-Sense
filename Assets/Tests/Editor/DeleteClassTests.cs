using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// CLASSES > DELETE (supabase/migrations/20261128044325_delete_class.sql): only the class's own teacher can delete it,
/// never a class with recorded results (student records are final), and signed-out callers can't reach it.
/// </summary>
public class DeleteClassTests
{
    const string Path = "supabase/migrations/20261128044325_delete_class.sql";
    static string Sql => File.ReadAllText(Path);
    static string Body => Regex.Match(Sql, @"function public\.delete_class\(p_class uuid\)[\s\S]*?end \$\$;").Value;

    [Test]
    public void OnlyTheClassTeacher_CanDelete()
    {
        StringAssert.IsMatch(@"if not public\.is_teacher\(\) then\s+raise exception", Body, "students are refused");
        StringAssert.Contains("teacher_id = auth.uid()", Body, "a teacher can only delete their own classes");
        StringAssert.Contains("security definer set search_path = ''", Sql);
    }

    [Test]
    public void ClassesWithResults_AreNeverDeleted()
    {
        string body = Body;
        int check = body.IndexOf("from public.game_results where class_id = p_class");
        int delete = body.IndexOf("delete from public.classes");
        Assert.Greater(check, 0, "it counts the class's recorded results");
        Assert.Greater(delete, check, "the results check comes before the delete");
        StringAssert.IsMatch(@"if n_results > 0 then\s+return jsonb_build_object\('ok', false", body, "a class with results is refused, not deleted");
    }

    [Test]
    public void SignedOutCallers_CantReachIt()
    {
        StringAssert.IsMatch(@"revoke execute on function public\.delete_class\(uuid\) from public, anon", Sql);
        StringAssert.IsMatch(@"grant execute on function public\.delete_class\(uuid\) to authenticated", Sql);
    }

    [Test]
    public void ThePanel_ConfirmsFirst_AndHidesArchivedClasses()
    {
        string ui = File.ReadAllText("Assets/Scripts/UI/ClassroomUI.cs");
        StringAssert.Contains("if (confirmDeleteClass != id) { confirmDeleteClass = id; Render(); return; }", ui, "the first press only asks");
        StringAssert.Contains("showArchived || !c.archived", ui, "archived classes are hidden unless shown");
        StringAssert.Contains("ClassroomService.DeleteClassAsync(id)", ui);
    }
}
