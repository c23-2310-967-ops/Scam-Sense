using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Drives the Settings panel: volume, sensitivity, fullscreen, quality, resolution.
/// Attach to SettingsPanel and assign references in the Inspector.
/// Unassigned references are skipped, so you can add controls one at a time.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioMixer mixer;          // exposed params: MasterVol, MusicVol, SFXVol
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text sfxValue;

    [Header("Gameplay")]
    [SerializeField] private Slider sensitivitySlider;  // e.g. range 0.1 - 5
    [SerializeField] private TMP_Text sensitivityValue;

    [Header("Display")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("Navigation")]
    [SerializeField] private GameObject mainMenuRoot;   // optional: what to show on Back
    [SerializeField] private GameObject settingsRoot;   // usually SettingsPanel itself

    // PlayerPrefs keys
    const string K_Master = "set_master";
    const string K_Music = "set_music";
    const string K_Sfx = "set_sfx";
    const string K_Sens = "set_sens";
    const string K_Full = "set_full";
    const string K_Quality = "set_quality";
    const string K_Res = "set_res";

    private Resolution[] resolutions;

    void Start()
    {
        SetupResolutions();
        SetupQuality();
        LoadAndApply();
        HookListeners();
    }

    // ---------- Setup ----------

    void SetupResolutions()
    {
        if (resolutionDropdown == null) return;

        resolutions = Screen.resolutions;
        var options = new List<string>();
        int current = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            var r = resolutions[i];
            options.Add($"{r.width} x {r.height}");
            if (r.width == Screen.currentResolution.width &&
                r.height == Screen.currentResolution.height)
                current = i;
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt(K_Res, current));
    }

    void SetupQuality()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        qualityDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt(K_Quality, QualitySettings.GetQualityLevel()));
    }

    void LoadAndApply()
    {
        InitSlider(masterSlider, K_Master, 0.8f);
        InitSlider(musicSlider, K_Music, 0.8f);
        InitSlider(sfxSlider, K_Sfx, 0.8f);
        InitSlider(sensitivitySlider, K_Sens, 1f);

        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(K_Full, Screen.fullScreen ? 1 : 0) == 1);

        // Apply everything once so saved values take effect at startup
        if (masterSlider) SetMaster(masterSlider.value);
        if (musicSlider) SetMusic(musicSlider.value);
        if (sfxSlider) SetSfx(sfxSlider.value);
        if (sensitivitySlider) SetSensitivity(sensitivitySlider.value);
        if (fullscreenToggle) SetFullscreen(fullscreenToggle.isOn);
        if (qualityDropdown) SetQuality(qualityDropdown.value);
        if (resolutionDropdown) SetResolution(resolutionDropdown.value);
    }

    void InitSlider(Slider s, string key, float fallback)
    {
        if (s == null) return;
        s.SetValueWithoutNotify(PlayerPrefs.GetFloat(key, fallback));
    }

    void HookListeners()
    {
        if (masterSlider) masterSlider.onValueChanged.AddListener(SetMaster);
        if (musicSlider) musicSlider.onValueChanged.AddListener(SetMusic);
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(SetSfx);
        if (sensitivitySlider) sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (qualityDropdown) qualityDropdown.onValueChanged.AddListener(SetQuality);
        if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    // ---------- Setters ----------

    // Slider (0.0001 - 1) -> decibels. Log mapping sounds natural to the ear.
    static float ToDb(float v) => Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f;

    public void SetMaster(float v) => ApplyVolume("MasterVol", v, K_Master, masterValue);
    public void SetMusic(float v) => ApplyVolume("MusicVol", v, K_Music, musicValue);
    public void SetSfx(float v) => ApplyVolume("SFXVol", v, K_Sfx, sfxValue);

    void ApplyVolume(string param, float v, string key, TMP_Text label)
    {
        if (mixer != null) mixer.SetFloat(param, ToDb(v));
        if (label != null) label.text = Mathf.RoundToInt(v * 100f) + "%";
        PlayerPrefs.SetFloat(key, v);
    }

    public void SetSensitivity(float v)
    {
        if (sensitivityValue != null) sensitivityValue.text = v.ToString("0.0") + "x";
        PlayerPrefs.SetFloat(K_Sens, v);
        // Read it in your player script: PlayerPrefs.GetFloat("set_sens", 1f)
    }

    public void SetFullscreen(bool on)
    {
        Screen.fullScreen = on;
        PlayerPrefs.SetInt(K_Full, on ? 1 : 0);
    }

    public void SetQuality(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt(K_Quality, index);
    }

    public void SetResolution(int index)
    {
        if (resolutions == null || index < 0 || index >= resolutions.Length) return;
        var r = resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen);
        PlayerPrefs.SetInt(K_Res, index);
    }

    // ---------- Buttons ----------

    public void Back()
    {
        PlayerPrefs.Save();
        if (settingsRoot != null) settingsRoot.SetActive(false);
        if (mainMenuRoot != null) mainMenuRoot.SetActive(true);
    }

    public void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(K_Master);
        PlayerPrefs.DeleteKey(K_Music);
        PlayerPrefs.DeleteKey(K_Sfx);
        PlayerPrefs.DeleteKey(K_Sens);
        PlayerPrefs.DeleteKey(K_Full);
        PlayerPrefs.DeleteKey(K_Quality);
        PlayerPrefs.DeleteKey(K_Res);
        LoadAndApply();
    }

    void OnDisable() => PlayerPrefs.Save();
}