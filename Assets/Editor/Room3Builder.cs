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

/// <summary>
/// Level 3 - The Digital Trap: builds the cybersecurity operations room.
///
/// Tools > Scam Sense > Level 3 > Create Room 3 Scene
///   Copies Room2_InvestigationOffice.unity to Room3_DigitalTrap.unity (only if Room 3 doesn't exist yet), so the
///   Player (kid character), EventSystem, level managers, email Workstation, sittable DeskChair and Phone are the
///   exact same, already-working objects. Then builds the new environment, points the level at Level 3, adds the
///   scene to Build Settings and links Level 2 -> Level 3.
/// Tools > Scam Sense > Level 3 > Rebuild Room 3 Environment
///   Rebuilds only the decor/lights/screens of the open Room 3 scene. Gameplay objects are kept as they are.
///
/// Room: 12 x 9 m, 3.4 m ceiling. North = main email station + wall display, east = server racks,
/// west = case-files computer + digital case wall, south = entrance, shelves, coffee point; centre = analyst desk.
/// </summary>
public static class Room3Builder
{
    const string Room2Path = "Assets/Scenes/Room2_InvestigationOffice.unity";
    public const string ScenePath = "Assets/Scenes/Room3_DigitalTrap.unity";
    const string SceneName = "Room3_DigitalTrap";
    const string Level2Path = "Assets/Levels/Level2_TheInvestigation.asset";
    const string Level3OldPath = "Assets/Levels/Level3_AdvancedThreats.asset";
    const string Level3Path = "Assets/Levels/Level3_TheDigitalTrap.asset";

    // Kept from Room 2 (gameplay objects) - moved 0.9 m north to sit against the new north wall.
    static readonly string[] Keep = { "Workstation", "Desk", "Keyboard", "Mouse", "DeskLamp", "DeskChair", "PhoneTable", "Phone" };
    const float KeepOffsetZ = 0.9f;

    const float HalfX = 6f, HalfZ = 4.5f, Height = 3.4f;

    // ---------- Menu ----------

    [MenuItem("Tools/Scam Sense/Level 3/Create Room 3 Scene")]
    public static void CreateMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Level 3", "Stop Play Mode first.", "OK"); return; }
        Debug.Log("Room3Builder: " + Create());
    }

    [MenuItem("Tools/Scam Sense/Level 3/Rebuild Room 3 Environment")]
    public static void RebuildMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Level 3", "Stop Play Mode first.", "OK"); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) { EditorUtility.DisplayDialog("Level 3", "Open " + ScenePath + " first.", "OK"); return; }
        BuildEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Room3Builder: environment rebuilt and saved.");
    }

    public static string Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return ScenePath + " already exists - nothing overwritten. Use 'Rebuild Room 3 Environment' to refresh its decor.";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "cancelled.";

        var level3 = SetupLevelAssets();
        if (!AssetDatabase.CopyAsset(Room2Path, ScenePath)) return "could not copy " + Room2Path;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Level managers: same components, now running Level 3.
        var managers = GameObject.Find("Level2");
        if (managers != null)
        {
            managers.name = "Level3";
            var gm = managers.GetComponent<GameManager>();
            var so = new SerializedObject(gm);
            so.FindProperty("level").objectReferenceValue = level3;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Room 2 decor that the new room replaces (this is the copy; Room 2 itself is untouched).
        foreach (var n in new[] { "VisualUpgrade", "ContactShadows" })
        {
            var g = GameObject.Find(n);
            if (g != null) Object.DestroyImmediate(g);
        }

        var env = GameObject.Find("Environment").transform;
        foreach (var n in Keep)
        {
            var t = env.Find(n);
            if (t != null) t.position += new Vector3(0f, 0f, KeepOffsetZ);
        }
        var phone = env.Find("Phone");
        if (phone != null) { var md = phone.GetComponent<MessageDevice>(); if (md != null) md.idleLabel = "Phone  -  no new texts"; }

        // Spawn by the entrance, facing into the room.
        var player = Object.FindFirstObjectByType<PlayerMotor>();
        if (player != null)
        {
            player.transform.position = new Vector3(3.6f, 1.02f, -3.3f);
            player.transform.rotation = Quaternion.Euler(0f, -38f, 0f);
        }

        BuildEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();
        return "created " + ScenePath + ", added it to Build Settings and linked Level 2 -> Level 3.";
    }

    static void AddToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == ScenePath)) return;
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---------- Level data ----------

    static LevelDefinition SetupLevelAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level3Path) == null && AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level3OldPath) != null)
            AssetDatabase.MoveAsset(Level3OldPath, Level3Path);   // same GUID: the Level Catalog keeps its reference
        var l3 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level3Path);
        if (l3 == null)
        {
            l3 = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(l3, Level3Path);
        }
        Level3Content.Fill(l3);
        l3.sceneName = SceneName;
        EditorUtility.SetDirty(l3);

        var l2 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level2Path);
        if (l2 != null)
        {
            l2.nextLevelId = l3.levelId;
            l2.nextSceneName = SceneName;
            EditorUtility.SetDirty(l2);
        }
        AssetDatabase.SaveAssets();
        return l3;
    }

    // ---------- Environment ----------

    static Transform structure, furniture, screens, props, lighting, shadows;

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
        var oldProbe = GameObject.Find("ReflectionProbe_Ops");
        if (oldProbe != null) Object.DestroyImmediate(oldProbe);

        structure = Group("Structure", env);
        furniture = Group("Furniture", env);
        screens = Group("Screens", env);
        props = Group("Props", env);
        lighting = Group("Lighting", env);
        shadows = Group("ContactShadows", env);

        BuildShell();
        BuildMainStation(env);
        BuildServerRacks();
        BuildCaseFilesStation(env);
        BuildCaseWall();
        BuildAnalystDesk(env);
        BuildSouthWall();
        BuildLighting();
        SetupRenderSettings();
        Recolor(env);

        AssignLevel();

        // New decor never moves: let Unity batch it. (Kept gameplay objects are left as they were.)
        foreach (var group in new[] { structure, furniture, screens, props })
            foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true))
                if (r.GetComponentInParent<Canvas>() == null)
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
    }

    /// <summary>Makes sure this scene's GameManager runs Level 3.</summary>
    static void AssignLevel()
    {
        var l3 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level3Path);
        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (l3 == null || gm == null) { Debug.LogWarning("Room3Builder: Level 3 asset or GameManager not found."); return; }
        var so = new SerializedObject(gm);
        so.FindProperty("level").objectReferenceValue = l3;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gm);
    }

    static void BuildShell()
    {
        var floor = M("R3_Floor", "#1B212A", 0.35f);
        var seam = M("R3_FloorSeam", "#12161D", 0.2f);
        var wall = M("R3_Wall", "#24314A", 0.1f);
        var panel = M("R3_WallPanel", "#2C3B58", 0.15f);
        var ceiling = M("R3_Ceiling", "#141A24", 0.05f);
        var baseboard = M("R3_Baseboard", "#0E131B", 0.2f);
        var trim = M("R3_TrimCyan", "#22D3EE", 0.4f, "#22D3EE", 1.3f);
        var lightPanel = M("R3_LightPanel", "#DCEBFF", 0.2f, "#DCEBFF", 1.6f);
        var vent = M("R3_Vent", "#2A313C", 0.3f);
        var door = M("R3_Door", "#2B3442", 0.35f);
        var metal = M("R3_Metal", "#5B6472", 0.5f);
        var ledGreen = M("R3_LedGreen", "#22C55E", 0.5f, "#22C55E", 2f);

        Box("Floor", structure, new Vector3(0, -0.05f, 0), new Vector3(HalfX * 2 + 0.4f, 0.1f, HalfZ * 2 + 0.4f), floor, true);
        Box("Ceiling", structure, new Vector3(0, Height + 0.05f, 0), new Vector3(HalfX * 2 + 0.4f, 0.1f, HalfZ * 2 + 0.4f), ceiling, true);
        Box("Wall_North", structure, new Vector3(0, Height / 2, HalfZ + 0.1f), new Vector3(HalfX * 2 + 0.4f, Height, 0.2f), wall, true);
        Box("Wall_South", structure, new Vector3(0, Height / 2, -HalfZ - 0.1f), new Vector3(HalfX * 2 + 0.4f, Height, 0.2f), wall, true);
        Box("Wall_East", structure, new Vector3(HalfX + 0.1f, Height / 2, 0), new Vector3(0.2f, Height, HalfZ * 2), wall, true);
        Box("Wall_West", structure, new Vector3(-HalfX - 0.1f, Height / 2, 0), new Vector3(0.2f, Height, HalfZ * 2), wall, true);

        // Raised-floor tile seams (1.2 m grid)
        for (float x = -HalfX + 1.2f; x < HalfX - 0.1f; x += 1.2f)
            Box("FloorSeam_X", structure, new Vector3(x, 0.002f, 0), new Vector3(0.02f, 0.004f, HalfZ * 2), seam, false, shadow: false);
        for (float z = -HalfZ + 1.2f; z < HalfZ - 0.1f; z += 1.2f)
            Box("FloorSeam_Z", structure, new Vector3(0, 0.002f, z), new Vector3(HalfX * 2, 0.004f, 0.02f), seam, false, shadow: false);

        // Baseboards and wall pilasters
        Box("Baseboard_N", structure, new Vector3(0, 0.06f, HalfZ - 0.015f), new Vector3(HalfX * 2, 0.12f, 0.03f), baseboard);
        Box("Baseboard_S", structure, new Vector3(0, 0.06f, -HalfZ + 0.015f), new Vector3(HalfX * 2, 0.12f, 0.03f), baseboard);
        Box("Baseboard_E", structure, new Vector3(HalfX - 0.015f, 0.06f, 0), new Vector3(0.03f, 0.12f, HalfZ * 2), baseboard);
        Box("Baseboard_W", structure, new Vector3(-HalfX + 0.015f, 0.06f, 0), new Vector3(0.03f, 0.12f, HalfZ * 2), baseboard);
        foreach (float z in new[] { -3.0f, 3.0f })
            Box("Pilaster_E", structure, new Vector3(HalfX - 0.06f, Height / 2, z), new Vector3(0.12f, Height, 0.3f), panel);
        foreach (float z in new[] { -3.9f, 3.0f })   // clear of the digital case wall (z -3.4 .. -0.8)
            Box("Pilaster_W", structure, new Vector3(-HalfX + 0.06f, Height / 2, z), new Vector3(0.12f, Height, 0.3f), panel);
        foreach (float x in new[] { -4.2f, 4.2f })
            Box("Pilaster_N", structure, new Vector3(x, Height / 2, HalfZ - 0.06f), new Vector3(0.3f, Height, 0.12f), panel);
        // Lower wall panelling on the north wall behind the main station
        Box("WallPanel_N", structure, new Vector3(0, 1.1f, HalfZ - 0.02f), new Vector3(8.1f, 2.2f, 0.04f), panel);
        // The only cyan accent line: frames the main station under the wall display
        Box("AccentStrip_N", structure, new Vector3(0, 2.18f, HalfZ - 0.045f), new Vector3(3.4f, 0.025f, 0.015f), trim, shadow: false);

        // Ceiling: light panels and vents
        foreach (var p in new[] { new Vector2(-2.4f, 1.8f), new Vector2(2.4f, 1.8f), new Vector2(-2.4f, -1.8f), new Vector2(2.4f, -1.8f) })
        {
            Box("CeilingLightFrame", structure, new Vector3(p.x, Height - 0.015f, p.y), new Vector3(1.5f, 0.03f, 0.6f), metal, shadow: false);
            Box("CeilingLightPanel", structure, new Vector3(p.x, Height - 0.035f, p.y), new Vector3(1.4f, 0.02f, 0.5f), lightPanel, shadow: false);
        }
        foreach (var p in new[] { new Vector2(0f, -0.2f), new Vector2(-4.6f, -2.6f), new Vector2(4.4f, 3.2f) })
        {
            Box("Vent", structure, new Vector3(p.x, Height - 0.012f, p.y), new Vector3(0.8f, 0.025f, 0.45f), vent, shadow: false);
            for (int i = 0; i < 6; i++)
                Box("VentSlat", structure, new Vector3(p.x, Height - 0.03f, p.y - 0.18f + i * 0.072f), new Vector3(0.74f, 0.012f, 0.02f), baseboard, shadow: false);
        }

        // Entrance door (south wall, near the spawn) with a badge reader
        float dx = 4.3f, dz = -HalfZ + 0.03f;
        Box("DoorFrame_L", structure, new Vector3(dx - 0.56f, 1.1f, dz), new Vector3(0.08f, 2.2f, 0.06f), metal);
        Box("DoorFrame_R", structure, new Vector3(dx + 0.56f, 1.1f, dz), new Vector3(0.08f, 2.2f, 0.06f), metal);
        Box("DoorFrame_T", structure, new Vector3(dx, 2.22f, dz), new Vector3(1.2f, 0.08f, 0.06f), metal);
        Box("Door", structure, new Vector3(dx, 1.08f, dz + 0.005f), new Vector3(1.04f, 2.16f, 0.05f), door);
        Box("DoorWindow", structure, new Vector3(dx, 1.6f, dz + 0.035f), new Vector3(0.3f, 0.5f, 0.01f), M("R3_Glass", "#0B1626", 0.9f));
        Box("DoorHandle", structure, new Vector3(dx - 0.38f, 1.0f, dz + 0.05f), new Vector3(0.18f, 0.03f, 0.04f), metal);
        Box("BadgeReader", structure, new Vector3(dx - 0.85f, 1.25f, dz + 0.02f), new Vector3(0.1f, 0.16f, 0.03f), baseboard);
        Box("BadgeReaderLed", structure, new Vector3(dx - 0.85f, 1.3f, dz + 0.037f), new Vector3(0.02f, 0.02f, 0.005f), ledGreen, shadow: false);
    }

    static void BuildMainStation(Transform env)
    {
        // Gameplay objects (Workstation = email ComputerTerminal, DeskChair, Phone) are kept from Room 2.
        var paper = M("R3_Paper", "#E8E6DF", 0.1f);
        var dark = M("R3_DarkMetal", "#2B313B", 0.4f);
        var mug = M("R3_Mug", "#E2E8F0", 0.5f);

        // Large threat-analysis wall display above the main station (live: OpsWallDisplay)
        var wd = new GameObject("WallDisplay").transform;
        wd.SetParent(screens, false);
        Box("Frame", wd, new Vector3(0, 2.78f, HalfZ - 0.04f), new Vector3(2.9f, 1.06f, 0.06f), dark);
        Box("Screen", wd, new Vector3(0, 2.78f, HalfZ - 0.072f), new Vector3(2.8f, 0.96f, 0.005f), ScreenMat(), shadow: false);
        var c = WorldCanvas("WallDisplayUI", wd, new Vector3(0, 2.78f, HalfZ - 0.076f), Vector3.back, 2.8f, new Vector2(1800, 617), false);
        var wall = wd.gameObject.AddComponent<OpsWallDisplay>();
        BuildWallDisplayUI(c, wall);

        // Trusted-contacts desk card (standing placard facing the chair)
        var card = new GameObject("TrustedContactsCard").transform;
        card.SetParent(props, false);
        card.position = new Vector3(-0.86f, 1.15f, 3.42f + 0.05f);
        card.rotation = Quaternion.Euler(0f, 20f, 0f);
        var cardBody = Box("Card", card, Vector3.zero, new Vector3(0.3f, 0.2f, 0.01f), paper);
        cardBody.transform.localPosition = new Vector3(0, 0.1f, 0);
        cardBody.transform.localRotation = Quaternion.Euler(-12f, 0, 0);
        LocalBox("Stand", card, new Vector3(0, 0.005f, 0.04f), new Vector3(0.22f, 0.01f, 0.08f), dark);
        var cc = WorldCanvasLocal("Text", cardBody.transform, new Vector3(0, 0, -0.51f), 0.3f, new Vector2(600, 400));
        Txt(cc, "Title", "TRUSTED CONTACTS", 44, UIKit.AccentDark, new Vector2(30, -24), new Vector2(540, 56), bold: true);
        Txt(cc, "Body",
            "Example Bank\n   <b>examplebank.example</b>\nExample Cloud\n   <b>examplecloud.example</b>\nYour company\n   <b>yourcompany.example</b>",
            30, new Color32(0x1F, 0x29, 0x37, 255), new Vector2(30, -88), new Vector2(540, 300));

        // Everyday objects on the main desk
        Mug("Mug_Main", new Vector3(0.62f, 1.15f, 3.95f), mug);
        Box("Notebook_Main", props, new Vector3(-0.5f, 1.158f, 3.92f), new Vector3(0.22f, 0.016f, 0.28f), M("R3_NotebookBlue", "#1E3A5F", 0.2f), rotY: 8f);
        Shadow(new Vector3(0f, 0f, 3.76f), new Vector2(2.5f, 1.5f));
        Shadow(new Vector3(1.75f, 0f, 3.9f), new Vector2(1.0f, 0.9f));
    }

    static void BuildWallDisplayUI(RectTransform root, OpsWallDisplay display)
    {
        UIKit.AddImage(root, new Color32(0x07, 0x0F, 0x1C, 255)).raycastTarget = false;
        Txt(root, "Title", "THREAT ANALYSIS  ·  CASE 03  ·  THE DIGITAL TRAP", 46, UIKit.Accent, new Vector2(48, -30), new Vector2(1500, 64), bold: true).characterSpacing = 4;
        Txt(root, "Clock", "OPS-WS3  ·  LIVE", 34, UIKit.TextSecondary, new Vector2(-48, -36), new Vector2(400, 56), align: TextAlignmentOptions.TopRight, anchor: new Vector2(1, 1));

        Txt(root, "ReviewedLabel", "MESSAGES REVIEWED", 30, UIKit.TextSecondary, new Vector2(48, -130), new Vector2(560, 40), bold: true);
        var reviewed = Txt(root, "Reviewed", "0 / 6", 120, UIKit.TextPrimary, new Vector2(48, -170), new Vector2(560, 140), bold: true);
        Txt(root, "AccuracyLabel", "ACCURACY", 30, UIKit.TextSecondary, new Vector2(650, -130), new Vector2(420, 40), bold: true);
        var accuracy = Txt(root, "Accuracy", "--", 120, UIKit.TextPrimary, new Vector2(650, -170), new Vector2(420, 140), bold: true);
        Txt(root, "StatusLabel", "STATUS", 30, UIKit.TextSecondary, new Vector2(1130, -130), new Vector2(620, 40), bold: true);
        var status = Txt(root, "Status", "MONITORING", 54, UIKit.Accent, new Vector2(1130, -190), new Vector2(620, 140), bold: true);

        // Progress bar
        var bar = UIKit.Rect("ProgressBar", root);
        UIKit.Place(bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -340), new Vector2(1704, 22));
        UIKit.AddImage(bar, UIKit.PanelRaised).raycastTarget = false;
        var fill = UIKit.Rect("Fill", bar);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
        UIKit.AddImage(fill, UIKit.Accent).raycastTarget = false;

        // Decorative traffic graph (orange bars mark blocked threats)
        Txt(root, "GraphLabel", "INBOUND MESSAGE TRAFFIC  ·  24 H", 26, UIKit.TextSecondary, new Vector2(48, -392), new Vector2(900, 36), bold: true);
        var rng = new System.Random(3);
        for (int i = 0; i < 48; i++)
        {
            float h = 30 + (float)rng.NextDouble() * 120 + Mathf.Sin(i * 0.4f) * 25;
            bool threat = i == 9 || i == 22 || i == 37;
            var b = UIKit.Rect("Bar", root);
            UIKit.Place(b, new Vector2(0, 0), new Vector2(0, 0), new Vector2(48 + i * 35.5f, 36), new Vector2(24, h));
            UIKit.AddImage(b, threat ? new Color32(0xF9, 0x73, 0x16, 255) : new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.55f)).raycastTarget = false;
        }

        var so = new SerializedObject(display);
        so.FindProperty("reviewedText").objectReferenceValue = reviewed;
        so.FindProperty("accuracyText").objectReferenceValue = accuracy;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("progressFill").objectReferenceValue = fill;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildServerRacks()
    {
        var rackMat = M("R3_Rack", "#1F252E", 0.45f);
        var rackDoor = M("R3_RackDoor", "#151A21", 0.6f);
        var slat = M("R3_RackSlat", "#2E3642", 0.4f);
        var green = M("R3_LedGreen", "#22C55E", 0.5f, "#22C55E", 2f);
        var cyan = M("R3_LedCyan", "#22D3EE", 0.5f, "#22D3EE", 2f);
        var orange = M("R3_LedOrange", "#F97316", 0.5f, "#F97316", 2.2f);
        var tray = M("R3_Metal", "#5B6472", 0.5f);

        float x = HalfX - 0.47f, front = x - 0.45f;
        float[] zs = { -0.95f, -0.2f, 0.55f, 1.3f };
        var rng = new System.Random(11);
        for (int r = 0; r < zs.Length; r++)
        {
            var rack = new GameObject("ServerRack_" + (r + 1)).transform;
            rack.SetParent(furniture, false);
            Box("Body", rack, new Vector3(x, 1.05f, zs[r]), new Vector3(0.9f, 2.1f, 0.7f), rackMat, true);
            Box("Door", rack, new Vector3(front - 0.005f, 1.05f, zs[r]), new Vector3(0.012f, 1.96f, 0.62f), rackDoor, shadow: false);
            for (int s = 0; s < 3; s++)
                Box("VentSlat", rack, new Vector3(front - 0.013f, 1.75f - s * 0.06f, zs[r]), new Vector3(0.004f, 0.025f, 0.5f), slat, shadow: false);
            for (int row = 0; row < 7; row++)
                for (int k = 0; k < 4; k++)
                {
                    bool warn = r == 2 && row == 3 && k == 1;   // one amber warning light
                    var led = rng.NextDouble() < 0.7 ? green : cyan;
                    Box("Led", rack, new Vector3(front - 0.014f, 0.45f + row * 0.17f, zs[r] - 0.2f + k * 0.05f), new Vector3(0.005f, 0.018f, 0.018f), warn ? orange : led, shadow: false);
                }
            Shadow(new Vector3(x, 0f, zs[r]), new Vector2(1.0f, 0.8f));
        }
        Box("CableTray", furniture, new Vector3(x + 0.1f, 2.45f, 0.18f), new Vector3(0.5f, 0.06f, 3.3f), tray);
        for (int i = 0; i < 4; i++)
            Box("TrayCable", furniture, new Vector3(x + 0.1f - 0.12f + i * 0.08f, 2.5f, 0.18f), new Vector3(0.03f, 0.03f, 3.2f), M("R3_Cable", "#0B0E13", 0.3f), shadow: false);

        // Small status screen above the racks
        var st = new GameObject("RackStatusScreen").transform;
        st.SetParent(screens, false);
        Box("Frame", st, new Vector3(HalfX - 0.03f, 2.85f, 0.18f), new Vector3(0.04f, 0.42f, 1.1f), M("R3_DarkMetal", "#2B313B", 0.4f));
        Box("Screen", st, new Vector3(HalfX - 0.052f, 2.85f, 0.18f), new Vector3(0.004f, 0.36f, 1.02f), ScreenMat(), shadow: false);
        var c = WorldCanvas("UI", st, new Vector3(HalfX - 0.056f, 2.85f, 0.18f), Vector3.left, 1.02f, new Vector2(1020, 360), false);
        UIKit.AddImage(c, new Color32(0x07, 0x0F, 0x1C, 255)).raycastTarget = false;
        Txt(c, "T", "SERVER STATUS", 46, UIKit.Accent, new Vector2(36, -30), new Vector2(900, 60), bold: true).characterSpacing = 4;
        Txt(c, "B", $"<color={UIKit.HexSafe}>●</color>  MAIL-GW-01   online\n<color={UIKit.HexSafe}>●</color>  AUTH-02      online\n<color=#F97316>●</color>  FILTER-03    reviewing quarantine",
            38, UIKit.TextPrimary, new Vector2(36, -110), new Vector2(950, 230));

        // Fan hum (3D, routed like the level ambience)
        var hum = new GameObject("RackHum", typeof(AudioSource), typeof(RackHum));
        hum.transform.SetParent(furniture, false);
        hum.transform.position = new Vector3(front, 1.2f, 0.18f);
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
    }

    static void BuildCaseFilesStation(Transform env)
    {
        var deskSrc = env.Find("Desk");
        var chairSrc = env.Find("DeskChair");
        var dark = M("R3_DarkMetal", "#2B313B", 0.4f);
        var keyboard = M("R3_Keyboard", "#1A1F27", 0.3f);

        var station = new GameObject("CaseFilesStation").transform;
        station.SetParent(furniture, false);

        // Desk against the west wall (same desk model as the main station), user side facing east
        if (deskSrc != null)
        {
            var desk = Object.Instantiate(deskSrc.gameObject, station);
            desk.name = "CaseDesk";
            desk.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            Fit(desk, -HalfX + 0.62f, 1.4f);
        }
        Shadow(new Vector3(-HalfX + 0.62f, 0f, 1.4f), new Vector2(1.5f, 2.5f));

        // Monitor = the interactable terminal (its colliders are children of the terminal object)
        var term = new GameObject("CaseFilesTerminal").transform;
        term.SetParent(station, false);
        float sx = -HalfX + 0.42f, sy = 1.59f, sz = 1.4f;
        Box("Base", term, new Vector3(sx - 0.02f, 1.16f, sz), new Vector3(0.22f, 0.02f, 0.3f), dark);
        Box("Stand", term, new Vector3(sx - 0.03f, 1.24f, sz), new Vector3(0.05f, 0.16f, 0.07f), dark);
        Box("Body", term, new Vector3(sx, sy, sz), new Vector3(0.05f, 0.62f, 1.02f), dark, true);
        Box("Screen", term, new Vector3(sx + 0.027f, sy, sz), new Vector3(0.004f, 0.56f, 0.96f), ScreenMat(), shadow: false);
        var vp = new GameObject("ViewPoint").transform;
        vp.SetParent(term, false);
        vp.position = new Vector3(sx + 0.03f + 0.6f, sy, sz);
        vp.rotation = Quaternion.LookRotation(Vector3.left);
        var ui = WorldCanvas("CaseFilesUI", term, new Vector3(sx + 0.031f, sy, sz), Vector3.right, 0.96f, new Vector2(1280, 747), true);
        var terminal = term.gameObject.AddComponent<CaseFilesTerminal>();
        BuildCaseFilesUI(ui, terminal, vp);

        // Desk items
        Box("Keyboard", props, new Vector3(-HalfX + 0.95f, 1.16f, 1.4f), new Vector3(0.16f, 0.02f, 0.46f), keyboard);
        Box("Mouse", props, new Vector3(-HalfX + 0.95f, 1.16f, 1.85f), new Vector3(0.1f, 0.025f, 0.06f), keyboard);
        Box("CaseFolder_1", props, new Vector3(-HalfX + 0.55f, 1.16f, 2.25f), new Vector3(0.24f, 0.02f, 0.32f), M("R3_FolderOrange", "#C2410C", 0.2f), rotY: 6f);
        Box("CaseFolder_2", props, new Vector3(-HalfX + 0.56f, 1.18f, 2.24f), new Vector3(0.24f, 0.02f, 0.32f), M("R3_FolderGrey", "#64748B", 0.2f), rotY: -4f);
        Mug("Mug_Case", new Vector3(-HalfX + 0.55f, 1.15f, 0.62f), M("R3_MugTeal", "#0F766E", 0.5f));
        Badge(new Vector3(-HalfX + 0.95f, 1.152f, 0.85f), 25f);

        // A spare chair turned aside (not in the line of sight to the screen)
        if (chairSrc != null) SpareChair(chairSrc, new Vector3(-4.35f, 0f, 2.55f), 115f);
    }

    static void BuildCaseFilesUI(RectTransform root, CaseFilesTerminal terminal, Transform viewPoint)
    {
        UIKit.AddImage(root, UIKit.Background).raycastTarget = false;

        var header = UIKit.Rect("Header", root);
        header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
        header.sizeDelta = new Vector2(0, 70); header.anchoredPosition = Vector2.zero;
        UIKit.AddImage(header, new Color32(0x07, 0x0C, 0x16, 255)).raycastTarget = false;
        var ht = UIKit.Text(header, "Title", "CASE FILES  ·  OPS-WS3", 28, UIKit.Accent, TextAlignmentOptions.MidlineLeft, bold: true);
        UIKit.Stretch(ht.rectTransform); ht.margin = new Vector4(24, 0, 0, 0); ht.characterSpacing = 3;
        var status = UIKit.Text(header, "Status", "Messages reviewed 0/6", 22, UIKit.TextSecondary, TextAlignmentOptions.MidlineRight);
        UIKit.Stretch(status.rectTransform); status.margin = new Vector4(0, 0, 24, 0);

        // Tabs (left column)
        var tabs = UIKit.Rect("Tabs", root);
        tabs.anchorMin = new Vector2(0, 0); tabs.anchorMax = new Vector2(0, 1); tabs.pivot = new Vector2(0, 1);
        tabs.offsetMin = new Vector2(20, 210); tabs.offsetMax = new Vector2(370, -90);
        var tv = UIKit.Vertical(tabs, 0, 12);
        tv.childControlHeight = true; tv.childForceExpandHeight = false;
        string[] names = { "TRUSTED CONTACTS", "ORDERS", "ACCOUNT ACTIVITY", "IT POLICY", "NOTES" };
        var buttons = new Button[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            buttons[i] = UIKit.HudButton(tabs, names[i], null, UIKit.HudVariant.Secondary, 22);
            buttons[i].name = "Tab_" + (i + 1);
            UIKit.Size(buttons[i], -1, 70);
            UnityEventTools.AddIntPersistentListener(buttons[i].onClick, new UnityAction<int>(terminal.ShowPage), i);
        }

        // New-message banner
        var banner = UIKit.Rect("NewMessageBanner", root);
        UIKit.Place(banner, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 104), new Vector2(350, 90));
        UIKit.PanelImage(banner, new Color(0.45f, 0.24f, 0.02f, 0.95f)).raycastTarget = false;
        var bt = UIKit.Text(banner, "Text", "NEW MESSAGE WAITING\n<size=18>Exit to read it</size>", 22, Color.white, TextAlignmentOptions.Center, bold: true);
        UIKit.Stretch(bt.rectTransform, 8);
        banner.gameObject.SetActive(false);

        // Exit
        var exit = UIKit.HudButton(root, "EXIT  [Esc]", null, UIKit.HudVariant.Primary, 22);
        exit.name = "ExitButton";
        UIKit.Place((RectTransform)exit.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(350, 66));
        UnityEventTools.AddPersistentListener(exit.onClick, new UnityAction(terminal.Exit));

        // Page
        var page = UIKit.Rect("Page", root);
        page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one;
        page.offsetMin = new Vector2(390, 20); page.offsetMax = new Vector2(-20, -90);
        UIKit.PanelImage(page, UIKit.Panel).raycastTarget = false;
        var pv = UIKit.Vertical(page, 28, 12);
        pv.padding.top = 22;
        var title = UIKit.Text(page, "PageTitle", "Trusted Contacts", 34, UIKit.TextPrimary, bold: true);
        UIKit.Divider(page);
        var body = UIKit.Text(page, "PageBody", "", 24, UIKit.TextPrimary);
        body.lineSpacing = 8f;
        var le = body.gameObject.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f; le.minHeight = 100f;
        body.enableAutoSizing = true; body.fontSizeMax = 24f; body.fontSizeMin = 16f;
        body.overflowMode = TextOverflowModes.Ellipsis;

        var so = new SerializedObject(terminal);
        so.FindProperty("screenCanvas").objectReferenceValue = root.GetComponent<Canvas>();
        so.FindProperty("viewPoint").objectReferenceValue = viewPoint;
        var tb = so.FindProperty("tabButtons");
        tb.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++) tb.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        so.FindProperty("pageTitle").objectReferenceValue = title;
        so.FindProperty("pageBody").objectReferenceValue = body;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("newMessageBanner").objectReferenceValue = banner.gameObject;
        so.FindProperty("exitButton").objectReferenceValue = exit;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildCaseWall()
    {
        var dark = M("R3_DarkMetal", "#2B313B", 0.4f);
        var pin = M("R3_PinOrange", "#F97316", 0.4f, "#F97316", 0.6f);
        var str = M("R3_StringRed", "#DC2626", 0.3f);
        float x = -HalfX + 0.03f, cy = 1.72f, cz = -2.1f;

        var board = new GameObject("DigitalCaseWall").transform;
        board.SetParent(screens, false);
        Box("Frame", board, new Vector3(x, cy, cz), new Vector3(0.05f, 1.42f, 2.62f), dark);
        Box("Panel", board, new Vector3(x + 0.03f, cy, cz), new Vector3(0.005f, 1.34f, 2.54f), M("R3_BoardPanel", "#101A2B", 0.2f), shadow: false);
        var c = WorldCanvas("UI", board, new Vector3(x + 0.034f, cy, cz), Vector3.right, 2.54f, new Vector2(1900, 1002), false);
        Txt(c, "Title", "CASE 03  ·  THE DIGITAL TRAP", 54, UIKit.Accent, new Vector2(50, -34), new Vector2(1500, 70), bold: true).characterSpacing = 4;
        Txt(c, "Sub", "Pattern notes from earlier investigations", 32, UIKit.TextSecondary, new Vector2(50, -108), new Vector2(1500, 44));

        string[,] cards =
        {
            { "DISPLAY NAMES LIE", "A sender name can say anything. Read the full address after the @." },
            { "LOOKALIKE DOMAINS", "Extra words, hyphens or swapped letters next to a real brand name." },
            { "DID YOU START IT?", "Codes, resets and deliveries you asked for are expected. Surprises need checking." },
            { "ALERTS CAN BE REAL", "Judge the request: a safe alert sends you to the official app, not a form." },
            { "URGENCY + SECRECY", "Pressure, \"keep this quiet\" and odd payments are classic manipulation." },
            { "VERIFY ELSEWHERE", "Use a channel you already trust: the official app, website or a known number." },
        };
        for (int i = 0; i < 6; i++)
        {
            int col = i % 3, row = i / 3;
            var card = UIKit.Rect("Card_" + (i + 1), c);
            UIKit.Place(card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50 + col * 610, -190 - row * 400), new Vector2(570, 350));
            UIKit.PanelImage(card, UIKit.PanelRaised).raycastTarget = false;
            UIKit.AccentStrip(card, i == 1 || i == 4 ? (Color)new Color32(0xF9, 0x73, 0x16, 255) : UIKit.Accent, 8f);
            var t = UIKit.Text(card, "Head", cards[i, 0], 36, UIKit.TextPrimary, bold: true);
            UIKit.Place(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -30), new Vector2(510, 50));
            var b = UIKit.Text(card, "Body", cards[i, 1], 31, UIKit.TextSecondary);
            UIKit.Place(b.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -96), new Vector2(510, 230));
        }

        // Pins and string between the two orange cards
        // Canvas pixels -> world (this canvas faces +X, so its right edge points to +Z)
        Vector3 Pt(float px, float py) => new Vector3(x + 0.045f, cy + 0.67f - py * (1.34f / 1002f), cz - 1.27f + px * (2.54f / 1900f));
        Vector3 a = Pt(50 + 610 + 285, 200), b2 = Pt(50 + 610 + 285, 600), d = Pt(50 + 1220 + 285, 600);
        foreach (var p in new[] { a, b2, d }) Box("Pin", board, p, new Vector3(0.02f, 0.03f, 0.03f), pin, shadow: false);
        StringBetween(board, a, b2, str);
        StringBetween(board, b2, d, str);
    }

    static void BuildAnalystDesk(Transform env)
    {
        var deskSrc = env.Find("Desk");
        var chairSrc = env.Find("DeskChair");
        var dark = M("R3_DarkMetal", "#2B313B", 0.4f);
        var keyboard = M("R3_Keyboard", "#1A1F27", 0.3f);
        var paper = M("R3_Paper", "#E8E6DF", 0.1f);

        var island = new GameObject("AnalystDesk").transform;
        island.SetParent(furniture, false);
        if (deskSrc != null)
        {
            var desk = Object.Instantiate(deskSrc.gameObject, island);
            desk.name = "AnalystDeskModel";
            desk.transform.rotation = Quaternion.identity;
            Fit(desk, -2.0f, -0.6f);
        }
        Shadow(new Vector3(-2.0f, 0f, -0.6f), new Vector2(2.6f, 1.5f));

        // Two dashboard monitors facing the entrance (south)
        string[] titles = { "NETWORK TRAFFIC", "ALERT QUEUE" };
        for (int m = 0; m < 2; m++)
        {
            float mx = m == 0 ? -2.48f : -1.52f, mz = -0.32f, my = 1.52f;
            float yaw = m == 0 ? 10f : -10f;
            var mon = new GameObject("Dashboard_" + (m + 1)).transform;
            mon.SetParent(screens, false);
            mon.position = new Vector3(mx, 0f, mz);
            mon.rotation = Quaternion.Euler(0f, yaw, 0f);
            LocalBox("Base", mon, new Vector3(0, 1.16f, 0.03f), new Vector3(0.24f, 0.02f, 0.18f), dark);
            LocalBox("Stand", mon, new Vector3(0, 1.25f, 0.04f), new Vector3(0.05f, 0.18f, 0.04f), dark);
            LocalBox("Body", mon, new Vector3(0, my, 0f), new Vector3(0.9f, 0.53f, 0.04f), dark, true);
            LocalBox("Screen", mon, new Vector3(0, my, -0.022f), new Vector3(0.85f, 0.48f, 0.004f), ScreenMat(), shadow: false);
            var c = WorldCanvasLocal("UI", mon, new Vector3(0, my, -0.0245f), 0.85f, new Vector2(1020, 576));
            UIKit.AddImage(c, new Color32(0x07, 0x0F, 0x1C, 255)).raycastTarget = false;
            Txt(c, "T", titles[m], 40, UIKit.Accent, new Vector2(34, -26), new Vector2(900, 56), bold: true).characterSpacing = 3;
            if (m == 0) DashboardTraffic(c); else DashboardAlerts(c);
        }

        Box("Keyboard", props, new Vector3(-2.0f, 1.16f, -0.95f), new Vector3(0.46f, 0.02f, 0.16f), keyboard);
        Box("Mouse", props, new Vector3(-1.6f, 1.16f, -0.95f), new Vector3(0.06f, 0.025f, 0.1f), keyboard);
        Box("PrintedNotes", props, new Vector3(-2.65f, 1.153f, -0.9f), new Vector3(0.21f, 0.004f, 0.297f), paper, rotY: -14f, shadow: false);
        Box("PrintedNotes_2", props, new Vector3(-2.6f, 1.157f, -0.86f), new Vector3(0.21f, 0.004f, 0.297f), paper, rotY: 9f, shadow: false);
        Mug("Mug_Analyst", new Vector3(-1.3f, 1.15f, -0.85f), M("R3_Mug", "#E2E8F0", 0.5f));
        Badge(new Vector3(-1.38f, 1.152f, -1.05f), -20f);

        if (chairSrc != null) SpareChair(chairSrc, new Vector3(-2.0f, 0f, -1.75f), 180f);
    }

    static void DashboardTraffic(RectTransform c)
    {
        var rng = new System.Random(5);
        for (int i = 0; i < 26; i++)
        {
            float h = 60 + (float)rng.NextDouble() * 260;
            var b = UIKit.Rect("Bar", c);
            UIKit.Place(b, new Vector2(0, 0), new Vector2(0, 0), new Vector2(34 + i * 36.5f, 70), new Vector2(26, h));
            UIKit.AddImage(b, i == 17 ? new Color32(0xF9, 0x73, 0x16, 255) : new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.6f)).raycastTarget = false;
        }
        Txt(c, "Legend", $"<color={UIKit.HexAccent}>■</color> normal     <color=#F97316>■</color> blocked spike", 26, UIKit.TextSecondary, new Vector2(34, -96), new Vector2(900, 40));
        Txt(c, "Axis", "00:00                                  12:00                                  NOW", 22, UIKit.TextSecondary, new Vector2(34, -530), new Vector2(950, 34));
    }

    static void DashboardAlerts(RectTransform c)
    {
        Txt(c, "List",
            "08:02   Firewall rules updated                <color=#22C55E>OK</color>\n" +
            "08:30   Phishing simulation report ready     <color=#22C55E>OK</color>\n" +
            "09:10   Quarantine: 14 messages held         <color=#F97316>REVIEW</color>\n" +
            "09:25   Backup verification complete         <color=#22C55E>OK</color>\n" +
            "09:40   Flagged messages routed to OPS-WS3   <color=#38BDF8>ACTIVE</color>",
            28, UIKit.TextPrimary, new Vector2(34, -100), new Vector2(960, 440));
    }

    static void BuildSouthWall()
    {
        var metal = M("R3_Metal", "#5B6472", 0.5f);
        var shelfBoard = M("R3_ShelfBoard", "#3A4250", 0.3f);
        var cab = M("R3_Cabinet", "#2F3846", 0.35f);
        Material[] binders = { M("R3_BinderBlue", "#1D4ED8", 0.2f), M("R3_BinderGrey", "#64748B", 0.2f), M("R3_BinderTeal", "#0F766E", 0.2f), M("R3_FolderOrange", "#C2410C", 0.2f), M("R3_BinderNavy", "#1E293B", 0.2f) };

        // Metal shelving with binders, folders and kit
        float sx = -2.6f, sz = -HalfZ + 0.22f;
        var shelf = new GameObject("Shelving").transform;
        shelf.SetParent(furniture, false);
        foreach (float px in new[] { -0.98f, 0.98f })
            foreach (float pz in new[] { -0.17f, 0.17f })
                Box("Post", shelf, new Vector3(sx + px, 1.0f, sz + pz), new Vector3(0.04f, 2.0f, 0.04f), metal);
        var rng = new System.Random(21);
        for (int s = 0; s < 4; s++)
        {
            float y = 0.12f + s * 0.58f;
            Box("Shelf", shelf, new Vector3(sx, y, sz), new Vector3(2.0f, 0.03f, 0.4f), shelfBoard, s == 0);
            if (s == 3) continue;
            float bx = sx - 0.9f;
            while (bx < sx + 0.85f)
            {
                if (rng.NextDouble() < 0.18) { bx += 0.15f; continue; }   // gaps
                float w = 0.06f + (float)rng.NextDouble() * 0.03f, h = 0.3f + (float)rng.NextDouble() * 0.06f;
                Box("Binder", shelf, new Vector3(bx + w / 2, y + 0.015f + h / 2, sz + 0.02f), new Vector3(w, h, 0.28f), binders[rng.Next(binders.Length)], shadow: false);
                bx += w + 0.008f;
            }
        }
        // Top shelf: a network switch with LEDs and a storage box
        Box("NetworkSwitch", shelf, new Vector3(sx - 0.4f, 1.9f + 0.04f, sz), new Vector3(0.5f, 0.06f, 0.3f), M("R3_Rack", "#1F252E", 0.45f));
        for (int i = 0; i < 8; i++)
            Box("SwitchLed", shelf, new Vector3(sx - 0.6f + i * 0.05f, 1.945f, sz + 0.151f), new Vector3(0.015f, 0.012f, 0.004f), i == 5 ? M("R3_LedOrange", "#F97316", 0.5f, "#F97316", 2.2f) : M("R3_LedGreen", "#22C55E", 0.5f, "#22C55E", 2f), shadow: false);
        Box("StorageBox", shelf, new Vector3(sx + 0.45f, 1.9f + 0.11f, sz), new Vector3(0.45f, 0.2f, 0.32f), M("R3_Cardboard", "#9C7A54", 0.1f));
        Shadow(new Vector3(sx, 0f, sz), new Vector2(2.2f, 0.6f));

        // Coffee point
        float cx = 0.9f;
        Box("CoffeeCabinet", furniture, new Vector3(cx, 0.45f, -HalfZ + 0.27f), new Vector3(1.0f, 0.9f, 0.5f), cab, true);
        Box("CoffeeTop", furniture, new Vector3(cx, 0.915f, -HalfZ + 0.27f), new Vector3(1.04f, 0.03f, 0.54f), shelfBoard);
        Box("CoffeeMachine", props, new Vector3(cx - 0.22f, 1.1f, -HalfZ + 0.24f), new Vector3(0.28f, 0.34f, 0.3f), M("R3_Rack", "#1F252E", 0.45f));
        Box("CoffeeMachineLed", props, new Vector3(cx - 0.22f, 1.2f, -HalfZ + 0.391f), new Vector3(0.03f, 0.015f, 0.004f), M("R3_LedCyan", "#22D3EE", 0.5f, "#22D3EE", 2f), shadow: false);
        Mug("Mug_Coffee_1", new Vector3(cx + 0.12f, 0.93f, -HalfZ + 0.3f), M("R3_Mug", "#E2E8F0", 0.5f));
        Mug("Mug_Coffee_2", new Vector3(cx + 0.3f, 0.93f, -HalfZ + 0.22f), M("R3_MugTeal", "#0F766E", 0.5f));
        Shadow(new Vector3(cx, 0f, -HalfZ + 0.27f), new Vector2(1.2f, 0.7f));

        // Awareness poster
        var poster = new GameObject("AwarenessPoster").transform;
        poster.SetParent(props, false);
        Box("Panel", poster, new Vector3(2.45f, 1.65f, -HalfZ + 0.012f), new Vector3(0.8f, 1.1f, 0.01f), M("R3_PosterNavy", "#0F1B30", 0.2f));
        Box("Accent", poster, new Vector3(2.08f, 1.65f, -HalfZ + 0.02f), new Vector3(0.03f, 1.0f, 0.004f), M("R3_PinOrange", "#F97316", 0.4f, "#F97316", 0.6f), shadow: false);
        var pc = WorldCanvas("UI", poster, new Vector3(2.47f, 1.65f, -HalfZ + 0.019f), Vector3.forward, 0.7f, new Vector2(700, 1000), false);
        Txt(pc, "T", "THINK\nBEFORE\nYOU CLICK", 92, UIKit.TextPrimary, new Vector2(40, -60), new Vector2(640, 380), bold: true);
        Txt(pc, "B", "STOP  ·  CHECK  ·  VERIFY\n\nReport anything suspicious to the security team.", 38, UIKit.Accent, new Vector2(40, -520), new Vector2(620, 400));

        // Wall clock above the poster
        var clock = new GameObject("WallClock").transform;
        clock.SetParent(props, false);
        var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        face.name = "Face";
        Object.DestroyImmediate(face.GetComponent<Collider>());
        face.transform.SetParent(clock, false);
        face.transform.position = new Vector3(2.45f, 2.65f, -HalfZ + 0.02f);
        face.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        face.transform.localScale = new Vector3(0.36f, 0.01f, 0.36f);
        face.GetComponent<MeshRenderer>().sharedMaterial = M("R3_Paper", "#E8E6DF", 0.1f);
        Box("HourHand", clock, new Vector3(2.45f, 2.68f, -HalfZ + 0.035f), new Vector3(0.015f, 0.08f, 0.005f), M("R3_Cable", "#0B0E13", 0.3f), shadow: false);
        Box("MinuteHand", clock, new Vector3(2.5f, 2.65f, -HalfZ + 0.037f), new Vector3(0.12f, 0.01f, 0.005f), M("R3_Cable", "#0B0E13", 0.3f), shadow: false);

        // Plants in two corners
        Plant(new Vector3(-HalfX + 0.4f, 0f, HalfZ - 0.45f));
        Plant(new Vector3(HalfX - 0.45f, 0f, -HalfZ + 0.45f));
    }

    static void BuildLighting()
    {
        // Cool overhead light with soft shadows, a dimmer fill, and small accent/task lights. No flicker.
        // One downlight under each ceiling panel; the two over the desks cast the soft furniture shadows.
        Light("Ceiling_Spot_NW", LightType.Spot, new Vector3(-2.4f, Height - 0.1f, 1.4f), Quaternion.Euler(90, 0, 0), "#D9E6FF", 2.4f, 9f, 135f, LightShadows.Soft);
        Light("Ceiling_Spot_NE", LightType.Spot, new Vector3(2.4f, Height - 0.1f, 1.8f), Quaternion.Euler(90, 0, 0), "#D9E6FF", 2.2f, 9f, 135f, LightShadows.Soft);
        Light("Ceiling_Spot_SW", LightType.Spot, new Vector3(-2.4f, Height - 0.1f, -1.8f), Quaternion.Euler(90, 0, 0), "#D9E6FF", 2.0f, 9f, 135f, LightShadows.Soft);
        Light("Ceiling_Spot_SE", LightType.Spot, new Vector3(2.4f, Height - 0.1f, -1.8f), Quaternion.Euler(90, 0, 0), "#D9E6FF", 1.8f, 9f, 135f, LightShadows.None);
        Light("DeskLamp_Light", LightType.Spot, new Vector3(0.93f, 1.62f, 4.1f), Quaternion.Euler(70, 200, 0), "#FFD7A0", 1.8f, 3.2f, 95f, LightShadows.Soft);
        Light("MainMonitorGlow", LightType.Point, new Vector3(0.15f, 1.7f, 3.55f), Quaternion.identity, "#40BFFF", 0.8f, 2.2f, 0f, LightShadows.None);
        Light("WallDisplayGlow", LightType.Point, new Vector3(0f, 2.7f, 3.9f), Quaternion.identity, "#38BDF8", 0.6f, 3f, 0f, LightShadows.None);
        Light("CaseMonitorGlow", LightType.Point, new Vector3(-HalfX + 1.0f, 1.6f, 1.4f), Quaternion.identity, "#40BFFF", 0.7f, 2.2f, 0f, LightShadows.None);
        Light("CaseWall_Spot", LightType.Spot, new Vector3(-3.9f, Height - 0.15f, -2.1f), Quaternion.Euler(35, -90, 0), "#FFE9C7", 1.5f, 6f, 70f, LightShadows.Soft);
        Light("Dashboard_Glow", LightType.Point, new Vector3(-2.0f, 1.5f, -0.75f), Quaternion.identity, "#40BFFF", 0.6f, 2.0f, 0f, LightShadows.None);
        Light("Rack_Accent", LightType.Point, new Vector3(HalfX - 1.3f, 2.2f, 0.2f), Quaternion.identity, "#22D3EE", 0.45f, 3f, 0f, LightShadows.None);

        var probeGo = new GameObject("ReflectionProbe_Ops");
        probeGo.transform.SetParent(lighting, false);
        probeGo.transform.position = new Vector3(0, 1.7f, 0);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.size = new Vector3(HalfX * 2, Height, HalfZ * 2);
        probe.boxProjection = true;
        probe.intensity = 0.7f;
    }

    static void SetupRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex("#5A6886");
        RenderSettings.ambientEquatorColor = Hex("#414D66");
        RenderSettings.ambientGroundColor = Hex("#262D3D");
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;
    }

    /// <summary>
    /// The desk/chair models come from the earlier rooms (warm wood, pink seat). In this scene only, their
    /// renderers use the operations palette instead. The model assets and the other rooms are not changed.
    /// </summary>
    static void Recolor(Transform env)
    {
        var map = new Dictionary<string, Material>
        {
            { "wood", M("R3_DeskTop", "#4B5563", 0.35f) },
            { "metal", M("R3_Metal", "#5B6472", 0.5f) },
            { "carpet", M("R3_ChairFabric", "#253047", 0.15f) },
            { "metalMedium", M("R3_DarkMetal", "#2B313B", 0.4f) },
        };
        var targets = new List<Transform> { env.Find("Desk"), env.Find("DeskChair"), env.Find("PhoneTable") };
        targets.AddRange(furniture.GetComponentsInChildren<Transform>().Where(t => t.name == "CaseDesk" || t.name == "AnalystDeskModel" || t.name == "SpareChair"));
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

    // ---------- Small builders ----------

    static void Mug(string name, Vector3 basePos, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(props, false);
        g.transform.position = basePos + new Vector3(0, 0.05f, 0);
        g.transform.localScale = new Vector3(0.08f, 0.05f, 0.08f);
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        var coffee = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coffee.name = "Coffee";
        Object.DestroyImmediate(coffee.GetComponent<Collider>());
        coffee.transform.SetParent(g.transform, false);
        coffee.transform.localPosition = new Vector3(0, 0.92f, 0);
        coffee.transform.localScale = new Vector3(0.85f, 0.1f, 0.85f);
        coffee.GetComponent<MeshRenderer>().sharedMaterial = M("R3_Coffee", "#3B2416", 0.7f);
        var handle = Box("Handle", g.transform, Vector3.zero, Vector3.one, m);
        handle.transform.localPosition = new Vector3(0.6f, 0f, 0f);
        handle.transform.localScale = new Vector3(0.25f, 1.1f, 0.2f);
    }

    static void Badge(Vector3 pos, float rotY)
    {
        var b = Box("SecurityBadge", props, pos + new Vector3(0, 0.002f, 0), new Vector3(0.086f, 0.003f, 0.054f), M("R3_BadgeWhite", "#F1F5F9", 0.3f), rotY: rotY, shadow: false);
        var stripe = Box("Stripe", b.transform, Vector3.zero, Vector3.one, M("R3_LedCyan", "#22D3EE", 0.5f, "#22D3EE", 0.5f), shadow: false);
        stripe.transform.localPosition = new Vector3(0, 0.6f, 0.38f);
        stripe.transform.localScale = new Vector3(1f, 0.4f, 0.22f);
        var canvasRoot = new GameObject("Text", typeof(RectTransform), typeof(Canvas)).transform as RectTransform;
        canvasRoot.SetParent(props, false);
        canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        canvasRoot.sizeDelta = new Vector2(430, 270);
        canvasRoot.position = pos + new Vector3(0, 0.0045f, 0);
        canvasRoot.rotation = Quaternion.Euler(90f, rotY, 0f);
        canvasRoot.localScale = Vector3.one * (0.086f / 430f);
        Txt(canvasRoot, "T", "SCAM SENSE OPS\n<size=38>ANALYST</size>\n<size=30>ID SS-0317  ·  Level 3 access</size>", 40, new Color32(0x1F, 0x29, 0x37, 255), new Vector2(24, -80), new Vector2(400, 180), bold: true);
    }

    static void Plant(Vector3 pos)
    {
        var root = new GameObject("Plant").transform;
        root.SetParent(props, false);
        var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = "Pot";
        pot.transform.SetParent(root, false);
        pot.transform.position = pos + new Vector3(0, 0.22f, 0);
        pot.transform.localScale = new Vector3(0.38f, 0.22f, 0.38f);
        pot.GetComponent<MeshRenderer>().sharedMaterial = M("R3_Pot", "#2F3846", 0.3f);
        var leaf = M("R3_Leaf", "#2F7D4F", 0.15f);
        var leafDark = M("R3_LeafDark", "#1F5E3A", 0.15f);
        var rng = new System.Random((int)(pos.x * 100));
        for (int i = 0; i < 6; i++)
        {
            var l = Box("Leaf", root, pos + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.25f, 0.6f + i * 0.09f, ((float)rng.NextDouble() - 0.5f) * 0.25f),
                        new Vector3(0.32f, 0.22f, 0.32f) * (1f - i * 0.1f), i % 2 == 0 ? leaf : leafDark);
            l.transform.rotation = Quaternion.Euler(rng.Next(0, 45), rng.Next(0, 90), rng.Next(0, 45));
        }
        Shadow(pos, new Vector2(0.55f, 0.55f));
    }

    static void SpareChair(Transform chairSrc, Vector3 pos, float yaw)
    {
        var model = chairSrc.Find("chair");
        if (model == null) return;
        var g = Object.Instantiate(model.gameObject, furniture);
        g.name = "SpareChair";
        g.transform.localScale = chairSrc.lossyScale;
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Fit(g, pos.x, pos.z);
        var b = RendererBounds(g);
        var col = new GameObject("SpareChair_Collider", typeof(BoxCollider));
        col.transform.SetParent(g.transform.parent, false);
        col.transform.position = new Vector3(b.center.x, 0.5f, b.center.z);
        col.GetComponent<BoxCollider>().size = new Vector3(b.size.x * 0.8f, 1f, b.size.z * 0.8f);
        Shadow(new Vector3(b.center.x, 0f, b.center.z), new Vector2(b.size.x, b.size.z));
    }

    static void StringBetween(Transform parent, Vector3 a, Vector3 b, Material m)
    {
        var g = Box("RedString", parent, (a + b) / 2f, new Vector3(0.004f, 0.004f, Vector3.Distance(a, b)), m, shadow: false);
        g.transform.rotation = Quaternion.LookRotation(b - a);
    }

    // ---------- Helpers ----------

    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    static Material M(string name, string hex, float smooth, string emissionHex = null, float emission = 0f)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = Hex(hex);
        m.SetFloat("_Glossiness", smooth);
        if (emissionHex != null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex(emissionHex) * emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ScreenMat() => M("R3_Screen", "#050B14", 0.85f, "#0A2A3A", 1f);

    static GameObject Box(string name, Transform parent, Vector3 worldPos, Vector3 size, Material m, bool collider = false, float rotY = 0f, bool shadow = true)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        g.transform.localScale = size;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return g;
    }

    static GameObject LocalBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material m, bool collider = false, bool shadow = true)
    {
        var g = Box(name, parent, Vector3.zero, size, m, collider, 0f, shadow);
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.identity;
        return g;
    }

    static void Light(string name, LightType type, Vector3 pos, Quaternion rot, string hex, float intensity, float range, float angle, LightShadows shadowsMode)
    {
        var g = new GameObject(name, typeof(UnityEngine.Light));
        g.transform.SetParent(lighting, false);
        g.transform.SetPositionAndRotation(pos, rot);
        var l = g.GetComponent<UnityEngine.Light>();
        l.type = type;
        l.color = Hex(hex);
        l.intensity = intensity;
        l.range = range;
        if (type == LightType.Spot) { l.spotAngle = angle; l.innerSpotAngle = angle * 0.55f; }
        l.shadows = shadowsMode;
        l.shadowStrength = 0.75f;
        l.lightmapBakeType = LightmapBakeType.Realtime;
    }

    /// <summary>World Space canvas whose front faces 'facing' (the direction of the viewer).</summary>
    static RectTransform WorldCanvas(string name, Transform parent, Vector3 centre, Vector3 facing, float widthMetres, Vector2 px, bool interactive)
    {
        var types = interactive
            ? new[] { typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster) }
            : new[] { typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler) };
        var go = new GameObject(name, types);
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
        rt.sizeDelta = px;
        rt.position = centre;
        rt.rotation = Quaternion.LookRotation(-facing, Vector3.up);
        rt.localScale = Vector3.one * (widthMetres / px.x);
        return rt;
    }

    /// <summary>World Space canvas placed in a parent's local space (front = parent's -Z).</summary>
    static RectTransform WorldCanvasLocal(string name, Transform parent, Vector3 localPos, float widthMetres, Vector2 px)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
        rt.sizeDelta = px;
        rt.localPosition = localPos;
        rt.localRotation = Quaternion.identity;
        Vector3 ls = parent.lossyScale;
        float s = widthMetres / px.x;
        rt.localScale = new Vector3(s / ls.x, s / ls.y, s / Mathf.Max(0.0001f, ls.z));
        return rt;
    }

    static TextMeshProUGUI Txt(RectTransform parent, string name, string text, float size, Color color, Vector2 pos, Vector2 box,
                               bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Vector2? anchor = null)
    {
        var t = UIKit.Text(parent, name, text, size, color, align, bold);
        Vector2 a = anchor ?? new Vector2(0, 1);
        UIKit.Place(t.rectTransform, a, a, pos, box);
        return t;
    }

    static Bounds RendererBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Moves an object so its footprint is centred on (x, z) and it stands on the floor.</summary>
    static void Fit(GameObject g, float x, float z)
    {
        var b = RendererBounds(g);
        g.transform.position += new Vector3(x - b.center.x, -b.min.y, z - b.center.z);
    }

    static void Shadow(Vector3 centre, Vector2 size)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FX_ContactShadow.mat");
        if (mat == null) return;
        var go = new GameObject("CS", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(shadows, false);
        go.transform.position = new Vector3(centre.x, 0.008f, centre.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(size.x + 0.3f, size.y + 0.3f, 1f);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
}
