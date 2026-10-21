#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Set dressing for the four rooms: low-poly props built from primitives (same flat style as the rooms) under one
/// "SetDressing" object per scene. Re-running replaces that object, so it is safe to run again after tweaks.
///   Tools > Scam Sense > Environment > Dress All Rooms
///
/// Every prop is checked before it is kept:
///   - it must not overlap existing furniture, screens, boards or other new props (resting on a surface is fine);
///   - it must stay inside the room;
///   - floor props with colliders must keep every interactive station reachable from the player's start
///     (a walkability grid using the player's capsule is compared before and after).
/// A prop that fails is removed and listed in the report. Screens glow with emissive materials (no extra lights,
/// except one small shadowless glow in Room 2). Small desk items have no colliders so they never snag the player.
/// </summary>
public static class EnvironmentDressing
{
    const string RootName = "SetDressing";
    const string MatFolder = "Assets/Materials/SetDressing";

    // ------------------------------------------------------------------ entry points

    [MenuItem("Tools/Scam Sense/Environment/Dress All Rooms")]
    public static void DressAllRooms()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        var report = new StringBuilder();
        foreach (var room in Rooms) report.AppendLine(DressRoom(room));
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("EnvironmentDressing report:\n" + report);
        LastReport = report.ToString();
    }

    [MenuItem("Tools/Scam Sense/Environment/Remove Set Dressing From All Rooms")]
    public static void RemoveAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        foreach (var room in Rooms)
        {
            var s = EditorSceneManager.OpenScene(room.scene, OpenSceneMode.Single);
            foreach (var go in s.GetRootGameObjects()) if (go.name == RootName) UnityEngine.Object.DestroyImmediate(go);
            EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s);
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    public static string LastReport;

    // ------------------------------------------------------------------ room definitions

    class Room
    {
        public string scene, title;
        public Bounds interior;               // inside of the walls (floor to ceiling)
        public Vector3 spawn;                 // player start (feet)
        public float floorY;
        public Vector3[] targets;             // interactive stations that must stay reachable (feet positions)
        public Action<Builder> build;
    }

    static Room[] Rooms => new[]
    {
        new Room
        {
            scene = "Assets/Scenes/Game.unity", title = "Room 1 - Investigator's home office",
            interior = Bounds(-5.27f, 8.53f, -1.69f, 2.40f, -6.10f, 7.70f), floorY = -1.69f, spawn = new Vector3(-3.1f, -1.69f, -0.8f),
            targets = new[] { new Vector3(-0.2f, -1.69f, -4.2f), new Vector3(4.0f, -1.69f, -3.6f), new Vector3(0.9f, -1.69f, 4.4f) },
            build = BuildRoom1,
        },
        new Room
        {
            scene = "Assets/Scenes/Room2_InvestigationOffice.unity", title = "Room 2 - Digital Communications Room",
            interior = Bounds(-4.5f, 4.5f, 0f, 3.6f, -3.5f, 3.5f), floorY = 0f, spawn = new Vector3(2.4f, 0f, -2.4f),
            targets = new[] { new Vector3(0.15f, 0f, 1.6f), new Vector3(1.75f, 0f, 2.35f), new Vector3(-3.7f, 0f, 0.2f),
                              new Vector3(3.3f, 0f, -1.2f), new Vector3(2.6f, 0f, -3.0f) },
            build = BuildRoom2,
        },
        new Room
        {
            scene = "Assets/Scenes/Room3_DigitalTrap.unity", title = "Room 3 - Cybersecurity Analysis Room",
            interior = Bounds(-6.0f, 6.0f, 0f, 3.5f, -4.5f, 4.5f), floorY = 0f, spawn = new Vector3(3.6f, 0f, -3.3f),
            targets = new[] { new Vector3(0.15f, 0f, 2.5f), new Vector3(1.75f, 0f, 3.2f), new Vector3(-2.0f, 0f, -1.75f),
                              new Vector3(-4.4f, 0f, 1.4f), new Vector3(4.5f, 0f, 0.2f), new Vector3(0.9f, 0f, -3.6f) },
            build = BuildRoom3,
        },
        new Room
        {
            scene = "Assets/Scenes/Room4_DigitalForensicsLab.unity", title = "Room 4 - Digital Forensics Lab",
            interior = Bounds(-6.0f, 6.0f, 0f, 3.3f, -4.5f, 4.5f), floorY = 0f, spawn = new Vector3(0.9f, 0f, -3.6f),
            targets = new[] { new Vector3(0.15f, 0f, 2.5f), new Vector3(1.75f, 0f, 3.2f), new Vector3(0f, 0f, -0.4f),
                              new Vector3(4.4f, 0f, 0.9f), new Vector3(4.4f, 0f, -2.8f), new Vector3(-3.4f, 0f, -1.8f),
                              new Vector3(-4.9f, 0f, 0.4f), new Vector3(-4.7f, 0f, -3.4f) },
            build = BuildRoom4,
        },
    };

    static Bounds Bounds(float x0, float x1, float y0, float y1, float z0, float z1)
    {
        var b = new Bounds(); b.SetMinMax(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1)); return b;
    }

    // ------------------------------------------------------------------ Room 1: the player's home - investigator's home office

    static void BuildRoom1(Builder b)
    {
        // Bedroom desk (top y -0.485) against the wall at x 1.27: case corkboard above the monitors.
        var board = b.Group("Wall_CaseCorkboard", new Vector3(1.25f, 0.55f, -4.7f), 90f);
        Props.Corkboard(b, board, 1.6f, 0.8f, 11, "MY CASE NOTES");
        // Free end of the desk: folders, a mug, sticky notes.
        Props.FolderStack(b, b.Group("Desk_Folders", new Vector3(0.86f, -0.485f, -3.12f), 8f), 3);
        Props.Mug(b, b.Group("Desk_Mug", new Vector3(0.58f, -0.485f, -3.28f), 0f), b.M("MugBlue"));
        Props.StickyPad(b, b.Group("Desk_StickyPad", new Vector3(0.62f, -0.485f, -2.95f), -12f), b.M("StickyYellow"));

        // Kitchen table (top y -0.525): breakfast table that is actually used.
        Props.FruitBowl(b, b.Group("Kitchen_FruitBowl", new Vector3(0.9f, -0.525f, 5.85f), 0f));
        Props.Mug(b, b.Group("Kitchen_Mug_1", new Vector3(0.45f, -0.525f, 5.45f), 0f), b.M("MugWhite"));
        Props.Mug(b, b.Group("Kitchen_Mug_2", new Vector3(1.35f, -0.525f, 6.15f), 40f), b.M("MugRed"));
        Props.PaperStack(b, b.Group("Kitchen_Mail", new Vector3(1.35f, -0.525f, 5.45f), 20f), 2, b.M("Paper"));

        // Living-room table (top y -0.935): coffee and the TV remote.
        Props.Mug(b, b.Group("Living_Mug", new Vector3(5.2f, -0.935f, -2.95f), 0f), b.M("MugWhite"));
        Props.Remote(b, b.Group("Living_Remote", new Vector3(5.25f, -0.935f, -3.5f), 25f));
    }

    // ------------------------------------------------------------------ Room 2: Digital Communications Room

    static void BuildRoom2(Builder b)
    {
        // Communications console against the south wall, under the awareness poster's left side.
        var desk = b.Group("Comms_Desk", new Vector3(-0.7f, 0f, -3.15f), 180f);
        Props.Desk(b, desk, 1.7f, 0.7f, 0.75f);
        var top = b.Group("Comms_DeskTop", new Vector3(-0.7f, 0.75f, -3.15f), 180f);
        Props.Monitor(b, top, new Vector3(-0.3f, 0f, 0.18f), 0f, Screen.Email);
        Props.Monitor(b, top, new Vector3(0.3f, 0f, 0.2f), -10f, Screen.Chat);
        Props.Keyboard(b, top, new Vector3(0f, 0f, -0.12f), 0f);
        Props.Mouse(b, top, new Vector3(0.33f, 0f, -0.14f));
        Props.DeskPhone(b, top, new Vector3(-0.65f, 0f, -0.05f), 15f);
        Props.Headset(b, top, new Vector3(0.65f, 0f, 0.05f));
        Props.StickyNote(b, top, new Vector3(-0.15f, 0.001f, -0.25f), 10f, b.M("StickyPink"));
        Props.StickyNote(b, top, new Vector3(-0.42f, 0.001f, -0.22f), -6f, b.M("StickyYellow"));
        Props.Chair(b, b.Group("Comms_Chair", new Vector3(-0.7f, 0f, -2.45f), 10f));
        b.Glow("Comms_ScreenGlow", new Vector3(-0.7f, 1.15f, -2.85f), new Color(0.55f, 0.75f, 1f), 0.5f, 2.2f);

        // Printer station on the north wall, binders on a wall shelf above it.
        var printer = b.Group("Printer_Cabinet", new Vector3(-2.6f, 0f, 3.2f), 0f);
        Props.Cabinet(b, printer, 0.8f, 0.55f, 0.72f, 2);
        Props.Printer(b, b.Group("Printer", new Vector3(-2.6f, 0.72f, 3.2f), 0f));
        Props.WallShelfBinders(b, b.Group("Wall_BinderShelf", new Vector3(-2.6f, 1.75f, 3.37f), 0f), 0.8f, 5);

        // Live feed monitor on the north wall, above the storage boxes.
        Props.WallTV(b, b.Group("Wall_FeedMonitor", new Vector3(2.9f, 1.9f, 3.455f), 0f), 1.1f, 0.62f, Screen.Dashboard);

        // Phishing printouts board on the south wall above the lounge chair.
        Props.Corkboard(b, b.Group("Wall_PhishingBoard", new Vector3(-3.3f, 1.94f, -3.465f), 180f), 1.4f, 0.82f, 21, "FLAGGED MESSAGES");

        // Main desk (top y 1.155): a mug and sticky notes in the free corner.
        Props.Mug(b, b.Group("Desk_Mug", new Vector3(1.0f, 1.155f, 2.62f), 0f), b.M("MugWhite"));
        Props.StickyNote(b, b.Group("Desk_Sticky", new Vector3(0.98f, 1.156f, 2.92f), 8f), Vector3.zero, 0f, b.M("StickyYellow"));

        // Two more photos in the gaps of the evidence board (west wall).
        Props.Photo(b, b.Group("Board_Photo_1", new Vector3(-4.43f, 1.22f, -1.0f), -90f), 0.2f, 0.25f, 3);
        Props.Photo(b, b.Group("Board_Photo_2", new Vector3(-4.43f, 1.22f, 1.4f), -90f), 0.2f, 0.25f, 5);
    }

    // ------------------------------------------------------------------ Room 3: Cybersecurity Analysis Room

    static void BuildRoom3(Builder b)
    {
        // SOC monitoring desk in the open east floor, facing the wall display.
        Props.Desk(b, b.Group("SOC_Desk", new Vector3(2.7f, 0f, 1.0f), 0f), 1.8f, 0.8f, 0.75f);
        var top = b.Group("SOC_DeskTop", new Vector3(2.7f, 0.75f, 1.0f), 0f);
        Props.Monitor(b, top, new Vector3(-0.6f, 0f, 0.22f), -18f, Screen.Log);
        Props.Monitor(b, top, new Vector3(0f, 0f, 0.26f), 0f, Screen.Dashboard);
        Props.Monitor(b, top, new Vector3(0.6f, 0f, 0.22f), 18f, Screen.Map);
        Props.Keyboard(b, top, new Vector3(0f, 0f, -0.12f), 0f);
        Props.Mouse(b, top, new Vector3(0.33f, 0f, -0.14f));
        Props.Mug(b, top, b.M("MugBlue"), new Vector3(-0.7f, 0f, -0.2f));
        Props.StickyNote(b, top, new Vector3(-0.32f, 0.001f, -0.27f), 12f, b.M("StickyYellow"));
        Props.StickyNote(b, top, new Vector3(0.62f, 0.001f, -0.25f), -9f, b.M("StickyBlue"));
        Props.Tower(b, b.Group("SOC_Tower", new Vector3(3.78f, 0f, 1.05f), 0f));
        Props.Chair(b, b.Group("SOC_Chair", new Vector3(2.7f, 0f, 0.2f), 180f));

        // Incident whiteboard on the south wall (between the shelving and the coffee corner).
        Props.Whiteboard(b, b.Group("Wall_IncidentWhiteboard", new Vector3(-0.7f, 1.3f, -4.465f), 180f), 1.4f, 0.9f, "INCIDENT LOG");

        // Printer and binders on the north wall, west of the main desk.
        Props.Cabinet(b, b.Group("Printer_Cabinet", new Vector3(-2.8f, 0f, 4.2f), 0f), 0.8f, 0.5f, 0.72f, 2);
        Props.Printer(b, b.Group("Printer", new Vector3(-2.8f, 0.72f, 4.2f), 0f));
        Props.WallShelfBinders(b, b.Group("Wall_BinderShelf", new Vector3(-2.8f, 1.8f, 4.36f), 0f), 0.8f, 6);

        // Archive boxes in the south-west corner (under the digital case wall).
        Props.ArchiveBox(b, b.Group("Floor_Archive_1", new Vector3(-5.35f, 0f, -4.05f), 180f), "CASE 03-A", true);
        Props.ArchiveBox(b, b.Group("Floor_Archive_2", new Vector3(-5.35f, 0.3f, -4.05f), 186f), "CASE 03-B", true);
        Props.ArchiveBox(b, b.Group("Floor_Archive_3", new Vector3(-4.8f, 0f, -4.1f), 172f), "LOGS 2026", true);

        // Analyst desk (top y 1.155): a case folder in its free corner.
        Props.FolderStack(b, b.Group("Analyst_Folder", new Vector3(-2.97f, 1.155f, -0.75f), 4f), 1);
    }

    // ------------------------------------------------------------------ Room 4: Digital Forensics Lab

    static void BuildRoom4(Builder b)
    {
        // Disk-imaging desk in the north-east corner.
        Props.Desk(b, b.Group("Imaging_Desk", new Vector3(3.3f, 0f, 4.0f), 0f), 1.6f, 0.7f, 0.75f);
        var top = b.Group("Imaging_DeskTop", new Vector3(3.3f, 0.75f, 4.0f), 0f);
        Props.Monitor(b, top, new Vector3(-0.3f, 0f, 0.18f), 8f, Screen.Log);
        Props.Monitor(b, top, new Vector3(0.32f, 0f, 0.18f), -8f, Screen.Email);
        Props.Keyboard(b, top, new Vector3(0f, 0f, -0.12f), 0f);
        Props.Mouse(b, top, new Vector3(0.3f, 0f, -0.14f));
        Props.DriveDock(b, top, new Vector3(-0.62f, 0f, -0.08f));
        Props.HardDriveStack(b, top, new Vector3(0.65f, 0f, -0.05f), 3);
        Props.EvidenceBag(b, top, new Vector3(0.6f, 0f, -0.27f), 15f);
        Props.Tower(b, b.Group("Imaging_Tower", new Vector3(2.35f, 0f, 4.15f), 0f));
        Props.Chair(b, b.Group("Imaging_Chair", new Vector3(3.3f, 0f, 3.25f), 180f));

        // Evidence cart against the south wall, under the lab sign.
        var cart = b.Group("Floor_EvidenceCart", new Vector3(-1.7f, 0f, -3.75f), 180f);
        Props.Cart(b, cart, 0.8f, 0.5f, 0.85f);
        var cartTop = b.Group("Cart_Items", new Vector3(-1.7f, 0.85f, -3.75f), 180f);
        Props.EvidenceBag(b, cartTop, new Vector3(-0.2f, 0f, 0.02f), -10f);
        Props.EvidenceBag(b, cartTop, new Vector3(0.12f, 0.004f, -0.05f), 12f);
        Props.Clipboard(b, cartTop, new Vector3(0.25f, 0.002f, 0.1f), -5f);

        // Sealed evidence boxes on top of the lockers.
        float[] lx = { -5.35f, -4.73f, -4.11f };
        string[] labels = { "EV-0412", "EV-0413", "EV-0415" };
        for (int i = 0; i < 3; i++)
            Props.ArchiveBox(b, b.Group("Locker_Box_" + i, new Vector3(lx[i], 1.9f, -4.18f), 175f + i * 5f), labels[i], false, 0.42f, 0.3f, 0.32f);

        // Website captures board on the north wall, between the timeline board and the dashboard.
        Props.Corkboard(b, b.Group("Wall_CapturesBoard", new Vector3(-2.2f, 1.3f, 4.465f), 0f), 1.2f, 0.8f, 33, "WEBSITE CAPTURES");

        // Chain-of-custody clipboard on the workbench.
        Props.Clipboard(b, b.Group("Bench_Clipboard", new Vector3(4.28f, 0.93f, -3.34f), 8f), Vector3.zero, 0f);
    }

    // ------------------------------------------------------------------ dressing one room

    static string DressRoom(Room room)
    {
        var scene = EditorSceneManager.OpenScene(room.scene, OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects()) if (go.name == RootName) UnityEngine.Object.DestroyImmediate(go);
        Physics.SyncTransforms();

        // Walkability before any new prop exists.
        var before = Walkability.Reachable(room, null);

        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        var b = new Builder(root.transform);
        room.build(b);

        // Validate each top-level prop group against the existing scene and earlier props.
        var existing = Existing(scene, root.transform);
        var report = new StringBuilder();
        report.AppendLine($"== {room.title} ({room.scene})");
        var accepted = new List<Bounds>();
        int kept = 0;
        foreach (Transform g in root.transform.Cast<Transform>().ToList())
        {
            if (!TryBounds(g, out var pb)) { kept++; continue; }   // lights etc.
            var test = Shrink(pb);
            string problem = null;
            if (!room.interior.Contains(pb.min + Vector3.one * 0.005f) || !room.interior.Contains(pb.max - Vector3.one * 0.005f)) problem = "outside the room";
            if (problem == null)
                foreach (var e in existing)
                {
                    if (!test.Intersects(e.bounds)) continue;
                    if (e.bounds.max.y <= pb.min.y + 0.03f) continue;   // resting on it (desk, cabinet, floor item)
                    problem = "overlaps " + e.name; break;
                }
            if (problem == null)
                foreach (var a in accepted)
                    if (test.Intersects(Shrink(a)) && !(a.max.y <= pb.min.y + 0.03f) && !(pb.max.y <= a.min.y + 0.03f)) { problem = "overlaps another new prop"; break; }
            if (problem == null && g.GetComponentInChildren<Collider>() != null)
            {
                var flat = new Vector2(pb.center.x - room.spawn.x, pb.center.z - room.spawn.z).magnitude;
                if (flat < 1.0f) problem = "too close to the player start";
            }
            if (problem != null)
            {
                report.AppendLine($"   SKIPPED {g.name}: {problem}");
                UnityEngine.Object.DestroyImmediate(g.gameObject);
                continue;
            }
            accepted.Add(pb); kept++;
        }

        // Walkability after: every station reachable before must still be reachable.
        Physics.SyncTransforms();
        var after = Walkability.Reachable(room, root.transform);
        for (int i = 0; i < room.targets.Length; i++)
        {
            string state = !before[i] ? "not reachable before (unchanged)" : after[i] ? "reachable" : "BLOCKED";
            report.AppendLine($"   station {i} at ({room.targets[i].x:F1},{room.targets[i].z:F1}): {state}");
        }
        if (Enumerable.Range(0, room.targets.Length).Any(i => before[i] && !after[i]))
        {
            report.AppendLine("   -> a station was blocked: floor props removed from this room.");
            foreach (Transform g in root.transform.Cast<Transform>().ToList())
                if (g.GetComponentInChildren<Collider>() != null) UnityEngine.Object.DestroyImmediate(g.gameObject);
        }

        GameObjectUtility.SetStaticEditorFlags(root, 0);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
        // Readable props (case folders, documents, boards) keep their E interaction after a rebuild.
        report.Append(InteractiveObjectsSetup.ApplyToScene(scene));
        report.AppendLine($"   kept {root.transform.childCount} prop groups, {root.GetComponentsInChildren<Renderer>().Length} renderers, " +
                          $"{root.GetComponentsInChildren<Light>().Length} lights, {root.GetComponentsInChildren<Collider>().Length} colliders");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return report.ToString();
    }

    struct Existing_ { public string name; public Bounds bounds; }

    /// <summary>Furniture, screens and props already in the scene (not walls, floors, ceilings, rugs or shadow decals).</summary>
    static List<Existing_> Existing(Scene scene, Transform skip)
    {
        var list = new List<Existing_>();
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.transform == skip || go.name == "Player") continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                string n = r.name;
                if (n.StartsWith("CS_") || n.Contains("Floor") || n.Contains("Ceiling") || n.Contains("Rug") || n.Contains("Trim") || n.Contains("Baseboard")) continue;
                var bb = r.bounds; var s = bb.size;
                float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z)), max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                if (min < 0.06f && max > 2.5f) continue;                       // big thin surfaces (floor / ceiling panels)
                if (s.y > 2.4f && (s.x < 0.35f || s.z < 0.35f)) continue;      // walls (wall decorations like screens and clocks stay)
                if (s.x > 6f || s.z > 6f) continue;        // room shells
                list.Add(new Existing_ { name = r.transform.parent != null ? r.transform.parent.name + "/" + n : n, bounds = bb });
            }
        }
        return list;
    }

    static Bounds Shrink(Bounds b)
    {
        var s = b.size;
        var d = new Vector3(Mathf.Min(0.03f, s.x * 0.3f), Mathf.Min(0.03f, s.y * 0.3f), Mathf.Min(0.03f, s.z * 0.3f));
        return new Bounds(b.center, s - d);
    }

    static bool TryBounds(Transform t, out Bounds b)
    {
        var rs = t.GetComponentsInChildren<Renderer>(true); b = default;
        if (rs.Length == 0) return false;
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return true;
    }

    // ------------------------------------------------------------------ walkability (player capsule on a grid)

    static class Walkability
    {
        const float Cell = 0.15f, Radius = 0.42f, Height = 1.7f;

        public static bool[] Reachable(Room room, Transform onlyCheckNew)
        {
            var ib = room.interior;
            int nx = Mathf.CeilToInt(ib.size.x / Cell), nz = Mathf.CeilToInt(ib.size.z / Cell);
            var free = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    var p = new Vector3(ib.min.x + (i + 0.5f) * Cell, room.floorY, ib.min.z + (k + 0.5f) * Cell);
                    free[i, k] = !Blocked(p, room.floorY);
                }
            var seen = new bool[nx, nz];
            var q = new Queue<Vector2Int>();
            var s = Cell2(room.spawn, ib, nx, nz);
            seen[s.x, s.y] = true; q.Enqueue(s);   // the start itself is always counted as free
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || seen[n.x, n.y] || !free[n.x, n.y]) continue;
                    seen[n.x, n.y] = true; q.Enqueue(n);
                }
            }
            var result = new bool[room.targets.Length];
            for (int t = 0; t < room.targets.Length; t++)
            {
                var c = Cell2(room.targets[t], ib, nx, nz);
                // A station counts as reachable if any cell within 0.45 m of it is reachable.
                int r = Mathf.CeilToInt(0.45f / Cell);
                for (int i = -r; i <= r && !result[t]; i++)
                    for (int k = -r; k <= r && !result[t]; k++)
                    {
                        int x = c.x + i, z = c.y + k;
                        if (x >= 0 && z >= 0 && x < nx && z < nz && seen[x, z]) result[t] = true;
                    }
            }
            return result;
        }

        static Vector2Int Cell2(Vector3 p, Bounds ib, int nx, int nz) =>
            new Vector2Int(Mathf.Clamp((int)((p.x - ib.min.x) / Cell), 0, nx - 1), Mathf.Clamp((int)((p.z - ib.min.z) / Cell), 0, nz - 1));

        static bool Blocked(Vector3 feet, float floorY)
        {
            var a = feet + Vector3.up * (Radius + 0.12f);   // above small steps / rugs
            var b = feet + Vector3.up * (Height - Radius);
            foreach (var c in Physics.OverlapCapsule(a, b, Radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.root.name == "Player") continue;
                return true;
            }
            return false;
        }
    }

    // ------------------------------------------------------------------ builder + materials

    public enum Screen { Email, Chat, Dashboard, Log, Map }

    class Builder
    {
        readonly Transform root;
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        public Builder(Transform r) { root = r; }

        public Transform Group(string name, Vector3 worldPos, float yaw)
        {
            var g = new GameObject(name).transform;
            g.SetParent(root, false);
            g.position = worldPos;
            g.rotation = Quaternion.Euler(0f, yaw, 0f);
            return g;
        }

        public void Glow(string name, Vector3 pos, Color c, float intensity, float range)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root, false);
            g.transform.position = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForceVertex;   // cheap: no per-pixel cost
        }

        /// <summary>A box in a group's local space (centre, size, local euler). Visual only unless collider is set.</summary>
        public Transform Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, Vector3 euler = default, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = size.x * size.y * size.z < 0.0004f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        public Transform Cylinder(Transform parent, string name, Vector3 center, float diameter, float height, Material m, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }

        /// <summary>Flat text facing -Z of its parent (readable when the parent's front faces the player).</summary>
        public void Label(Transform parent, string text, Vector3 center, float height, Color c, float width, bool bold = true)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.color = c;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = false;
            t.fontSize = height * 10f;   // TextMeshPro (3D): font size 10 is about 1 unit tall
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(width, height * 1.4f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public Material M(string key)
        {
            if (mats.TryGetValue(key, out var m)) return m;
            if (!Palette.TryGetValue(key, out var spec)) spec = (Color.magenta, 0f, Color.black);
            string path = $"{MatFolder}/SD_{key}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "SetDressing");
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = spec.color;
            m.SetFloat("_Glossiness", spec.gloss);
            if (spec.emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", spec.emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            mats[key] = m;
            return m;
        }
    }

    static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    static readonly Dictionary<string, (Color color, float gloss, Color emission)> Palette = new Dictionary<string, (Color, float, Color)>
    {
        { "Paper", (H("#F1F0EA"), 0.1f, Color.black) }, { "PaperBlue", (H("#E4ECF5"), 0.1f, Color.black) },
        { "Ink", (H("#8C939E"), 0.1f, Color.black) }, { "InkDark", (H("#3B4250"), 0.1f, Color.black) },
        { "Highlight", (H("#E5484D"), 0.2f, Color.black) }, { "HeaderBlue", (H("#2F6FB3"), 0.2f, Color.black) },
        { "Manila", (H("#D9B97A"), 0.15f, Color.black) }, { "FolderRed", (H("#B5483F"), 0.2f, Color.black) },
        { "FolderBlue", (H("#3C6E9E"), 0.2f, Color.black) }, { "FolderGreen", (H("#4E8A5E"), 0.2f, Color.black) },
        { "Cork", (H("#B98A57"), 0.05f, Color.black) }, { "Frame", (H("#5A3E2B"), 0.3f, Color.black) },
        { "Pin", (H("#D83A3A"), 0.6f, Color.black) }, { "String", (H("#C62828"), 0.2f, Color.black) },
        { "StickyYellow", (H("#F6E27A"), 0.1f, Color.black) }, { "StickyPink", (H("#F3A6C0"), 0.1f, Color.black) },
        { "StickyBlue", (H("#9CCBF0"), 0.1f, Color.black) }, { "StickyGreen", (H("#A9DFA0"), 0.1f, Color.black) },
        { "Plastic", (H("#2A2F38"), 0.35f, Color.black) }, { "PlasticLight", (H("#C9CED6"), 0.35f, Color.black) },
        { "Metal", (H("#8E949C"), 0.55f, Color.black) }, { "MetalDark", (H("#4A4F57"), 0.5f, Color.black) },
        { "DeskTop", (H("#C7B08A"), 0.25f, Color.black) }, { "DeskWhite", (H("#E8EAED"), 0.3f, Color.black) },
        { "Fabric", (H("#3C4A5C"), 0.05f, Color.black) }, { "Cardboard", (H("#B88D5A"), 0.05f, Color.black) },
        { "Whiteboard", (H("#F7F8FA"), 0.7f, Color.black) }, { "Marker", (H("#2457A6"), 0.2f, Color.black) },
        { "MarkerRed", (H("#C0392B"), 0.2f, Color.black) },
        { "ScreenWhite", (H("#DDE6F0"), 0.2f, H("#C9D6E4") * 0.55f) }, { "ScreenDark", (H("#0E1726"), 0.3f, H("#0E1726") * 0.6f) },
        { "UiBlue", (H("#5AA9E6"), 0.2f, H("#5AA9E6") * 0.9f) }, { "UiGreen", (H("#4ADE80"), 0.2f, H("#4ADE80") * 0.9f) },
        { "UiAmber", (H("#F59E0B"), 0.2f, H("#F59E0B") * 0.9f) }, { "UiRed", (H("#EF4444"), 0.2f, H("#EF4444") * 0.9f) },
        { "UiGrey", (H("#94A3B8"), 0.2f, H("#94A3B8") * 0.55f) }, { "UiLine", (H("#7A8796"), 0.2f, H("#7A8796") * 0.4f) },
        { "LedGreen", (H("#22C55E"), 0.4f, H("#22C55E") * 1.6f) }, { "LedBlue", (H("#38BDF8"), 0.4f, H("#38BDF8") * 1.6f) },
        { "MugWhite", (H("#F4F4F2"), 0.5f, Color.black) }, { "MugBlue", (H("#2E5E8C"), 0.5f, Color.black) },
        { "MugRed", (H("#B23A3A"), 0.5f, Color.black) }, { "Coffee", (H("#4A2C1A"), 0.6f, Color.black) },
        { "Bag", (H("#E9EEF2"), 0.6f, Color.black) }, { "BagRed", (H("#D94141"), 0.3f, Color.black) },
        { "Photo1", (H("#6B8FB5"), 0.3f, Color.black) }, { "Photo2", (H("#7FA36B"), 0.3f, Color.black) },
        { "Photo3", (H("#9C8A7A"), 0.3f, Color.black) }, { "Photo4", (H("#5E6B7E"), 0.3f, Color.black) },
        { "Fruit1", (H("#E05A2B"), 0.4f, Color.black) }, { "Fruit2", (H("#E8C547"), 0.4f, Color.black) },
        { "Fruit3", (H("#7DBA4A"), 0.4f, Color.black) }, { "Bowl", (H("#E7E2D6"), 0.4f, Color.black) },
    };

    // ------------------------------------------------------------------ prop library (local origin = bottom centre)

    static class Props
    {
        // ---- paper and desk items ----
        public static void PaperStack(Builder b, Transform g, int n, Material m)
        {
            for (int i = 0; i < n; i++)
                b.Box(g, "Sheet" + i, new Vector3(i * 0.006f, 0.002f + i * 0.003f, -i * 0.004f), new Vector3(0.21f, 0.002f, 0.297f), m, new Vector3(0, i * 4f - 4f, 0));
        }

        public static void FolderStack(Builder b, Transform g, int n)
        {
            string[] cols = { "Manila", "FolderRed", "FolderBlue", "FolderGreen" };
            for (int i = 0; i < n; i++)
            {
                float y = i * 0.014f;
                b.Box(g, "Folder" + i, new Vector3(i * 0.008f, y + 0.006f, -i * 0.01f), new Vector3(0.23f, 0.01f, 0.31f), b.M(cols[i % cols.Length]), new Vector3(0, i * 5f - 5f, 0));
                b.Box(g, "Papers" + i, new Vector3(i * 0.008f + 0.012f, y + 0.012f, -i * 0.01f + 0.01f), new Vector3(0.21f, 0.004f, 0.3f), b.M("Paper"), new Vector3(0, i * 5f - 3f, 0));
            }
        }

        public static void StickyPad(Builder b, Transform g, Material m)
        {
            b.Box(g, "Pad", new Vector3(0, 0.008f, 0), new Vector3(0.076f, 0.016f, 0.076f), m);
            b.Box(g, "PenOnPad", new Vector3(0.06f, 0.006f, 0.01f), new Vector3(0.012f, 0.012f, 0.14f), b.M("MarkerRed"), new Vector3(0, 20f, 0));
        }

        public static void StickyNote(Builder b, Transform g, Vector3 at, float yaw, Material m)
        {
            b.Box(g, "StickyNote", at + new Vector3(0, 0.0012f, 0), new Vector3(0.075f, 0.0016f, 0.075f), m, new Vector3(0, yaw, 0));
        }

        public static void Mug(Builder b, Transform g, Material m) => Mug(b, g, m, Vector3.zero);
        public static void Mug(Builder b, Transform g, Material m, Vector3 at)
        {
            b.Cylinder(g, "Mug", at + new Vector3(0, 0.05f, 0), 0.08f, 0.1f, m);
            b.Cylinder(g, "Coffee", at + new Vector3(0, 0.096f, 0), 0.07f, 0.004f, b.M("Coffee"));
            b.Box(g, "Handle", at + new Vector3(0.05f, 0.05f, 0), new Vector3(0.02f, 0.06f, 0.012f), m);
        }

        public static void Keyboard(Builder b, Transform g, Vector3 at, float yaw)
        {
            var k = b.Box(g, "Keyboard", at + new Vector3(0, 0.012f, 0), new Vector3(0.44f, 0.024f, 0.14f), b.M("Plastic"), new Vector3(0, yaw, 0));
            b.Box(g, "Keys", at + new Vector3(0, 0.0255f, 0.004f), new Vector3(0.41f, 0.003f, 0.11f), b.M("MetalDark"), new Vector3(0, yaw, 0));
        }

        public static void Mouse(Builder b, Transform g, Vector3 at) =>
            b.Box(g, "Mouse", at + new Vector3(0, 0.017f, 0), new Vector3(0.06f, 0.034f, 0.1f), b.M("Plastic"));

        public static void Monitor(Builder b, Transform g, Vector3 at, float yaw, Screen kind)
        {
            var m = new GameObject("Monitor_" + kind).transform;
            m.SetParent(g, false); m.localPosition = at; m.localEulerAngles = new Vector3(0, yaw, 0);
            b.Box(m, "Base", new Vector3(0, 0.008f, 0.04f), new Vector3(0.22f, 0.016f, 0.16f), b.M("Plastic"));
            b.Box(m, "Neck", new Vector3(0, 0.12f, 0.06f), new Vector3(0.05f, 0.22f, 0.03f), b.M("Plastic"));
            b.Box(m, "Bezel", new Vector3(0, 0.32f, 0.03f), new Vector3(0.56f, 0.34f, 0.03f), b.M("Plastic"));
            ScreenFace(b, m, new Vector3(0, 0.325f, 0.0135f), 0.53f, 0.31f, kind);
        }

        /// <summary>Emissive screen with a simple UI drawn from flat bars. Faces -Z of its parent.</summary>
        public static void ScreenFace(Builder b, Transform parent, Vector3 c, float w, float h, Screen kind)
        {
            bool light = kind == Screen.Email;
            b.Box(parent, "Screen", c, new Vector3(w, h, 0.002f), b.M(light ? "ScreenWhite" : "ScreenDark"));
            float z = c.z - 0.0016f;
            Vector3 P(float x, float y) => new Vector3(c.x + x * w, c.y + y * h, z);
            void Bar(string mat, float x, float y, float bw, float bh) => b.Box(parent, "Ui", P(x, y), new Vector3(bw * w, bh * h, 0.001f), b.M(mat));
            switch (kind)
            {
                case Screen.Email:
                    Bar("HeaderBlue", 0f, 0.43f, 1f, 0.12f);
                    Bar("UiGrey", -0.36f, -0.06f, 0.26f, 0.84f);
                    for (int i = 0; i < 6; i++)
                    {
                        Bar(i == 1 ? "UiRed" : "InkDark", 0.13f, 0.27f - i * 0.13f, 0.66f, 0.03f);
                        Bar("UiLine", 0.08f, 0.22f - i * 0.13f, 0.56f, 0.02f);
                    }
                    break;
                case Screen.Chat:
                    Bar("UiGrey", 0f, 0.43f, 1f, 0.1f);
                    for (int i = 0; i < 4; i++)
                    {
                        bool me = i % 2 == 1;
                        Bar(me ? "UiBlue" : "UiLine", me ? 0.2f : -0.2f, 0.25f - i * 0.2f, 0.5f, 0.12f);
                    }
                    break;
                case Screen.Dashboard:
                    Bar("UiBlue", -0.3f, 0.38f, 0.35f, 0.06f);
                    Bar("UiAmber", 0.3f, 0.38f, 0.2f, 0.06f);
                    float[] hs = { 0.25f, 0.4f, 0.32f, 0.55f, 0.45f, 0.7f, 0.38f, 0.6f, 0.5f, 0.3f };
                    for (int i = 0; i < hs.Length; i++)
                        Bar(i == 5 ? "UiRed" : "UiBlue", -0.42f + i * 0.093f, -0.42f + hs[i] * 0.5f, 0.06f, hs[i]);
                    break;
                case Screen.Log:
                    for (int i = 0; i < 9; i++)
                        Bar(i == 4 ? "UiAmber" : i == 6 ? "UiRed" : "UiGreen", -0.12f + (i % 3) * 0.04f, 0.4f - i * 0.1f, 0.62f - (i % 4) * 0.08f, 0.035f);
                    break;
                case Screen.Map:
                    Bar("UiLine", 0f, 0f, 0.86f, 0.004f);
                    Bar("UiLine", 0f, 0f, 0.004f, 0.8f);
                    Vector2[] pts = { new Vector2(-0.3f, 0.2f), new Vector2(0.25f, 0.3f), new Vector2(0.1f, -0.2f), new Vector2(-0.15f, -0.3f), new Vector2(0.35f, -0.05f) };
                    for (int i = 0; i < pts.Length; i++) Bar(i == 1 ? "UiRed" : "UiAmber", pts[i].x, pts[i].y, 0.05f, 0.08f);
                    Bar("UiBlue", -0.32f, 0.42f, 0.3f, 0.06f);
                    break;
            }
        }

        public static void Tower(Builder b, Transform g)
        {
            b.Box(g, "Tower", new Vector3(0, 0.23f, 0), new Vector3(0.2f, 0.46f, 0.44f), b.M("Plastic"), default, true);
            b.Box(g, "PowerLed", new Vector3(0, 0.4f, -0.222f), new Vector3(0.02f, 0.02f, 0.004f), b.M("LedBlue"));
            b.Box(g, "Vent", new Vector3(0, 0.2f, -0.222f), new Vector3(0.14f, 0.2f, 0.003f), b.M("MetalDark"));
        }

        public static void DeskPhone(Builder b, Transform g, Vector3 at, float yaw)
        {
            var e = new Vector3(0, yaw, 0);
            b.Box(g, "PhoneBase", at + new Vector3(0, 0.025f, 0), new Vector3(0.18f, 0.05f, 0.2f), b.M("Plastic"), e);
            b.Box(g, "Handset", at + new Vector3(-0.045f, 0.06f, 0), new Vector3(0.06f, 0.03f, 0.21f), b.M("Plastic"), e);
            b.Box(g, "Display", at + new Vector3(0.04f, 0.052f, 0.04f), new Vector3(0.07f, 0.004f, 0.04f), b.M("UiGreen"), e);
        }

        public static void Headset(Builder b, Transform g, Vector3 at)
        {
            b.Box(g, "Band", at + new Vector3(0, 0.08f, 0), new Vector3(0.17f, 0.02f, 0.03f), b.M("Plastic"));
            b.Box(g, "EarL", at + new Vector3(-0.08f, 0.035f, 0), new Vector3(0.03f, 0.07f, 0.07f), b.M("Fabric"));
            b.Box(g, "EarR", at + new Vector3(0.08f, 0.035f, 0), new Vector3(0.03f, 0.07f, 0.07f), b.M("Fabric"));
        }

        public static void Remote(Builder b, Transform g)
        {
            b.Box(g, "Remote", new Vector3(0, 0.01f, 0), new Vector3(0.05f, 0.02f, 0.17f), b.M("Plastic"));
            b.Box(g, "Button", new Vector3(0, 0.021f, 0.06f), new Vector3(0.015f, 0.003f, 0.015f), b.M("Highlight"));
        }

        public static void FruitBowl(Builder b, Transform g)
        {
            b.Cylinder(g, "Bowl", new Vector3(0, 0.04f, 0), 0.26f, 0.08f, b.M("Bowl"));
            string[] f = { "Fruit1", "Fruit2", "Fruit3", "Fruit1" };
            Vector3[] p = { new Vector3(-0.05f, 0.1f, 0.02f), new Vector3(0.05f, 0.1f, -0.03f), new Vector3(0.01f, 0.11f, 0.06f), new Vector3(0.02f, 0.14f, -0.01f) };
            for (int i = 0; i < f.Length; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
                s.name = "Fruit"; s.transform.SetParent(g, false); s.transform.localPosition = p[i]; s.transform.localScale = Vector3.one * 0.075f;
                s.GetComponent<MeshRenderer>().sharedMaterial = b.M(f[i]);
            }
        }

        public static void Printer(Builder b, Transform g)
        {
            b.Box(g, "Body", new Vector3(0, 0.12f, 0), new Vector3(0.46f, 0.24f, 0.38f), b.M("PlasticLight"));
            b.Box(g, "Top", new Vector3(0, 0.245f, 0.02f), new Vector3(0.44f, 0.01f, 0.32f), b.M("Plastic"));
            b.Box(g, "Tray", new Vector3(0, 0.07f, -0.22f), new Vector3(0.3f, 0.01f, 0.1f), b.M("Plastic"));
            b.Box(g, "PrintOut", new Vector3(0, 0.08f, -0.24f), new Vector3(0.21f, 0.004f, 0.14f), b.M("Paper"), new Vector3(-8f, 0, 0));
            b.Box(g, "Panel", new Vector3(0.15f, 0.2f, -0.192f), new Vector3(0.1f, 0.05f, 0.004f), b.M("UiBlue"));
        }

        public static void DriveDock(Builder b, Transform g, Vector3 at)
        {
            b.Box(g, "Dock", at + new Vector3(0, 0.03f, 0), new Vector3(0.14f, 0.06f, 0.1f), b.M("Plastic"));
            b.Box(g, "Drive", at + new Vector3(0, 0.1f, 0), new Vector3(0.1f, 0.1f, 0.016f), b.M("Metal"));
            b.Box(g, "Led", at + new Vector3(0.05f, 0.04f, -0.051f), new Vector3(0.012f, 0.012f, 0.003f), b.M("LedGreen"));
        }

        public static void HardDriveStack(Builder b, Transform g, Vector3 at, int n)
        {
            for (int i = 0; i < n; i++)
                b.Box(g, "HardDrive" + i, at + new Vector3(i * 0.004f, 0.008f + i * 0.017f, 0), new Vector3(0.1f, 0.016f, 0.147f), b.M(i % 2 == 0 ? "Metal" : "MetalDark"), new Vector3(0, i * 7f, 0));
        }

        public static void EvidenceBag(Builder b, Transform g, Vector3 at, float yaw)
        {
            b.Box(g, "EvidenceBag", at + new Vector3(0, 0.004f, 0), new Vector3(0.22f, 0.008f, 0.3f), b.M("Bag"), new Vector3(0, yaw, 0));
            b.Box(g, "BagStripe", at + new Vector3(0, 0.0085f, 0.1f), new Vector3(0.22f, 0.002f, 0.03f), b.M("BagRed"), new Vector3(0, yaw, 0));
        }

        public static void Clipboard(Builder b, Transform g, Vector3 at, float yaw)
        {
            var e = new Vector3(0, yaw, 0);
            b.Box(g, "Clipboard", at + new Vector3(0, 0.004f, 0), new Vector3(0.23f, 0.008f, 0.32f), b.M("Frame"), e);
            b.Box(g, "Form", at + new Vector3(0, 0.009f, -0.01f), new Vector3(0.2f, 0.002f, 0.27f), b.M("Paper"), e);
            b.Box(g, "Clip", at + new Vector3(0, 0.014f, 0.14f), new Vector3(0.08f, 0.01f, 0.03f), b.M("Metal"), e);
            for (int i = 0; i < 5; i++) b.Box(g, "Line", at + new Vector3(0, 0.0105f, 0.08f - i * 0.045f), new Vector3(0.16f, 0.001f, 0.008f), b.M("Ink"), e);
        }

        public static void Photo(Builder b, Transform g, float w, float h, int seed)
        {
            b.Box(g, "PhotoBorder", new Vector3(0, h * 0.5f, 0), new Vector3(w, h, 0.004f), b.M("Paper"));
            b.Box(g, "PhotoImage", new Vector3(0, h * 0.55f, -0.0025f), new Vector3(w * 0.86f, h * 0.72f, 0.002f), b.M("Photo" + (1 + seed % 4)));
            b.Box(g, "Pin", new Vector3(0, h * 0.92f, -0.008f), new Vector3(0.016f, 0.016f, 0.012f), b.M("Pin"));
        }

        // ---- furniture ----
        public static void Desk(Builder b, Transform g, float w, float d, float h)
        {
            b.Box(g, "Top", new Vector3(0, h - 0.02f, 0), new Vector3(w, 0.04f, d), b.M("DeskWhite"), default, true);
            foreach (var sx in new[] { -1f, 1f })
            {
                b.Box(g, "Leg", new Vector3(sx * (w * 0.5f - 0.04f), (h - 0.04f) * 0.5f, 0), new Vector3(0.05f, h - 0.04f, d - 0.06f), b.M("MetalDark"), default, true);
            }
            b.Box(g, "ModestyPanel", new Vector3(0, h * 0.55f, d * 0.5f - 0.04f), new Vector3(w - 0.1f, h * 0.45f, 0.02f), b.M("MetalDark"));
            b.Box(g, "Cable", new Vector3(w * 0.3f, h * 0.45f, d * 0.5f - 0.06f), new Vector3(0.015f, h * 0.9f, 0.015f), b.M("Plastic"));
        }

        public static void Chair(Builder b, Transform g)
        {
            b.Box(g, "Seat", new Vector3(0, 0.47f, 0), new Vector3(0.48f, 0.08f, 0.46f), b.M("Fabric"));
            b.Box(g, "Back", new Vector3(0, 0.85f, 0.21f), new Vector3(0.46f, 0.6f, 0.07f), b.M("Fabric"), new Vector3(-6f, 0, 0));
            b.Box(g, "Post", new Vector3(0, 0.25f, 0), new Vector3(0.05f, 0.4f, 0.05f), b.M("MetalDark"));
            for (int i = 0; i < 5; i++)
                b.Box(g, "Foot", new Vector3(0, 0.04f, 0), new Vector3(0.05f, 0.03f, 0.58f), b.M("MetalDark"), new Vector3(0, i * 72f, 0));
            // One simple collider so the player bumps into the chair instead of walking through it.
            var col = new GameObject("ChairCollider");
            col.transform.SetParent(g, false);
            var box = col.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.55f, 0.03f); box.size = new Vector3(0.55f, 1.1f, 0.55f);
        }

        public static void Cabinet(Builder b, Transform g, float w, float d, float h, int drawers)
        {
            b.Box(g, "Cabinet", new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), b.M("PlasticLight"), default, true);
            for (int i = 0; i < drawers; i++)
            {
                float y = (i + 0.5f) * (h / drawers);
                b.Box(g, "Drawer", new Vector3(0, y, -d * 0.5f - 0.004f), new Vector3(w - 0.06f, h / drawers - 0.04f, 0.008f), b.M("DeskWhite"));
                b.Box(g, "Handle", new Vector3(0, y + 0.02f, -d * 0.5f - 0.012f), new Vector3(0.14f, 0.02f, 0.012f), b.M("MetalDark"));
            }
        }

        public static void Cart(Builder b, Transform g, float w, float d, float h)
        {
            b.Box(g, "TopShelf", new Vector3(0, h - 0.015f, 0), new Vector3(w, 0.03f, d), b.M("Metal"), default, true);
            b.Box(g, "LowShelf", new Vector3(0, 0.2f, 0), new Vector3(w, 0.03f, d), b.M("Metal"));
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    b.Box(g, "Post", new Vector3(sx * (w * 0.5f - 0.02f), h * 0.5f, sz * (d * 0.5f - 0.02f)), new Vector3(0.03f, h - 0.03f, 0.03f), b.M("MetalDark"));
                    b.Cylinder(g, "Wheel", new Vector3(sx * (w * 0.5f - 0.03f), 0.035f, sz * (d * 0.5f - 0.03f)), 0.07f, 0.03f, b.M("Plastic"), new Vector3(0, 0, 90f));
                }
            ArchiveBox(b, g, "BOX", false, 0.36f, 0.22f, 0.28f, new Vector3(-0.15f, 0.215f, 0f));
        }

        public static void ArchiveBox(Builder b, Transform g, string label, bool collider, float w = 0.45f, float h = 0.3f, float d = 0.35f) =>
            ArchiveBox(b, g, label, collider, w, h, d, Vector3.zero);

        public static void ArchiveBox(Builder b, Transform g, string label, bool collider, float w, float h, float d, Vector3 at)
        {
            b.Box(g, "Box", at + new Vector3(0, h * 0.47f, 0), new Vector3(w, h * 0.94f, d), b.M("Cardboard"), default, collider);
            b.Box(g, "Lid", at + new Vector3(0, h * 0.97f, 0), new Vector3(w + 0.012f, h * 0.08f, d + 0.012f), b.M("Cardboard"));
            b.Box(g, "LabelCard", at + new Vector3(0, h * 0.5f, -d * 0.5f - 0.002f), new Vector3(w * 0.6f, h * 0.3f, 0.003f), b.M("Paper"));
            b.Label(g, label, at + new Vector3(0, h * 0.5f, -d * 0.5f - 0.005f), h * 0.13f, H("#1F2937"), w * 0.58f);
        }

        public static void WallShelfBinders(Builder b, Transform g, float w, int n)
        {
            b.Box(g, "Shelf", new Vector3(0, 0, 0), new Vector3(w, 0.025f, 0.22f), b.M("PlasticLight"));
            b.Box(g, "BracketL", new Vector3(-w * 0.4f, -0.06f, 0.08f), new Vector3(0.02f, 0.12f, 0.05f), b.M("MetalDark"));
            b.Box(g, "BracketR", new Vector3(w * 0.4f, -0.06f, 0.08f), new Vector3(0.02f, 0.12f, 0.05f), b.M("MetalDark"));
            string[] cols = { "FolderBlue", "FolderRed", "FolderBlue", "Plastic", "FolderGreen", "FolderBlue" };
            float x = -w * 0.5f + 0.06f;
            for (int i = 0; i < n; i++)
            {
                float t = 0.055f + (i % 3) * 0.01f;
                b.Box(g, "Binder" + i, new Vector3(x + t * 0.5f, 0.165f, 0.01f), new Vector3(t, 0.3f, 0.2f), b.M(cols[i % cols.Length]));
                b.Box(g, "Spine" + i, new Vector3(x + t * 0.5f, 0.2f, -0.092f), new Vector3(t * 0.7f, 0.06f, 0.003f), b.M("Paper"));
                x += t + 0.005f;
            }
        }

        // ---- wall pieces (front faces -Z of the group; the group origin is the bottom edge on the wall) ----
        public static void Corkboard(Builder b, Transform g, float w, float h, int seed, string title)
        {
            var rnd = new System.Random(seed);
            b.Box(g, "Frame", new Vector3(0, h * 0.5f, 0.012f), new Vector3(w + 0.05f, h + 0.05f, 0.025f), b.M("Frame"));
            b.Box(g, "Cork", new Vector3(0, h * 0.5f, -0.002f), new Vector3(w, h, 0.01f), b.M("Cork"));
            b.Box(g, "TitleCard", new Vector3(0, h - 0.07f, -0.009f), new Vector3(Mathf.Min(w * 0.6f, 0.7f), 0.075f, 0.003f), b.M("Paper"));
            b.Label(g, title, new Vector3(0, h - 0.07f, -0.012f), 0.04f, H("#1F2937"), Mathf.Min(w * 0.6f, 0.7f));

            // Pinned items in a loose grid: printed email screenshots, photos and sticky notes.
            int cols = Mathf.Max(2, Mathf.RoundToInt(w / 0.32f)), rows = 2;
            var pins = new List<Vector3>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float cx = -w * 0.5f + (c + 0.5f) * (w / cols) + (float)(rnd.NextDouble() - 0.5) * 0.04f;
                    float cy = (r == 0 ? h * 0.62f : h * 0.27f) - 0.04f + (float)(rnd.NextDouble() - 0.5) * 0.04f;
                    float tilt = (float)(rnd.NextDouble() - 0.5) * 8f;
                    int kind = (r * cols + c + seed) % 3;
                    var item = new GameObject("Pinned_" + (kind == 0 ? "EmailPrintout" : kind == 1 ? "Photo" : "Sticky")).transform;
                    item.SetParent(g, false); item.localPosition = new Vector3(cx, cy, -0.009f); item.localEulerAngles = new Vector3(0, 0, tilt);
                    if (kind == 0) EmailPrintout(b, item, 0.19f, 0.25f, rnd.Next(3) == 0);
                    else if (kind == 1)
                    {
                        b.Box(item, "PhotoBorder", Vector3.zero, new Vector3(0.15f, 0.12f, 0.003f), b.M("Paper"));
                        b.Box(item, "PhotoImage", new Vector3(0, 0.006f, -0.002f), new Vector3(0.13f, 0.09f, 0.002f), b.M("Photo" + (1 + rnd.Next(4))));
                    }
                    else
                    {
                        string[] st = { "StickyYellow", "StickyPink", "StickyBlue", "StickyGreen" };
                        b.Box(item, "Sticky", Vector3.zero, new Vector3(0.085f, 0.085f, 0.002f), b.M(st[rnd.Next(st.Length)]));
                        for (int i = 0; i < 3; i++) b.Box(item, "Scribble", new Vector3(0, 0.02f - i * 0.018f, -0.0015f), new Vector3(0.06f, 0.004f, 0.001f), b.M("InkDark"));
                    }
                    var pin = new Vector3(cx, cy + 0.07f, -0.018f);
                    b.Box(g, "Pin", pin, new Vector3(0.016f, 0.016f, 0.014f), b.M("Pin"));
                    pins.Add(pin);
                }
            // Red string between a few pins.
            for (int i = 0; i + 1 < pins.Count; i += 2)
            {
                var a = pins[i]; var c2 = pins[Mathf.Min(pins.Count - 1, i + cols + 1)];
                if (a == c2) continue;
                var mid = (a + c2) * 0.5f + new Vector3(0, 0, -0.002f);
                var dir = c2 - a;
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                b.Box(g, "RedString", mid, new Vector3(dir.magnitude, 0.004f, 0.002f), b.M("String"), new Vector3(0, 0, ang));
            }
        }

        /// <summary>A printed email screenshot: white sheet, blue header bar, text lines, optional red circle mark.</summary>
        public static void EmailPrintout(Builder b, Transform item, float w, float h, bool marked)
        {
            b.Box(item, "Sheet", Vector3.zero, new Vector3(w, h, 0.002f), b.M("Paper"));
            b.Box(item, "Header", new Vector3(0, h * 0.4f, -0.0015f), new Vector3(w * 0.92f, h * 0.1f, 0.001f), b.M("HeaderBlue"));
            for (int i = 0; i < 6; i++)
                b.Box(item, "Text", new Vector3(-w * 0.05f * (i % 2), h * 0.24f - i * h * 0.1f, -0.0015f), new Vector3(w * (0.78f - (i % 3) * 0.1f), h * 0.025f, 0.001f), b.M("Ink"));
            if (marked)
            {
                b.Box(item, "LinkMark", new Vector3(0, -h * 0.32f, -0.0018f), new Vector3(w * 0.7f, h * 0.06f, 0.001f), b.M("Highlight"));
                b.Box(item, "LinkText", new Vector3(0, -h * 0.32f, -0.0022f), new Vector3(w * 0.6f, h * 0.02f, 0.001f), b.M("Paper"));
            }
        }

        public static void Whiteboard(Builder b, Transform g, float w, float h, string title)
        {
            b.Box(g, "Frame", new Vector3(0, h * 0.5f, 0.01f), new Vector3(w + 0.04f, h + 0.04f, 0.02f), b.M("Metal"));
            b.Box(g, "Board", new Vector3(0, h * 0.5f, -0.002f), new Vector3(w, h, 0.008f), b.M("Whiteboard"));
            b.Box(g, "Tray", new Vector3(0, -0.01f, -0.03f), new Vector3(w * 0.6f, 0.02f, 0.05f), b.M("Metal"));
            b.Box(g, "MarkerBlue", new Vector3(-0.1f, 0.005f, -0.035f), new Vector3(0.12f, 0.018f, 0.018f), b.M("Marker"));
            b.Box(g, "MarkerRed", new Vector3(0.08f, 0.005f, -0.035f), new Vector3(0.12f, 0.018f, 0.018f), b.M("MarkerRed"));
            b.Label(g, title, new Vector3(-w * 0.18f, h - 0.08f, -0.008f), 0.06f, H("#2457A6"), w * 0.6f);
            // Handwritten lines: timeline with times, a crossed-out item and sticky notes.
            for (int i = 0; i < 5; i++)
            {
                float y = h - 0.2f - i * 0.12f;
                b.Box(g, "Time", new Vector3(-w * 0.4f, y, -0.0065f), new Vector3(0.08f, 0.012f, 0.001f), b.M(i == 2 ? "MarkerRed" : "Marker"));
                b.Box(g, "Note", new Vector3(-w * 0.12f, y, -0.0065f), new Vector3(w * (0.42f - (i % 3) * 0.07f), 0.01f, 0.001f), b.M("Marker"));
            }
            b.Box(g, "Arrow", new Vector3(w * 0.18f, h * 0.55f, -0.0065f), new Vector3(0.18f, 0.012f, 0.001f), b.M("MarkerRed"), new Vector3(0, 0, -20f));
            string[] st = { "StickyYellow", "StickyPink", "StickyGreen" };
            for (int i = 0; i < 3; i++)
                b.Box(g, "Sticky", new Vector3(w * 0.33f + (i % 2) * 0.1f, h * 0.68f - i * 0.18f, -0.0075f), new Vector3(0.085f, 0.085f, 0.002f), b.M(st[i]), new Vector3(0, 0, i * 6f - 6f));
        }

        public static void WallTV(Builder b, Transform g, float w, float h, Screen kind)
        {
            b.Box(g, "Mount", new Vector3(0, h * 0.5f, 0.02f), new Vector3(0.2f, 0.2f, 0.04f), b.M("MetalDark"));
            b.Box(g, "Bezel", new Vector3(0, h * 0.5f, -0.01f), new Vector3(w, h, 0.04f), b.M("Plastic"));
            ScreenFace(b, g, new Vector3(0, h * 0.5f, -0.031f), w - 0.04f, h - 0.04f, kind);
        }
    }
}
#endif
