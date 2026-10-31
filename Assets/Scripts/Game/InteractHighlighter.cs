using System.Collections.Generic;
using UnityEngine;

/// <summary>Optional: an interactable whose visible model is not the object holding the script (e.g. a terminal's monitor).</summary>
public interface IHighlightable
{
    Transform HighlightRoot { get; }
}

/// <summary>
/// Makes the object you can interact with stand out: while an actionable "[E] ..." prompt is shown and the player is
/// looking at the object it belongs to, that object gets a soft cyan rim glow that gently breathes, fading in and out.
///   - Terminals and chairs report themselves to InteractionPrompt; doors and readable documents are found from the
///     shared look ray (PlayerInteractor reports for them). The pocket phone and locked/idle labels get no glow.
///   - The glow is a thin outline plus a soft rim: an unlit, additive redraw of that one object (shader:
///     Resources/Shaders/InteractHighlight), with no extra lights, no post-processing and no per-frame allocations.
///     World-space UI (the computer's email screen) still draws on top, so text stays readable.
/// Creates itself when a scene loads; nothing to set up.
/// </summary>
[DefaultExecutionOrder(1000)]   // after InteractionPrompt has picked this frame's prompt
public class InteractHighlighter : MonoBehaviour
{
    const float FadeSeconds = 0.18f, PulseSeconds = 1.8f, MaxStrength = 0.55f;
    const float MaxExtent = 4f;     // ignore huge renderers (walls, floors) if a script sits on a large parent
    const int MaxRenderers = 96;    // the case corkboard is built from ~70 small pieces
    static readonly Color GlowColor = new Color(0.55f, 0.85f, 1f);

    class Target
    {
        public Transform root;
        public readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        public readonly List<Mesh> meshes = new List<Mesh>();
        public readonly List<int> firstSubmesh = new List<int>(), submeshCount = new List<int>();
        public float strength;
    }

    static InteractHighlighter instance;
    Material material;
    MaterialPropertyBlock block;
    int strengthId;
    Target current, fading;
    readonly List<MeshRenderer> scratch = new List<MeshRenderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("InteractHighlighter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<InteractHighlighter>();
    }

    void Awake()
    {
        var shader = Resources.Load<Shader>("Shaders/InteractHighlight");
        if (shader == null || !shader.isSupported) { enabled = false; return; }
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        material.SetColor("_Color", GlowColor);
        block = new MaterialPropertyBlock();
        strengthId = Shader.PropertyToID("_Strength");
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (instance == this) instance = null;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        Transform want = FindTarget(cam);

        if (current != null && current.root != want) { fading = current; current = null; }
        if (want != null && current == null) current = Build(want);

        float step = Time.unscaledDeltaTime / FadeSeconds;
        if (current != null) current.strength = Mathf.MoveTowards(current.strength, 1f, step);
        if (fading != null)
        {
            fading.strength = Mathf.MoveTowards(fading.strength, 0f, step);
            if (fading.strength <= 0f) fading = null;
        }
        if (cam == null) return;

        float pulse = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / PulseSeconds));
        Draw(current, pulse, cam);
        Draw(fading, pulse, cam);
    }

    /// <summary>The object behind the prompt shown this frame, if the player is actually looking at it.</summary>
    static Transform FindTarget(Camera cam)
    {
        object owner = InteractionPrompt.ActiveOwner;
        if (owner == null || !InteractionPrompt.ActiveIsActionable || cam == null) return null;
        if (!LookRaycast.TryGetFirstHit(cam, cam.transform, LookRaycast.MaxDistance, out var hit)) return null;

        Transform h = hit.collider.transform;
        if (owner is Component c && c != null && (h == c.transform || h.IsChildOf(c.transform)))                       // terminals, chairs
            return owner is IHighlightable hl && hl.HighlightRoot != null ? hl.HighlightRoot : c.transform;
        var door = hit.collider.GetComponentInParent<Door>();
        if (door != null) return door.transform;
        var item = hit.collider.GetComponentInParent<InspectableObject>();
        if (item != null) return item.transform;
        return null;
    }

    Target Build(Transform root)
    {
        var t = new Target { root = root };
        root.GetComponentsInChildren(false, scratch);
        foreach (var r in scratch)
        {
            if (t.renderers.Count >= MaxRenderers) break;
            if (!r.enabled) continue;
            var ext = r.bounds.extents;
            if (Mathf.Max(ext.x, ext.y, ext.z) > MaxExtent) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;      // world-space UI (screens, labels)
            if (r.GetComponent<TMPro.TMP_Text>() != null) continue;      // 3D text (corkboard label)
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            t.renderers.Add(r);
            t.meshes.Add(mf.sharedMesh);
            // A static-batched renderer shares one combined, world-space mesh; draw only its own submeshes.
            t.firstSubmesh.Add(r.isPartOfStaticBatch ? r.subMeshStartIndex : 0);
            t.submeshCount.Add(r.isPartOfStaticBatch ? r.sharedMaterials.Length : mf.sharedMesh.subMeshCount);
        }
        return t;
    }

    void Draw(Target t, float pulse, Camera cam)
    {
        if (t == null || t.strength <= 0f) return;
        block.SetFloat(strengthId, t.strength * MaxStrength * pulse);
        for (int i = 0; i < t.renderers.Count; i++)
        {
            var r = t.renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            var mesh = t.meshes[i];
            var matrix = r.isPartOfStaticBatch ? Matrix4x4.identity : r.localToWorldMatrix;
            int first = t.firstSubmesh[i], end = Mathf.Min(first + t.submeshCount[i], mesh.subMeshCount);
            for (int s = first; s < end; s++)
                Graphics.DrawMesh(mesh, matrix, material, r.gameObject.layer, cam, s, block, false, false, false);
        }
    }
}
