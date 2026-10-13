using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Shared audio/mouse settings used by both the main menu (SettingsMenu) and the in-game PauseMenu.
/// Values are 0..1, saved in PlayerPrefs, and applied to MainMixer's exposed parameters
/// MasterVol / MusicVol / SFXVol (in decibels).
/// </summary>
public static class GameAudioSettings
{
    public const string KeyMaster = "set_master";
    public const string KeyMusic = "set_music";
    public const string KeySfx = "set_sfx";
    public const string KeySensitivity = "set_sens";

    public const string ParamMaster = "MasterVol";
    public const string ParamMusic = "MusicVol";
    public const string ParamSfx = "SFXVol";

    public const float DefaultVolume = 0.8f;
    public const float DefaultSensitivity = 1f;

    /// <summary>Slider value (0..1) to decibels. Log mapping sounds natural to the ear.</summary>
    public static float ToDb(float v) => Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f;

    public static float GetVolume(string key) => Mathf.Clamp01(PlayerPrefs.GetFloat(key, DefaultVolume));

    public static void SetVolume(AudioMixer mixer, string param, string key, float value)
    {
        value = Mathf.Clamp01(value);
        if (mixer != null) mixer.SetFloat(param, ToDb(value));
        PlayerPrefs.SetFloat(key, value);
    }

    public static void ApplySavedVolumes(AudioMixer mixer)
    {
        if (mixer == null) return;
        mixer.SetFloat(ParamMaster, ToDb(GetVolume(KeyMaster)));
        mixer.SetFloat(ParamMusic, ToDb(GetVolume(KeyMusic)));
        mixer.SetFloat(ParamSfx, ToDb(GetVolume(KeySfx)));
    }

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(KeySensitivity, DefaultSensitivity);
        set => PlayerPrefs.SetFloat(KeySensitivity, Mathf.Clamp(value, 0.1f, 5f));
    }

    public static void Save() => PlayerPrefs.Save();
}
