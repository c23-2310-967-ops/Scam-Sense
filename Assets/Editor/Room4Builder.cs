using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
using R3 = Room3Builder;

/// <summary>
/// Level 4 - Digital Forensics Lab: builds the lab scene.
///
/// Tools > Scam Sense > Level 4 > Create Room 4 Scene
///   Copies Room3_DigitalTrap.unity to Room4_DigitalForensicsLab.unity (only if Room 4 doesn't exist yet), so the
///   Player (kid character), EventSystem, level managers, email Workstation, sittable DeskChair and Phone are the
///   exact same working objects. Then builds the lab around them, points the level at Level 4, registers the
///   scene in Build Settings, adds Level 4 to the Level Catalog and links Level 3 -> Level 4.
/// Tools > Scam Sense > Level 4 > Rebuild Room 4 Environment
///   Rebuilds the lab's decor, lights and stations in the open Room 4 scene. Gameplay objects are kept.
///
/// Zones (12 x 9 m, 3.2 m ceiling): A entrance (south, spawn), B main workstation + case dashboard (north),
/// C evidence wall (west), D network analysis station (east), E final case review terminal (centre).
/// </summary>
public static class Room4Builder
{
    const string Room3Path = "Assets/Scenes/Room3_DigitalTrap.unity";
    public const string ScenePath = "Assets/Scenes/Room4_DigitalForensicsLab.unity";
    const string SceneName = "Room4_DigitalForensicsLab";
    const string Level3Path = "Assets/Levels/Level3_TheDigitalTrap.asset";
    const string Level4Path = "Assets/Levels/Level4_DigitalForensicsLab.asset";
    const string CatalogPath = "Assets/Levels/LevelCatalog.asset";

    static readonly string[] Keep = { "Workstation", "Desk", "Keyboard", "Mouse", "DeskLamp", "DeskChair", "PhoneTable", "Phone" };
    const float HalfX = 6f, HalfZ = 4.5f, Height = 3.2f;

    // ---------- Menu ----------

    [MenuItem("Tools/Scam Sense/Level 4/Create Room 4 Scene")]
    public static void CreateMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Level 4", "Stop Play Mode first.", "OK"); return; }
        Debug.Log("Room4Builder: " + Create());
    }

    [MenuItem("Tools/Scam Sense/Level 4/Rebuild Room 4 Environment")]
    public static void RebuildMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Level 4", "Stop Play Mode first.", "OK"); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) { EditorUtility.DisplayDialog("Level 4", "Open " + ScenePath + " first.", "OK"); return; }
        BuildEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Room4Builder: environment rebuilt and saved.");
    }

    public static string Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return ScenePath + " already exists - nothing overwritten. Use 'Rebuild Room 4 Environment' to refresh its decor.";

        SetupLevelAssets();
        if (!AssetDatabase.CopyAsset(Room3Path, ScenePath)) return "could not copy " + Room3Path;
        // Opened next to whatever is open (never saving or discarding it), built, saved, then closed again.
        var previous = EditorSceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null)
        {
            gm.gameObject.name = "Level4";
            if (gm.GetComponent<EvidenceTracker>() == null) gm.gameObject.AddComponent<EvidenceTracker>();
        }

        // Spawn by the entrance, facing the work area (north).
        var player = Object.FindFirstObjectByType<PlayerMotor>();
        if (player != null)
        {
            player.transform.position = new Vector3(0.9f, 1.02f, -3.6f);
            player.transform.rotation = Quaternion.Euler(0f, -6f, 0f);
        }
        var phone = GameObject.Find("Environment/Phone");
        if (phone != null) { var md = phone.GetComponent<MessageDevice>(); if (md != null) md.idleLabel = "Phone  -  no new texts"; }

        BuildEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (previous.IsValid() && previous.isLoaded && previous != scene)
        {
            EditorSceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        AddToBuildSettings();
        return "created " + ScenePath + ", added it to Build Settings and the Level Catalog, and linked Level 3 -> Level 4.";
    }

    static void AddToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == ScenePath)) return;
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>Creates/refreshes the Level 4 asset, links Level 3 to it and lists it in the catalog. Other levels' data is untouched.</summary>
    static LevelDefinition SetupLevelAssets()
    {
        var l4 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level4Path);
        if (l4 == null)
        {
            l4 = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(l4, Level4Path);
        }
        Level4Content.Fill(l4);
        l4.sceneName = SceneName;
        EditorUtility.SetDirty(l4);

        var l3 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level3Path);
        if (l3 != null)
        {
            l3.nextLevelId = l4.levelId;   // was already "level_4": saves that unlocked it stay valid
            l3.nextSceneName = SceneName;
            EditorUtility.SetDirty(l3);
        }

        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog != null && !catalog.levels.Contains(l4))
        {
            catalog.levels.Add(l4);
            EditorUtility.SetDirty(catalog);
        }
        AssetDatabase.SaveAssets();
        return l4;
    }

    static void AssignLevel()
    {
        var l4 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level4Path);
        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (l4 == null || gm == null) { Debug.LogWarning("Room4Builder: Level 4 asset or GameManager not found."); return; }
        var so = new SerializedObject(gm);
        so.FindProperty("level").objectReferenceValue = l4;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gm);
        if (gm.GetComponent<EvidenceTracker>() == null) gm.gameObject.AddComponent<EvidenceTracker>();
    }

    // ---------- Environment ----------

    public static void BuildEnvironment()
    {
        var envGo = GameObject.Find("Environment");
        if (envGo == null) envGo = new GameObject("Environment");
        var env = envGo.transform;
        for (int i = env.childCount - 1; i >= 0; i--)
        {
            var c = env.GetChild(i);
            if (!Keep.Contains(c.name)) Object.DestroyImmediate(c.gameObject);
        }

        R3.structure = R3.Group("Structure", env);
        R3.furniture = R3.Group("Furniture", env);
        R3.screens = R3.Group("Screens", env);
        R3.props = R3.Group("Props", env);
        R3.lighting = R3.Group("Lighting", env);
        R3.shadows = R3.Group("ContactShadows", env);

        BuildShell();
        BuildEntrance();
        BuildMainWorkstation(env);
        BuildEvidenceWall();
        BuildNetworkStation(env);
        BuildFinalCaseTerminal();
        BuildLabProps(env);
        BuildLightTable();
        BuildObservationWindow();
        BuildTimelineBoard();
        BuildLighting();
        SetupRenderSettings();
        LabRecolor(env);
        AssignLevel();

        foreach (var group in new[] { R3.structure, R3.furniture, R3.screens, R3.props })
            foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true))
                if (r.GetComponentInParent<Canvas>() == null && r.GetComponentInParent<CaseFilesTerminal>() == null && r.GetComponentInParent<FinalCaseTerminal>() == null)
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
    }

    static Material Mat(string name, string hex, float smooth, string emission = null, float strength = 0f) =>
        R3.M("R4_" + name, hex, smooth, emission, strength);

    static void BuildShell()
    {
        // A bright, clinical forensics lab (deliberately unlike Level 3's dark navy operations room):
        // light grey lab walls over a dark lower band, polished concrete floor, tiled ceiling with square
        // recessed lights. Cyan appears only on screens; amber marks evidence and safety lines.
        var floor = Mat("Floor", "#6B7077", 0.55f);              // polished concrete
        var seam = Mat("FloorSeam", "#555A61", 0.3f);
        var wall = Mat("Wall", "#AEB4BB", 0.15f);               // light lab wall
        var lowerBand = Mat("WallLower", "#2C3138", 0.3f);       // dark protective lower band
        var bandTrim = Mat("BandTrim", "#8A9098", 0.5f);
        var ceiling = Mat("Ceiling", "#C9CDD2", 0.05f);
        var tileLine = Mat("CeilingGrid", "#9AA0A7", 0.3f);
        var lightPanel = Mat("LightPanel", "#F4F7FB", 0.2f, "#F4F7FB", 1.4f);
        var metal = Mat("Metal", "#8C939C", 0.6f);
        var safety = Mat("SafetyYellow", "#E8B931", 0.35f);

        var s = R3.structure;
        R3.Box("Floor", s, new Vector3(0, -0.05f, 0), new Vector3(HalfX * 2 + 0.4f, 0.1f, HalfZ * 2 + 0.4f), floor, true);
        R3.Box("Ceiling", s, new Vector3(0, Height + 0.05f, 0), new Vector3(HalfX * 2 + 0.4f, 0.1f, HalfZ * 2 + 0.4f), ceiling, true);
        R3.Box("Wall_North", s, new Vector3(0, Height / 2, HalfZ + 0.1f), new Vector3(HalfX * 2 + 0.4f, Height, 0.2f), wall, true);
        R3.Box("Wall_South", s, new Vector3(0, Height / 2, -HalfZ - 0.1f), new Vector3(HalfX * 2 + 0.4f, Height, 0.2f), wall, true);
        R3.Box("Wall_East", s, new Vector3(HalfX + 0.1f, Height / 2, 0), new Vector3(0.2f, Height, HalfZ * 2), wall, true);
        R3.Box("Wall_West", s, new Vector3(-HalfX - 0.1f, Height / 2, 0), new Vector3(0.2f, Height, HalfZ * 2), wall, true);

        // Large polished-concrete slabs (2 m) with thin saw-cut joints
        for (float x = -HalfX + 2f; x < HalfX - 0.1f; x += 2f)
            R3.Box("FloorJoint_X", s, new Vector3(x, 0.002f, 0), new Vector3(0.012f, 0.004f, HalfZ * 2), seam, false, shadow: false);
        for (float z = -HalfZ + 2f; z < HalfZ - 0.1f; z += 2f)
            R3.Box("FloorJoint_Z", s, new Vector3(0, 0.002f, z), new Vector3(HalfX * 2, 0.004f, 0.012f), seam, false, shadow: false);

        // Dark lower wall band (1.0 m) with a metal cap rail, all round (gap for the door)
        void Band(string n, Vector3 c, Vector3 size, Vector3 capOffset, Vector3 capSize)
        {
            R3.Box(n, s, c, size, lowerBand);
            R3.Box(n + "_Cap", s, c + capOffset, capSize, bandTrim);
        }
        Band("LowerBand_N", new Vector3(0, 0.5f, HalfZ - 0.02f), new Vector3(HalfX * 2, 1.0f, 0.04f), new Vector3(0, 0.51f, -0.01f), new Vector3(HalfX * 2, 0.03f, 0.06f));
        Band("LowerBand_W", new Vector3(-HalfX + 0.02f, 0.5f, 0), new Vector3(0.04f, 1.0f, HalfZ * 2), new Vector3(0.01f, 0.51f, 0), new Vector3(0.06f, 0.03f, HalfZ * 2));
        Band("LowerBand_E", new Vector3(HalfX - 0.02f, 0.5f, 0), new Vector3(0.04f, 1.0f, HalfZ * 2), new Vector3(-0.01f, 0.51f, 0), new Vector3(0.06f, 0.03f, HalfZ * 2));
        float doorL = 1.2f - 0.7f, doorR = 1.2f + 0.7f;
        Band("LowerBand_SW", new Vector3((-HalfX + doorL) / 2f, 0.5f, -HalfZ + 0.02f), new Vector3(doorL + HalfX, 1.0f, 0.04f), new Vector3(0, 0.51f, 0.01f), new Vector3(doorL + HalfX, 0.03f, 0.06f));
        Band("LowerBand_SE", new Vector3((HalfX + doorR) / 2f, 0.5f, -HalfZ + 0.02f), new Vector3(HalfX - doorR, 1.0f, 0.04f), new Vector3(0, 0.51f, 0.01f), new Vector3(HalfX - doorR, 0.03f, 0.06f));

        // Tiled ceiling (0.6 m grid) with square recessed light panels
        for (float x = -HalfX + 0.6f; x < HalfX - 0.05f; x += 0.6f)
            R3.Box("CeilingGrid_X", s, new Vector3(x, Height - 0.004f, 0), new Vector3(0.02f, 0.008f, HalfZ * 2), tileLine, false, shadow: false);
        for (float z = -HalfZ + 0.6f; z < HalfZ - 0.05f; z += 0.6f)
            R3.Box("CeilingGrid_Z", s, new Vector3(0, Height - 0.004f, z), new Vector3(HalfX * 2, 0.008f, 0.02f), tileLine, false, shadow: false);
        foreach (var p in new[] { new Vector2(-3f, 2.4f), new Vector2(0f, 2.4f), new Vector2(3f, 2.4f), new Vector2(-3f, -0.6f), new Vector2(0f, -0.6f), new Vector2(3f, -0.6f), new Vector2(-3f, -3.3f), new Vector2(3f, -3.3f) })
        {
            R3.Box("RecessedLight", s, new Vector3(p.x, Height - 0.012f, p.y), new Vector3(0.58f, 0.012f, 0.58f), lightPanel, shadow: false);
            R3.Box("RecessedLightFrame", s, new Vector3(p.x, Height - 0.008f, p.y), new Vector3(0.62f, 0.008f, 0.62f), metal, shadow: false);
        }

        // Yellow safety walkway: from the door to the final case console, then to the workstation
        void Line(Vector3 c, Vector3 size) => R3.Box("SafetyLine", s, c, size, safety, false, shadow: false);
        foreach (float x in new[] { 0.45f, 1.95f })
            Line(new Vector3(x, 0.004f, -2.5f), new Vector3(0.05f, 0.006f, 3.6f));
        Line(new Vector3(1.2f, 0.004f, -0.7f), new Vector3(1.55f, 0.006f, 0.05f));
        // Floor decal: case number inside the walkway
        var decal = R3.WorldCanvas("FloorDecal", s, new Vector3(1.2f, 0.006f, -2.6f), Vector3.up, 1.2f, new Vector2(1200, 300), false);
        decal.rotation = Quaternion.Euler(90f, 0f, 0f);
        R3.Txt(decal, "T", "CASE-04", 200, new Color(0.91f, 0.73f, 0.19f, 0.55f), new Vector2(0, -40), new Vector2(1200, 240), bold: true, align: TextAlignmentOptions.Top).characterSpacing = 20;

        R3.Box("CableTray", s, new Vector3(4.6f, Height - 0.25f, 0.4f), new Vector3(0.45f, 0.06f, 7.6f), metal);
    }

    static void BuildEntrance()
    {
        var metal = Mat("Metal", "#6B7480", 0.6f);
        var door = Mat("DoorSteel", "#3A424D", 0.55f);
        var glass = Mat("Glass", "#0D1824", 0.92f);
        var dark = Mat("DarkMetal", "#22272E", 0.45f);
        var ledGreen = Mat("LedGreen", "#22C55E", 0.5f, "#22C55E", 2f);
        float dx = 1.2f, dz = -HalfZ + 0.03f;
        var s = R3.structure;

        R3.Box("DoorFrame_L", s, new Vector3(dx - 0.62f, 1.12f, dz), new Vector3(0.09f, 2.24f, 0.08f), metal);
        R3.Box("DoorFrame_R", s, new Vector3(dx + 0.62f, 1.12f, dz), new Vector3(0.09f, 2.24f, 0.08f), metal);
        R3.Box("DoorFrame_T", s, new Vector3(dx, 2.26f, dz), new Vector3(1.33f, 0.09f, 0.08f), metal);
        R3.Box("SecureDoor", s, new Vector3(dx, 1.1f, dz + 0.01f), new Vector3(1.14f, 2.2f, 0.06f), door);
        R3.Box("DoorWindow", s, new Vector3(dx, 1.62f, dz + 0.045f), new Vector3(0.24f, 0.62f, 0.01f), glass, shadow: false);
        R3.Box("PushBar", s, new Vector3(dx, 1.02f, dz + 0.07f), new Vector3(0.8f, 0.05f, 0.05f), metal);

        // Access-control panel: keypad + card reader with a green status light
        R3.Box("AccessPanel", s, new Vector3(dx - 0.95f, 1.3f, dz + 0.02f), new Vector3(0.14f, 0.22f, 0.035f), dark);
        R3.Box("AccessPanelScreen", s, new Vector3(dx - 0.95f, 1.36f, dz + 0.04f), new Vector3(0.1f, 0.05f, 0.004f), R3.ScreenMat(), shadow: false);
        R3.Box("AccessPanelLed", s, new Vector3(dx - 0.95f, 1.24f, dz + 0.04f), new Vector3(0.025f, 0.025f, 0.004f), ledGreen, shadow: false);

        // Wall-mounted lab sign beside the door (seen when turning back to the entrance)
        var sign = new GameObject("LabSign").transform;
        sign.SetParent(R3.screens, false);
        R3.Box("Frame", sign, new Vector3(-1.6f, 2.15f, -HalfZ + 0.04f), new Vector3(1.9f, 0.5f, 0.05f), dark);
        var c = R3.WorldCanvas("UI", sign, new Vector3(-1.6f, 2.15f, -HalfZ + 0.068f), Vector3.forward, 1.84f, new Vector2(1840, 440), false);
        UIKit.AddImage(c, new Color32(0x07, 0x0F, 0x1C, 255)).raycastTarget = false;
        R3.Txt(c, "Title", "DIGITAL FORENSICS LAB", 120, UIKit.TextPrimary, new Vector2(60, -70), new Vector2(1720, 170), bold: true).characterSpacing = 8;
        R3.Txt(c, "Sub", "Trace the evidence. Expose the scam.   ·   Authorised investigators only", 46, UIKit.Accent, new Vector2(60, -270), new Vector2(1720, 80));
    }

    static void BuildMainWorkstation(Transform env)
    {
        // Zone B: the kept email Workstation, desk, chair and phone table, plus tower, extra monitor and case dashboard.
        var dark = Mat("DarkMetal", "#22272E", 0.45f);
        var tower = Mat("Tower", "#1A1E24", 0.5f);
        var cyan = Mat("LedCyan", "#22D3EE", 0.5f, "#22D3EE", 2f);

        R3.Box("ComputerTower", R3.furniture, new Vector3(-1.45f, 0.24f, 4.05f), new Vector3(0.22f, 0.48f, 0.5f), tower, true);
        R3.Box("TowerLed", R3.furniture, new Vector3(-1.45f, 0.42f, 3.795f), new Vector3(0.12f, 0.008f, 0.004f), cyan, shadow: false);
        R3.Shadow(new Vector3(-1.45f, 0f, 4.05f), new Vector2(0.3f, 0.55f));

        // Live case dashboard above the desk (reuses OpsWallDisplay: messages reviewed, accuracy, status)
        var wd = new GameObject("CaseDashboard").transform;
        wd.SetParent(R3.screens, false);
        R3.Box("Frame", wd, new Vector3(0, 2.72f, HalfZ - 0.05f), new Vector3(2.9f, 1.06f, 0.06f), dark);
        R3.Box("Screen", wd, new Vector3(0, 2.72f, HalfZ - 0.082f), new Vector3(2.8f, 0.96f, 0.005f), R3.ScreenMat(), shadow: false);
        var c = R3.WorldCanvas("UI", wd, new Vector3(0, 2.72f, HalfZ - 0.086f), Vector3.back, 2.8f, new Vector2(1800, 617), false);
        R3.BuildWallDisplayUI(c, wd.gameObject.AddComponent<OpsWallDisplay>(), "CASE DASHBOARD  ·  CASE-04  ·  DIGITAL FORENSICS LAB");

        R3.Shadow(new Vector3(0f, 0f, 3.76f), new Vector2(2.5f, 1.5f));
        R3.Shadow(new Vector3(1.75f, 0f, 3.9f), new Vector2(1.0f, 0.9f));
        R3.Mug("Mug_Main", new Vector3(0.62f, 1.15f, 3.95f), Mat("Mug", "#E2E8F0", 0.5f));
    }

    static void BuildEvidenceWall()
    {
        // Zone C: a wall-sized evidence screen (CaseFilesTerminal with the level's Case Files) framed by printed evidence cards.
        var dark = Mat("DarkMetal", "#22272E", 0.45f);
        var cork = Mat("Board", "#1B232E", 0.15f);
        var paper = Mat("Paper", "#ECEAE3", 0.1f);
        var pin = Mat("PinOrange", "#F97316", 0.4f, "#F97316", 0.6f);
        var str = Mat("StringRed", "#DC2626", 0.3f);
        float x = -HalfX + 0.03f, cz = 0.4f, cy = 1.62f;

        var wall = new GameObject("EvidenceWall").transform;
        wall.SetParent(R3.screens, false);
        R3.Box("Backboard", wall, new Vector3(x, 1.65f, cz), new Vector3(0.04f, 2.1f, 4.6f), cork);

        var term = new GameObject("EvidenceWallTerminal").transform;
        term.SetParent(wall, false);
        float sx = x + 0.05f;
        R3.Box("Body", term, new Vector3(sx, cy, cz), new Vector3(0.06f, 1.12f, 1.9f), dark, true);
        R3.Box("Screen", term, new Vector3(sx + 0.032f, cy, cz), new Vector3(0.004f, 1.04f, 1.82f), R3.ScreenMat(), shadow: false);
        var vp = new GameObject("ViewPoint").transform;
        vp.SetParent(term, false);
        vp.position = new Vector3(sx + 1.2f, cy, cz);
        vp.rotation = Quaternion.LookRotation(Vector3.left);
        var ui = R3.WorldCanvas("EvidenceUI", term, new Vector3(sx + 0.036f, cy, cz), Vector3.right, 1.82f, new Vector2(1280, 732), true);
        var cft = term.gameObject.AddComponent<CaseFilesTerminal>();
        R3.BuildCaseFilesUI(ui, cft, vp, "EVIDENCE WALL  ·  CASE-04");
        var so = new SerializedObject(cft);
        so.FindProperty("displayName").stringValue = "Inspect the evidence wall";
        so.ApplyModifiedPropertiesWithoutUndo();
        var l4 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level4Path);
        if (l4 != null) LabelTabs(cft, l4.caseFiles);

        // Printed evidence cards around the screen (readable labels; the full records are on the screen)
        string[,] cards =
        {
            { "EMAIL · 08:41", "billing@novacloud-support.example" },
            { "DOMAIN RECORD", "3 days old · owner hidden · not NovaCloud's" },
            { "TEXT · 10:05", "+1 (555) 017-8820 · same link" },
            { "SHARED SERVER", "also hosts novacl0ud-login.example" },
        };
        var cardPos = new[] { new Vector2(cz - 1.75f, 2.35f), new Vector2(cz + 1.75f, 2.35f), new Vector2(cz - 1.75f, 0.95f), new Vector2(cz + 1.75f, 0.95f) };
        var pins = new List<Vector3>();
        for (int i = 0; i < 4; i++)
        {
            var cardRoot = new GameObject("EvidenceCard_" + (i + 1)).transform;
            cardRoot.SetParent(wall, false);
            Vector3 centre = new Vector3(x + 0.03f, cardPos[i].y, cardPos[i].x);
            R3.Box("Paper", cardRoot, centre, new Vector3(0.005f, 0.42f, 0.7f), paper, shadow: false);
            var cc = R3.WorldCanvas("Text", cardRoot, centre + Vector3.right * 0.004f, Vector3.right, 0.66f, new Vector2(660, 400), false);
            R3.Txt(cc, "Head", cards[i, 0], 58, new Color32(0x1F, 0x29, 0x37, 255), new Vector2(30, -40), new Vector2(600, 80), bold: true);
            R3.Txt(cc, "Body", cards[i, 1], 40, new Color32(0x37, 0x41, 0x51, 255), new Vector2(30, -140), new Vector2(600, 220));
            Vector3 pinPos = centre + new Vector3(0.012f, 0.17f, 0f);
            R3.Box("Pin", cardRoot, pinPos, new Vector3(0.02f, 0.025f, 0.025f), pin, shadow: false);
            pins.Add(pinPos);
        }
        // Connections: email -> domain <- text, domain -> server
        StringBetween(wall, pins[0], pins[1], str);
        StringBetween(wall, pins[2], pins[1], str);
        StringBetween(wall, pins[1], pins[3], str);

        // Label strip above the wall
        var label = R3.WorldCanvas("WallLabel", wall, new Vector3(x + 0.03f, 2.86f, cz), Vector3.right, 2.4f, new Vector2(2400, 140), false);
        R3.Txt(label, "T", "EVIDENCE WALL  ·  OPERATION BILLING STORM", 76, UIKit.Accent, new Vector2(0, -20), new Vector2(2400, 110), bold: true, align: TextAlignmentOptions.Top).characterSpacing = 6;
        R3.Shadow(new Vector3(x + 0.4f, 0f, cz), new Vector2(0.5f, 4.6f));
    }

    /// <summary>Shows the station's real record titles on its tab buttons and hides unused tabs.</summary>
    static void LabelTabs(CaseFilesTerminal terminal, List<CaseFile> pages)
    {
        var tabs = new SerializedObject(terminal).FindProperty("tabButtons");
        for (int i = 0; i < tabs.arraySize; i++)
        {
            var b = tabs.GetArrayElementAtIndex(i).objectReferenceValue as Button;
            if (b == null) continue;
            bool used = pages != null && i < pages.Count;
            b.gameObject.SetActive(used);
            if (used) UIKit.SetLabel(b, pages[i].title.ToUpperInvariant());
        }
        var title = terminal.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "PageTitle");
        var body = terminal.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "PageBody");
        if (pages != null && pages.Count > 0)
        {
            if (title != null) title.text = pages[0].title;
            if (body != null) body.text = pages[0].body;
        }
    }

    static void StringBetween(Transform parent, Vector3 a, Vector3 b, Material m)
    {
        var g = R3.Box("RedString", parent, (a + b) / 2f, new Vector3(0.004f, 0.004f, Vector3.Distance(a, b)), m, shadow: false);
        g.transform.rotation = Quaternion.LookRotation(b - a);
    }

    static void BuildNetworkStation(Transform env)
    {
        // Zone D: desk against the east wall; the left monitor is the interactive records station, the right shows a live-looking traffic graph.
        var deskSrc = env.Find("Desk");
        var chairSrc = env.Find("DeskChair");
        var dark = Mat("DarkMetal", "#22272E", 0.45f);
        var keyboard = Mat("Keyboard", "#16191E", 0.3f);

        var station = new GameObject("NetworkStation").transform;
        station.SetParent(R3.furniture, false);
        if (deskSrc != null)
        {
            var desk = Object.Instantiate(deskSrc.gameObject, station);
            desk.name = "CaseDesk";
            desk.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            R3.Fit(desk, HalfX - 0.62f, 0.9f);
        }
        R3.Shadow(new Vector3(HalfX - 0.62f, 0f, 0.9f), new Vector2(1.5f, 2.5f));

        float sx = HalfX - 0.42f, sy = 1.59f;
        var term = new GameObject("NetworkAnalysisTerminal").transform;
        term.SetParent(station, false);
        float tz = 0.45f;
        R3.Box("Base", term, new Vector3(sx + 0.02f, 1.16f, tz), new Vector3(0.22f, 0.02f, 0.3f), dark);
        R3.Box("Stand", term, new Vector3(sx + 0.03f, 1.24f, tz), new Vector3(0.05f, 0.16f, 0.07f), dark);
        R3.Box("Body", term, new Vector3(sx, sy, tz), new Vector3(0.05f, 0.62f, 1.02f), dark, true);
        R3.Box("Screen", term, new Vector3(sx - 0.027f, sy, tz), new Vector3(0.004f, 0.56f, 0.96f), R3.ScreenMat(), shadow: false);
        var vp = new GameObject("ViewPoint").transform;
        vp.SetParent(term, false);
        vp.position = new Vector3(sx - 0.63f, sy, tz);
        vp.rotation = Quaternion.LookRotation(Vector3.right);
        var ui = R3.WorldCanvas("NetworkUI", term, new Vector3(sx - 0.031f, sy, tz), Vector3.left, 0.96f, new Vector2(1280, 747), true);
        var cft = term.gameObject.AddComponent<CaseFilesTerminal>();
        R3.BuildCaseFilesUI(ui, cft, vp, "NETWORK ANALYSIS  ·  CASE-04");
        var so = new SerializedObject(cft);
        so.FindProperty("displayName").stringValue = "Use the network analysis station";
        so.FindProperty("useOwnPages").boolValue = true;
        var pagesProp = so.FindProperty("fallbackPages");
        var pages = Level4Content.NetworkStationPages();
        pagesProp.arraySize = pages.Count;
        for (int i = 0; i < pages.Count; i++)
        {
            pagesProp.GetArrayElementAtIndex(i).FindPropertyRelative("title").stringValue = pages[i].title;
            pagesProp.GetArrayElementAtIndex(i).FindPropertyRelative("body").stringValue = pages[i].body;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        LabelTabs(cft, pages);

        // Second monitor: decorative traffic graph (no interaction, fictional data)
        var mon = new GameObject("TrafficMonitor").transform;
        mon.SetParent(R3.screens, false);
        float mz = 1.55f;
        R3.Box("Base", mon, new Vector3(sx + 0.02f, 1.16f, mz), new Vector3(0.22f, 0.02f, 0.3f), dark);
        R3.Box("Stand", mon, new Vector3(sx + 0.03f, 1.24f, mz), new Vector3(0.05f, 0.16f, 0.07f), dark);
        R3.Box("Body", mon, new Vector3(sx, sy, mz), new Vector3(0.05f, 0.5f, 0.86f), dark);
        R3.Box("Screen", mon, new Vector3(sx - 0.027f, sy, mz), new Vector3(0.004f, 0.45f, 0.8f), R3.ScreenMat(), shadow: false);
        var g = R3.WorldCanvas("UI", mon, new Vector3(sx - 0.031f, sy, mz), Vector3.left, 0.8f, new Vector2(960, 540), false);
        UIKit.AddImage(g, new Color32(0x07, 0x0F, 0x1C, 255)).raycastTarget = false;
        R3.Txt(g, "T", "MESSAGE TRAFFIC  ·  CAMPAIGN WINDOW", 34, UIKit.Accent, new Vector2(30, -24), new Vector2(900, 50), bold: true);
        var rng = new System.Random(4);
        for (int i = 0; i < 24; i++)
        {
            bool campaign = i >= 8 && i <= 11;
            float h = campaign ? 240 + (float)rng.NextDouble() * 120 : 50 + (float)rng.NextDouble() * 90;
            var b = UIKit.Rect("Bar", g);
            UIKit.Place(b, new Vector2(0, 0), new Vector2(0, 0), new Vector2(30 + i * 37f, 60), new Vector2(26, h));
            UIKit.AddImage(b, campaign ? new Color32(0xF9, 0x73, 0x16, 255) : new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.55f)).raycastTarget = false;
        }
        R3.Txt(g, "Legend", $"<color=#F97316>■</color> 08:40-10:10 campaign spike    <color={UIKit.HexAccent}>■</color> normal", 24, UIKit.TextSecondary, new Vector2(30, -78), new Vector2(900, 36));

        R3.Box("Keyboard", R3.props, new Vector3(HalfX - 0.95f, 1.16f, 0.9f), new Vector3(0.16f, 0.02f, 0.46f), keyboard);
        R3.Box("Mouse", R3.props, new Vector3(HalfX - 0.95f, 1.16f, 1.35f), new Vector3(0.1f, 0.025f, 0.06f), keyboard);
        if (chairSrc != null) R3.SpareChair(chairSrc, new Vector3(HalfX - 1.65f, 0f, 2.0f), -60f);
    }

    static void BuildFinalCaseTerminal()
    {
        // Zone E: a standing review console in the middle of the lab, its tilted screen facing the entrance.
        var dark = Mat("DarkMetal", "#22272E", 0.45f);
        var metal = Mat("Metal", "#6B7480", 0.6f);
        Vector3 basePos = new Vector3(0f, 0f, 0.55f);

        var root = new GameObject("FinalCaseTerminal").transform;
        root.SetParent(R3.furniture, false);
        root.position = basePos;
        R3.LocalBox("Plinth", root, new Vector3(0, 0.03f, 0), new Vector3(0.9f, 0.06f, 0.7f), metal);
        R3.LocalBox("Column", root, new Vector3(0, 0.55f, 0.05f), new Vector3(0.34f, 1.0f, 0.3f), dark, true);
        var edge = Mat("EdgeCyan", "#22D3EE", 0.4f, "#22D3EE", 0.8f);
        R3.LocalBox("CyanEdge_F", root, new Vector3(0, 0.062f, -0.352f), new Vector3(0.9f, 0.006f, 0.012f), edge, shadow: false);
        R3.LocalBox("CyanEdge_B", root, new Vector3(0, 0.062f, 0.352f), new Vector3(0.9f, 0.006f, 0.012f), edge, shadow: false);
        R3.LocalBox("CyanEdge_L", root, new Vector3(-0.452f, 0.062f, 0), new Vector3(0.012f, 0.006f, 0.7f), edge, shadow: false);
        R3.LocalBox("CyanEdge_R", root, new Vector3(0.452f, 0.062f, 0), new Vector3(0.012f, 0.006f, 0.7f), edge, shadow: false);

        var pivot = new GameObject("ScreenPivot").transform;
        pivot.SetParent(root, false);
        pivot.localPosition = new Vector3(0, 1.28f, 0f);
        pivot.localRotation = Quaternion.Euler(28f, 0f, 0f);   // top tilted away from the viewer
        R3.LocalBox("Body", pivot, Vector3.zero, new Vector3(1.0f, 0.64f, 0.05f), dark, true);
        R3.LocalBox("Screen", pivot, new Vector3(0, 0, -0.027f), new Vector3(0.94f, 0.58f, 0.004f), R3.ScreenMat(), shadow: false);
        var vp = new GameObject("ViewPoint").transform;
        vp.SetParent(pivot, false);
        vp.localPosition = new Vector3(0, 0, -0.66f);
        vp.localRotation = Quaternion.identity;

        var canvasGo = new GameObject("FinalCaseUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var rt = (RectTransform)canvasGo.transform;
        rt.SetParent(pivot, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
        rt.sizeDelta = new Vector2(1280, 790);
        rt.localPosition = new Vector3(0, 0, -0.0295f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one * (0.94f / 1280f);

        var terminal = root.gameObject.AddComponent<FinalCaseTerminal>();
        BuildFinalCaseUI(rt, terminal, vp);
        R3.Shadow(basePos, new Vector2(0.9f, 0.7f));
    }

    static void BuildFinalCaseUI(RectTransform root, FinalCaseTerminal terminal, Transform viewPoint)
    {
        UIKit.AddImage(root, UIKit.Background).raycastTarget = false;
        var header = UIKit.Rect("Header", root);
        header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
        header.sizeDelta = new Vector2(0, 72); header.anchoredPosition = Vector2.zero;
        UIKit.AddImage(header, new Color32(0x07, 0x0C, 0x16, 255)).raycastTarget = false;
        var ht = UIKit.Text(header, "Title", "FINAL CASE REVIEW  ·  CASE-04", 30, UIKit.Accent, TextAlignmentOptions.MidlineLeft, bold: true);
        UIKit.Stretch(ht.rectTransform); ht.margin = new Vector4(28, 0, 0, 0); ht.characterSpacing = 4;

        RectTransform View(string name)
        {
            var v = UIKit.Rect(name, root);
            v.anchorMin = Vector2.zero; v.anchorMax = Vector2.one;
            v.offsetMin = new Vector2(28, 100); v.offsetMax = new Vector2(-28, -90);
            var lay = UIKit.Vertical(v, 0, 14);
            lay.childControlHeight = true; lay.childForceExpandHeight = false;
            return v;
        }
        LayoutElement Grow(TMP_Text t, float min)
        {
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f; le.minHeight = 60f;
            t.enableAutoSizing = true; t.fontSizeMax = t.fontSize; t.fontSizeMin = min;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return le;
        }

        // Locked view: live checklist
        var locked = View("LockedView");
        var status = UIKit.Text(locked, "Status", "LOCKED · COMPLETE THE CHECKLIST", 34, UIKit.Warning, bold: true);
        UIKit.Divider(locked);
        var checklist = UIKit.Text(locked, "Checklist", "", 27, UIKit.TextPrimary);
        checklist.lineSpacing = 18f;
        Grow(checklist, 18);
        UIKit.Text(locked, "Hint", "Evidence stations: <b>evidence wall</b> (west wall) and <b>network analysis</b> (east desk). Reported emails and web pages are on the main workstation; texts arrive on your phone.", 21, UIKit.TextSecondary);

        // Case view: summary, findings, choice, submit
        var caseV = View("CaseView");
        var caseTitle = UIKit.Text(caseV, "CaseTitle", "", 34, UIKit.TextPrimary, bold: true);
        var caseBody = UIKit.Text(caseV, "CaseBody", "", 23, UIKit.TextPrimary);
        caseBody.lineSpacing = 6f;
        var findings = UIKit.Text(caseV, "Findings", "", 21, UIKit.TextPrimary);
        findings.lineSpacing = 4f;
        Grow(findings, 15);
        var timer = UIKit.Text(caseV, "Timer", "", 20, UIKit.TextSecondary);
        var selection = UIKit.Text(caseV, "Selection", "", 21, UIKit.TextPrimary);
        var choiceRow = UIKit.Rect("ChoiceRow", caseV);
        UIKit.Horizontal(choiceRow, 14);
        UIKit.Size(choiceRow, -1, 64);
        var scam = UIKit.HudButton(choiceRow, "×  SCAM", null, UIKit.HudVariant.Danger, 26);
        scam.name = "ScamButton";
        var legit = UIKit.HudButton(choiceRow, "✓  LEGITIMATE", null, UIKit.HudVariant.Safe, 26);
        legit.name = "LegitimateButton";
        var submit = UIKit.HudButton(caseV, "SUBMIT FINDINGS", null, UIKit.HudVariant.Primary, 28);
        submit.name = "SubmitFindingsButton";
        UIKit.Size(submit, -1, 66);
        UnityEventTools.AddPersistentListener(scam.onClick, new UnityAction(terminal.SelectScam));
        UnityEventTools.AddPersistentListener(legit.onClick, new UnityAction(terminal.SelectLegitimate));
        UnityEventTools.AddPersistentListener(submit.onClick, new UnityAction(terminal.SubmitFindings));

        // Feedback view
        var fb = View("FeedbackView");
        var verdict = UIKit.Text(fb, "Verdict", "", 36, UIKit.TextPrimary, bold: true);
        var explanation = UIKit.Text(fb, "Explanation", "", 23, UIKit.TextPrimary);
        explanation.lineSpacing = 6f;
        Grow(explanation, 16);
        var score = UIKit.Text(fb, "Score", "", 20, UIKit.TextSecondary);
        var finish = UIKit.HudButton(fb, "FINISH CASE", null, UIKit.HudVariant.Primary, 28);
        finish.name = "FinishCaseButton";
        UIKit.Size(finish, -1, 66);
        UnityEventTools.AddPersistentListener(finish.onClick, new UnityAction(terminal.FinishCase));

        // Exit (hidden on the locked view)
        var exit = UIKit.HudButton(root, "EXIT  [Esc]", null, UIKit.HudVariant.Secondary, 22);
        exit.name = "ExitButton";
        UIKit.Place((RectTransform)exit.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 20), new Vector2(300, 62));
        UnityEventTools.AddPersistentListener(exit.onClick, new UnityAction(terminal.Exit));

        caseV.gameObject.SetActive(false);
        fb.gameObject.SetActive(false);

        var so = new SerializedObject(terminal);
        so.FindProperty("screenCanvas").objectReferenceValue = root.GetComponent<Canvas>();
        so.FindProperty("viewPoint").objectReferenceValue = viewPoint;
        so.FindProperty("lockedView").objectReferenceValue = locked.gameObject;
        so.FindProperty("caseView").objectReferenceValue = caseV.gameObject;
        so.FindProperty("feedbackView").objectReferenceValue = fb.gameObject;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("checklistText").objectReferenceValue = checklist;
        so.FindProperty("caseTitle").objectReferenceValue = caseTitle;
        so.FindProperty("caseBody").objectReferenceValue = caseBody;
        so.FindProperty("findingsText").objectReferenceValue = findings;
        so.FindProperty("timerText").objectReferenceValue = timer;
        so.FindProperty("selectionText").objectReferenceValue = selection;
        so.FindProperty("scamButton").objectReferenceValue = scam;
        so.FindProperty("legitButton").objectReferenceValue = legit;
        so.FindProperty("submitButton").objectReferenceValue = submit;
        so.FindProperty("verdictText").objectReferenceValue = verdict;
        so.FindProperty("explanationText").objectReferenceValue = explanation;
        so.FindProperty("scoreText").objectReferenceValue = score;
        so.FindProperty("finishButton").objectReferenceValue = finish;
        so.FindProperty("exitButton").objectReferenceValue = exit;
        var req = so.FindProperty("requiredEvidence");
        req.arraySize = Level4Content.RequiredEvidence.Length;
        for (int i = 0; i < Level4Content.RequiredEvidence.Length; i++) req.GetArrayElementAtIndex(i).stringValue = Level4Content.RequiredEvidence[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildLabProps(Transform env)
    {
        var metal = Mat("Metal", "#6B7480", 0.6f);
        var locker = Mat("Locker", "#3B4450", 0.4f);
        var bench = Mat("Benchtop", "#2B3139", 0.5f);
        var bag = Mat("EvidenceBag", "#C9D3DC", 0.6f);
        var tag = Mat("EvidenceTag", "#F97316", 0.3f);
        var drive = Mat("Drive", "#11151A", 0.55f);
        var green = Mat("LedGreen", "#22C55E", 0.5f, "#22C55E", 2f);
        var orange = Mat("LedOrange", "#F97316", 0.5f, "#F97316", 2.2f);
        var rack = Mat("Rack", "#1A1E24", 0.45f);

        // Evidence lockers (south-west)
        for (int i = 0; i < 3; i++)
        {
            float x = -5.35f + i * 0.62f;
            R3.Box("EvidenceLocker", R3.furniture, new Vector3(x, 0.95f, -HalfZ + 0.32f), new Vector3(0.6f, 1.9f, 0.55f), locker, true);
            R3.Box("LockerSeam", R3.furniture, new Vector3(x, 0.95f, -HalfZ + 0.598f), new Vector3(0.008f, 1.8f, 0.004f), metal, shadow: false);
            R3.Box("LockerHandle", R3.furniture, new Vector3(x + 0.05f, 1.05f, -HalfZ + 0.61f), new Vector3(0.02f, 0.16f, 0.02f), metal);
            R3.Box("LockerLabel", R3.furniture, new Vector3(x - 0.15f, 1.6f, -HalfZ + 0.6f), new Vector3(0.16f, 0.06f, 0.004f), tag, shadow: false);
        }
        R3.Shadow(new Vector3(-4.73f, 0f, -HalfZ + 0.32f), new Vector2(1.9f, 0.6f));

        // Forensic workbench with seized devices (south-east)
        float bx = 4.4f, bz = -3.6f;
        R3.Box("Workbench", R3.furniture, new Vector3(bx, 0.9f, bz), new Vector3(1.8f, 0.06f, 0.8f), bench, true);
        foreach (var lx in new[] { -0.82f, 0.82f })
            foreach (var lz in new[] { -0.34f, 0.34f })
                R3.Box("BenchLeg", R3.furniture, new Vector3(bx + lx, 0.44f, bz + lz), new Vector3(0.05f, 0.88f, 0.05f), metal);
        R3.Box("Laptop_Base", R3.props, new Vector3(bx - 0.45f, 0.94f, bz), new Vector3(0.36f, 0.02f, 0.25f), drive);
        var lid = R3.Box("Laptop_Lid", R3.props, new Vector3(bx - 0.45f, 1.06f, bz + 0.13f), new Vector3(0.36f, 0.24f, 0.012f), drive);
        lid.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
        for (int i = 0; i < 3; i++)
        {
            R3.Box("EvidenceBag", R3.props, new Vector3(bx + 0.1f + i * 0.24f, 0.94f, bz - 0.1f + (i % 2) * 0.12f), new Vector3(0.2f, 0.015f, 0.28f), bag, rotY: i * 9f - 9f, shadow: false);
            R3.Box("BagTag", R3.props, new Vector3(bx + 0.1f + i * 0.24f, 0.95f, bz - 0.2f + (i % 2) * 0.12f), new Vector3(0.08f, 0.004f, 0.04f), tag, rotY: i * 9f - 9f, shadow: false);
        }
        R3.Box("HardDrive", R3.props, new Vector3(bx + 0.62f, 0.95f, bz + 0.22f), new Vector3(0.1f, 0.03f, 0.15f), drive);
        R3.Box("WriteBlocker", R3.props, new Vector3(bx + 0.42f, 0.95f, bz + 0.24f), new Vector3(0.12f, 0.04f, 0.09f), metal);
        R3.Box("WriteBlockerLed", R3.props, new Vector3(bx + 0.42f, 0.972f, bz + 0.195f), new Vector3(0.012f, 0.004f, 0.004f), green, shadow: false);
        R3.Shadow(new Vector3(bx, 0f, bz), new Vector2(1.9f, 0.9f));

        // Equipment rack next to the network station
        float rx = HalfX - 0.42f, rz = -1.5f;
        R3.Box("EquipmentRack", R3.furniture, new Vector3(rx, 1.0f, rz), new Vector3(0.7f, 2.0f, 0.65f), rack, true);
        var rng = new System.Random(8);
        for (int row = 0; row < 6; row++)
            for (int k = 0; k < 4; k++)
                R3.Box("RackLed", R3.furniture, new Vector3(rx - 0.352f, 0.5f + row * 0.2f, rz - 0.18f + k * 0.05f), new Vector3(0.004f, 0.016f, 0.016f), row == 2 && k == 2 ? orange : green, shadow: false);
        var hum = new GameObject("RackHum", typeof(AudioSource), typeof(RackHum));
        hum.transform.SetParent(R3.furniture, false);
        hum.transform.position = new Vector3(rx - 0.4f, 1.1f, rz);
        hum.GetComponent<AudioSource>().playOnAwake = false;
        var la = Object.FindFirstObjectByType<LevelAudio>();
        if (la != null)
        {
            var group = new SerializedObject(la).FindProperty("musicGroup");
            if (group != null && group.objectReferenceValue != null)
            {
                var so = new SerializedObject(hum.GetComponent<RackHum>());
                so.FindProperty("outputGroup").objectReferenceValue = group.objectReferenceValue;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        R3.Shadow(new Vector3(rx, 0f, rz), new Vector2(0.8f, 0.75f));

        R3.Badge(new Vector3(-0.85f, 1.152f, 3.45f), 15f);
    }

    /// <summary>Forensic light table: a backlit glass top with evidence photos and bags (south-west).</summary>
    static void BuildLightTable()
    {
        var metal = Mat("Metal", "#8C939C", 0.6f);
        var glow = Mat("LightTableTop", "#F2F6FA", 0.8f, "#DDE8F5", 1.2f);
        var photo = Mat("Photo", "#2A3038", 0.4f);
        var bag = Mat("EvidenceBag", "#C9D3DC", 0.6f);
        var tag = Mat("EvidenceTag", "#E8B931", 0.3f);
        float x = -3.4f, z = -2.7f, top = 0.95f;
        var t = new GameObject("LightTable").transform;
        t.SetParent(R3.furniture, false);
        R3.Box("Frame", t, new Vector3(x, top - 0.04f, z), new Vector3(1.7f, 0.06f, 0.95f), metal, true);
        R3.Box("GlowTop", t, new Vector3(x, top, z), new Vector3(1.6f, 0.02f, 0.85f), glow, shadow: false);
        foreach (var lx in new[] { -0.78f, 0.78f })
            foreach (var lz in new[] { -0.4f, 0.4f })
                R3.Box("Leg", t, new Vector3(x + lx, (top - 0.07f) / 2f, z + lz), new Vector3(0.05f, top - 0.07f, 0.05f), metal);
        var rng = new System.Random(12);
        for (int i = 0; i < 5; i++)
            R3.Box("Photo", R3.props, new Vector3(x - 0.6f + i * 0.3f, top + 0.012f, z - 0.15f + (float)rng.NextDouble() * 0.3f), new Vector3(0.18f, 0.003f, 0.13f), photo, rotY: rng.Next(-12, 12), shadow: false);
        R3.Box("EvidenceBag", R3.props, new Vector3(x + 0.45f, top + 0.018f, z + 0.25f), new Vector3(0.24f, 0.012f, 0.32f), bag, rotY: 8f, shadow: false);
        R3.Box("BagTag", R3.props, new Vector3(x + 0.45f, top + 0.026f, z + 0.12f), new Vector3(0.09f, 0.004f, 0.04f), tag, rotY: 8f, shadow: false);
        // Magnifier lamp on an arm
        R3.Box("LampBase", R3.props, new Vector3(x - 0.7f, top + 0.02f, z + 0.32f), new Vector3(0.12f, 0.02f, 0.12f), metal);
        R3.Box("LampArm", R3.props, new Vector3(x - 0.7f, top + 0.25f, z + 0.32f), new Vector3(0.02f, 0.45f, 0.02f), metal);
        R3.Box("LampHead", R3.props, new Vector3(x - 0.55f, top + 0.46f, z + 0.25f), new Vector3(0.22f, 0.04f, 0.16f), metal);
        R3.Shadow(new Vector3(x, 0f, z), new Vector2(1.7f, 0.95f));
    }

    /// <summary>Observation window on the east wall looking into a lit server room (north of the network station).</summary>
    static void BuildObservationWindow()
    {
        var frame = Mat("WindowFrame", "#3A3F46", 0.5f);
        var glass = Mat("WindowGlass", "#9FB3C8", 0.95f);
        MakeTransparent(glass, 0.16f);
        var roomBack = Mat("ServerRoomBack", "#0B1622", 0.2f, "#0E2A44", 1.0f);
        var rackFace = Mat("ServerRack", "#0A0D11", 0.5f);
        var ledCyan = Mat("LedCyan", "#22D3EE", 0.5f, "#22D3EE", 2f);
        var ledGreen = Mat("LedGreen", "#22C55E", 0.5f, "#22C55E", 2f);
        float wx = HalfX - 0.01f, zc = 3.35f, yc = 1.75f, w = 1.7f, h = 1.2f;
        var win = new GameObject("ObservationWindow").transform;
        win.SetParent(R3.structure, false);
        // The "server room" seen through the glass: a lit backdrop with rack faces (flat, very cheap)
        R3.Box("ServerRoom", win, new Vector3(wx + 0.005f, yc, zc), new Vector3(0.005f, h, w), roomBack, shadow: false);
        var rng = new System.Random(5);
        for (int r = 0; r < 4; r++)
        {
            float rz = zc - 0.62f + r * 0.42f;
            R3.Box("RackFace", win, new Vector3(wx, yc - 0.05f, rz), new Vector3(0.004f, 1.0f, 0.3f), rackFace, shadow: false);
            for (int k = 0; k < 6; k++)
                R3.Box("Led", win, new Vector3(wx - 0.003f, yc - 0.4f + k * 0.14f, rz - 0.08f + (float)rng.NextDouble() * 0.16f), new Vector3(0.002f, 0.012f, 0.03f), k % 2 == 0 ? ledCyan : ledGreen, shadow: false);
        }
        R3.Box("Glass", win, new Vector3(wx - 0.03f, yc, zc), new Vector3(0.01f, h, w), glass, shadow: false);
        R3.Box("Frame_T", win, new Vector3(wx - 0.04f, yc + h / 2f + 0.03f, zc), new Vector3(0.08f, 0.06f, w + 0.12f), frame);
        R3.Box("Frame_B", win, new Vector3(wx - 0.05f, yc - h / 2f - 0.03f, zc), new Vector3(0.1f, 0.06f, w + 0.12f), frame);
        R3.Box("Frame_L", win, new Vector3(wx - 0.04f, yc, zc - w / 2f - 0.03f), new Vector3(0.08f, h, 0.06f), frame);
        R3.Box("Frame_R", win, new Vector3(wx - 0.04f, yc, zc + w / 2f + 0.03f), new Vector3(0.08f, h, 0.06f), frame);
        var label = R3.WorldCanvas("Label", win, new Vector3(wx - 0.045f, yc + h / 2f + 0.16f, zc), Vector3.left, 1.6f, new Vector2(1600, 120), false);
        R3.Txt(label, "T", "EVIDENCE SERVER ROOM  ·  RESTRICTED", 64, new Color32(0x2C, 0x31, 0x38, 255), new Vector2(0, -20), new Vector2(1600, 100), bold: true, align: TextAlignmentOptions.Top).characterSpacing = 4;
    }

    /// <summary>Standard shader in Transparent mode (glass): reflections stay, the room behind shows through.</summary>
    static void MakeTransparent(Material m, float alpha)
    {
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)BlendMode.One);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        var c = m.color; c.a = alpha; m.color = c;
        EditorUtility.SetDirty(m);
    }

    /// <summary>Whiteboard with a hand-written-style investigation timeline (north wall, west).</summary>
    static void BuildTimelineBoard()
    {
        var board = Mat("Whiteboard", "#F3F4F2", 0.7f);
        var frame = Mat("Metal", "#8C939C", 0.6f);
        float bx = -4.1f, by = 1.75f, bz = HalfZ - 0.03f;
        var t = new GameObject("TimelineBoard").transform;
        t.SetParent(R3.props, false);
        R3.Box("Board", t, new Vector3(bx, by, bz), new Vector3(2.2f, 1.1f, 0.02f), board);
        R3.Box("Frame_T", t, new Vector3(bx, by + 0.57f, bz - 0.01f), new Vector3(2.26f, 0.04f, 0.04f), frame);
        R3.Box("Frame_B", t, new Vector3(bx, by - 0.57f, bz - 0.02f), new Vector3(2.26f, 0.05f, 0.08f), frame);   // marker tray
        var c = R3.WorldCanvas("Ink", t, new Vector3(bx, by, bz - 0.012f), Vector3.back, 2.1f, new Vector2(2100, 1050), false);
        var ink = new Color32(0x1E, 0x3A, 0x5F, 255);
        var red = new Color32(0xC2, 0x41, 0x0C, 255);
        R3.Txt(c, "Head", "CASE-04  ·  BILLING STORM  ·  TIMELINE", 72, ink, new Vector2(60, -40), new Vector2(2000, 100), bold: true);
        R3.Txt(c, "Body",
            "08:41   billing email  ->  novacloud-support.example\n" +
            "09:12   real NovaCloud alert (blocked sign-in)  ✓\n" +
            "09:40   fake sign-in page reported  (novacl0ud-login)\n" +
            "10:05   follow-up text, same link  ->  \"FINAL NOTICE\"",
            54, ink, new Vector2(60, -180), new Vector2(2000, 520));
        R3.Txt(c, "Note", "same server? check the domain record!", 58, red, new Vector2(60, -760), new Vector2(2000, 120), bold: true);
    }

    /// <summary>
    /// Lab furniture colours for this scene only: white laminate desks, black chairs, brushed metal.
    /// Works whether the renderers still use the model's own materials or Level 3's recoloured ones.
    /// The model assets and the other rooms are not changed.
    /// </summary>
    static void LabRecolor(Transform env)
    {
        var deskTop = Mat("DeskWhite", "#D9DDE2", 0.35f);
        var metal = Mat("Metal", "#8C939C", 0.6f);
        var seat = Mat("ChairBlack", "#1E2126", 0.2f);
        var frame = Mat("ChairFrame", "#3A3F46", 0.45f);
        var map = new Dictionary<string, Material>
        {
            { "wood", deskTop }, { "R3_DeskTop", deskTop },
            { "metal", metal }, { "R3_Metal", metal },
            { "carpet", seat }, { "R3_ChairFabric", seat },
            { "metalMedium", frame }, { "R3_DarkMetal", frame },
        };
        var targets = new List<Transform> { env.Find("Desk"), env.Find("DeskChair"), env.Find("PhoneTable") };
        targets.AddRange(R3.furniture.GetComponentsInChildren<Transform>().Where(t => t.name == "CaseDesk" || t.name == "SpareChair"));
        foreach (var t in targets)
        {
            if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && map.TryGetValue(mats[i].name, out var rep)) { mats[i] = rep; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }

    static void BuildLighting()
    {
        // Spot lights only for shadows (no point-light cube maps), small unshadowed glows for screens.
        R3.Light("Ceiling_Spot_Work", LightType.Spot, new Vector3(0f, Height - 0.1f, 2.6f), Quaternion.Euler(90, 0, 0), "#DCE7FA", 2.3f, 8f, 120f, LightShadows.Soft);
        R3.Light("Ceiling_Spot_Centre", LightType.Spot, new Vector3(0f, Height - 0.1f, -0.4f), Quaternion.Euler(90, 0, 0), "#DCE7FA", 2.0f, 8f, 125f, LightShadows.Soft);
        R3.Light("EvidenceWall_Spot", LightType.Spot, new Vector3(-3.6f, Height - 0.12f, 0.4f), Quaternion.Euler(35, -90, 0), "#F1F4FA", 2.2f, 7f, 80f, LightShadows.Soft);
        R3.Light("Ceiling_Spot_East", LightType.Spot, new Vector3(3.6f, Height - 0.1f, 0.2f), Quaternion.Euler(90, 0, 0), "#DCE7FA", 2.3f, 8f, 125f, LightShadows.None);
        R3.Light("Fill_Lab", LightType.Point, new Vector3(0f, 2.8f, -1.6f), Quaternion.identity, "#A9B8D3", 0.8f, 9f, 0f, LightShadows.None);
        R3.Light("Fill_West", LightType.Point, new Vector3(-3.8f, 2.6f, -2.6f), Quaternion.identity, "#A9B8D3", 0.5f, 6f, 0f, LightShadows.None);
        R3.Light("Entrance_Warm", LightType.Spot, new Vector3(1.2f, Height - 0.1f, -3.4f), Quaternion.Euler(90, 0, 0), "#FFD9B0", 1.4f, 6f, 110f, LightShadows.None);
        R3.Light("DeskLamp_Light", LightType.Spot, new Vector3(0.93f, 1.62f, 4.1f), Quaternion.Euler(70, 200, 0), "#FFD7A0", 1.6f, 3.2f, 95f, LightShadows.None);
        R3.Light("FinalCase_Glow", LightType.Point, new Vector3(0f, 1.4f, 0.0f), Quaternion.identity, "#38BDF8", 0.6f, 2.0f, 0f, LightShadows.None);
        R3.Light("Network_Glow", LightType.Point, new Vector3(HalfX - 1.1f, 1.6f, 0.9f), Quaternion.identity, "#40BFFF", 0.6f, 2.2f, 0f, LightShadows.None);

        var probeGo = new GameObject("ReflectionProbe_Lab");
        probeGo.transform.SetParent(R3.lighting, false);
        probeGo.transform.position = new Vector3(0, 1.6f, 0);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.size = new Vector3(HalfX * 2, Height, HalfZ * 2);
        probe.boxProjection = true;
        probe.intensity = 0.7f;
    }

    static void SetupRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = R3.Hex("#6C7892");
        RenderSettings.ambientEquatorColor = R3.Hex("#4E586C");
        RenderSettings.ambientGroundColor = R3.Hex("#2E343E");
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;
    }
}
