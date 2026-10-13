using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saved display settings (resolution, fullscreen, graphics quality), shared by the main menu.
/// Uses the same PlayerPrefs keys as SettingsMenu (set_res / set_full / set_quality).
/// Resolution is stored as an index into Screen.resolutions, like SettingsMenu does.
/// Note: in the Unity Editor, resolution and fullscreen changes do not resize the Game view;
/// they apply in a built game.
/// </summary>
public static class GameDisplaySettings
{
    public const string KeyResolution = "set_res";
    public const string KeyFullscreen = "set_full";
    public const string KeyQuality = "set_quality";

    /// <summary>Distinct resolutions (by size), smallest first.</summary>
    public static List<Resolution> Resolutions()
    {
        var list = new List<Resolution>();
        foreach (var r in Screen.resolutions)
        {
            bool dup = false;
            foreach (var e in list) if (e.width == r.width && e.height == r.height) { dup = true; break; }
            if (!dup) list.Add(r);
        }
        if (list.Count == 0) list.Add(new Resolution { width = Screen.width, height = Screen.height });
        return list;
    }

    public static int CurrentResolutionIndex(List<Resolution> list)
    {
        int saved = PlayerPrefs.GetInt(KeyResolution, -1);
        if (saved >= 0 && saved < Screen.resolutions.Length)
        {
            var r = Screen.resolutions[saved];
            int i = list.FindIndex(x => x.width == r.width && x.height == r.height);
            if (i >= 0) return i;
        }
        int cur = list.FindIndex(x => x.width == Screen.width && x.height == Screen.height);
        return cur >= 0 ? cur : list.Count - 1;
    }

    public static bool Fullscreen => PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

    public static int Quality => Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);

    /// <summary>Applies and saves the chosen display settings.</summary>
    public static void Apply(Resolution res, bool fullscreen, int quality)
    {
        QualitySettings.SetQualityLevel(Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1));
        Screen.SetResolution(res.width, res.height, fullscreen);

        int index = System.Array.FindIndex(Screen.resolutions, x => x.width == res.width && x.height == res.height);
        if (index >= 0) PlayerPrefs.SetInt(KeyResolution, index);
        PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyQuality, quality);
        PlayerPrefs.Save();
    }

    /// <summary>Re-applies saved values at startup (only keys that were actually saved).</summary>
    public static void ApplySaved()
    {
        if (PlayerPrefs.HasKey(KeyQuality)) QualitySettings.SetQualityLevel(Quality);
        if (PlayerPrefs.HasKey(KeyResolution) || PlayerPrefs.HasKey(KeyFullscreen))
        {
            var list = Resolutions();
            var r = list[CurrentResolutionIndex(list)];
            Screen.SetResolution(r.width, r.height, Fullscreen);
        }
    }

    public static void ClearSaved()
    {
        PlayerPrefs.DeleteKey(KeyResolution);
        PlayerPrefs.DeleteKey(KeyFullscreen);
        PlayerPrefs.DeleteKey(KeyQuality);
    }
}
