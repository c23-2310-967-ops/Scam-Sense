using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Scam Sense > Performance > Prefill UI Fonts
///
/// The UI fonts (Inter Regular / SemiBold) are Dynamic TextMeshPro fonts that shipped with no glyphs, so in a
/// build every new letter was rasterised and the 1024x1024 atlas re-uploaded the first time it appeared on screen
/// (briefing, notifications, messages, results) - a frame hitch plus about 1 MB of garbage each time.
/// This adds every character the game uses to the font atlases ahead of time and keeps them in the build
/// ("Clear Dynamic Data On Build" off). The fonts stay Dynamic, so any unexpected character still works.
/// Characters a font doesn't contain (e.g. ★ ✓) are added to its fallback font instead.
/// Run it again after adding text with new symbols.
/// </summary>
public static class FontPrefill
{
    const string Fonts = "Assets/Fonts/Resources/";

    // ASCII, the symbols found in the game's scripts/levels/scenes, TMP's ellipsis and common typographic punctuation.
    static string Characters()
    {
        var sb = new StringBuilder();
        for (char c = ' '; c <= '~'; c++) sb.Append(c);
        sb.Append("·×—–•›←→■●★✓…‘’“”©®°±");
        return sb.ToString();
    }

    [MenuItem("Tools/Scam Sense/Performance/Prefill UI Fonts")]
    public static void PrefillMenu() => Debug.Log(Prefill());

    public static string Prefill()
    {
        var report = new StringBuilder();
        string chars = Characters();
        foreach (var name in new[] { "Inter-Regular SDF", "Inter-SemiBold SDF" })
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + name + ".asset");
            if (font == null) { report.AppendLine(name + ": not found"); continue; }
            report.AppendLine(Fill(font, chars, out string missing));

            // Glyphs this font lacks go into its fallback fonts (only those that have a source font file).
            if (!string.IsNullOrEmpty(missing) && font.fallbackFontAssetTable != null)
                foreach (var fb in font.fallbackFontAssetTable)
                    if (fb != null && fb.atlasPopulationMode == AtlasPopulationMode.Dynamic && !string.IsNullOrEmpty(missing))
                        report.AppendLine("   fallback " + Fill(fb, missing, out missing));
            if (!string.IsNullOrEmpty(missing)) report.AppendLine("   still missing: " + missing);
        }
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    static string Fill(TMP_FontAsset font, string chars, out string missing)
    {
        int before = font.characterTable.Count;
        font.TryAddCharacters(chars, out missing);
        // Keep the prefilled glyphs in the build (Inspector: Generation Settings > Clear Dynamic Data On Build).
        var so = new SerializedObject(font);
        var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
        if (clear != null) { clear.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
        EditorUtility.SetDirty(font);
        foreach (var tex in font.atlasTextures) if (tex != null) EditorUtility.SetDirty(tex);
        if (font.material != null) EditorUtility.SetDirty(font.material);
        return $"{font.name}: {before} -> {font.characterTable.Count} characters";
    }
}
