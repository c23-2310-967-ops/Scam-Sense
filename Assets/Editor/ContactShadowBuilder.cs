using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor tool: adds soft "contact shadows" (a darkened, blurred footprint) under furniture so props
/// look grounded instead of floating. It's the low-cost stand-in for ambient occlusion in the Built-in
/// pipeline without a post-processing package.
///
/// Menu: Tools > Scam Sense > Rebuild Contact Shadows (open scene). Run it again after moving furniture;
/// it replaces the previous set. Everything it creates sits under one scene root, "ContactShadows",
/// which you can delete to undo.
/// </summary>
public static class ContactShadowBuilder
{
    const string RootName = "ContactShadows";
    const string TexturePath = "Assets/Textures/FX_ContactShadow.png";
    const string MaterialPath = "Assets/Materials/FX_ContactShadow.mat";

    // Scene roots that hold furniture (Level 1 uses the first two, Level 2 the third).
    static readonly string[] PropRoots = { "Furnitures", "ComputerRoom", "Environment" };

    // Names that are structure or flat decoration, not props standing on a surface.
    static readonly string[] Skip =
    {
        "wall", "window", "ceiling", "rug", "panel", "door", "light", "trim",
        "board", "screen", "paper", "note", "pin", "string", "frame", "focus", "seat", "exit", "stairs",
        "shadow", "glow", "spot", "fan", "poster", "sign", "clock", "cable", "monitor", "keyboard", "mouse"
    };

    [MenuItem("Tools/Scam Sense/Rebuild Contact Shadows")]
    public static void RebuildMenu()
    {
        int n = Rebuild();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"ContactShadowBuilder: {n} contact shadows created in '{EditorSceneManager.GetActiveScene().name}'. Save the scene to keep them.");
    }

    public static int Rebuild()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        var mat = GetMaterial();
        var root = new GameObject(RootName).transform;
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        int count = 0;

        foreach (var candidate in Candidates())
        {
            if (!TryGetFootprint(candidate, out Bounds b, out float supportY)) continue;

            var go = new GameObject("CS_" + candidate.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(b.center.x, supportY + 0.008f, b.center.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float pad = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.25f, 0.06f, 0.3f);
            go.transform.localScale = new Vector3(b.size.x + pad * 2f, b.size.z + pad * 2f, 1f);
            go.GetComponent<MeshFilter>().sharedMesh = quad;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            count++;
        }
        return count;
    }

    static IEnumerable<GameObject> Candidates()
    {
        foreach (var rootName in PropRoots)
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (Transform child in root.transform)
            {
                // A group (no renderer of its own) is unpacked one level, e.g. Workstation -> Monitor.
                if (child.GetComponent<Renderer>() == null && child.GetComponentInChildren<Renderer>() != null && child.childCount > 1 && IsGroup(child))
                {
                    foreach (Transform g in child) yield return g.gameObject;
                }
                else yield return child.gameObject;
            }
        }
    }

    // A group holds several separate props side by side (not one model split into parts).
    static bool IsGroup(Transform t)
    {
        int withRenderers = 0;
        foreach (Transform c in t) if (c.GetComponentInChildren<Renderer>() != null) withRenderers++;
        return withRenderers >= 3 && t.GetComponentInChildren<LODGroup>() == null && PrefabUtility.GetPrefabAssetType(t.gameObject) == PrefabAssetType.NotAPrefab;
    }

    static bool TryGetFootprint(GameObject go, out Bounds b, out float supportY)
    {
        b = default; supportY = 0f;
        if (!go.activeInHierarchy) return false;
        string lower = go.name.ToLowerInvariant();
        foreach (var s in Skip) if (lower.Contains(s)) return false;

        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        float area = b.size.x * b.size.z;
        if (b.size.y < 0.12f || b.size.y > 3.2f || area < 0.015f || area > 7f) return false;

        // What is it standing on? Cast down from just above its base, ignoring its own colliders.
        var own = new HashSet<Collider>(go.GetComponentsInChildren<Collider>());
        var hits = Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.05f, b.center.z), Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (own.Contains(h.collider)) continue;
            if (h.distance < best) { best = h.distance; supportY = h.point.y; }
        }
        if (best == float.MaxValue) return false;          // nothing underneath: hanging or wall-mounted
        if (b.min.y - supportY > 0.2f) return false;        // floating well above its support (e.g. wall cabinets)
        return true;
    }

    static Material GetMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Rounded-rectangle falloff: darkest under the object, fading to white (no change) at the edge.
                    float u = Mathf.Abs(x / (size - 1f) * 2f - 1f), v = Mathf.Abs(y / (size - 1f) * 2f - 1f);
                    float d = Mathf.Pow(Mathf.Pow(u, 4f) + Mathf.Pow(v, 4f), 0.25f);
                    float shade = Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0.3f, 1f, d));
                    tex.SetPixel(x, y, new Color(shade, shade, shade));
                }
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.sRGBTexture = true;
            imp.SaveAndReimport();
        }

        // Multiply blending: white = untouched floor, grey = darker. Never brightens, no lighting cost.
        mat = new Material(Shader.Find("Legacy Shaders/Particles/Multiply"));
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }
}
