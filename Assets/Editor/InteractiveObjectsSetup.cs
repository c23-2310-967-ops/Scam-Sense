#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes existing room objects readable with E (case folders, evidence documents, investigation boards) by adding
/// a configured InspectableObject and, if needed, a BoxCollider fitted to the object's renderers.
///   Tools > Scam Sense > Environment > Set Up Interactive Objects (All Rooms)
/// Safe to run again: existing InspectableObjects on these objects are updated, not duplicated.
/// EnvironmentDressing calls ApplyToScene after rebuilding a room, so its dressed props keep their interactions.
/// All text is fictional and consistent with each level's case notes; it never states a message's answer.
/// </summary>
public static class InteractiveObjectsSetup
{
    class Entry
    {
        public string path;
        public InspectableObject.Kind kind;
        public InspectableObject.Source source;
        public string title, prompt;
        public float distance = 2.5f;
        public bool needsReviewed;
        public CaseFile[] pages;
    }

    static readonly Dictionary<string, Entry[]> Scenes = new Dictionary<string, Entry[]>
    {
        ["Assets/Scenes/Game.unity"] = new[]
        {
            new Entry { path = "SetDressing/Desk_Folders", kind = InspectableObject.Kind.CaseFolder, source = InspectableObject.Source.LevelBriefing, title = "Case 01 - The First Case" },
            new Entry { path = "SetDressing/Wall_CaseCorkboard", kind = InspectableObject.Kind.InvestigationBoard, source = InspectableObject.Source.ReviewedEvidence,
                        title = "My Case Notes", needsReviewed = true, distance = 3f },
            new Entry { path = "SetDressing/Kitchen_Mail", kind = InspectableObject.Kind.EvidenceDocument, source = InspectableObject.Source.CustomPages, title = "Today's mail",
                        pages = new[] { new CaseFile { title = "Order receipt (printed)", body =
                            "Example Delivery - order receipt\n\n<b>Item</b>  Desk lamp\n<b>Ordered</b>  Today, in the official Example Delivery app\n<b>Status</b>  Confirmed, estimated delivery tomorrow\n\n" +
                            "Updates for this order appear in the Example Delivery app under My Orders.\n\n<color=#B9C6D8>Clue: a message about an order is easier to judge when you know what you actually ordered, and where you ordered it.</color>" } } },
        },
        ["Assets/Scenes/Room2_InvestigationOffice.unity"] = new[]
        {
            new Entry { path = "Environment/DeskFiles", kind = InspectableObject.Kind.CaseFolder, source = InspectableObject.Source.LevelBriefing, title = "Case 02 - The Investigation" },
            new Entry { path = "Environment/EvidenceBoard", kind = InspectableObject.Kind.InvestigationBoard, source = InspectableObject.Source.ReviewedEvidence,
                        title = "Investigation Board", needsReviewed = true, distance = 3.2f },
            new Entry { path = "VisualUpgrade/Props/PrintedEmail_1", kind = InspectableObject.Kind.EvidenceDocument, source = InspectableObject.Source.CustomPages, title = "Purchase record",
                        pages = new[] { new CaseFile { title = "Northstar Office Supplies - order record", body =
                            "Office supply order placed during your setup week.\n\n<b>Supplier</b>  Northstar Office Supplies\n<b>Billing contact</b>  billing@northstar-supplies.example\n" +
                            "<b>Status</b>  Delivered - final shipping cost to follow in your customer account\n\n<color=#B9C6D8>Clue: compare any billing message with this record - the supplier, the address and where it asks you to go.</color>" } } },
            new Entry { path = "VisualUpgrade/Props/PrintedEmail_2", kind = InspectableObject.Kind.EvidenceDocument, source = InspectableObject.Source.CustomPages, title = "Company announcement",
                        pages = new[] { new CaseFile { title = "IT announcement (printed this morning)", body =
                            "<b>Scheduled password change</b>\n\nAll staff: please change your password <b>by Friday</b>.\n" +
                            "- Change it yourself in the IT portal (use your bookmark).\n- IT will never ask for your password or a verification code - not by email, text or chat.\n" +
                            "- Questions: call the IT Service Desk on extension 4100.\n\n<color=#B9C6D8>Clue: messages about this change should match this announcement.</color>" } } },
        },
        ["Assets/Scenes/Room3_DigitalTrap.unity"] = new[]
        {
            new Entry { path = "Environment/Props/CaseFolder_1", kind = InspectableObject.Kind.CaseFolder, source = InspectableObject.Source.LevelBriefing, title = "Case 03 - The Digital Trap" },
            new Entry { path = "Environment/Props/PrintedNotes", kind = InspectableObject.Kind.EvidenceDocument, source = InspectableObject.Source.LevelCaseFiles, title = "Printed case files" },
            new Entry { path = "SetDressing/Wall_IncidentWhiteboard", kind = InspectableObject.Kind.InvestigationBoard, source = InspectableObject.Source.ReviewedEvidence,
                        title = "Incident Log", needsReviewed = true, distance = 3.2f },
        },
        ["Assets/Scenes/Room4_DigitalForensicsLab.unity"] = new[]
        {
            new Entry { path = "SetDressing/Bench_Clipboard", kind = InspectableObject.Kind.CaseFolder, source = InspectableObject.Source.LevelBriefing, title = "Case 04 - Digital Forensics Lab" },
            new Entry { path = "SetDressing/Cart_Items", kind = InspectableObject.Kind.EvidenceDocument, source = InspectableObject.Source.CustomPages, title = "Chain of custody",
                        pages = new[] { new CaseFile { title = "Chain of custody form", body =
                            "Every piece of evidence is logged before it is examined, so nothing can be changed without a record.\n\n" +
                            "<b>EV-0412</b>  Reported email (printed and exported)  -  logged 08:41\n<b>EV-0413</b>  Domain registration record  -  logged 09:05\n" +
                            "<b>EV-0415</b>  Follow-up text message (screenshot)  -  logged 10:05\n\nSealed by: Case Review team    Seal intact: yes\n\n" +
                            "<color=#B9C6D8>Clue: compare these items on the evidence wall before you submit the final case.</color>" } } },
            new Entry { path = "SetDressing/Wall_CapturesBoard", kind = InspectableObject.Kind.InvestigationBoard, source = InspectableObject.Source.ReviewedEvidence,
                        title = "Website Captures", needsReviewed = true, distance = 3.2f },
        },
    };

    [MenuItem("Tools/Scam Sense/Environment/Set Up Interactive Objects (All Rooms)")]
    public static void SetUpAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        var report = new StringBuilder();
        foreach (var path in Scenes.Keys)
        {
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            report.Append(ApplyToScene(s));
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        LastReport = report.ToString();
        Debug.Log("InteractiveObjectsSetup:\n" + LastReport);
    }

    public static string LastReport;

    /// <summary>Adds / updates the InspectableObjects of one open scene (does not save it).</summary>
    public static string ApplyToScene(Scene scene)
    {
        var sb = new StringBuilder();
        if (!Scenes.TryGetValue(scene.path, out var entries)) return "";
        sb.AppendLine("== " + scene.path);
        foreach (var e in entries)
        {
            var t = Find(scene, e.path);
            if (t == null) { sb.AppendLine($"   MISSING {e.path} (skipped)"); continue; }
            var io = t.GetComponent<InspectableObject>();
            if (io == null) io = t.gameObject.AddComponent<InspectableObject>();
            io.kind = e.kind;
            io.source = e.source;
            io.title = e.title;
            io.promptLabel = e.prompt ?? "";
            io.interactDistance = e.distance;
            io.requirement = e.needsReviewed ? InspectableObject.Requirement.MessagesReviewed : InspectableObject.Requirement.None;
            io.requiredCount = 1;
            io.lockedHint = "Review a message first";
            io.lockedMessage = "Nothing pinned yet. Read and answer your first message, then come back to review the evidence.";
            io.pages = e.pages != null ? new List<CaseFile>(e.pages) : new List<CaseFile>();
            EditorUtility.SetDirty(io);
            string col = EnsureCollider(t);
            sb.AppendLine($"   {e.path}: {e.kind} ({e.source}){col}");
        }
        return sb.ToString();
    }

    static Transform Find(Scene scene, string path)
    {
        int slash = path.IndexOf('/');
        string rootName = slash < 0 ? path : path.Substring(0, slash);
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name != rootName) continue;
            var t = slash < 0 ? r.transform : r.transform.Find(path.Substring(slash + 1));
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>A non-trigger BoxCollider fitted to the renderers, if the object has no collider yet.</summary>
    static string EnsureCollider(Transform t)
    {
        foreach (var c in t.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) return "";
        var rs = t.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return "  (no renderer - no collider added)";
        // Bounds in the object's local space (works for rotated objects).
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue); var max = -min;
        foreach (var r in rs)
        {
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var l = t.InverseTransformPoint(corner);
                min = Vector3.Min(min, l); max = Vector3.Max(max, l);
            }
        }
        var box = t.gameObject.AddComponent<BoxCollider>();
        box.center = (min + max) * 0.5f;
        var size = max - min;
        // Thin papers get a little thickness so they are easy to look at.
        box.size = new Vector3(Mathf.Max(size.x, 0.03f), Mathf.Max(size.y, 0.03f), Mathf.Max(size.z, 0.03f));
        return "  + collider";
    }
}
#endif
