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
/// Creates Assets/Scenes/MenuBackdrop_Room1.unity for the main-menu background: copies of the game's rooms
/// (Level 1's house and Rooms 2-4) with every gameplay object and script removed (player, managers, screen UI,
/// interactions, colliders, audio), each under its own "Room_..." parent, plus the <see cref="MenuBackdrop"/>
/// camera with its cinematic shots. Only one room is switched on at a time at runtime.
/// The level scenes are opened only to copy them and are closed without saving. Re-run after changing a room.
/// </summary>
public static class MenuBackdropBuilder
{
    public const string BackdropPath = "Assets/Scenes/MenuBackdrop_Room1.unity";

    /// <summary>Source rooms, in Rooms[] order (shots refer to them by index).</summary>
    static readonly (string name, string path)[] Sources =
    {
        ("Level 1 · The First Case", "Assets/Scenes/Game.unity"),
        ("Level 2 · The Investigation", "Assets/Scenes/Room2_InvestigationOffice.unity"),
        ("Level 3 · The Digital Trap", "Assets/Scenes/Room3_DigitalTrap.unity"),
        ("Level 4 · Digital Forensics Lab", "Assets/Scenes/Room4_DigitalForensicsLab.unity"),
    };

    // Visual-only scripts that are kept.
    static readonly HashSet<Type> KeepScripts = new HashSet<Type> { typeof(ScreenFlicker) };

    /// <summary>
    /// The shot loop (positions in each room's own world space, checked against the rooms for clear lines of
    /// sight). Adjust here and rebuild, or edit the Shots list on the MenuBackdrop object in the backdrop scene.
    /// </summary>
    static MenuBackdrop.Shot[] Shots() => new[]
    {
        Shot("Establishing · Level 1 computer room", 0, new Vector3(4.2f, 4.5f, 2.6f), new Vector3(2.9f, 4.55f, 2.45f),
             new Vector3(0.6f, 3.85f, 7.3f), new Vector3(0.9f, 3.9f, 7.3f), 58f, 11f),
        Shot("Workstation close-up · threat monitor", 0, new Vector3(2.6f, 4.55f, 5.4f), new Vector3(2.2f, 4.45f, 5.95f),
             new Vector3(1.35f, 4.12f, 7.4f), new Vector3(1.45f, 4.18f, 7.4f), 48f, 9f),
        Shot("Investigation office · Level 2", 1, new Vector3(3.0f, 1.75f, -3.0f), new Vector3(2.2f, 1.7f, -2.0f),
             new Vector3(-0.9f, 1.05f, 2.8f), new Vector3(-1.6f, 1.2f, 2.6f), 60f, 10f),
        Shot("Operations room · Level 3", 2, new Vector3(4.6f, 1.85f, -3.4f), new Vector3(3.3f, 1.95f, -3.7f),
             new Vector3(-0.8f, 1.15f, 2.6f), new Vector3(-0.2f, 1.2f, 2.6f), 62f, 11f),
        Shot("Evidence wall detail · Level 4", 3, new Vector3(-3.6f, 1.7f, -0.8f), new Vector3(-3.4f, 1.75f, 1.4f),
             new Vector3(-6f, 1.6f, 0.0f), new Vector3(-6f, 1.6f, 0.9f), 52f, 10f),
        Shot("Forensics lab wide · Level 4", 3, new Vector3(4.6f, 1.85f, -3.5f), new Vector3(3.8f, 1.95f, -3.1f),
             new Vector3(-1.0f, 1.0f, 2.4f), new Vector3(-1.6f, 1.0f, 2.0f), 62f, 11f),
    };

    static MenuBackdrop.Shot Shot(string name, int room, Vector3 from, Vector3 to, Vector3 lookFrom, Vector3 lookTo, float fov, float seconds) =>
        new MenuBackdrop.Shot { name = name, room = room, fromPosition = from, toPosition = to, fromLookAt = lookFrom, toLookAt = lookTo, fieldOfView = fov, duration = seconds };

    // Level 1 workstation monitor face, measured from ComputerTerminal's own screen canvas in Play Mode.
    static readonly Vector3 ScreenPos = new Vector3(1.45f, 4.2324f, 7.3763f);
    static readonly Quaternion ScreenRot = new Quaternion(0.06976f, 0f, 0f, 0.99756f);
    const float ScreenScale = 0.001066f;
    static readonly Vector2 ScreenSize = new Vector2(1280f, 789f);

    [MenuItem("Tools/Scam Sense/Menu/Build Menu Backdrop")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("MenuBackdropBuilder: stop Play Mode first."); return; }
        foreach (var path in Sources.Select(s => s.path).Append(BackdropPath))
        {
            var open = SceneManager.GetSceneByPath(path);
            if (open.IsValid() && open.isLoaded)
            {
                EditorUtility.DisplayDialog("Build Menu Backdrop", $"Close {System.IO.Path.GetFileName(path)} first (it is open in the editor).", "OK");
                return;
            }
        }

        var previousActive = SceneManager.GetActiveScene();
        var opened = new List<Scene>();
        try
        {
            Scene target = default;
            var rooms = new List<MenuBackdrop.Room>();
            int removedComponents = 0;
            for (int i = 0; i < Sources.Length; i++)
            {
                var scene = EditorSceneManager.OpenScene(Sources[i].path, OpenSceneMode.Additive);
                opened.Add(scene);
                if (i == 0) target = scene;
                removedComponents += Strip(scene);
                rooms.Add(CaptureLighting(scene, Sources[i].name));
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);

                // Everything left becomes one room group inside the backdrop scene (world positions unchanged).
                var group = new GameObject("Room_" + (i + 1));
                SceneManager.MoveGameObjectToScene(group, scene);
                foreach (var root in scene.GetRootGameObjects())
                    if (root != group) root.transform.SetParent(group.transform, true);
                if (i > 0) SceneManager.MoveGameObjectToScene(group, target);
                rooms[i].root = group;
            }

            var room1 = rooms[0].root.transform;
            var rootGo = new GameObject("MenuBackdrop");
            SceneManager.MoveGameObjectToScene(rootGo, target);
            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(rootGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.05f, 0.09f);
            cam.depth = -5;
            var shots = Shots();
            camGo.transform.SetPositionAndRotation(shots[0].fromPosition, Quaternion.LookRotation(shots[0].fromLookAt - shots[0].fromPosition));
            AddDust(camGo.transform);

            var (feed, clock) = BuildThreatMonitor(room1);
            AddGlow(room1, "MonitorGlow", ScreenPos - (ScreenRot * Vector3.forward) * 0.45f, 0.5f, 2.2f);
            var leds = room1.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Led_")).ToArray();

            var backdrop = rootGo.AddComponent<MenuBackdrop>();
            var so = new SerializedObject(backdrop);
            so.FindProperty("cam").objectReferenceValue = cam;
            var shotProp = so.FindProperty("shots");
            shotProp.arraySize = shots.Length;
            for (int i = 0; i < shots.Length; i++)
            {
                var e = shotProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = shots[i].name;
                e.FindPropertyRelative("room").intValue = shots[i].room;
                e.FindPropertyRelative("fromPosition").vector3Value = shots[i].fromPosition;
                e.FindPropertyRelative("toPosition").vector3Value = shots[i].toPosition;
                e.FindPropertyRelative("fromLookAt").vector3Value = shots[i].fromLookAt;
                e.FindPropertyRelative("toLookAt").vector3Value = shots[i].toLookAt;
                e.FindPropertyRelative("fieldOfView").floatValue = shots[i].fieldOfView;
                e.FindPropertyRelative("duration").floatValue = shots[i].duration;
            }
            var roomProp = so.FindProperty("rooms");
            roomProp.arraySize = rooms.Count;
            for (int i = 0; i < rooms.Count; i++)
            {
                var e = roomProp.GetArrayElementAtIndex(i);
                var r = rooms[i];
                e.FindPropertyRelative("name").stringValue = r.name;
                e.FindPropertyRelative("root").objectReferenceValue = r.root;
                e.FindPropertyRelative("ambientMode").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(UnityEngine.Rendering.AmbientMode)), r.ambientMode);
                e.FindPropertyRelative("ambientSky").colorValue = r.ambientSky;
                e.FindPropertyRelative("ambientEquator").colorValue = r.ambientEquator;
                e.FindPropertyRelative("ambientGround").colorValue = r.ambientGround;
                e.FindPropertyRelative("ambientIntensity").floatValue = r.ambientIntensity;
                e.FindPropertyRelative("skybox").objectReferenceValue = r.skybox;
                e.FindPropertyRelative("reflectionIntensity").floatValue = r.reflectionIntensity;
                e.FindPropertyRelative("fog").boolValue = r.fog;
                e.FindPropertyRelative("fogMode").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(FogMode)), r.fogMode);
                e.FindPropertyRelative("fogColor").colorValue = r.fogColor;
                e.FindPropertyRelative("fogDensity").floatValue = r.fogDensity;
                e.FindPropertyRelative("fogStart").floatValue = r.fogStart;
                e.FindPropertyRelative("fogEnd").floatValue = r.fogEnd;
                r.root.SetActive(i == shots[0].room);   // saved with only the first shot's room on
            }
            var ledProp = so.FindProperty("blinkingLeds");
            ledProp.arraySize = leds.Length;
            for (int i = 0; i < leds.Length; i++) ledProp.GetArrayElementAtIndex(i).objectReferenceValue = leds[i];
            so.FindProperty("screenFeed").objectReferenceValue = feed;
            so.FindProperty("screenClock").objectReferenceValue = clock;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(target, BackdropPath, true);   // saved as a copy; the level scenes are untouched
            Debug.Log($"MenuBackdropBuilder: wrote {BackdropPath} - {rooms.Count} rooms, {shots.Length} shots, {removedComponents} gameplay components removed, {leds.Length} LEDs.");
        }
        finally
        {
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            foreach (var s in opened) if (s.IsValid() && s.isLoaded) EditorSceneManager.CloseScene(s, true);   // discard: never saved
        }
        AddToBuildSettings(BackdropPath);
    }

    /// <summary>Removes gameplay: player, managers, screen-space UI and event systems, then every script except visual ones.</summary>
    static int Strip(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            bool gameplay = root.GetComponentInChildren<GameManager>(true) != null || root.GetComponentInChildren<QuizManager>(true) != null
                            || root.GetComponentInChildren<CharacterController>(true) != null
                            || root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null
                            || root.CompareTag("Player");
            if (gameplay) { UnityEngine.Object.DestroyImmediate(root); continue; }
            foreach (var c in root.GetComponentsInChildren<Canvas>(true))
                if (c != null && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) UnityEngine.Object.DestroyImmediate(c.gameObject);
        }
        int removed = 0;
        // Scripts first (they can [RequireComponent] an AudioSource or collider), then built-in components.
        for (int pass = 0; pass < 2; pass++)
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || (c is MonoBehaviour) != (pass == 0) || !ShouldRemove(c)) continue;
                    UnityEngine.Object.DestroyImmediate(c);
                    removed++;
                }
        return removed;
    }

    static MenuBackdrop.Room CaptureLighting(Scene scene, string name)
    {
        SceneManager.SetActiveScene(scene);   // RenderSettings belong to the active scene
        return new MenuBackdrop.Room
        {
            name = name,
            ambientMode = RenderSettings.ambientMode,
            ambientSky = RenderSettings.ambientSkyColor,
            ambientEquator = RenderSettings.ambientEquatorColor,
            ambientGround = RenderSettings.ambientGroundColor,
            ambientIntensity = RenderSettings.ambientIntensity,
            skybox = RenderSettings.skybox,
            reflectionIntensity = RenderSettings.reflectionIntensity,
            fog = RenderSettings.fog,
            fogMode = RenderSettings.fogMode,
            fogColor = RenderSettings.fogColor,
            fogDensity = RenderSettings.fogDensity,
            fogStart = RenderSettings.fogStartDistance,
            fogEnd = RenderSettings.fogEndDistance,
        };
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

    /// <summary>A dark "threat monitor" UI on the Level 1 workstation screen; MenuBackdrop types the feed at runtime.</summary>
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

    /// <summary>Soft cyan light in front of a monitor (no shadows, small range) - the screen's glow on the desk.</summary>
    static void AddGlow(Transform parent, string name, Vector3 position, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.22f, 0.74f, 0.97f);
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.Auto;
    }

    /// <summary>A few very faint dust motes drifting in front of the camera (about 60 particles, unlit).</summary>
    static void AddDust(Transform cam)
    {
        var go = new GameObject("Dust");
        go.transform.SetParent(cam, false);
        go.transform.localPosition = new Vector3(0f, 0f, 2.2f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.016f);
        main.startColor = new Color(1f, 1f, 1f, 0.35f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.prewarm = true;
        var emission = ps.emission;
        emission.rateOverTime = 6f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(3.5f, 2f, 3f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.03f;
        noise.frequency = 0.2f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
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
