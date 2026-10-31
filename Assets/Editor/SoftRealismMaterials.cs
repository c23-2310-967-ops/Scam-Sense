using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3D soft realism material look (Built-in Standard shader, no textures, no extra shader cost).
///
/// Kenney Furniture Kit models (Assets/Models/**.fbx): their materials live inside the model files, all with
/// smoothness 0 and very bright pastel colours, so furniture looked flat and chalky. This postprocessor adjusts
/// them every time Unity imports a model - wood gets a soft sheen and a slightly richer colour, metal becomes real
/// (metallic) metal, fabric stays matte, washed-out whites become soft off-whites and lamp shades glow gently.
/// Nothing is extracted or renamed, so scene references are untouched; delete this file and reimport the models
/// to go back. Other models (Phone, ink pen, Room 5 OBJ models) are not matched and stay as they are.
///
/// Tools > Scam Sense > Visual Style > Apply Soft Realism Materials
///   Also makes the project's metal materials metallic (the room builders reset colour and smoothness on a
///   rebuild, never metallic, so this survives rebuilds) and reimports the Kenney models.
/// </summary>
public class SoftRealismMaterials : AssetPostprocessor
{
    const uint Version = 2;
    public override uint GetVersion() => Version;   // bump to re-run the look on every model
    public override int GetPostprocessOrder() => 100;   // after Unity's own material setup

    struct Look
    {
        public float smooth, metal, tint, glow;
        public string hex;   // replaces the colour (for washed-out whites)
    }

    static Look L(float smooth, float metal = 0f, float tint = 1f, string hex = null, float glow = 0f) =>
        new Look { smooth = smooth, metal = metal, tint = tint, hex = hex, glow = glow };

    // Kenney Furniture Kit material names
    static readonly Dictionary<string, Look> Kenney = new Dictionary<string, Look>
    {
        { "wood",         L(0.28f, tint: 0.94f) },
        { "woodDark",     L(0.30f, tint: 0.93f) },
        { "metal",        L(0.60f, metal: 0.75f) },
        { "metalLight",   L(0.62f, metal: 0.70f, hex: "#E4E8E7") },
        { "metalMedium",  L(0.55f, metal: 0.75f) },
        { "metalDark",    L(0.50f, metal: 0.70f) },
        { "glass",        L(0.88f) },
        { "carpet",       L(0.06f, tint: 0.95f) },
        { "carpetBlue",   L(0.06f, tint: 0.95f) },
        { "carpetDarker", L(0.06f, tint: 0.95f) },
        { "carpetWhite",  L(0.06f, hex: "#ECEAE5") },
        { "fur",          L(0.08f) },
        { "plant",        L(0.32f, tint: 0.90f) },
        { "lamp",         L(0.20f, glow: 0.35f) },
        { "_defaultMat",  L(0.20f, hex: "#ECECEC") },
    };

    // Project materials that are real metal (frames that are wood or painted are left alone)
    static readonly Dictionary<string, float> ProjectMetals = new Dictionary<string, float>
    {
        { "Assets/Materials/R5_ChairChrome.mat", 0.85f },
        { "Assets/Materials/R5_Steel.mat", 0.65f },
        { "Assets/Materials/R2_Handle.mat", 0.80f },
        { "Assets/Materials/R5_SilverInlay.mat", 0.30f },   // thin wall lines: stay bright against the taupe panel
        { "Assets/Materials/R5_AioSilver.mat", 0.60f },
        { "Assets/Materials/R4_Metal.mat", 0.70f },
        { "Assets/Materials/R3_Metal.mat", 0.65f },
        { "Assets/Materials/SetDressing/SD_Metal.mat", 0.70f },
        { "Assets/Materials/R4_DoorSteel.mat", 0.60f },
        { "Assets/Materials/R3_DarkMetal.mat", 0.50f },   // dark metals stay softer so they don't turn black
        { "Assets/Materials/R4_DarkMetal.mat", 0.50f },
        { "Assets/Materials/R4_ChairFrame.mat", 0.50f },
        { "Assets/Materials/SetDressing/SD_MetalDark.mat", 0.50f },
    };

    // The Kenney models import their materials "via material description", which calls this hook (not OnPostprocessMaterial).
    void OnPreprocessMaterialDescription(UnityEditor.AssetImporters.MaterialDescription description, Material m, AnimationClip[] animations)
    {
        if (!assetPath.StartsWith("Assets/Models/") || !assetPath.EndsWith(".fbx")) return;
        if (!Kenney.TryGetValue(description.materialName, out var look)) return;

        Color c = look.hex != null && ColorUtility.TryParseHtmlString(look.hex, out var h) ? h : m.color * look.tint;
        c.a = 1f;
        m.color = c;
        m.SetFloat("_Glossiness", look.smooth);
        m.SetFloat("_Metallic", look.metal);
        if (look.glow > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * look.glow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
    }

    [MenuItem("Tools/Scam Sense/Visual Style/Apply Soft Realism Materials")]
    public static void ApplyAll()
    {
        int metals = 0;
        foreach (var kv in ProjectMetals)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(kv.Key);
            if (m == null) { Debug.LogWarning("SoftRealismMaterials: missing " + kv.Key); continue; }
            m.SetFloat("_Metallic", kv.Value);
            EditorUtility.SetDirty(m);
            metals++;
        }
        AssetDatabase.SaveAssets();

        int models = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx")) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                models++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Debug.Log($"SoftRealismMaterials: {metals} project metal materials updated, {models} models reimported.");
    }
}
