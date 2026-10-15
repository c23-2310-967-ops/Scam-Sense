using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools > Scam Sense > Menu > Build Menu Backdrop.
/// Creates Assets/Scenes/MenuBackdrop_Room1.unity: Level 1's house (Game.unity) with every gameplay object and
/// script removed (player, managers, UI, interactions, colliders, audio), plus a <see cref="MenuBackdrop"/>
/// camera that drifts through the computer room. MainMenuUI loads it additively behind the menu.
/// Level 1 itself is opened only to copy it and is closed without saving. Re-run after changing Level 1.
/// </summary>
public static class MenuBackdropBuilder
{
    public const string SourcePath = "Assets/Scenes/Game.unity";
    public const string BackdropPath = "Assets/Scenes/MenuBackdrop_Room1.unity";

    // Gameplay roots of Game.unity that never belong in a background.
    static readonly string[] RemoveRoots = { "Player", "Canvas", "EventSystem", "Level1", "menu" };

    // Visual-only scripts that are kept.
    static readonly HashSet<Type> KeepScripts = new HashSet<Type> { typeof(ScreenFlicker) };

    // Computer room viewpoints (the Level 1 room the player investigates from): wide -> centre -> other side.
    // High enough that the desk chair never hides the monitor while the camera glides between them.
    static readonly Vector3[] Positions =
    {
        new Vector3(4.0f, 4.55f, 2.7f),
        new Vector3(1.5f, 4.75f, 2.4f),
        new Vector3(-1.0f, 4.55f, 2.9f),
    };
    static readonly Vector3[] LookTargets =
    {
        new Vector3(0.3f, 3.8f, 7.3f),
        new Vector3(1.2f, 3.95f, 7.4f),
        new Vector3(2.3f, 3.9f, 7.4f),
    };

    // Workstation monitor face, measured from ComputerTerminal's own screen canvas in Play Mode.
    static readonly Vector3 ScreenPos = new Vector3(1.45f, 4.2324f, 7.3763f);
    static readonly Quaternion ScreenRot = new Quaternion(0.06976f, 0f, 0f, 0.99756f);
    const float ScreenScale = 0.001066f;
    static readonly Vector2 ScreenSize = new Vector2(1280f, 789f);

    [MenuItem("Tools/Scam Sense/Menu/Build Menu Backdrop")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("MenuBackdropBuilder: stop Play Mode first."); return; }
        foreach (var path in new[] { SourcePath, BackdropPath })
        {
            var open = SceneManager.GetSceneByPath(path);
            if (open.IsValid() && open.isLoaded)
            {
                EditorUtility.DisplayDialog("Build Menu Backdrop", $"Close {System.IO.Path.GetFileName(path)} first (it is open in the editor).", "OK");
                return;
            }
        }

        var previousActive = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
        try
        {
            int removedRoots = 0, removedComponents = 0;
            foreach (var root in scene.GetRootGameObjects())
                if (RemoveRoots.Contains(root.name)) { UnityEngine.Object.DestroyImmediate(root); removedRoots++; }

            // Strip behaviour; keep meshes, lights, probes, world-space screens and the visual ScreenFlicker.
            for (int pass = 0; pass < 3; pass++)   // RequireComponent dependencies come off on a later pass
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<Component>(true))
                    {
                        if (c == null || !ShouldRemove(c)) continue;
                        try { UnityEngine.Object.DestroyImmediate(c); removedComponents++; } catch { /* retried next pass */ }
                    }

            // Level 1's lighting settings travel with the backdrop (an additive scene doesn't bring its own).
            SceneManager.SetActiveScene(scene);
            var lighting = new
            {
                RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                RenderSettings.ambientIntensity, RenderSettings.skybox, RenderSettings.reflectionIntensity, RenderSettings.fog,
                RenderSettings.fogMode, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance,
            };
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);

            var rootGo = new GameObject("MenuBackdrop");
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(rootGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = lighting.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.05f, 0.09f);
            cam.depth = -5;
            camGo.transform.SetPositionAndRotation(Positions[0], Quaternion.LookRotation(LookTargets[0] - Positions[0]));

            var (feed, clock) = BuildThreatMonitor(rootGo.transform);
            var leds = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true))
                            .Where(r => r.name.StartsWith("Led_")).ToArray();

            var backdrop = rootGo.AddComponent<MenuBackdrop>();
            var so = new SerializedObject(backdrop);
            so.FindProperty("cam").objectReferenceValue = cam;
            SetVectors(so.FindProperty("positions"), Positions);
            SetVectors(so.FindProperty("lookTargets"), LookTargets);
            so.FindProperty("ambientMode").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(UnityEngine.Rendering.AmbientMode)), lighting.ambientMode);
            so.FindProperty("ambientSky").colorValue = lighting.ambientSkyColor;
            so.FindProperty("ambientEquator").colorValue = lighting.ambientEquatorColor;
            so.FindProperty("ambientGround").colorValue = lighting.ambientGroundColor;
            so.FindProperty("ambientIntensity").floatValue = lighting.ambientIntensity;
            so.FindProperty("skybox").objectReferenceValue = lighting.skybox;
            so.FindProperty("reflectionIntensity").floatValue = lighting.reflectionIntensity;
            so.FindProperty("fog").boolValue = lighting.fog;
            so.FindProperty("fogMode").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(FogMode)), lighting.fogMode);
            so.FindProperty("fogColor").colorValue = lighting.fogColor;
            so.FindProperty("fogDensity").floatValue = lighting.fogDensity;
            so.FindProperty("fogStart").floatValue = lighting.fogStartDistance;
            so.FindProperty("fogEnd").floatValue = lighting.fogEndDistance;
            var ledProp = so.FindProperty("blinkingLeds");
            ledProp.arraySize = leds.Length;
            for (int i = 0; i < leds.Length; i++) ledProp.GetArrayElementAtIndex(i).objectReferenceValue = leds[i];
            so.FindProperty("screenFeed").objectReferenceValue = feed;
            so.FindProperty("screenClock").objectReferenceValue = clock;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BackdropPath, true);   // saved as a copy; Level 1 itself is untouched
            Debug.Log($"MenuBackdropBuilder: wrote {BackdropPath} ({removedRoots} gameplay roots and {removedComponents} components removed, {leds.Length} LEDs).");
        }
        finally
        {
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);   // discard: never saved over Game.unity
        }
        AddToBuildSettings(BackdropPath);
    }

    static bool ShouldRemove(Component c)
    {
        if (c is Transform || c is MeshFilter || c is Renderer || c is Light || c is ReflectionProbe || c is LightProbeGroup) return false;
        if (c is Canvas || c is CanvasRenderer || c is CanvasScaler || c is Graphic || c is LayoutGroup || c is LayoutElement
            || c is ContentSizeFitter || c is CanvasGroup || c is Mask || c is RectMask2D) return false;
        if (c is Collider || c is Rigidbody || c is CharacterController || c is AudioSource || c is AudioListener
            || c is Camera || c is GraphicRaycaster || c is UnityEngine.EventSystems.EventSystem
            || c is UnityEngine.EventSystems.BaseInputModule || c is Selectable) return true;
        if (c is MonoBehaviour mb && !KeepScripts.Contains(mb.GetType())) return true;   // any other script: gameplay
        return false;
    }

    /// <summary>A dark "threat monitor" UI on the workstation screen; MenuBackdrop types the feed at runtime.</summary>
    static (TMP_Text feed, TMP_Text clock) BuildThreatMonitor(Transform parent)
    {
        var go = new GameObject("ThreatMonitor", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = ScreenSize;
        rt.rotation = ScreenRot;
        rt.position = ScreenPos - (ScreenRot * Vector3.forward) * 0.004f;   // a hair in front of the monitor face
        rt.localScale = Vector3.one * ScreenScale;

        var bg = UIKit.Rect("Background", rt);
        UIKit.Stretch(bg);
        UIKit.AddImage(bg, new Color(0.035f, 0.07f, 0.12f, 1f)).raycastTarget = false;

        var bar = UIKit.Rect("Bar", rt);
        bar.anchorMin = new Vector2(0, 1);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(0, 74);
        UIKit.AddImage(bar, new Color(0.06f, 0.13f, 0.22f, 1f)).raycastTarget = false;
        var title = UIKit.Text(bar, "Title", "THREAT MONITOR  ·  LIVE", 34, UIKit.Accent, TextAlignmentOptions.MidlineLeft, bold: true);
        title.characterSpacing = 4f;
        UIKit.Stretch(title.rectTransform);
        title.margin = new Vector4(40, 0, 200, 0);
        var clock = UIKit.Text(bar, "Clock", "09:41", 32, UIKit.TextSecondary, TextAlignmentOptions.MidlineRight, bold: true);
        UIKit.Stretch(clock.rectTransform);
        clock.margin = new Vector4(0, 0, 40, 0);

        var feed = UIKit.Text(rt, "Feed", "", 31, new Color(0.78f, 0.86f, 0.95f), TextAlignmentOptions.TopLeft);
        UIKit.Stretch(feed.rectTransform);
        feed.rectTransform.offsetMin = new Vector2(44, 40);
        feed.rectTransform.offsetMax = new Vector2(-44, -100);
        feed.lineSpacing = 12f;
        feed.textWrappingMode = TextWrappingModes.NoWrap;
        feed.overflowMode = TextOverflowModes.Truncate;
        return (feed, clock);
    }

    static void SetVectors(SerializedProperty p, Vector3[] values)
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).vector3Value = values[i];
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("MenuBackdropBuilder: added " + path + " to Build Settings.");
    }
}
