using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.Events;
#endif

/// <summary>
/// Scam Sense main menu, in the same style as the in-game UI (UIKit):
///   Play - Continue (only with saved progress) - Level Selection - Settings - How to Play - About - Quit.
/// The menu UI lives in the scene ("MainMenu Canvas"), created once with
/// Tools > Scam Sense > Build Menu UI In Scene. Every button's OnClick is a normal persistent event you can see
/// and edit in the Inspector, and all references below are assigned. If the scene UI is missing, the menu is
/// built at runtime instead (same look), so the scene never breaks.
/// Only the level cards are generated at runtime, because they come from the LevelCatalog data.
/// Background: slow cinematic shots of the game's house (or the existing workstation video).
/// Uses the existing systems: LevelCatalog + LocalDataManager (progress / unlocks), GameAudioSettings and
/// GameDisplaySettings (same saved keys as the old Settings panel), SceneLoader (loading screen), MainMixer.
/// Put it on any object in the Menu scene and assign the references (see Docs/MainMenu.md).
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public enum BackgroundMode { HouseShots, ExistingVideo }

    [Header("Game")]
    [SerializeField] private LevelCatalog catalog;

    [Header("Background")]
    [SerializeField] private BackgroundMode backgroundMode = BackgroundMode.HouseShots;
    [Tooltip("Rendered views of the game's house, shown with a slow pan and cross-fade.")]
    [SerializeField] private Sprite[] backgroundShots;
    [SerializeField] private float shotDuration = 9f;
    [Tooltip("The menu's existing background video (switched off when House Shots are used).")]
    [SerializeField] private UnityEngine.Video.VideoPlayer existingVideo;
    [SerializeField] private float parallax = 14f;

    [Header("Audio")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 0.6f;

    [Header("Old menu")]
    [Tooltip("The previous menu canvas. Hidden at runtime (not deleted) so it can be restored.")]
    [SerializeField] private GameObject legacyMenuRoot;

    [System.Serializable]
    public class NamedPanel
    {
        public string id;
        public RectTransform panel;
    }

    // ---------- Scene UI (assigned by Tools > Scam Sense > Build Menu UI In Scene) ----------
    [Header("Scene UI (built in the scene - see tooltip)")]
    [Tooltip("True once the menu UI exists in the scene. Untick (or delete 'MainMenu Canvas') to fall back to building it at runtime.")]
    [SerializeField] private bool builtInScene;
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform bgLayer, menuColumn, panelHost, feedCard;
    [SerializeField] private Image bgA, bgB;
    [SerializeField] private CanvasGroup fade, titleGroup, columnGroup, panelDim;
    [SerializeField] private RectTransform titleLine;
    [SerializeField] private Button playButton, continueButton;
    [SerializeField] private TMP_Text continueSub, progressChip, statusText;
    [SerializeField] private List<NamedPanel> panelList = new List<NamedPanel>();

    // ---------- runtime ----------
    private int shotIndex;
    private float shotTimer;
    private readonly Dictionary<string, RectTransform> panels = new Dictionary<string, RectTransform>();
    private RectTransform openPanel;
    private bool loading;
    private AccountUI account;

    /// <summary>The menu canvas root and the panel container (AccountUI puts its panels here).</summary>
    public RectTransform Root => root;
    public RectTransform PanelHost => panelHost;
    public CanvasGroup Fade => fade;

    [Header("Scene UI - settings panel")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider, sfxSlider, sensSlider;
    [SerializeField] private TMP_Text resValue, modeValue, qualityValue, pendingText;
    [SerializeField] private Button applyButton;

    [Header("Scene UI - level selection & live feed")]
    [Tooltip("Level cards are generated here at runtime from the LevelCatalog.")]
    [SerializeField] private RectTransform levelRow;
    [SerializeField] private TMP_Text feedSender, feedBody, feedTime;
    [SerializeField] private CanvasGroup feedGroup;

    // Settings state
    private List<Resolution> resolutions;
    private int resIndex, qualityIndex;
    private bool fullscreen;
    private int feedIndex;
    private static readonly string[,] FeedSamples =
    {
        { "Unknown Number", "Your package is on hold. Confirm your details using the link provided.", "SMS" },
        { "Example Bank", "Your monthly statement is ready to view in the official app.", "EMAIL" },
        { "Rewards Center", "Congratulations! Claim your free premium reward immediately.", "EMAIL" },
        { "Example Delivery", "Your order is confirmed. Track it in the official delivery app.", "SMS" },
        { "Security Team", "Your access may be restricted today unless you verify your account.", "EMAIL" },
    };

    // ---------- Unity ----------

    void Start()
    {
        Time.timeScale = 1f;
        InteractionGate.ResetAll();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (legacyMenuRoot != null) legacyMenuRoot.SetActive(false);
        bool useShots = backgroundMode == BackgroundMode.HouseShots && backgroundShots != null && backgroundShots.Length > 0;
        if (existingVideo != null) existingVideo.enabled = !useShots;

        GameAudioSettings.ApplySavedVolumes(mixer);
        GameDisplaySettings.ApplySaved();

        if (catalog == null) Debug.LogError("MainMenuUI: assign the LevelCatalog asset (Assets/Levels/LevelCatalog.asset).", this);

        bool inScene = builtInScene && root != null;
        if (inScene) root.gameObject.SetActive(true);
        else Build(useShots);   // fallback: same UI, created at runtime
        BindRuntime();
        RefreshProgress();

        // Account panels (guest / sign-in / cloud status) - in the scene under MainMenu Canvas/Panels.
        account = GetComponent<AccountUI>();
        if (account == null) account = gameObject.AddComponent<AccountUI>();
        account.Init(this);

        StartCoroutine(Intro());
        StartCoroutine(FeedLoop());
    }

    /// <summary>Things that can't be saved in a scene: the panel lookup and hover sounds (C# callbacks).</summary>
    void BindRuntime()
    {
        panels.Clear();
        foreach (var p in panelList)
            if (p != null && p.panel != null && !string.IsNullOrEmpty(p.id)) panels[p.id] = p.panel;
        foreach (var fx in root.GetComponentsInChildren<ButtonPressFeedback>(true)) fx.onHover = Hover;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor only (Tools > Scam Sense > Build Menu UI In Scene): creates the whole menu as scene objects,
    /// assigns every reference above and connects all buttons with persistent OnClick events.
    /// </summary>
    public void BuildIntoScene()
    {
        bool useShots = backgroundMode == BackgroundMode.HouseShots && backgroundShots != null && backgroundShots.Length > 0;
        panelList.Clear();
        Build(useShots);
        fade.alpha = 0f;   // Intro() fades in at runtime; keep the scene view visible while editing
        builtInScene = true;
    }
#endif

    // ---------- Wiring: persistent (Inspector-visible) events when built in the editor, listeners at runtime ----------

    void Wire(Button b, UnityEngine.Events.UnityAction call)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEventTools.AddPersistentListener(b.onClick, call); return; }
#endif
        b.onClick.AddListener(call);
    }

    void Wire(Button b, UnityEngine.Events.UnityAction<string> call, string arg)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEventTools.AddStringPersistentListener(b.onClick, call, arg); return; }
#endif
        b.onClick.AddListener(() => call(arg));
    }

    void Wire(Button b, UnityEngine.Events.UnityAction<int> call, int arg)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEventTools.AddIntPersistentListener(b.onClick, call, arg); return; }
#endif
        b.onClick.AddListener(() => call(arg));
    }

    void Wire(Slider s, UnityEngine.Events.UnityAction<float> call)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEventTools.AddPersistentListener(s.onValueChanged, call); return; }
#endif
        s.onValueChanged.AddListener(call);
    }

    void Update()
    {
        AnimateBackground();

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !loading && !AccountUI.IsBlocking)
        {
            if (openPanel != null) ClosePanel();
            else OpenPanel("quit");
        }
    }

    // ---------- Actions (public so they can be picked in a Button's OnClick list) ----------

    /// <summary>PLAY: asks how to play (guest / sign in) if nobody is signed in yet, then starts Level 1.</summary>
    public void Play() => LoadLevel(0);

    public void Continue()
    {
        if (catalog == null || !catalog.HasProgress()) return;
        LoadLevel(catalog.ContinueIndex());
    }

    /// <summary>Starts a level. Without an account choice yet, the Authentication panel opens first.</summary>
    public void LoadLevel(int index)
    {
        if (loading || catalog == null || index < 0 || index >= catalog.Count) return;
        if (account != null && !account.ReadyToPlay)
        {
            ClosePanel();
            account.RequestAccount(() => LoadLevel(index));
            return;
        }
        var level = catalog.levels[index];
        if (!catalog.IsUnlocked(index))
        {
            ShowStatus("That room is still locked.");
            return;
        }
        if (!LevelCatalog.SceneAvailable(level))
        {
            string msg = $"Scene '{level?.sceneName}' is not in the Build Profile scene list (File > Build Profiles).";
            Debug.LogError("MainMenuUI: " + msg, this);
            ShowStatus(msg);
            return;
        }
        StartCoroutine(LoadRoutine(level.sceneName));
    }

    IEnumerator LoadRoutine(string sceneName)
    {
        loading = true;
        fade.blocksRaycasts = true;
        float startVol = music != null ? music.volume : 0f;
        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.7f);
            fade.alpha = k;
            if (music != null) music.volume = Mathf.Lerp(startVol, 0f, k);
            yield return null;
        }
        fade.alpha = 1f;
        GameAudioSettings.Save();
        // SceneLoader waits for this black "FadeOverlay", then shows the loading screen with a tip.
        if (SceneLoader.Instance != null) SceneLoader.LoadScene(sceneName);
        else SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        GameAudioSettings.Save();
#if UNITY_EDITOR
        // Application.Quit() is ignored in the Editor; don't force-stop Play Mode from game code.
        ShowStatus("Quit closes the built game. In the Unity Editor, press the Play button to stop.");
        ClosePanel();
#else
        Application.Quit();
#endif
    }

    // ---------- Panels ----------

    public void OpenPanel(string id)
    {
        if (!panels.TryGetValue(id, out var p)) return;
        if (openPanel == p) return;
        if (openPanel != null) openPanel.gameObject.SetActive(false);
        if (id == "settings") LoadSettingsIntoUI();
        if (id == "levels") BuildLevelCards();
        openPanel = p;
        panelDim.gameObject.SetActive(true);
        p.gameObject.SetActive(true);
        StartCoroutine(PanelIn(p));
    }

    public void ClosePanel()
    {
        if (openPanel == null) return;
        if (openPanel == panels["settings"]) GameAudioSettings.Save();
        openPanel.gameObject.SetActive(false);
        openPanel = null;
        panelDim.gameObject.SetActive(false);
    }

    IEnumerator PanelIn(RectTransform p)
    {
        var g = p.GetComponent<CanvasGroup>();
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.18f);
            g.alpha = k;
            panelDim.alpha = k;
            p.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k);
            yield return null;
        }
        g.alpha = 1f; panelDim.alpha = 1f; p.localScale = Vector3.one;
    }

    void ShowStatus(string msg)
    {
        statusText.text = msg;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    // ---------- Progress ----------

    /// <summary>Re-reads saved progress (also called by AccountUI after the save profile changes).</summary>
    public void RefreshProgress()
    {
        bool has = catalog != null && catalog.HasProgress();
        continueButton.gameObject.SetActive(has);
        if (has)
        {
            int i = catalog.ContinueIndex();
            var lvl = catalog.levels[i];
            var p = LocalDataManager.Peek(lvl.levelId);
            continueSub.text = $"Level {lvl.levelNumber}" + (p != null && p.completed ? $"  ·  best {p.bestScore}" : "");
        }

        if (!has) { progressChip.text = "No saved progress yet  ·  start with Play"; return; }
        var sb = new StringBuilder("Saved progress  ");
        foreach (var l in catalog.levels)
        {
            if (l == null) continue;
            var p = LocalDataManager.Peek(l.levelId);
            if (p == null || !p.completed) continue;
            sb.Append($"·  Level {l.levelNumber}: <b>{p.bestScore}</b> pts  <color={UIKit.HexWarning}>{Stars(p.bestStars)}</color>  ");
        }
        progressChip.text = sb.ToString();
    }

    /// <summary>Three stars: earned ones gold, the rest dim (plus "n/3" so it never relies on colour alone).</summary>
    static string Stars(int n)
    {
        n = Mathf.Clamp(n, 0, 3);
        return $"<color={UIKit.HexWarning}>{new string('★', n)}</color><color=#FFFFFF33>{new string('★', 3 - n)}</color> <size=80%><color={UIKit.HexSecondary}>{n}/3</color></size>";
    }

    /// <summary>Makes a column share the row width equally instead of collapsing to its minimum.</summary>
    static void Column(RectTransform r, float preferredWidth)
    {
        var le = r.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.flexibleWidth = 1f;
    }

    // ---------- Settings ----------

    void LoadSettingsIntoUI()
    {
        masterSlider.SetValueWithoutNotify(GameAudioSettings.GetVolume(GameAudioSettings.KeyMaster));
        musicSlider.SetValueWithoutNotify(GameAudioSettings.GetVolume(GameAudioSettings.KeyMusic));
        sfxSlider.SetValueWithoutNotify(GameAudioSettings.GetVolume(GameAudioSettings.KeySfx));
        sensSlider.SetValueWithoutNotify(GameAudioSettings.MouseSensitivity);
        resolutions = GameDisplaySettings.Resolutions();
        resIndex = GameDisplaySettings.CurrentResolutionIndex(resolutions);
        fullscreen = GameDisplaySettings.Fullscreen;
        qualityIndex = GameDisplaySettings.Quality;
        RefreshSettingsLabels(false);
    }

    void RefreshSettingsLabels(bool pending)
    {
        UIKit.SetSliderValueLabel(masterSlider, Mathf.RoundToInt(masterSlider.value * 100f) + "%");
        UIKit.SetSliderValueLabel(musicSlider, Mathf.RoundToInt(musicSlider.value * 100f) + "%");
        UIKit.SetSliderValueLabel(sfxSlider, Mathf.RoundToInt(sfxSlider.value * 100f) + "%");
        UIKit.SetSliderValueLabel(sensSlider, sensSlider.value.ToString("0.0") + "x");
        var r = resolutions[resIndex];
        resValue.text = $"{r.width} × {r.height}";
        modeValue.text = fullscreen ? "Fullscreen" : "Windowed";
        qualityValue.text = QualitySettings.names.Length > 0 ? QualitySettings.names[qualityIndex] : "-";
        pendingText.text = pending ? "Display changes not applied yet" : "";
        UIKit.SetInteractable(applyButton, pending);
    }

    // Slider handlers (persistent OnValueChanged events on the settings sliders)
    public void OnMasterVolume(float v) { GameAudioSettings.SetVolume(mixer, GameAudioSettings.ParamMaster, GameAudioSettings.KeyMaster, v); RefreshSettingsLabels(applyButton.interactable); }
    public void OnMusicVolume(float v) { GameAudioSettings.SetVolume(mixer, GameAudioSettings.ParamMusic, GameAudioSettings.KeyMusic, v); RefreshSettingsLabels(applyButton.interactable); }
    public void OnSfxVolume(float v) { GameAudioSettings.SetVolume(mixer, GameAudioSettings.ParamSfx, GameAudioSettings.KeySfx, v); RefreshSettingsLabels(applyButton.interactable); }
    public void OnSensitivity(float v) { GameAudioSettings.MouseSensitivity = v; RefreshSettingsLabels(applyButton.interactable); }

    public void StepResolution(int d) { resIndex = (resIndex + d + resolutions.Count) % resolutions.Count; RefreshSettingsLabels(true); }
    public void StepMode(int d) { fullscreen = !fullscreen; RefreshSettingsLabels(true); }
    public void StepQuality(int d) { int n = Mathf.Max(1, QualitySettings.names.Length); qualityIndex = (qualityIndex + d + n) % n; RefreshSettingsLabels(true); }

    public void ApplyDisplay()
    {
        GameDisplaySettings.Apply(resolutions[resIndex], fullscreen, qualityIndex);
        RefreshSettingsLabels(false);
#if UNITY_EDITOR
        pendingText.text = "Applied. (Resolution / fullscreen take effect in the built game.)";
#endif
    }

    public void ResetSettings()
    {
        foreach (var k in new[] { GameAudioSettings.KeyMaster, GameAudioSettings.KeyMusic, GameAudioSettings.KeySfx, GameAudioSettings.KeySensitivity })
            PlayerPrefs.DeleteKey(k);
        GameDisplaySettings.ClearSaved();
        GameAudioSettings.ApplySavedVolumes(mixer);
        LoadSettingsIntoUI();
        // Defaults for display: current native size, fullscreen, project default quality.
        resIndex = resolutions.Count - 1;
        fullscreen = true;
        RefreshSettingsLabels(true);
        PlayerPrefs.Save();
    }

    // ---------- Level selection ----------

    void BuildLevelCards()
    {
        foreach (Transform c in levelRow) Destroy(c.gameObject);
        if (catalog == null) return;

        // The next room after the last catalogued one, if the save knows about it. The panel fits three
        // cards, so from three rooms on it becomes a note on the last card instead of a fourth card.
        var last = catalog.Count > 0 ? catalog.levels[catalog.Count - 1] : null;
        bool hasUnbuiltNext = last != null && !string.IsNullOrEmpty(last.nextLevelId);
        bool nextAsCard = hasUnbuiltNext && catalog.Count < 3;

        for (int i = 0; i < catalog.Count; i++)
        {
            var lvl = catalog.levels[i];
            if (lvl == null) continue;
            bool unlocked = catalog.IsUnlocked(i);
            var p = LocalDataManager.Peek(lvl.levelId);
            string req = i > 0 && catalog.levels[i - 1] != null
                ? $"Score {catalog.levels[i - 1].minScoreToUnlockNext} in Level {catalog.levels[i - 1].levelNumber} to unlock."
                : "";
            string note = LevelCatalog.SceneAvailable(lvl) ? null
                              : string.IsNullOrEmpty(lvl.sceneName) ? "Coming soon - this room isn't built yet"
                              : "Scene missing from build";
            if (note == null && lvl == last && hasUnbuiltNext && !nextAsCard && LocalDataManager.IsUnlocked(last.nextLevelId))
                note = $"Level {last.levelNumber + 1} unlocked · coming in a future update";
            int idx = i;
            MakeLevelCard(lvl.levelNumber, lvl.displayName, lvl.description, unlocked, p, req,
                          unlocked && LevelCatalog.SceneAvailable(lvl) ? () => LoadLevel(idx) : (UnityEngine.Events.UnityAction)null,
                          note);
        }

        if (nextAsCard)
        {
            bool unlocked = LocalDataManager.IsUnlocked(last.nextLevelId);
            MakeLevelCard(last.levelNumber + 1, "Coming soon", "The next case is still being prepared.", unlocked, null,
                          $"Score {last.minScoreToUnlockNext} in Level {last.levelNumber} to unlock.", null,
                          unlocked ? "Unlocked · available in a future update" : null);
        }
    }

    void MakeLevelCard(int number, string title, string desc, bool unlocked, LocalDataManager.LevelProgress p,
                       string requirement, UnityEngine.Events.UnityAction onPlay, string note)
    {
        var card = UIKit.Rect("Level_" + number, levelRow);
        UIKit.Size(card, 380, 470);   // three cards fit the panel
        UIKit.PanelImage(card, unlocked ? UIKit.PanelRaised : new Color(0.07f, 0.09f, 0.14f, 1f));
        var v = UIKit.Vertical(card, 26, 12);
        v.childForceExpandHeight = false;

        var head = UIKit.Rect("Head", card);
        var h = UIKit.Horizontal(head, 10, TextAnchor.MiddleLeft);
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        UIKit.Size(head, -1, 40);
        var num = UIKit.Text(head, "Num", $"LEVEL {number:00} · ROOM {number}", 18, UIKit.Accent, TextAlignmentOptions.MidlineLeft, bold: true);
        num.characterSpacing = 1f;
        num.textWrappingMode = TextWrappingModes.NoWrap;
        UIKit.Size(num, 175);
        string tag = !unlocked ? "LOCKED" : (p != null && p.completed ? "COMPLETED" : "UNLOCKED");
        Color tagBg = !unlocked ? UIKit.Border : (p != null && p.completed ? UIKit.SafeButton : UIKit.AccentDark);
        UIKit.Badge(head, (unlocked ? "" : "× ") + tag, tagBg, Color.white, 17);

        var t = UIKit.Text(card, "Title", title, 32, unlocked ? UIKit.TextPrimary : UIKit.TextSecondary, bold: true);
        UIKit.Text(card, "Desc", string.IsNullOrEmpty(desc) ? " " : desc, 21, UIKit.TextSecondary).lineSpacing = 4f;

        var stats = UIKit.Text(card, "Stats", "", 22, UIKit.TextPrimary);
        if (p != null && p.completed)
            stats.text = $"Best score  <b>{p.bestScore}</b>\nBest stars  <color={UIKit.HexWarning}><size=28>{Stars(p.bestStars)}</size></color>";
        else if (unlocked)
            stats.text = $"<color={UIKit.HexSecondary}>Not completed yet</color>";
        else
            stats.text = $"<color={UIKit.HexSecondary}>{requirement}</color>";

        if (!string.IsNullOrEmpty(note))
            UIKit.Text(card, "Note", note, 19, UIKit.Warning).fontStyle = FontStyles.Italic;

        var spacer = UIKit.Rect("Spacer", card);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var btn = UIKit.HudButton(card, onPlay != null ? "PLAY" : (unlocked ? "UNAVAILABLE" : "LOCKED"), onPlay,
                                  onPlay != null ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary, 26);
        UIKit.Size(btn, -1, 66);
        UIKit.SetInteractable(btn, onPlay != null);
        AddFx(btn);
    }

    // ---------- Animations ----------

    IEnumerator Intro()
    {
        fade.alpha = 1f;
        titleGroup.alpha = 0f;
        columnGroup.alpha = 0f;
        var titleRt = (RectTransform)titleGroup.transform;
        Vector2 titlePos = titleRt.anchoredPosition;
        float lineW = titleLine.sizeDelta.x;
        titleLine.sizeDelta = new Vector2(0f, titleLine.sizeDelta.y);

        for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
            fade.alpha = 1f - k;
            float tk = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.2f) / 0.7f));
            titleGroup.alpha = tk;
            titleRt.anchoredPosition = titlePos + new Vector2(0f, -24f * (1f - tk));
            titleLine.sizeDelta = new Vector2(lineW * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.5f) / 0.6f)), titleLine.sizeDelta.y);
            columnGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.45f) / 0.6f));
            yield return null;
        }
        fade.alpha = 0f; fade.blocksRaycasts = false;
        titleGroup.alpha = 1f; columnGroup.alpha = 1f;
        titleRt.anchoredPosition = titlePos;
        titleLine.sizeDelta = new Vector2(lineW, titleLine.sizeDelta.y);
    }

    void AnimateBackground()
    {
        if (bgA == null) return;
        shotTimer += Time.unscaledDeltaTime;
        float k = backgroundShots.Length > 1
            ? Mathf.Clamp01(shotTimer / Mathf.Max(1f, shotDuration))
            : Mathf.PingPong(shotTimer / Mathf.Max(1f, shotDuration), 1f);   // single shot: drift in and out

        // Slow push-in on the current shot, gentle mouse parallax on the whole layer.
        bgA.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.04f, 1.12f, k);
        if (Mouse.current != null && Screen.width > 0)
        {
            Vector2 m = Mouse.current.position.ReadValue();
            Vector2 n = new Vector2(m.x / Screen.width - 0.5f, m.y / Screen.height - 0.5f);
            bgLayer.anchoredPosition = Vector2.Lerp(bgLayer.anchoredPosition, -n * parallax, Time.unscaledDeltaTime * 3f);
        }

        // Cross-fade to the next shot during the last second.
        if (backgroundShots.Length > 1)
        {
            float remain = shotDuration - shotTimer;
            bgB.color = new Color(1, 1, 1, Mathf.Clamp01(1f - remain / 1.2f));
            if (remain <= 0f)
            {
                shotIndex = (shotIndex + 1) % backgroundShots.Length;
                bgA.sprite = backgroundShots[shotIndex];
                bgB.sprite = backgroundShots[(shotIndex + 1) % backgroundShots.Length];
                bgB.color = new Color(1, 1, 1, 0);
                shotTimer = 0f;
            }
            bgB.rectTransform.localScale = Vector3.one * 1.04f;
        }
    }

    IEnumerator FeedLoop()
    {
        while (true)
        {
            feedSender.text = FeedSamples[feedIndex, 0];
            feedBody.text = FeedSamples[feedIndex, 1];
            feedTime.text = FeedSamples[feedIndex, 2] + "  ·  now";
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { feedGroup.alpha = t / 0.4f; feedCard.anchoredPosition = new Vector2(0, -20f * (1f - t / 0.4f)); yield return null; }
            feedGroup.alpha = 1f; feedCard.anchoredPosition = Vector2.zero;
            yield return new WaitForSecondsRealtime(4.5f);
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { feedGroup.alpha = 1f - t / 0.4f; yield return null; }
            feedGroup.alpha = 0f;
            feedIndex = (feedIndex + 1) % FeedSamples.GetLength(0);
        }
    }

    // ---------- Sounds & button feel ----------

    /// <summary>Click sound (added to every menu button's OnClick list).</summary>
    public void PlayClickSound()
    {
        if (uiSource != null && clickClip != null) uiSource.PlayOneShot(clickClip, uiVolume);
    }

    void Hover()
    {
        if (uiSource != null && hoverClip != null) uiSource.PlayOneShot(hoverClip, uiVolume * 0.6f);
    }

    /// <summary>Hover + click sounds (the hover/press scaling comes from UIKit's ButtonPressFeedback).</summary>
    public void AddFx(Button b)
    {
        var fx = b.GetComponent<ButtonPressFeedback>();
        if (fx != null) fx.onHover = Hover;
        Wire(b, PlayClickSound);
    }

    // ---------- Build ----------

    void Build(bool useShots)
    {
        root = UIKit.CreateCanvas(transform, "MainMenu Canvas", 10);

        // Background: shots (or transparent so the existing video shows), tint, gradient, grid
        bgLayer = UIKit.Rect("Background", root);
        UIKit.Stretch(bgLayer, -40);
        if (useShots)
        {
            bgB = MakeShot("ShotNext", backgroundShots.Length > 1 ? backgroundShots[1] : backgroundShots[0]);
            bgA = MakeShot("ShotCurrent", backgroundShots[0]);
            bgB.transform.SetAsLastSibling();   // next shot fades in above the current one
            bgB.color = new Color(1, 1, 1, 0);
        }
        var tint = UIKit.Rect("Tint", root);
        UIKit.Stretch(tint);
        UIKit.AddImage(tint, new Color(0.03f, 0.07f, 0.14f, useShots ? 0.45f : 0.35f)).raycastTarget = false;
        var grad = UIKit.Rect("Gradient", root);
        UIKit.Stretch(grad);
        var gi = UIKit.AddImage(grad, Color.white, MenuSprite("Menu_Gradient", GradientSprite));
        gi.raycastTarget = false;
        var grid = UIKit.Rect("Grid", root);
        UIKit.Stretch(grid);
        var gimg = UIKit.AddImage(grid, Color.white, MenuSprite("Menu_Grid", GridSprite));
        gimg.type = Image.Type.Tiled;
        gimg.raycastTarget = false;

        // Title block (top-left)
        var title = UIKit.Rect("Title", root);
        UIKit.Place(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -90), new Vector2(900, 220));
        titleGroup = title.gameObject.AddComponent<CanvasGroup>();
        titleGroup.blocksRaycasts = false;
        var big = UIKit.Text(title, "Name", "SCAM SENSE", 104, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        big.characterSpacing = 12f;
        big.textWrappingMode = TextWrappingModes.NoWrap;
        UIKit.Place(big.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(900, 120));
        titleLine = UIKit.Rect("Line", title);
        UIKit.Place(titleLine, new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -126), new Vector2(420, 4));
        UIKit.AddImage(titleLine, UIKit.Accent).raycastTarget = false;
        var tag = UIKit.Text(title, "Tagline", "THINK BEFORE YOU CLICK", 30, UIKit.Accent, TextAlignmentOptions.TopLeft, bold: true);
        tag.characterSpacing = 10f;
        UIKit.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -146), new Vector2(900, 40));
        var sub = UIKit.Text(title, "Sub", "A first-person cybersecurity awareness game", 22, UIKit.TextSecondary);
        UIKit.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -188), new Vector2(900, 30));

        // Navigation column (left)
        menuColumn = UIKit.Rect("Navigation", root);
        UIKit.Place(menuColumn, new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -350), new Vector2(500, 0));
        var col = UIKit.Vertical(menuColumn, 0, 12);
        UIKit.FitHeight(menuColumn);
        columnGroup = menuColumn.gameObject.AddComponent<CanvasGroup>();

        playButton = MenuButton("PLAY", "New case", primary: true);
        Wire(playButton, Play);
        continueButton = MenuButton("CONTINUE", "", primary: false);
        Wire(continueButton, Continue);
        continueSub = continueButton.transform.Find("Sub").GetComponent<TMP_Text>();
        Wire(MenuButton("LEVEL SELECTION", ""), OpenPanel, "levels");
        Wire(MenuButton("SETTINGS", ""), OpenPanel, "settings");
        Wire(MenuButton("HOW TO PLAY", ""), OpenPanel, "how");
        Wire(MenuButton("ABOUT", ""), OpenPanel, "about");
        Wire(MenuButton("QUIT GAME", ""), OpenPanel, "quit");

        statusText = UIKit.Text(menuColumn, "Status", "", UIKit.SizeSmall, UIKit.Warning);
        statusText.gameObject.SetActive(false);

        // Footer: saved progress + offline note
        progressChip = UIKit.Text(root, "Progress", "", 22, UIKit.TextSecondary, TextAlignmentOptions.BottomLeft);
        UIKit.Place(progressChip.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(120, 48), new Vector2(1100, 36));
        var offline = UIKit.Text(root, "Offline", "Offline training  ·  all messages are fictional  ·  no real links",
                                 20, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.BottomRight);
        UIKit.Place(offline.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 48), new Vector2(900, 30));

        BuildFeed();

        // Panels
        panelDim = UIKit.Dimmer(root, 0.7f).gameObject.AddComponent<CanvasGroup>();
        panelDim.name = "PanelDim";
        Wire(panelDim.gameObject.AddComponent<Button>(), ClosePanel);   // click outside closes
        panelDim.gameObject.SetActive(false);
        panelHost = UIKit.Rect("Panels", root);
        UIKit.Stretch(panelHost);

        BuildSettings();
        BuildLevels();
        BuildHowTo();
        BuildAbout();
        BuildQuit();

        // Fade overlay (named so SceneLoader waits for it before showing the loading screen)
        var fadeCanvas = UIKit.CreateCanvas(transform, "MainMenu Fade Canvas", 500);
        var f = UIKit.Rect("FadeOverlay", fadeCanvas);
        UIKit.Stretch(f);
        UIKit.AddImage(f, Color.black);
        fade = f.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = false;
    }

    Image MakeShot(string name, Sprite s)
    {
        var r = UIKit.Rect(name, bgLayer);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        var img = UIKit.AddImage(r, Color.white, s);
        img.raycastTarget = false;
        var fitter = r.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = s.rect.width / s.rect.height;
        return img;
    }

    Button MenuButton(string label, string subLabel, bool primary = false)
    {
        // HUD-style button with the Digital Light Sweep; PLAY also gets a gentle idle sweep every 6 s.
        var b = UIKit.HudButton(menuColumn, label, null, primary, 28, primary ? 6f : 0f);
        UIKit.Size(b, 500, primary ? 84 : 72);
        var lbl = b.transform.Find("Label").GetComponent<TMP_Text>();
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        lbl.margin = new Vector4(52, 0, 20, 0);
        lbl.characterSpacing = 4f;

        var sub = UIKit.Text(b.transform, "Sub", subLabel, 20, primary ? Color.white : UIKit.TextSecondary, TextAlignmentOptions.MidlineRight);
        UIKit.Stretch(sub.rectTransform);
        sub.margin = new Vector4(20, 0, 26, 0);
        sub.textWrappingMode = TextWrappingModes.NoWrap;
        AddFx(b);
        return b;
    }

    void BuildFeed()
    {
        var holder = UIKit.Rect("LiveFeed", root);
        UIKit.Place(holder, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-120, -40), new Vector2(560, 300));

        var caption = UIKit.Text(holder, "Caption", "INCOMING  ·  CAN YOU SPOT THE SCAM?", 20, UIKit.Accent, TextAlignmentOptions.TopLeft, bold: true);
        caption.characterSpacing = 3f;
        UIKit.Place(caption.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(560, 30));

        var cardHost = UIKit.Rect("CardHost", holder);
        UIKit.Place(cardHost, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -44), new Vector2(560, 190));
        feedCard = UIKit.Rect("Card", cardHost);
        UIKit.Stretch(feedCard);
        UIKit.PanelImage(feedCard, new Color(UIKit.Panel.r, UIKit.Panel.g, UIKit.Panel.b, 0.92f)).raycastTarget = false;
        feedGroup = feedCard.gameObject.AddComponent<CanvasGroup>();
        feedGroup.blocksRaycasts = false;
        UIKit.AccentStrip(feedCard);

        feedTime = UIKit.Text(feedCard, "Time", "", 18, UIKit.TextSecondary, TextAlignmentOptions.TopLeft, bold: true);
        feedTime.characterSpacing = 2f;
        UIKit.Place(feedTime.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -20), new Vector2(500, 26));
        feedSender = UIKit.Text(feedCard, "Sender", "", 28, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(feedSender.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -50), new Vector2(500, 36));
        feedBody = UIKit.Text(feedCard, "Body", "", 22, UIKit.TextSecondary, TextAlignmentOptions.TopLeft);
        UIKit.Place(feedBody.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -92), new Vector2(500, 84));

        var hint = UIKit.Text(holder, "Hint", "Scam or legitimate? Check the sender, the context, urgency and links.", 19, new Color(1, 1, 1, 0.6f));
        UIKit.Place(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -246), new Vector2(560, 50));
    }

    RectTransform MakePanel(string id, string title, float width)
    {
        var p = UIKit.Rect("Panel_" + id, panelHost);
        UIKit.Place(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0));
        UIKit.PanelImage(p);
        var v = UIKit.Vertical(p, 40, 16);
        v.padding.left = v.padding.right = 52;
        UIKit.FitHeight(p);
        UIKit.AccentStrip(p, null, 8f);
        p.gameObject.AddComponent<CanvasGroup>();

        var head = UIKit.Rect("Header", p);
        UIKit.Size(head, -1, 56);
        var t = UIKit.Text(head, "Title", title, UIKit.SizeH1, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, bold: true);
        t.characterSpacing = 6f;
        UIKit.Stretch(t.rectTransform);
        var close = UIKit.Button(head, "×", null, UIKit.Secondary, 34);
        Wire(close, ClosePanel);
        UIKit.Place((RectTransform)close.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(56, 56));
        close.transform.Find("Accent")?.gameObject.SetActive(false);

        p.gameObject.SetActive(false);
        panels[id] = p;
        panelList.Add(new NamedPanel { id = id, panel = p });
        return p;
    }

    void FooterButtons(RectTransform panel, params (string label, UnityEngine.Events.UnityAction action, UIKit.HudVariant variant)[] buttons)
    {
        var row = UIKit.Rect("Footer", panel);
        UIKit.Horizontal(row, 16);
        UIKit.Size(row, -1, 72);
        foreach (var b in buttons)
        {
            var btn = UIKit.HudButton(row, b.label, null, b.variant, 26);
            Wire(btn, b.action);
            AddFx(btn);
        }
    }

    void BuildSettings()
    {
        var p = MakePanel("settings", "SETTINGS", 1240);
        var cols = UIKit.Rect("Columns", p);
        var h = UIKit.Horizontal(cols, 56, TextAnchor.UpperLeft);
        h.childForceExpandHeight = false;

        var left = UIKit.Rect("Audio", cols);
        UIKit.Vertical(left, 0, 10);
        Column(left, 540);
        UIKit.Header(left, "Audio");
        masterSlider = UIKit.LabeledSlider(left, "Master volume", 0f, 1f, null);
        Wire(masterSlider, OnMasterVolume);
        musicSlider = UIKit.LabeledSlider(left, "Music", 0f, 1f, null);
        Wire(musicSlider, OnMusicVolume);
        sfxSlider = UIKit.LabeledSlider(left, "Sound effects", 0f, 1f, null);
        Wire(sfxSlider, OnSfxVolume);
        UIKit.Header(left, "Controls");
        sensSlider = UIKit.LabeledSlider(left, "Mouse sensitivity", 0.1f, 3f, null);
        Wire(sensSlider, OnSensitivity);

        var right = UIKit.Rect("Display", cols);
        UIKit.Vertical(right, 0, 14);
        Column(right, 540);
        UIKit.Header(right, "Display");
        resValue = Stepper(right, "Resolution", StepResolution);
        modeValue = Stepper(right, "Display mode", StepMode);
        qualityValue = Stepper(right, "Graphics quality", StepQuality);
        pendingText = UIKit.Text(right, "Pending", "", UIKit.SizeSmall, UIKit.Warning);
        UIKit.Text(right, "Note", "Audio and mouse changes apply instantly. Display changes apply when you press Apply.",
                   20, UIKit.TextSecondary);

        var row = UIKit.Rect("Footer", p);
        UIKit.Horizontal(row, 16);
        UIKit.Size(row, -1, 72);
        var reset = UIKit.HudButton(row, "RESET TO DEFAULTS", null, UIKit.HudVariant.Secondary, 24); Wire(reset, ResetSettings); AddFx(reset);
        applyButton = UIKit.HudButton(row, "APPLY", null, UIKit.HudVariant.Primary, 26); Wire(applyButton, ApplyDisplay); AddFx(applyButton);
        var back = UIKit.HudButton(row, "BACK", null, UIKit.HudVariant.Secondary, 26); Wire(back, ClosePanel); AddFx(back);
    }

    TMP_Text Stepper(RectTransform parent, string label, UnityEngine.Events.UnityAction<int> step)
    {
        var block = UIKit.Rect("Stepper_" + label, parent);
        UIKit.Size(block, -1, 104);
        var name = UIKit.Text(block, "Label", label, UIKit.SizeBody, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(500, 36));

        var row = UIKit.Rect("Row", block);
        row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, 0); row.pivot = new Vector2(0.5f, 0);
        row.sizeDelta = new Vector2(0, 58); row.anchoredPosition = Vector2.zero;
        UIKit.AddImage(row, UIKit.Background, rounded: true);
        var prev = UIKit.Button(row, "←", null, UIKit.Secondary, 28);
        Wire(prev, step, -1);
        UIKit.Place((RectTransform)prev.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(64, 50));
        prev.transform.Find("Accent")?.gameObject.SetActive(false);
        var next = UIKit.Button(row, "→", null, UIKit.Secondary, 28);
        Wire(next, step, 1);
        UIKit.Place((RectTransform)next.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(64, 50));
        next.transform.Find("Accent")?.gameObject.SetActive(false);
        AddFx(prev); AddFx(next);
        var value = UIKit.Text(row, "Value", "", UIKit.SizeBody, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        UIKit.Stretch(value.rectTransform);
        value.margin = new Vector4(76, 0, 76, 0);
        return value;
    }

    void BuildLevels()
    {
        var p = MakePanel("levels", "LEVEL SELECTION", 1320);
        UIKit.Text(p, "Intro", "Each room is a new case. Reach the required score to unlock the next room — stars reward how well you did.",
                   UIKit.SizeSmall, UIKit.TextSecondary);
        levelRow = UIKit.Rect("Cards", p);
        var h = UIKit.Horizontal(levelRow, 24, TextAnchor.UpperLeft);
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        UIKit.Size(levelRow, -1, 470);
        FooterButtons(p, ("BACK", (UnityEngine.Events.UnityAction)ClosePanel, UIKit.HudVariant.Secondary));
    }

    void BuildHowTo()
    {
        var p = MakePanel("how", "HOW TO PLAY", 1320);
        var cols = UIKit.Rect("Columns", p);
        var h = UIKit.Horizontal(cols, 56, TextAnchor.UpperLeft);
        h.childForceExpandHeight = false;

        var left = UIKit.Rect("Controls", cols);
        UIKit.Vertical(left, 0, 12);
        Column(left, 560);
        UIKit.Header(left, "Controls");
        KeyRow(left, "W A S D", "Move");
        KeyRow(left, "MOUSE", "Look around");
        KeyRow(left, "E", "Interact: phone, computer, chairs, doors");
        KeyRow(left, "SPACE", "Stand up from a chair (E also works)");
        KeyRow(left, "CLICK", "Choose answers and buttons");
        KeyRow(left, "ESC", "Pause menu / exit the computer");

        var right = UIKit.Rect("Investigate", cols);
        UIKit.Vertical(right, 0, 12);
        Column(right, 600);
        UIKit.Header(right, "Investigate");
        var lvl = catalog != null && catalog.Count > 0 ? catalog.levels[0] : null;
        int unlock = lvl != null ? lvl.minScoreToUnlockNext : 300;
        string stars = lvl != null ? $"{lvl.oneStarScore} / {lvl.twoStarScore} / {lvl.threeStarScore}" : "200 / 300 / 400";
        var text = UIKit.Text(right, "Steps",
            Bullet("Text messages arrive on your phone — press <b>E</b> to read them.") +
            Bullet("Emails are read on a <b>computer</b>: the downstairs desk or the upstairs computer room. Look at the monitor and press <b>E</b>.") +
            Bullet("Decide: <color=" + UIKit.HexDanger + "><b>SCAM</b></color> or <color=" + UIKit.HexSafe + "><b>LEGITIMATE</b></color>.") +
            Bullet("Check the <b>sender</b>, whether it matches something <b>you did</b>, any <b>urgency</b> or pressure, and where <b>links</b> lead.") +
            Bullet("Faster correct answers earn more points (up to 100 per message). Wrong answers earn 0.") +
            Bullet($"Stars reward your score ({stars}). Reach <b>{unlock}</b> points to unlock the next room."),
            23, UIKit.TextPrimary);
        text.lineSpacing = 8f;

        FooterButtons(p, ("BACK", (UnityEngine.Events.UnityAction)ClosePanel, UIKit.HudVariant.Secondary));
    }

    static string Bullet(string s) => $"<color={UIKit.HexAccent}>•</color>  {s}\n";

    void KeyRow(RectTransform parent, string key, string action)
    {
        var row = UIKit.Rect("Key_" + key, parent);
        var h = UIKit.Horizontal(row, 16, TextAnchor.MiddleLeft);
        h.childForceExpandWidth = false;
        UIKit.Size(row, -1, 50);
        var cap = UIKit.Badge(row, key, UIKit.Accent, UIKit.Background, 20);
        UIKit.Size(cap.transform.parent.GetComponent<RectTransform>(), 150, 44);
        var t = UIKit.Text(row, "Action", action, 23, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft);
        UIKit.Size(t, 420);
    }

    void BuildAbout()
    {
        var p = MakePanel("about", "ABOUT", 1000);
        UIKit.Text(p, "Name", "SCAM SENSE: THINK BEFORE YOU CLICK", 28, UIKit.Accent, bold: true).characterSpacing = 3f;
        UIKit.Text(p, "Desc",
            "Scam Sense: Think Before You Click is a first-person cybersecurity awareness game that teaches players how to " +
            "identify scam messages and recognize legitimate communications through interactive everyday scenarios.",
            UIKit.SizeBody, UIKit.TextPrimary).lineSpacing = 8f;
        UIKit.Header(p, "Why it matters");
        UIKit.Text(p, "Purpose",
            "Scams rely on pressure, surprise and familiar-looking messages. Practising in a safe, offline house helps you slow down, " +
            "look for warning signs and recognise when a message really does match something you did.",
            UIKit.SizeSmall, UIKit.TextSecondary).lineSpacing = 6f;
        var box = UIKit.Rect("Reminder", p);
        UIKit.AddImage(box, new Color(UIKit.AccentDark.r, UIKit.AccentDark.g, UIKit.AccentDark.b, 0.35f), rounded: true);
        UIKit.Vertical(box, 20, 0);
        UIKit.Text(box, "Text",
            "<b>Remember:</b> if a real message worries you, don't use its links or numbers. Contact the company through its official app, " +
            "website or phone number to verify it.", UIKit.SizeSmall, UIKit.TextPrimary).lineSpacing = 6f;
        UIKit.Text(p, "Fiction", "All senders, brands and links in the game are fictional.", 20, UIKit.TextSecondary).fontStyle = FontStyles.Italic;
        FooterButtons(p, ("BACK", (UnityEngine.Events.UnityAction)ClosePanel, UIKit.HudVariant.Secondary));
    }

    void BuildQuit()
    {
        var p = MakePanel("quit", "QUIT GAME", 720);
        UIKit.Text(p, "Question", "Are you sure you want to quit Scam Sense?", UIKit.SizeBody, UIKit.TextPrimary);
        UIKit.Text(p, "Saved", "Your scores, stars and unlocked rooms are saved.", UIKit.SizeSmall, UIKit.TextSecondary);
        FooterButtons(p,
            ("CANCEL", (UnityEngine.Events.UnityAction)ClosePanel, UIKit.HudVariant.Secondary),
            ("YES, QUIT", (UnityEngine.Events.UnityAction)QuitGame, UIKit.HudVariant.Danger));
    }

    // ---------- Procedural background sprites (saved as assets in Assets/Resources/UIKit/ by the menu builder) ----------

    static Sprite MenuSprite(string name, System.Func<Sprite> make)
    {
        var s = Resources.Load<Sprite>("UIKit/" + name);
        return s != null ? s : make();
    }

    /// <summary>Freshly generated menu background sprites (used to write the sprite assets).</summary>
    public static (string name, Sprite sprite)[] GenerateMenuSprites() => new[]
    {
        ("Menu_Gradient", GradientSprite()),
        ("Menu_Grid", GridSprite()),
    };

    static Sprite GradientSprite()
    {
        const int w = 256, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color c = UIKit.Background;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x / (float)(w - 1), fy = y / (float)(h - 1);
                float left = Mathf.Lerp(0.95f, 0.15f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fx / 0.7f)));
                float bottom = Mathf.Lerp(0.55f, 0f, Mathf.Clamp01(fy / 0.35f));
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, Mathf.Clamp01(Mathf.Max(left, bottom))));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    static Sprite GridSprite()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        Color line = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.05f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, (x == 0 || y == 0) ? line : Color.clear);
        tex.SetPixel(0, 0, new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.18f));   // node dot
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
