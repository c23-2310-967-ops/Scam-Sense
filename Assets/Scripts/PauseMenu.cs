using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In-game menu (separate from the main menu scene). Opens with Esc / P or the MENU button (top-right).
///   Resume - Settings (Master / Music / SFX volume, mouse sensitivity) - Return to Main Menu - Exit Game.
/// Pausing sets timeScale to 0 (so message timers stop) and holds the InteractionGate, so the cursor and
/// player controls are restored correctly whatever else is open (computer, message, results).
/// Settings use the same saved values and mixer parameters as the main menu's Settings panel.
/// Builds its own UI at runtime - put this on any object in the game scene.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Keys")]
    [SerializeField] private Key pauseKey = Key.Escape;
    [Tooltip("Backup key, because the Unity Editor sometimes grabs Esc.")]
    [SerializeField] private Key altPauseKey = Key.P;

    [Header("Audio")]
    [Tooltip("MainMixer (exposed params MasterVol / MusicVol / SFXVol).")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private LevelAudio levelAudio;

    [Header("Navigation")]
    [Tooltip("Must match a scene in the Build Profile scene list.")]
    [SerializeField] private string mainMenuScene = "Menu";
    [SerializeField] private bool showMenuButton = true;
    [SerializeField] private int sortingOrder = 100;

    public static PauseMenu Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    private RectTransform pauseRoot, mainPanel, settingsPanel, confirmPanel;
    private GameObject menuButtonRoot;
    private CanvasGroup pauseGroup;
    private TMP_Text confirmTitle, confirmBody, statusText;
    private Button confirmYes;
    private System.Action confirmAction;
    private Slider masterSlider, musicSlider, sfxSlider, sensSlider;
    private float timeScaleBeforePause = 1f;
    private bool leaving;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        IsPaused = false;
        Build();
    }

    void Start()
    {
        GameAudioSettings.ApplySavedVolumes(mixer);   // works even if the game scene is started directly
        if (levelAudio == null) levelAudio = FindFirstObjectByType<LevelAudio>();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        // Never leave the game frozen if the scene changes while paused.
        if (IsPaused) Time.timeScale = 1f;
        IsPaused = false;
        Instance = null;
        GameAudioSettings.Save();
    }

    void Update()
    {
        if (leaving || Keyboard.current == null) return;
        menuButtonRoot.SetActive(showMenuButton && !IsPaused);

        bool pressed = Keyboard.current[pauseKey].wasPressedThisFrame ||
                       (altPauseKey != Key.None && Keyboard.current[altPauseKey].wasPressedThisFrame);
        if (!pressed) return;

        if (IsPaused) Back();
        else if (!InteractionGate.TryHandleEscape()) Pause();   // e.g. Esc first closes the computer
    }

    // ---------- Public actions ----------

    public void Pause()
    {
        if (IsPaused || leaving) return;
        IsPaused = true;
        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        InteractionGate.Acquire(this);
        ShowPanel(mainPanel);
        pauseRoot.gameObject.SetActive(true);
        StartCoroutine(Fade(pauseGroup, 0f, 1f, 0.15f));
    }

    public void Resume()
    {
        if (!IsPaused) return;
        Click();
        IsPaused = false;
        pauseRoot.gameObject.SetActive(false);
        Time.timeScale = timeScaleBeforePause;
        GameAudioSettings.Save();
        InteractionGate.Release(this);   // restores cursor/controls only if nothing else is open
    }

    /// <summary>Esc inside the menu: confirm -> menu, settings -> menu, menu -> resume.</summary>
    void Back()
    {
        if (confirmPanel.gameObject.activeSelf || settingsPanel.gameObject.activeSelf) { Click(); ShowPanel(mainPanel); }
        else Resume();
    }

    void OpenSettings()
    {
        Click();
        masterSlider.SetValueWithoutNotify(GameAudioSettings.GetVolume(GameAudioSettings.KeyMaster));
        musicSlider.SetValueWithoutNotify(GameAudioSettings.GetVolume(GameAudioSettings.KeyMusic));
        sfxSlider.SetValueWithoutNotify(GameAudioSettings.GetVolume(GameAudioSettings.KeySfx));
        sensSlider.SetValueWithoutNotify(GameAudioSettings.MouseSensitivity);
        RefreshSliderLabels();
        ShowPanel(settingsPanel);
    }

    void AskMainMenu()
    {
        Click();
        Confirm("Return to main menu?", "Your progress in the current case will be lost. Saved scores and unlocked rooms are kept.",
            "MAIN MENU", ReturnToMainMenu);
    }

    void AskExit()
    {
        Click();
        Confirm("Exit Scam Sense?", "The game will close. Saved scores and unlocked rooms are kept.", "EXIT GAME", ExitGame);
    }

    void ReturnToMainMenu()
    {
        if (string.IsNullOrEmpty(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            statusText.text = $"Main menu scene '{mainMenuScene}' is not in the Build Profile scene list. " +
                              "Set PauseMenu > Main Menu Scene in the Inspector.";
            Debug.LogError("PauseMenu: " + statusText.text, this);
            ShowPanel(mainPanel);
            return;
        }

        leaving = true;
        Time.timeScale = 1f;
        IsPaused = false;
        GameAudioSettings.Save();
        InteractionGate.ResetAll();
        MouseLook.SetCursorLocked(false);   // the main menu needs a visible cursor
        if (SceneLoader.Instance != null) SceneLoader.LoadScene(mainMenuScene);
        else SceneManager.LoadScene(mainMenuScene);
    }

    void ExitGame()
    {
        GameAudioSettings.Save();
#if UNITY_EDITOR
        // Application.Quit() is ignored in the Editor; don't force-stop Play Mode from gameplay code.
        statusText.text = "Exit Game closes the built game. In the Unity Editor, press the Play button to stop.";
        ShowPanel(mainPanel);
#else
        Application.Quit();
#endif
    }

    void Confirm(string title, string body, string yesLabel, System.Action action)
    {
        confirmTitle.text = title;
        confirmBody.text = body;
        UIKit.SetLabel(confirmYes, yesLabel);
        confirmAction = action;
        ShowPanel(confirmPanel);
    }

    void ShowPanel(RectTransform panel)
    {
        mainPanel.gameObject.SetActive(panel == mainPanel);
        settingsPanel.gameObject.SetActive(panel == settingsPanel);
        confirmPanel.gameObject.SetActive(panel == confirmPanel);
        if (panel != mainPanel) statusText.text = "";
    }

    void Click()
    {
        if (levelAudio != null) levelAudio.PlayClick();
    }

    // ---------- Settings ----------

    void OnMaster(float v) { GameAudioSettings.SetVolume(mixer, GameAudioSettings.ParamMaster, GameAudioSettings.KeyMaster, v); RefreshSliderLabels(); }
    void OnMusic(float v)  { GameAudioSettings.SetVolume(mixer, GameAudioSettings.ParamMusic, GameAudioSettings.KeyMusic, v); RefreshSliderLabels(); }
    void OnSfx(float v)    { GameAudioSettings.SetVolume(mixer, GameAudioSettings.ParamSfx, GameAudioSettings.KeySfx, v); RefreshSliderLabels(); }
    void OnSens(float v)   { GameAudioSettings.MouseSensitivity = v; RefreshSliderLabels(); }

    void RefreshSliderLabels()
    {
        SetValueLabel(masterSlider, Mathf.RoundToInt(masterSlider.value * 100f) + "%");
        SetValueLabel(musicSlider, Mathf.RoundToInt(musicSlider.value * 100f) + "%");
        SetValueLabel(sfxSlider, Mathf.RoundToInt(sfxSlider.value * 100f) + "%");
        SetValueLabel(sensSlider, sensSlider.value.ToString("0.0") + "x");
    }

    static void SetValueLabel(Slider s, string text) => UIKit.SetSliderValueLabel(s, text);

    IEnumerator Fade(CanvasGroup g, float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        g.alpha = to;
    }

    // ---------- UI building ----------

    void Build()
    {
        // MENU button (top-right of the HUD). Clickable whenever the cursor is free.
        var hud = UIKit.CreateCanvas(transform, "MenuButton Canvas", 12);
        menuButtonRoot = hud.gameObject;
        var mb = UIKit.Button(hud, "MENU   Esc", Pause, UIKit.Secondary, 24);
        UIKit.Place((RectTransform)mb.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -28), new Vector2(210, 60));

        // Pause overlay
        pauseRoot = UIKit.CreateCanvas(transform, "PauseMenu Canvas", sortingOrder);
        pauseGroup = pauseRoot.gameObject.AddComponent<CanvasGroup>();
        UIKit.Dimmer(pauseRoot, 0.88f);   // opaque enough that an open message can't be read while paused

        mainPanel = MakePanel("Main", 620);
        var title = UIKit.Text(mainPanel, "Title", "PAUSED", UIKit.SizeDisplay, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        title.characterSpacing = 8f;
        UIKit.Text(mainPanel, "Sub", "The case timer is paused.", UIKit.SizeSmall, UIKit.TextSecondary, TextAlignmentOptions.Center);
        AddMenuButton(mainPanel, "RESUME GAME", Resume, UIKit.Primary);
        AddMenuButton(mainPanel, "SETTINGS", OpenSettings, UIKit.Secondary);
        AddMenuButton(mainPanel, "RETURN TO MAIN MENU", AskMainMenu, UIKit.Secondary);
        AddMenuButton(mainPanel, "EXIT GAME", AskExit, UIKit.Secondary);
        statusText = UIKit.Text(mainPanel, "Status", "", UIKit.SizeSmall, UIKit.Warning, TextAlignmentOptions.Center);

        settingsPanel = MakePanel("Settings", 760);
        var st = UIKit.Text(settingsPanel, "Title", "SETTINGS", UIKit.SizeH1, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        st.characterSpacing = 6f;
        UIKit.Header(settingsPanel, "Audio");
        masterSlider = AddSlider(settingsPanel, "Master volume", 0f, 1f, OnMaster);
        musicSlider = AddSlider(settingsPanel, "Music & ambience", 0f, 1f, OnMusic);
        sfxSlider = AddSlider(settingsPanel, "Sound effects", 0f, 1f, OnSfx);
        UIKit.Header(settingsPanel, "Controls");
        sensSlider = AddSlider(settingsPanel, "Mouse sensitivity", 0.1f, 3f, OnSens);
        AddMenuButton(settingsPanel, "BACK", () => { Click(); ShowPanel(mainPanel); }, UIKit.Primary);

        confirmPanel = MakePanel("Confirm", 680);
        confirmTitle = UIKit.Text(confirmPanel, "Title", "", UIKit.SizeH1, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        confirmBody = UIKit.Text(confirmPanel, "Body", "", UIKit.SizeBody, UIKit.TextSecondary, TextAlignmentOptions.Center);
        var row = UIKit.Rect("Row", confirmPanel);
        UIKit.Horizontal(row, 16);
        UIKit.Size(row, -1, 76);
        UIKit.Button(row, "CANCEL", () => { Click(); ShowPanel(mainPanel); }, UIKit.Secondary);
        confirmYes = UIKit.Button(row, "YES", () => { Click(); confirmAction?.Invoke(); },
            new UIKit.ButtonStyle { background = UIKit.DangerButton, outline = UIKit.Danger, accent = new Color(1, 1, 1, 0.35f) });

        pauseRoot.gameObject.SetActive(false);
    }

    RectTransform MakePanel(string name, float width)
    {
        var p = UIKit.Rect(name, pauseRoot);
        UIKit.Place(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0));
        UIKit.PanelImage(p);
        var v = UIKit.Vertical(p, 40, 18);
        v.padding.left = v.padding.right = 48;
        UIKit.FitHeight(p);
        UIKit.AccentStrip(p);
        return p;
    }

    void AddMenuButton(RectTransform panel, string label, UnityEngine.Events.UnityAction action, UIKit.ButtonStyle style)
    {
        var b = UIKit.Button(panel, label, action, style);
        UIKit.Size(b, -1, 76);
    }

    Slider AddSlider(RectTransform panel, string label, float min, float max, UnityEngine.Events.UnityAction<float> onChange) =>
        UIKit.LabeledSlider(panel, label, min, max, onChange);
}
