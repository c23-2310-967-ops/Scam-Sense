using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Opens every scene in the build (without saving anything) and checks for the kinds of breakage that are easy to
/// miss by eye: missing scripts, invisible objects with no mesh or material, computers with no screen and chairs
/// you cannot sit on. The scenes that were open before the tests are reopened afterwards.
/// </summary>
public class SceneIntegrityTests
{
    SceneSetup[] before;

    static IEnumerable<string> BuildScenes() =>
        EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path);

    [OneTimeSetUp]
    public void RememberOpenScenes()
    {
        if (Application.isPlaying) Assert.Ignore("Stop Play Mode before running the scene tests.");
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty)
                Assert.Ignore("Save or discard your scene changes first - the scene tests open other scenes.");
        before = EditorSceneManager.GetSceneManagerSetup();
    }

    [OneTimeTearDown]
    public void ReopenScenes()
    {
        if (before != null && before.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(before);
    }

    static List<GameObject> AllObjects(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var all = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            all.AddRange(root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
        return all;
    }

    static string PathOf(GameObject go)
    {
        string p = go.name;
        for (var t = go.transform.parent; t != null; t = t.parent) p = t.name + "/" + p;
        return p;
    }

    [TestCaseSource(nameof(BuildScenes))]
    public void NoMissingScripts(string scenePath)
    {
        var broken = AllObjects(scenePath)
            .Where(go => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
            .Select(PathOf).ToList();
        Assert.IsEmpty(broken, "Objects with a missing (deleted or renamed) script:\n" + string.Join("\n", broken));
    }

    [TestCaseSource(nameof(BuildScenes))]
    public void VisibleObjects_HaveAMeshAndMaterials(string scenePath)
    {
        var problems = new List<string>();
        foreach (var go in AllObjects(scenePath))
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || !go.activeInHierarchy) continue;
            if (go.GetComponent<TMPro.TMP_Text>() != null) continue;   // TMP fills its own mesh at runtime
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh == null) problems.Add($"{PathOf(go)}: no mesh");
            if (r.sharedMaterials.Any(m => m == null)) problems.Add($"{PathOf(go)}: empty material slot (shows pink/invisible)");
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [TestCaseSource(nameof(BuildScenes))]
    public void Computers_HaveAScreen(string scenePath)
    {
        var problems = new List<string>();
        foreach (var go in AllObjects(scenePath))
            foreach (var term in go.GetComponents<ComputerTerminal>())
            {
                var screen = new SerializedObject(term).FindProperty("screenSurface").objectReferenceValue as Transform;
                if (screen == null) problems.Add($"{PathOf(go)}: no screenSurface (emails cannot be shown)");
                else if (screen.GetComponent<MeshFilter>() == null || screen.GetComponent<Renderer>() == null)
                    problems.Add($"{PathOf(go)}: screenSurface '{screen.name}' has no mesh to place the screen on");
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [TestCaseSource(nameof(BuildScenes))]
    public void Chairs_HaveSeatAndExitPoints(string scenePath)
    {
        var problems = new List<string>();
        foreach (var go in AllObjects(scenePath))
            foreach (var chair in go.GetComponents<SittableChair>())
            {
                var so = new SerializedObject(chair);
                bool seat = so.FindProperty("seatPoint").objectReferenceValue != null || HasChild(go, "SeatPoint");
                bool exit = so.FindProperty("exitPoint").objectReferenceValue != null || HasChild(go, "ExitPoint");
                if (!seat || !exit)
                    problems.Add($"{PathOf(go)}: missing {(seat ? "" : "SeatPoint ")}{(exit ? "" : "ExitPoint ")}- the player cannot sit here");
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    static bool HasChild(GameObject go, string childName) =>
        go.GetComponentsInChildren<Transform>(true).Any(t => t.gameObject != go &&
            t.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase));
}
