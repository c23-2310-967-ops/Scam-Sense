using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using R3 = Room3Builder;

/// <summary>
/// Level 5 - The Compromised Campus Network: builds the university MacLab scene.
///
/// Tools > Scam Sense > Level 5 > Create Room 5 Scene
///   Copies Room4_DigitalForensicsLab.unity to Room5_MacLab.unity (only if Room 5 doesn't exist yet), so the Player,
///   EventSystem, level managers (scoring, HUD, briefing, results, quiz, pause) and the sittable DeskChair are the
///   exact same working objects. Then builds the lab around them, points the level at Level 5, registers the scene
///   in Build Settings, adds Level 5 to the Level Catalog and links Level 4 -> Level 5.
/// Tools > Scam Sense > Level 5 > Rebuild Room 5 Environment
///   Rebuilds the lab in the open Room 5 scene (gameplay objects are kept).
///
/// Layout (20 x 14 m, 2.75 m suspended ceiling), modelled on a real university MacLab, with roomy walkways
/// (about 2 m centre aisle, 2.2 m or more between pods, about 1.5 m along the walls):
///   north wall - wall-mounted display above the instructor counter (instructor iMac = official records terminal)
///   south wall - row of windows;  west / east walls - taupe feature panels with diagonal silver inlays
///   six hexagonal 6-seat pods in two rows of three (36 all-in-one computers, radial partitions, black chairs)
///   east door into the centre aisle (spawn): windows on the left, display on the right.
/// Five pod seats are flagged investigation workstations (Level5Content.Stations); the rest are props.
/// No people are modelled.
/// </summary>
public static class Room5Builder
{
    const string Room4Path = "Assets/Scenes/Room4_DigitalForensicsLab.unity";
    public const string ScenePath = "Assets/Scenes/Room5_MacLab.unity";
    const string SceneName = "Room5_MacLab";
    const string Level4Path = "Assets/Levels/Level4_DigitalForensicsLab.asset";
    const string Level5Path = "Assets/Levels/Level5_MacLab.asset";
    const string CatalogPath = "Assets/Levels/LevelCatalog.asset";
    const string HexMeshPath = "Assets/Materials/R5_HexDeskTop.asset";

    static readonly string[] Keep = { "DeskChair" };
    const float HalfX = 10f, HalfZ = 7f, Height = 2.75f;
    const float DeskH = 0.74f, TopT = 0.03f, Apothem = 1.2f, ChairR = 1.65f, ChairScale = 0.78f;
    static float TopY => DeskH + TopT;

    // Pod centres: north row (pods 1-3, west to east) and south row (pods 4-6). Seat k faces 60k degrees (0 = east).
    static readonly Vector2[] Pods = { new Vector2(-6.3f, 2.6f), new Vector2(0f, 2.6f), new Vector2(6.3f, 2.6f), new Vector2(-6.3f, -2.9f), new Vector2(0f, -2.9f), new Vector2(6.3f, -2.9f) };
    const float AisleZ = -0.15f;   // centre of the aisle between the two rows (spawn line)

    static Transform desks, computers, stations;
    static Transform chairSrc;
    static int screenVariant;

    // ---------- Menu ----------

    [MenuItem("Tools/Scam Sense/Level 5/Create Room 5 Scene")]
    public static void CreateMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Level 5", "Stop Play Mode first.", "OK"); return; }
        Debug.Log("Room5Builder: " + Create());
    }

    [MenuItem("Tools/Scam Sense/Level 5/Rebuild Room 5 Environment")]
    public static void RebuildMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Level 5", "Stop Play Mode first.", "OK"); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) { EditorUtility.DisplayDialog("Level 5", "Open " + ScenePath + " first.", "OK"); return; }
        SetupLevelAssets();
        BuildEnvironment();
        PlaceSpawn(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RefreshMenuPreview();
        Debug.Log("Room5Builder: environment rebuilt and saved; menu preview refreshed.");
    }

    public static string Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return ScenePath + " already exists - nothing overwritten. Use 'Rebuild Room 5 Environment' to refresh it.";

        SetupLevelAssets();
        if (!AssetDatabase.CopyAsset(Room4Path, ScenePath)) return "could not copy " + Room4Path;
        // Opened next to whatever is open (never saving or discarding it), built, saved, then closed again.
        var previous = EditorSceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "SetDressing") Object.DestroyImmediate(root);   // Room 4's decor dressing
        var gm = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(g => g.gameObject.scene == scene);
        if (gm != null) gm.gameObject.name = "Level5";

        // Spawn just inside the east door, looking down the centre aisle (west).
        PlaceSpawn(scene);

        BuildEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (previous.IsValid() && previous.isLoaded && previous != scene)
        {
            EditorSceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        AddToBuildSettings();
        RefreshMenuPreview();
        return "created " + ScenePath + ", added it to Build Settings and the Level Catalog, linked Level 4 -> Level 5 and captured its menu preview.";
    }

    /// <summary>Spawn just inside the east door, looking down the centre aisle (west).</summary>
    static void PlaceSpawn(UnityEngine.SceneManagement.Scene scene)
    {
        var player = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).FirstOrDefault(p => p.gameObject.scene == scene);
        if (player == null) return;
        player.transform.position = new Vector3(HalfX - 0.9f, 1.02f, AisleZ);
        player.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
    }

    /// <summary>The main menu's sneak peek is a still of this very scene (LevelPreviewTools), re-captured after every build.</summary>
    static void RefreshMenuPreview()
    {
        var l5 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level5Path);
        if (!LevelPreviewTools.CaptureLevel(l5)) Debug.LogWarning("Room5Builder: could not capture the Level 5 menu preview.");
    }

    static void AddToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == ScenePath)) return;
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>Creates/refreshes the Level 5 asset, links Level 4 to it and lists it in the catalog. Other levels' data is untouched.</summary>
    static LevelDefinition SetupLevelAssets()
    {
        var l5 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level5Path);
        if (l5 == null)
        {
            l5 = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(l5, Level5Path);
        }
        Level5Content.Fill(l5);
        l5.sceneName = SceneName;
        foreach (var m in l5.messages)
            foreach (var p in InvestigationSession.Validate(m)) Debug.LogWarning($"Room5Builder: {m.id}: {p}");
        EditorUtility.SetDirty(l5);

        var l4 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level4Path);
        if (l4 != null)
        {
            l4.nextLevelId = l5.levelId;   // was already "level_5": saves that unlocked it stay valid
            l4.nextSceneName = SceneName;
            EditorUtility.SetDirty(l4);
        }

        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog != null && !catalog.levels.Contains(l5))
        {
            catalog.levels.Add(l5);
            EditorUtility.SetDirty(catalog);
        }
        AssetDatabase.SaveAssets();
        return l5;
    }

    static void AssignLevel()
    {
        var l5 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level5Path);
        var scene = EditorSceneManager.GetActiveScene();
        var gm = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(g => g.gameObject.scene == scene);
        if (l5 == null || gm == null) { Debug.LogWarning("Room5Builder: Level 5 asset or GameManager not found."); return; }
        var so = new SerializedObject(gm);
        so.FindProperty("level").objectReferenceValue = l5;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gm);
        if (gm.GetComponent<EvidenceTracker>() == null) gm.gameObject.AddComponent<EvidenceTracker>();
    }

    // ---------- Environment ----------

    public static void BuildEnvironment()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var envGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Environment");
        if (envGo == null) { envGo = new GameObject("Environment"); SceneManager_Move(envGo, scene); }
        var env = envGo.transform;
        for (int i = env.childCount - 1; i >= 0; i--)
        {
            var c = env.GetChild(i);
            if (!Keep.Contains(c.name)) Object.DestroyImmediate(c.gameObject);
        }

        R3.structure = R3.Group("MacLab_Room", env);
        desks = R3.Group("MacLab_Desks", env);
        computers = R3.Group("MacLab_Computers", env);
        R3.furniture = R3.Group("MacLab_Chairs", env);
        stations = R3.Group("MacLab_InteractiveStations", env);
        R3.screens = R3.Group("MacLab_Displays", env);
        R3.props = R3.Group("MacLab_Props", env);
        R3.lighting = R3.Group("MacLab_Lighting", env);
        R3.shadows = R3.Group("MacLab_ContactShadows", env);
        chairSrc = env.Find("DeskChair");
        screenVariant = 0;

        BuildShell();
        BuildWindows();
        BuildFeatureWalls();
        BuildDoor();
        BuildFront();
        for (int p = 0; p < Pods.Length; p++) BuildPod(p);
        BuildProps();
        BuildLighting();
        SetupRenderSettings();
        AssignLevel();

        // Static batching for plain geometry (never the interactive computers: they measure their screens at runtime).
        foreach (var group in new[] { R3.structure, desks, computers, R3.furniture, R3.props })
            foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true))
                if (r.GetComponentInParent<Canvas>() == null && r.GetComponentInParent<ComputerTerminal>() == null && r.GetComponentInParent<CaseFilesTerminal>() == null)
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
    }

    static void SceneManager_Move(GameObject g, UnityEngine.SceneManagement.Scene s) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, s);

    static Material Mat(string name, string hex, float smooth, string emission = null, float strength = 0f) =>
        R3.M("R5_" + name, hex, smooth, emission, strength);

    // ---------- Shell: floor, ceiling, walls ----------

    static void BuildShell()
    {
        var floor = Mat("FloorTile", "#D6D8D7", 0.62f);          // light glossy floor tiles
        var seam = Mat("TileSeam", "#BFC2C2", 0.3f);
        var wall = Mat("WallWhite", "#E9EAE8", 0.12f);
        var skirting = Mat("Skirting", "#A9ACAF", 0.35f);
        var ceiling = Mat("CeilingTile", "#EEEFF0", 0.05f);
        var grid = Mat("CeilingGrid", "#CFD2D5", 0.4f);
        var panel = Mat("LedPanel", "#FBFCFF", 0.2f, "#FBFCFF", 1.5f);
        var s = R3.structure;

        R3.Box("Floor", s, new Vector3(0, -0.05f, 0), new Vector3(HalfX * 2 + 0.4f, 0.1f, HalfZ * 2 + 0.4f), floor, true);
        R3.Box("Ceiling", s, new Vector3(0, Height + 0.05f, 0), new Vector3(HalfX * 2 + 0.4f, 0.1f, HalfZ * 2 + 0.4f), ceiling, true);
        R3.Box("Wall_North", s, new Vector3(0, Height / 2, HalfZ + 0.1f), new Vector3(HalfX * 2 + 0.4f, Height, 0.2f), wall, true);
        R3.Box("Wall_South", s, new Vector3(0, Height / 2, -HalfZ - 0.1f), new Vector3(HalfX * 2 + 0.4f, Height, 0.2f), wall, true);
        R3.Box("Wall_East", s, new Vector3(HalfX + 0.1f, Height / 2, 0), new Vector3(0.2f, Height, HalfZ * 2), wall, true);
        R3.Box("Wall_West", s, new Vector3(-HalfX - 0.1f, Height / 2, 0), new Vector3(0.2f, Height, HalfZ * 2), wall, true);

        // 60 cm floor tiles
        for (float x = -HalfX + 0.6f; x < HalfX - 0.05f; x += 0.6f)
            R3.Box("TileSeam_X", s, new Vector3(x, 0.001f, 0), new Vector3(0.008f, 0.002f, HalfZ * 2), seam, false, shadow: false);
        for (float z = -HalfZ + 0.6f; z < HalfZ - 0.05f; z += 0.6f)
            R3.Box("TileSeam_Z", s, new Vector3(0, 0.001f, z), new Vector3(HalfX * 2, 0.002f, 0.008f), seam, false, shadow: false);

        // Skirting all round (door gap on the east wall)
        R3.Box("Skirting_N", s, new Vector3(0, 0.05f, HalfZ - 0.01f), new Vector3(HalfX * 2, 0.1f, 0.02f), skirting, shadow: false);
        R3.Box("Skirting_S", s, new Vector3(0, 0.05f, -HalfZ + 0.01f), new Vector3(HalfX * 2, 0.1f, 0.02f), skirting, shadow: false);
        R3.Box("Skirting_W", s, new Vector3(-HalfX + 0.01f, 0.05f, 0), new Vector3(0.02f, 0.1f, HalfZ * 2), skirting, shadow: false);
        R3.Box("Skirting_E1", s, new Vector3(HalfX - 0.01f, 0.05f, (-HalfZ + AisleZ - 0.62f) / 2f), new Vector3(0.02f, 0.1f, HalfZ + AisleZ - 0.62f), skirting, shadow: false);
        R3.Box("Skirting_E2", s, new Vector3(HalfX - 0.01f, 0.05f, (HalfZ + AisleZ + 0.62f) / 2f), new Vector3(0.02f, 0.1f, HalfZ - AisleZ - 0.62f), skirting, shadow: false);

        // Suspended ceiling: 60 cm grid with square recessed LED panels in a regular pattern
        for (float x = -HalfX + 0.6f; x < HalfX - 0.05f; x += 0.6f)
            R3.Box("CeilingGrid_X", s, new Vector3(x, Height - 0.004f, 0), new Vector3(0.022f, 0.008f, HalfZ * 2), grid, false, shadow: false);
        for (float z = -HalfZ + 0.6f; z < HalfZ - 0.05f; z += 0.6f)
            R3.Box("CeilingGrid_Z", s, new Vector3(0, Height - 0.004f, z), new Vector3(HalfX * 2, 0.008f, 0.022f), grid, false, shadow: false);
        for (float x = -8.4f; x <= 8.5f; x += 2.1f)
            for (float z = -5.4f; z <= 5.5f; z += 1.8f)
                R3.Box("LedPanel", s, new Vector3(x, Height - 0.012f, z), new Vector3(0.58f, 0.012f, 0.58f), panel, shadow: false);
    }

    static void BuildWindows()
    {
        // South wall: a long run of tall windows (bright daylight glass, white aluminium frames) between pilasters.
        var glass = Mat("WindowGlass", "#EEF4FA", 0.9f, "#EAF2FB", 1.05f);
        var frame = Mat("WindowFrame", "#F4F5F6", 0.5f);
        var pilaster = Mat("WallWhite", "#E9EAE8", 0.12f);
        var sill = Mat("Sill", "#DADCDD", 0.4f);
        var s = R3.structure;
        float z = -HalfZ + 0.02f;
        for (int g = 0; g < 5; g++)
        {
            float gx = -8f + g * 4f;   // groups centred at -8, -4, 0, 4, 8
            for (int w = 0; w < 2; w++)
            {
                float x = gx - 0.85f + w * 1.7f;
                R3.Box("WindowGlass", s, new Vector3(x, 1.7f, z), new Vector3(1.55f, 1.55f, 0.01f), glass, shadow: false);
                R3.Box("Frame_T", s, new Vector3(x, 2.49f, z + 0.02f), new Vector3(1.62f, 0.05f, 0.05f), frame);
                R3.Box("Frame_B", s, new Vector3(x, 0.91f, z + 0.02f), new Vector3(1.62f, 0.05f, 0.05f), frame);
                R3.Box("Frame_L", s, new Vector3(x - 0.79f, 1.7f, z + 0.02f), new Vector3(0.05f, 1.6f, 0.05f), frame);
                R3.Box("Frame_R", s, new Vector3(x + 0.79f, 1.7f, z + 0.02f), new Vector3(0.05f, 1.6f, 0.05f), frame);
                R3.Box("Mullion", s, new Vector3(x, 1.7f, z + 0.02f), new Vector3(0.03f, 1.55f, 0.03f), frame);
                R3.Box("Transom", s, new Vector3(x, 2.05f, z + 0.02f), new Vector3(1.55f, 0.03f, 0.03f), frame);
            }
            R3.Box("Sill", s, new Vector3(gx, 0.87f, z + 0.08f), new Vector3(3.5f, 0.04f, 0.16f), sill);
        }
        foreach (float px in new[] { -HalfX + 0.2f, -6f, -2f, 2f, 6f, HalfX - 0.2f })
            R3.Box("Pilaster", s, new Vector3(px, Height / 2f, -HalfZ + 0.12f), new Vector3(0.4f, Height, 0.24f), pilaster, true);
    }

    static void BuildFeatureWalls()
    {
        // West and east walls: warm taupe panelling with thin diagonal silver inlays, as in the reference lab.
        var taupe = Mat("FeatureTaupe", "#BCAEA3", 0.25f);
        var inlay = Mat("SilverInlay", "#E1E0DD", 0.85f);
        var s = R3.structure;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (HalfX - 0.015f);
            var panel = R3.Box("FeaturePanel", s, new Vector3(x, 1.42f, 0), new Vector3(0.02f, 2.64f, HalfZ * 2 - 0.1f), taupe);
            panel.name = side < 0 ? "FeatureWall_West" : "FeatureWall_East";
            var rng = new System.Random(side < 0 ? 5 : 9);
            int lines = Mathf.FloorToInt((HalfZ * 2f - 1.2f) / 1.3f);
            for (int i = 0; i < lines; i++)
            {
                float zc = -HalfZ + 0.8f + i * 1.3f + (float)rng.NextDouble() * 0.4f;
                float len = 1.6f + (float)rng.NextDouble() * 1.4f;
                float yc = 0.7f + (float)rng.NextDouble() * 1.4f;
                float angle = (i % 2 == 0 ? 1 : -1) * (25f + (float)rng.NextDouble() * 25f);
                if (side > 0 && Mathf.Abs(zc - AisleZ) < 1.0f) continue;   // keep the door area clean
                var line = R3.Box("Inlay", s, new Vector3(x - side * 0.012f, yc, zc), new Vector3(0.006f, 0.012f, len), inlay, shadow: false);
                line.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
            }
        }
    }

    static void BuildDoor()
    {
        var frame = Mat("DoorFrame", "#C9CCCF", 0.5f);
        var door = Mat("Door", "#D8D9DA", 0.35f);
        var steel = Mat("Steel", "#A6AAAF", 0.75f);
        var glass = Mat("DoorGlass", "#AFC4D2", 0.9f);
        var s = R3.structure;
        float x = HalfX - 0.03f, z = AisleZ;
        R3.Box("DoorFrame_L", s, new Vector3(x, 1.12f, z - 0.62f), new Vector3(0.08f, 2.24f, 0.08f), frame);
        R3.Box("DoorFrame_R", s, new Vector3(x, 1.12f, z + 0.62f), new Vector3(0.08f, 2.24f, 0.08f), frame);
        R3.Box("DoorFrame_T", s, new Vector3(x, 2.26f, z), new Vector3(0.08f, 0.08f, 1.32f), frame);
        R3.Box("Door", s, new Vector3(x - 0.01f, 1.1f, z), new Vector3(0.05f, 2.2f, 1.16f), door);
        R3.Box("DoorVision", s, new Vector3(x - 0.04f, 1.55f, z + 0.3f), new Vector3(0.01f, 0.7f, 0.18f), glass, shadow: false);
        R3.Box("PushBar", s, new Vector3(x - 0.07f, 1.02f, z), new Vector3(0.04f, 0.04f, 0.85f), steel);

        // Room sign beside the door
        var c = R3.WorldCanvas("RoomSign", R3.screens, new Vector3(HalfX - 0.03f, 1.62f, AisleZ + 1.15f), Vector3.left, 0.5f, new Vector2(500, 220), false);
        UIKit.AddImage(c, new Color32(0x1F, 0x2A, 0x44, 255)).raycastTarget = false;
        R3.Txt(c, "Room", "MACLAB 3", 70, Color.white, new Vector2(30, -26), new Vector2(440, 90), bold: true).characterSpacing = 6;
        R3.Txt(c, "Dept", "College of Computer Studies\nUPHSL Biñan", 30, new Color(1, 1, 1, 0.75f), new Vector2(30, -120), new Vector2(440, 90));
    }

    // ---------- Front: wall display + instructor counter ----------

    static void BuildFront()
    {
        var black = Mat("DisplayBezel", "#101215", 0.6f);
        var counterGrey = Mat("CounterPanel", "#CFD2D5", 0.3f);
        var counterTop = Mat("CounterTop", "#F1F2F2", 0.45f);
        var kick = Mat("Kick", "#8E9297", 0.3f);
        float tvX = -2.3f, tvY = 1.85f, tvZ = HalfZ - 0.04f;

        // Wall-mounted display: campus cybersecurity notice (fictional) + this week's schedule.
        var tv = new GameObject("WallDisplay").transform;
        tv.SetParent(R3.screens, false);
        R3.Box("Bezel", tv, new Vector3(tvX, tvY, tvZ), new Vector3(1.96f, 1.14f, 0.06f), black);
        var c = R3.WorldCanvas("Notice", tv, new Vector3(tvX, tvY, tvZ - 0.032f), Vector3.back, 1.88f, new Vector2(1600, 906), false);
        UIKit.AddImage(c, new Color32(0x14, 0x1C, 0x33, 255)).raycastTarget = false;
        var band = UIKit.Rect("Band", c);
        UIKit.Place(band, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1600, 120));
        UIKit.AddImage(band, new Color32(0xB3, 0x26, 0x3A, 255)).raycastTarget = false;
        R3.Txt(c, "Head", "CAMPUS CYBERSECURITY NOTICE", 64, Color.white, new Vector2(60, -24), new Vector2(1480, 80), bold: true).characterSpacing = 5;
        R3.Txt(c, "Body",
            "Suspicious emails and texts are circulating on campus.\n\n" +
            "•  UPHSL IT will <b>never</b> ask for your password or a verification code.\n" +
            "•  Official mail comes only from <b>@uphsl-binan.example</b>.\n" +
            "•  Don't open unexpected attachments or click <b>Enable Content</b>.\n" +
            "•  Report anything suspicious to <b>security@uphsl-binan.example</b>.",
            40, Color.white, new Vector2(60, -170), new Vector2(1480, 470));
        var foot = UIKit.Rect("Foot", c);
        UIKit.Place(foot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(1600, 190));
        UIKit.AddImage(foot, new Color32(0x0C, 0x12, 0x24, 255)).raycastTarget = false;
        R3.Txt(c, "Week", "<b>THIS WEEK</b>   Today 1:00-4:00 PM  MacLab 3 cybersecurity workshop   ·   Saturday 6:00-9:00 AM  campus Wi-Fi maintenance",
            30, new Color32(0xCF, 0xD8, 0xEA, 255), new Vector2(60, 150), new Vector2(1480, 110), anchor: new Vector2(0, 0));
        R3.Box("Cable", tv, new Vector3(tvX + 0.6f, 0.68f, tvZ + 0.015f), new Vector3(0.02f, 1.0f, 0.015f), Mat("Cable", "#E5E6E7", 0.3f), shadow: false);

        // Instructor counter (reception-style, grey panel front) - the instructor's iMac faces the wall side.
        var counter = new GameObject("InstructorCounter").transform;
        counter.SetParent(desks, false);
        float cx = -2.5f, cz = HalfZ - 1.35f;   // back face 1.0 m from the wall: room to stand behind it
        R3.Box("Body", counter, new Vector3(cx, 0.52f, cz), new Vector3(2.2f, 1.04f, 0.7f), counterGrey, true);
        R3.Box("Kick", counter, new Vector3(cx, 0.05f, cz - 0.352f), new Vector3(2.2f, 0.1f, 0.01f), kick, shadow: false);
        R3.Box("Top", counter, new Vector3(cx, 1.055f, cz), new Vector3(2.28f, 0.03f, 0.78f), counterTop);
        R3.Box("Return", counter, new Vector3(cx + 1.25f, 0.375f, cz + 0.25f), new Vector3(0.3f, 0.75f, 1.2f), counterGrey, true);
        R3.Shadow(new Vector3(cx, 0f, cz), new Vector2(2.3f, 0.8f));
        BuildInstructorTerminal(new Vector3(cx - 0.35f, 1.07f, cz + 0.08f));
        BuildLaptop(new Vector3(cx + 0.45f, 1.07f, cz + 0.12f));
    }

    static void BuildInstructorTerminal(Vector3 basePos)
    {
        // The instructor iMac = the official records station (CaseFilesTerminal with the level's Case Files).
        var term = new GameObject("InstructorTerminal").transform;
        term.SetParent(stations, false);
        term.position = basePos;
        term.rotation = Quaternion.LookRotation(Vector3.forward);   // screen faces north (the instructor's side)
        AllInOne(term, out _, out var screenCentre, collider: true);
        var vp = new GameObject("ViewPoint").transform;
        vp.SetParent(term, false);
        vp.localPosition = new Vector3(0f, screenCentre.y, 0.62f);
        vp.localRotation = Quaternion.LookRotation(Vector3.back);
        var ui = R3.WorldCanvas("RecordsUI", term, term.TransformPoint(new Vector3(0, screenCentre.y, screenCentre.z + 0.004f)), Vector3.forward, 0.52f, new Vector2(1280, 726), true);
        var cft = term.gameObject.AddComponent<CaseFilesTerminal>();
        R3.BuildCaseFilesUI(ui, cft, vp, "OFFICIAL RECORDS  ·  UPHSL BIÑAN IT");
        var so = new SerializedObject(cft);
        so.FindProperty("displayName").stringValue = "Check the official records";
        so.ApplyModifiedPropertiesWithoutUndo();
        // Tab labels from the level's Case Files (the terminal relabels them at runtime too; this keeps the editor view right).
        var l5 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level5Path);
        var tabs = so.FindProperty("tabButtons");
        for (int i = 0; tabs != null && l5 != null && i < tabs.arraySize; i++)
        {
            var b = tabs.GetArrayElementAtIndex(i).objectReferenceValue as Button;
            if (b == null) continue;
            bool used = i < l5.caseFiles.Count;
            b.gameObject.SetActive(used);
            if (used) UIKit.SetLabel(b, l5.caseFiles[i].title.ToUpperInvariant());
        }
        Tent(term.TransformPoint(new Vector3(0.36f, 0f, 0.12f)), Quaternion.LookRotation(Vector3.forward), "OFFICIAL RECORDS", new Color32(0x1F, 0x4E, 0x8C, 255));
    }

    static void BuildLaptop(Vector3 pos)
    {
        var shell = Mat("LaptopShell", "#3B3F45", 0.55f);
        var lap = new GameObject("Laptop").transform;
        lap.SetParent(computers, false);
        lap.position = pos;
        R3.LocalBox("Base", lap, new Vector3(0, 0.008f, 0), new Vector3(0.32f, 0.016f, 0.22f), shell);
        var lid = R3.LocalBox("Lid", lap, new Vector3(0, 0.11f, -0.1f), new Vector3(0.32f, 0.21f, 0.01f), shell);
        lid.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        var c = R3.WorldCanvas("Screen", lap, lid.transform.position + lid.transform.forward * 0.006f, lid.transform.forward, 0.29f, new Vector2(640, 400), false);
        c.rotation = Quaternion.LookRotation(-lid.transform.forward, lid.transform.up);
        UIKit.AddImage(c, new Color32(0x1C, 0x3D, 0x6E, 255)).raycastTarget = false;
        R3.Txt(c, "T", "Lecture notes - Week 9\nPhishing & social engineering", 34, Color.white, new Vector2(30, -30), new Vector2(580, 140), bold: true);
    }

    // ---------- Pods ----------

    static Mesh HexMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(HexMeshPath);
        if (mesh != null) return mesh;
        float R = Apothem / Mathf.Cos(30f * Mathf.Deg2Rad), h = TopT * 0.5f;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Vector3 Corner(int k, float y) { float a = (30f + 60f * k) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * R, y, Mathf.Sin(a) * R); }
        foreach (float y in new[] { h, -h })   // top and bottom fans
        {
            int c0 = v.Count;
            v.Add(new Vector3(0, y, 0)); n.Add(y > 0 ? Vector3.up : Vector3.down);
            for (int k = 0; k < 6; k++) { v.Add(Corner(k, y)); n.Add(y > 0 ? Vector3.up : Vector3.down); }
            for (int k = 0; k < 6; k++)
            {
                int a = c0 + 1 + k, b = c0 + 1 + (k + 1) % 6;
                if (y > 0) { t.Add(c0); t.Add(b); t.Add(a); } else { t.Add(c0); t.Add(a); t.Add(b); }
            }
        }
        for (int k = 0; k < 6; k++)   // sides
        {
            Vector3 a = Corner(k, h), b = Corner((k + 1) % 6, h), c = Corner((k + 1) % 6, -h), d = Corner(k, -h);
            Vector3 nn = Vector3.Cross(b - a, d - a).normalized;
            if (Vector3.Dot(nn, (a + b) * 0.5f) < 0) nn = -nn;
            int i0 = v.Count;
            v.AddRange(new[] { a, b, c, d }); n.AddRange(new[] { nn, nn, nn, nn });
            t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
        }
        mesh = new Mesh { name = "R5_HexDeskTop" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        // Check winding: every triangle's normal should point outwards; flip if not.
        var tris = mesh.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 p0 = v[tris[i]], p1 = v[tris[i + 1]], p2 = v[tris[i + 2]];
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n[tris[i]]) < 0f) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
        }
        mesh.triangles = tris;
        AssetDatabase.CreateAsset(mesh, HexMeshPath);
        return mesh;
    }

    static void BuildPod(int p)
    {
        var top = Mat("DeskWhite", "#F2F2F0", 0.45f);
        var leg = Mat("DeskLeg", "#C4C7CA", 0.6f);
        var core = Mat("DeskCore", "#D9DBDD", 0.3f);
        var partition = Mat("Partition", "#CBCED1", 0.3f);
        var cap = Mat("PartitionCap", "#B5B9BD", 0.7f);
        Vector2 c2 = Pods[p];
        var centre = new Vector3(c2.x, 0f, c2.y);

        var pod = new GameObject("Pod_" + (p + 1)).transform;
        pod.SetParent(desks, false);
        pod.position = centre;

        // Hexagonal desk top (flat faces towards the six seats) with a solid collider
        var deskTop = new GameObject("DeskTop", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        deskTop.transform.SetParent(pod, false);
        deskTop.transform.localPosition = new Vector3(0, DeskH + TopT * 0.5f, 0);
        deskTop.GetComponent<MeshFilter>().sharedMesh = HexMesh();
        deskTop.GetComponent<MeshRenderer>().sharedMaterial = top;
        var mc = deskTop.GetComponent<MeshCollider>();
        mc.sharedMesh = HexMesh();
        mc.convex = true;
        // Desk legs at the hexagon's corners and a central cable core
        float R = Apothem / Mathf.Cos(30f * Mathf.Deg2Rad);
        for (int k = 0; k < 6; k++)
        {
            float a = (30f + 60f * k) * Mathf.Deg2Rad;
            R3.LocalBox("Leg", pod, new Vector3(Mathf.Cos(a) * (R - 0.12f), DeskH / 2f, Mathf.Sin(a) * (R - 0.12f)), new Vector3(0.045f, DeskH, 0.045f), leg, true);
        }
        R3.LocalBox("CableCore", pod, new Vector3(0, DeskH / 2f, 0), new Vector3(0.42f, DeskH, 0.42f), core, true);
        R3.Shadow(centre, new Vector2(2.4f, 2.4f));

        // Radial partitions between the seats (towards the hexagon's corners)
        for (int k = 0; k < 6; k++)
        {
            float deg = 30f + 60f * k;
            var dir = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            R3.Box("Partition", pod, centre + dir * 0.6f + Vector3.up * (TopY + 0.21f), new Vector3(0.024f, 0.42f, 1.1f), partition, true, yaw);
            R3.Box("PartitionCap", pod, centre + dir * 0.6f + Vector3.up * (TopY + 0.425f), new Vector3(0.032f, 0.012f, 1.1f), cap, false, yaw, shadow: false);
        }

        // Six workstations
        for (int k = 0; k < 6; k++)
        {
            int number = p * 6 + k + 1;
            string id = $"MAC-{number:00}";
            float deg = 60f * k;
            var dir = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));
            bool flagged = System.Array.IndexOf(Level5Content.Stations, id) >= 0;
            BuildSeat(pod, centre, dir, id, flagged);
        }
    }

    static void BuildSeat(Transform pod, Vector3 centre, Vector3 dir, string id, bool flagged)
    {
        var root = new GameObject(id).transform;
        root.SetParent(flagged ? stations : computers, false);
        root.position = centre + dir * 0.6f + Vector3.up * TopY;
        root.rotation = Quaternion.LookRotation(dir);   // +Z = towards the person sitting there

        AllInOne(root, out var screen, out var screenCentre, collider: true);
        Peripherals(root);
        DeskLabel(root, id);

        if (flagged)
        {
            var term = root.gameObject.AddComponent<ComputerTerminal>();
            var hint = new GameObject("FrontHint").transform;
            hint.SetParent(root, false);
            hint.localPosition = new Vector3(0, screenCentre.y, 0.6f);
            var so = new SerializedObject(term);
            so.FindProperty("screenSurface").objectReferenceValue = screen;
            so.FindProperty("screenSubmesh").intValue = -1;
            so.FindProperty("frontHint").objectReferenceValue = hint;
            so.FindProperty("displayName").stringValue = $"Investigate workstation {id}";
            so.FindProperty("stationId").stringValue = id;
            so.FindProperty("interactDistance").floatValue = 2.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var device = root.gameObject.AddComponent<MessageDevice>();
            device.channel = MessageChannel.Email;
            device.idleLabel = $"Workstation {id}";
            // Physical cue (no glowing markers): an amber "flagged" tent card and the chair pushed back.
            Tent(root.TransformPoint(new Vector3(-0.22f, 0f, 0.36f)), root.rotation, "FLAGGED · " + id, new Color32(0xD9, 0x8C, 0x1A, 255));
        }
        else DecorativeScreen(root, screenCentre);

        // Chair facing the screen (flagged seats: pushed back and turned, ready to step in)
        var seatPos = centre + dir * (flagged ? ChairR + 0.1f : ChairR);
        float turn = flagged ? 28f : 0f;
        var face = Quaternion.Euler(0, turn, 0) * -dir;
        if (flagged && id == Level5Content.StationAccount && chairSrc != null) PlaceDeskChair(seatPos, face);
        else Chair(seatPos, face);
    }

    /// <summary>A 24-inch all-in-one in silver and white (no brand marks). Origin = desk surface below the stand; front = +Z.</summary>
    static void AllInOne(Transform root, out Transform screen, out Vector3 screenCentre, bool collider)
    {
        var silver = Mat("AioSilver", "#D5D8DB", 0.7f);
        var bezel = Mat("AioBezel", "#F5F6F7", 0.6f);
        var screenMat = Mat("AioScreen", "#0B0F14", 0.92f);
        R3.LocalBox("StandFoot", root, new Vector3(0, 0.004f, -0.03f), new Vector3(0.15f, 0.008f, 0.15f), silver);
        var arm = R3.LocalBox("StandArm", root, new Vector3(0, 0.11f, -0.085f), new Vector3(0.15f, 0.22f, 0.012f), silver);
        arm.transform.localRotation = Quaternion.Euler(-12f, 0, 0);
        R3.LocalBox("Body", root, new Vector3(0, 0.335f, -0.062f), new Vector3(0.547f, 0.461f, 0.012f), silver, collider);
        R3.LocalBox("Bezel", root, new Vector3(0, 0.392f, -0.0555f), new Vector3(0.547f, 0.347f, 0.002f), bezel, shadow: false);
        var s = R3.LocalBox("Screen", root, new Vector3(0, 0.395f, -0.0538f), new Vector3(0.52f, 0.293f, 0.002f), screenMat, shadow: false);
        screen = s.transform;
        screenCentre = new Vector3(0, 0.395f, -0.0528f);
    }

    static void Peripherals(Transform root)
    {
        var white = Mat("KeyboardWhite", "#F3F3F2", 0.5f);
        var keys = Mat("KeyGrey", "#E1E2E3", 0.3f);
        R3.LocalBox("Keyboard", root, new Vector3(0, 0.004f, 0.24f), new Vector3(0.28f, 0.008f, 0.115f), white);
        R3.LocalBox("Keys", root, new Vector3(0, 0.0085f, 0.243f), new Vector3(0.262f, 0.001f, 0.094f), keys, shadow: false);
        R3.LocalBox("Mouse", root, new Vector3(0.24f, 0.007f, 0.255f), new Vector3(0.057f, 0.014f, 0.105f), white);
    }

    static void DeskLabel(Transform root, string id)
    {
        // Small seat number on the desk's front edge (every university lab numbers its seats)
        var c = R3.WorldCanvas("SeatLabel", root, root.TransformPoint(new Vector3(-0.42f, 0.0015f, 0.5f)), root.up, 0.11f, new Vector2(220, 70), false);
        c.rotation = Quaternion.LookRotation(Vector3.down, -root.forward);   // lies flat, readable from the seat
        UIKit.AddImage(c, new Color32(0xE4, 0xE6, 0xE8, 255)).raycastTarget = false;
        R3.Txt(c, "N", id, 44, new Color32(0x3A, 0x40, 0x48, 255), new Vector2(0, -8), new Vector2(220, 56), bold: true, align: TextAlignmentOptions.Top);
    }

    static void Tent(Vector3 pos, Quaternion facing, string text, Color32 color)
    {
        var card = Mat("TentCard", "#F7F3EA", 0.2f);
        var t = new GameObject("TentCard").transform;
        t.SetParent(R3.props, false);
        t.SetPositionAndRotation(pos, facing);
        var front = R3.LocalBox("Front", t, new Vector3(0, 0.035f, 0.012f), new Vector3(0.16f, 0.075f, 0.002f), card);
        front.transform.localRotation = Quaternion.Euler(-18f, 0, 0);
        var back = R3.LocalBox("Back", t, new Vector3(0, 0.035f, -0.012f), new Vector3(0.16f, 0.075f, 0.002f), card);
        back.transform.localRotation = Quaternion.Euler(18f, 0, 0);
        var c = R3.WorldCanvas("Label", t, front.transform.position + front.transform.forward * 0.0015f, front.transform.forward, 0.15f, new Vector2(300, 140), false);
        c.rotation = Quaternion.LookRotation(-front.transform.forward, front.transform.up);
        var bg = UIKit.Rect("Band", c);
        UIKit.Stretch(bg);
        UIKit.AddImage(bg, color).raycastTarget = false;
        var label = R3.Txt(c, "T", text, 26, Color.white, new Vector2(0, -52), new Vector2(300, 50), bold: true, align: TextAlignmentOptions.Top);
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    /// <summary>Ordinary desktop content for the prop computers (varied, nothing interactive, no people).</summary>
    static void DecorativeScreen(Transform root, Vector3 screenCentre)
    {
        int v = screenVariant++ % 7;
        var c = R3.WorldCanvas("Desktop", root, root.TransformPoint(screenCentre + new Vector3(0, 0, 0.0015f)), root.forward, 0.52f, new Vector2(1280, 721), false);
        Color32[] wall =
        {
            new Color32(0x2B, 0x4F, 0x8E, 255), new Color32(0x7A, 0x3E, 0x7D, 255), new Color32(0xC9, 0x5B, 0x5B, 255),
            new Color32(0x1F, 0x6F, 0x78, 255), new Color32(0x35, 0x3A, 0x55, 255), new Color32(0x8A, 0x5A, 0x2E, 255), new Color32(0x24, 0x5C, 0x45, 255),
        };
        UIKit.AddImage(c, wall[v]).raycastTarget = false;
        var glow = UIKit.Rect("Glow", c);
        UIKit.Place(glow, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 60), new Vector2(760, 520));
        UIKit.AddImage(glow, new Color(1, 1, 1, 0.1f), UIKit.CircleSprite).raycastTarget = false;
        var menuBar = UIKit.Rect("MenuBar", c);
        UIKit.Place(menuBar, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1280, 30));
        UIKit.AddImage(menuBar, new Color(1, 1, 1, 0.55f)).raycastTarget = false;
        var dock = UIKit.Rect("Dock", c);
        UIKit.Place(dock, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(620, 70));
        UIKit.AddImage(dock, new Color(1, 1, 1, 0.35f), rounded: true).raycastTarget = false;
        for (int i = 0; i < 9; i++)
        {
            var icon = UIKit.Rect("Icon", dock);
            UIKit.Place(icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22 + i * 66, 0), new Vector2(52, 52));
            UIKit.AddImage(icon, Color.HSVToRGB((i * 0.11f + v * 0.07f) % 1f, 0.55f, 0.95f), rounded: true).raycastTarget = false;
        }
        if (v == 1 || v == 4) return;   // plain wallpaper (lock screen / idle)

        // An app window: browser on the student portal, a document, a code editor, a spreadsheet, a slide deck
        var win = UIKit.Rect("Window", c);
        UIKit.Place(win, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(980, 540));
        bool dark = v == 3;
        UIKit.AddImage(win, dark ? new Color32(0x1E, 0x1F, 0x24, 255) : new Color32(0xFA, 0xFA, 0xFA, 255), rounded: true).raycastTarget = false;
        var title = UIKit.Rect("TitleBar", win);
        UIKit.Place(title, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(980, 44));
        UIKit.AddImage(title, dark ? new Color32(0x2C, 0x2D, 0x33, 255) : new Color32(0xE8, 0xE9, 0xEB, 255)).raycastTarget = false;
        string[] titles = { "Student Portal - UPHSL Biñan", "", "Lab_Exercise_09.docx", "main.py - Visual Editor", "", "Grades_Tracker.xlsx", "Week9_Phishing_Awareness.pptx" };
        R3.Txt(win, "Title", titles[v], 22, dark ? new Color(0.8f, 0.8f, 0.85f) : new Color(0.25f, 0.27f, 0.3f), new Vector2(90, -10), new Vector2(860, 30));
        Color ink = dark ? new Color(0.55f, 0.75f, 0.95f) : new Color(0.72f, 0.74f, 0.78f);
        var rng = new System.Random(v * 13 + 1);
        if (v == 0)
        {
            var hero = UIKit.Rect("Hero", win);
            UIKit.Place(hero, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -44), new Vector2(980, 150));
            UIKit.AddImage(hero, new Color32(0x1F, 0x3B, 0x6E, 255)).raycastTarget = false;
            R3.Txt(win, "Portal", "Welcome to the Student Portal", 40, Color.white, new Vector2(40, -90), new Vector2(900, 60), bold: true);
        }
        for (int i = 0; i < 10; i++)
        {
            var line = UIKit.Rect("Line", win);
            float w = v == 5 ? 900 : 300 + (float)rng.NextDouble() * 580;
            UIKit.Place(line, new Vector2(0, 1), new Vector2(0, 1), new Vector2(dark && i % 3 == 1 ? 80 : 40, (v == 0 ? -220 : -80) - i * 40), new Vector2(w, 14));
            UIKit.AddImage(line, v == 5 && i % 2 == 0 ? new Color(0.82f, 0.9f, 0.82f) : ink).raycastTarget = false;
        }
    }

    // ---------- Chairs ----------

    /// <summary>A lab chair (the project's office-chair model, recoloured black, a little smaller) whose seat faces 'face'.</summary>
    static void Chair(Vector3 pos, Vector3 face)
    {
        var model = chairSrc != null ? chairSrc.Find("chair") : null;
        if (model == null) return;
        var g = Object.Instantiate(model.gameObject, R3.furniture);
        g.name = "LabChair";
        g.transform.localScale = chairSrc.lossyScale * ChairScale;
        g.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(-face.x, -face.z) * Mathf.Rad2Deg, 0f);   // the model's seat faces its -Z
        Recolor(g.transform);
        R3.Fit(g, pos.x, pos.z);
        var b = R3.RendererBounds(g);
        var col = new GameObject("LabChair_Collider", typeof(BoxCollider));
        col.transform.SetParent(R3.furniture, false);
        col.transform.position = new Vector3(b.center.x, 0.45f, b.center.z);
        col.GetComponent<BoxCollider>().size = new Vector3(b.size.x * 0.75f, 0.9f, b.size.z * 0.75f);
        R3.Shadow(new Vector3(b.center.x, 0f, b.center.z), new Vector2(b.size.x * 0.8f, b.size.z * 0.8f));
    }

    /// <summary>The kept, sittable DeskChair goes to the first flagged workstation.</summary>
    static void PlaceDeskChair(Vector3 pos, Vector3 face)
    {
        // Keeps its own scale (its seat and exit points depend on it). Like the model, yaw 180 faces north (+Z).
        chairSrc.rotation = Quaternion.Euler(0f, Mathf.Atan2(-face.x, -face.z) * Mathf.Rad2Deg, 0f);
        var model = chairSrc.Find("chair");
        var b = R3.RendererBounds(model != null ? model.gameObject : chairSrc.gameObject);
        chairSrc.position += new Vector3(pos.x - b.center.x, 0f, pos.z - b.center.z);
        Recolor(chairSrc);
        R3.Shadow(new Vector3(pos.x, 0f, pos.z), new Vector2(0.7f, 0.7f));
    }

    static void Recolor(Transform t)
    {
        var seat = Mat("ChairMesh", "#1C1E22", 0.25f);
        var frame = Mat("ChairChrome", "#9EA3A9", 0.8f);
        foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name;
                if (n.Contains("ChairBlack") || n == "carpet" || n.Contains("ChairFabric")) mats[i] = seat;
                else if (n.Contains("ChairFrame") || n.StartsWith("metal") || n.Contains("DarkMetal")) mats[i] = frame;
            }
            r.sharedMaterials = mats;
        }
    }

    // ---------- Props ----------

    static void BuildProps()
    {
        var white = Mat("AcWhite", "#F2F3F3", 0.4f);
        var grille = Mat("AcGrille", "#D3D6D9", 0.3f);
        var dark = Mat("CctvDark", "#2A2D31", 0.5f);
        var p = R3.props;

        // Floor-standing air conditioner by the west feature wall (north corner)
        float ax = -HalfX + 0.32f, az = HalfZ - 1.0f;
        R3.Box("AirConditioner", p, new Vector3(ax, 0.92f, az), new Vector3(0.38f, 1.84f, 0.55f), white, true);
        for (int i = 0; i < 8; i++)
            R3.Box("AcGrille", p, new Vector3(ax + 0.192f, 1.2f + i * 0.06f, az), new Vector3(0.004f, 0.018f, 0.42f), grille, shadow: false);
        R3.Shadow(new Vector3(ax, 0f, az), new Vector2(0.4f, 0.6f));

        // Ceiling CCTV domes in two corners
        foreach (var c in new[] { new Vector3(-HalfX + 0.5f, Height - 0.06f, -HalfZ + 0.5f), new Vector3(HalfX - 0.5f, Height - 0.06f, HalfZ - 0.5f) })
        {
            R3.Box("CctvBase", p, c + Vector3.up * 0.04f, new Vector3(0.14f, 0.02f, 0.14f), white, shadow: false);
            R3.Box("CctvDome", p, c, new Vector3(0.1f, 0.07f, 0.1f), dark, shadow: false);
        }

        // Wall clock above the door side
        var clock = R3.WorldCanvas("WallClock", R3.screens, new Vector3(HalfX - 0.02f, 2.25f, -1.5f), Vector3.left, 0.32f, new Vector2(320, 320), false);
        UIKit.AddImage(clock, Color.white, UIKit.CircleSprite).raycastTarget = false;
        R3.Txt(clock, "Brand", "UPHSL", 30, new Color(0.3f, 0.3f, 0.35f), new Vector2(0, -90), new Vector2(320, 50), bold: true, align: TextAlignmentOptions.Top);

        // Waste bins at the ends of the centre aisle and a printer on a cabinet by the east wall
        var bin = Mat("Bin", "#8D9196", 0.3f);
        R3.Box("Bin", p, new Vector3(-HalfX + 0.3f, 0.2f, -0.2f), new Vector3(0.28f, 0.4f, 0.28f), bin, true);
        R3.Box("Cabinet", p, new Vector3(HalfX - 0.32f, 0.4f, HalfZ - 1.0f), new Vector3(0.55f, 0.8f, 1.0f), Mat("CounterPanel", "#CFD2D5", 0.3f), true);
        R3.Box("Printer", p, new Vector3(HalfX - 0.34f, 0.95f, HalfZ - 1.0f), new Vector3(0.45f, 0.3f, 0.5f), white);
        R3.Box("PrinterTray", p, new Vector3(HalfX - 0.6f, 0.9f, HalfZ - 1.0f), new Vector3(0.12f, 0.02f, 0.3f), grille, shadow: false);
        R3.Shadow(new Vector3(HalfX - 0.32f, 0f, HalfZ - 1.0f), new Vector2(0.6f, 1.05f));
    }

    // ---------- Lighting ----------

    static void BuildLighting()
    {
        // Bright, even academic lighting: one soft-shadowed spot over each row's centre pod, unshadowed spots over the
        // others, daylight from the south windows, a front light for the counter. Contact shadows sit under furniture.
        foreach (var pod in Pods)
        {
            bool shadowed = Mathf.Abs(pod.x) < 0.1f;
            R3.Light("Ceiling_Pod", LightType.Spot, new Vector3(pod.x, Height - 0.05f, pod.y), Quaternion.Euler(90, 0, 0), "#F3F6FF", shadowed ? 1.7f : 1.5f, 6.5f, 135f, shadowed ? LightShadows.Soft : LightShadows.None);
        }
        R3.Light("Daylight_West", LightType.Spot, new Vector3(-4.5f, Height - 0.3f, -HalfZ + 0.2f), Quaternion.Euler(38, 0, 0), "#FFF4E2", 1.3f, 10f, 110f, LightShadows.Soft);
        R3.Light("Daylight_East", LightType.Spot, new Vector3(4.5f, Height - 0.3f, -HalfZ + 0.2f), Quaternion.Euler(38, 0, 0), "#FFF4E2", 1.2f, 10f, 110f, LightShadows.None);
        R3.Light("Front_Counter", LightType.Spot, new Vector3(-2.5f, Height - 0.05f, HalfZ - 1.2f), Quaternion.Euler(90, 0, 0), "#F3F6FF", 1.3f, 5f, 120f, LightShadows.None);
        R3.Light("Fill_Room", LightType.Point, new Vector3(0f, 2.3f, 0f), Quaternion.identity, "#E8EEF8", 0.6f, 15f, 0f, LightShadows.None);

        var probeGo = new GameObject("ReflectionProbe_MacLab");
        probeGo.transform.SetParent(R3.lighting, false);
        probeGo.transform.position = new Vector3(0, 1.4f, 0);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;   // captured once when the level loads
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution = 128;
        probe.size = new Vector3(HalfX * 2, Height, HalfZ * 2);
        probe.boxProjection = true;
        probe.intensity = 0.6f;
    }

    static void SetupRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        // Light ground colour too: the white floor bounces light up, so the ceiling reads white (as in the real lab).
        RenderSettings.ambientSkyColor = R3.Hex("#C9D2DE");
        RenderSettings.ambientEquatorColor = R3.Hex("#BCC1C8");
        RenderSettings.ambientGroundColor = R3.Hex("#C2C6CB");
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;
    }
}
