using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The simplified main menu (one PLAY, no Level Selection / How to Play) and the first-time tips that replace the
/// How to Play page. The scene checks open Menu.unity without saving it and reopen the previous scenes afterwards.
/// </summary>
public class MainMenuTests
{
    const string MenuScene = "Assets/Scenes/Menu.unity";
    SceneSetup[] before;

    // ---------- First-time tips (pure) ----------

    static ScamMessage Msg(string station = "", bool choices = false) =>
        new ScamMessage { id = "X01", stationId = station, choices = choices ? new[] { "A", "B", "C", "D" } : new string[0] };

    [Test]
    public void Tips_WhereToOpenComesFirst()
    {
        var seen = new HashSet<string>();
        Assert.AreEqual(FirstTimeTips.Phone, FirstTimeTips.NextTip(Msg(choices: true), false, false, seen.Contains, out var t1));
        StringAssert.Contains("phone", t1);
        Assert.AreEqual(FirstTimeTips.Computer, FirstTimeTips.NextTip(Msg(), true, false, seen.Contains, out _));
        Assert.AreEqual(FirstTimeTips.Workstation, FirstTimeTips.NextTip(Msg("B"), true, false, seen.Contains, out _));
        Assert.AreEqual(FirstTimeTips.CaseTerminal, FirstTimeTips.NextTip(Msg(), false, true, seen.Contains, out _));
        var question = Msg(choices: true);
        question.channel = MessageChannel.Question;
        Assert.AreEqual(FirstTimeTips.Choice, FirstTimeTips.NextTip(question, false, false, seen.Contains, out _),
                        "a case question isn't a text message: no phone tip");
    }

    [Test]
    public void Tips_EachShownOnce()
    {
        var seen = new HashSet<string>();
        var q = Msg(choices: true);
        seen.Add(FirstTimeTips.NextTip(q, false, false, seen.Contains, out _));          // phone
        Assert.AreEqual(FirstTimeTips.Choice, FirstTimeTips.NextTip(q, false, false, seen.Contains, out var t));
        StringAssert.Contains("best answer", t);
        seen.Add(FirstTimeTips.Choice);
        Assert.IsNull(FirstTimeTips.NextTip(q, false, false, seen.Contains, out var none), "nothing left to explain");
        Assert.IsNull(none);
        Assert.IsNull(FirstTimeTips.NextTip(null, true, true, seen.Contains, out _));
    }

    [Test]
    public void Tips_AreShort()
    {
        var seen = new HashSet<string>();
        var cases = new (ScamMessage m, bool pc, bool term)[] { (Msg(), false, false), (Msg(), true, false), (Msg("A"), true, false), (Msg(), false, true), (Msg(choices: true), true, false) };
        foreach (var c in cases)
        {
            string id = FirstTimeTips.NextTip(c.m, c.pc, c.term, seen.Contains, out var text);
            if (id == null) continue;
            seen.Add(id);
            Assert.LessOrEqual(UIKit.StripRichText(text).Length, 140, id + " tip is too long for a toast");
        }
        Assert.AreEqual(5, seen.Count, "every tip is reachable");
    }

    // ---------- The menu scene ----------

    [OneTimeSetUp]
    public void Remember()
    {
        if (Application.isPlaying) Assert.Ignore("Stop Play Mode first.");
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save or discard scene changes first - this test opens the Menu scene.");
        before = EditorSceneManager.GetSceneManagerSetup();
    }

    [OneTimeTearDown]
    public void Restore()
    {
        if (before != null && before.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(before);
    }

    static Transform Navigation()
    {
        var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        var menu = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<MainMenuUI>(true)).FirstOrDefault(m => m != null);
        Assert.IsNotNull(menu, "Menu scene has no MainMenuUI");
        var nav = menu.transform.Find("MainMenu Canvas/Navigation");
        Assert.IsNotNull(nav, "Rebuild the menu (Tools > Scam Sense > Build Menu UI In Scene)");
        return nav;
    }

    [Test]
    public void Menu_HasOnePlayAndNoLevelPickerOrHowTo()
    {
        var nav = Navigation();
        var labels = nav.GetComponentsInChildren<Button>(true).Select(b => b.name).ToList();
        CollectionAssert.AreEqual(new[] { "PLAY Button", "CLASSROOM Button", "SETTINGS Button", "ABOUT Button", "QUIT GAME Button" }, labels);
        var canvas = nav.parent;
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
        {
            Assert.AreNotEqual("LEVEL SELECTION Button", t.name);
            Assert.AreNotEqual("HOW TO PLAY Button", t.name);
            Assert.AreNotEqual("CONTINUE Button", t.name, "PLAY continues the progression; there is no second start button");
        }
        var play = nav.Find("PLAY Button").GetComponent<Button>();
        var methods = Enumerable.Range(0, play.onClick.GetPersistentEventCount()).Select(play.onClick.GetPersistentMethodName).ToList();
        CollectionAssert.Contains(methods, "Play", "PLAY starts the next case (plus the click sound)");
        Assert.Greater(play.GetComponent<LayoutElement>().preferredHeight, nav.Find("SETTINGS Button").GetComponent<LayoutElement>().preferredHeight * 1.5f,
                       "PLAY is clearly the largest button");
    }

    [Test]
    public void Menu_EveryButtonHasAWorkingClick()
    {
        var canvas = Navigation().parent;
        var broken = new List<string>();
        foreach (var b in canvas.GetComponentsInChildren<Button>(true))
        {
            int n = b.onClick.GetPersistentEventCount();
            if (n == 0) { broken.Add(b.name + ": no OnClick"); continue; }
            for (int i = 0; i < n; i++)
            {
                var target = b.onClick.GetPersistentTarget(i);
                string method = b.onClick.GetPersistentMethodName(i);
                if (target == null || string.IsNullOrEmpty(method) || target.GetType().GetMethod(method) == null)
                    broken.Add($"{b.name}: OnClick {i} -> {(target == null ? "missing object" : target.GetType().Name)}.{method}");
            }
        }
        Assert.IsEmpty(broken, string.Join("\n", broken));
    }
}
