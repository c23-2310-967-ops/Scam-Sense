using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
///   PLAY (one primary action: opens the next case in the progression) - Classroom - Settings - About - Quit.
/// There is no level picker: rooms open in order through the existing unlock rules (score to unlock the next room;
/// RESULTS > NEXT ROOM / REPLAY inside a level). Instructions are given in the game where they are needed (Level 1
/// guide, briefing controls strip, first-time tips, pause menu), not on a How to Play page.
/// The menu UI lives in the scene ("MainMenu Canvas"), created once with
/// Tools > Scam Sense > Build Menu UI In Scene. Every button's OnClick is a normal persistent event you can see
/// and edit in the Inspector, and all references below are assigned. If the scene UI is missing, the menu is
/// built at runtime instead (same look), so the scene never breaks.
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
    [SerializeField] private Button playButton;
    [Tooltip("The line under PLAY: which case it opens.")]
    [SerializeField] private TMP_Text playSub;
    [SerializeField] private TMP_Text progressChip, statusText;
    [SerializeField] private List<NamedPanel> panelList = new List<NamedPanel>();

    // ---------- runtime ----------
    private int shotIndex;
    private float shotTimer;
    private readonly Dictionary<string, RectTransform> panels = new Dictionary<string, RectTransform>();
    private RectTransform openPanel;
    private bool loading;
    private AccountUI account;
    private ClassroomUI classroom;

    /// <summary>The menu canvas root and the panel container (AccountUI puts its panels here).</summary>
    public RectTransform Root => root;
    public LevelCatalog Catalog => catalog;

    /// <summary>PLAY from the Classroom: the same as the menu's PLAY.</summary>
    public void PlayNextCase() => Play();

    public RectTransform PanelHost => panelHost;
    public CanvasGroup Fade => fade;

    [Header("Scene UI - settings panel")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider, sfxSlider, sensSlider;
    [SerializeField] private TMP_Text resValue, modeValue, qualityValue, pendingText;
    [SerializeField] private Button applyButton;

    [Header("Scene UI - reset progress (Settings > Saved progress)")]
    [SerializeField] private Button resetConfirmButton;
    [SerializeField] private Button resetCancelButton;
    [SerializeField] private TMP_Text resetScopeText, resetResultTitle, resetResultBody;

    [Header("Scene UI - live feed")]
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
        else SceneLoader.RegisterLevels(catalog.levels);   // the loading screen shows the room that is opening

        bool inScene = builtInScene && root != null;
        if (inScene) root.gameObject.SetActive(true);
        else Build(useShots);   // fallback: same UI, created at runtime
        BindRuntime();
        if (!panels.ContainsKey("resetprogress")) BuildResetProgress();   // menu baked before RESET PROGRESS existed
        AddVersionLabel();
        RefreshProgress();

        // Account panels (guest / sign-in / cloud status) - in the scene under MainMenu Canvas/Panels.
        account = GetComponent<AccountUI>();
        if (account == null) account = gameObject.AddComponent<AccountUI>();
        account.Init(this);

        // Classroom (student / teacher portals) - its panel shell is in the scene too; the menu button opens it.
        classroom = GetComponent<ClassroomUI>();
        if (classroom == null) classroom = gameObject.AddComponent<ClassroomUI>();
        classroom.Init(this);

        StartCoroutine(Intro());
        StartCoroutine(FeedLoop());
        StartCoroutine(LoadBackdrop());
    }

    /// <summary>
    /// "Version 0.1.0" in the top-right corner, from Player Settings > Version, so testers can tell which build they have.
    /// Created at runtime (not baked into the scene) so it always shows the version of the build that is running.
    /// </summary>
    void AddVersionLabel()
    {
        if (root == null || root.Find("Version") != null) return;
        var v = UIKit.Text(root, "Version", "Version " + Application.version, 18, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.TopRight);
        // Bottom-right, just above the footer note: the top-right corner belongs to the account status chip.
        v.alignment = TextAlignmentOptions.BottomRight;
        UIKit.Place(v.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 46), new Vector2(400, 26));
        v.raycastTarget = false;
        if (panelDim != null) v.transform.SetSiblingIndex(panelDim.transform.GetSiblingIndex());   // under the dim and the panels
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
        DipBackdropForCuts();

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !loading && !AccountUI.IsBlocking && !ClassroomUI.IsOpen)
        {
            if (openPanel != null) ClosePanel();
            else OpenPanel("quit");
        }
    }

    // ---------- Actions (public so they can be picked in a Button's OnClick list) ----------

    /// <summary>
    /// PLAY: asks how to play (guest / sign in) if nobody chose yet, then opens the next case in the progression -
    /// Level 1 for a new player, otherwise the first unlocked room not completed yet (or the furthest unlocked one).
    /// The room is worked out after signing in, because signing in can switch to a different save.
    /// </summary>
    public void Play()
    {
        if (loading || catalog == null) return;
        if (account != null && !account.ReadyToPlay)
        {
            ClosePanel();
            account.RequestAccount(Play);
            return;
        }
        LoadLevel(CurrentRoomIndex());
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

    bool quitting;

    /// <summary>YES, QUIT: closes the built game; in the Unity Editor it stops Play Mode (Application.Quit is ignored there).</summary>
    public void QuitGame()
    {
        if (quitting) return;   // repeated clicks while closing do nothing
        quitting = true;
        Debug.Log("Exit Game button pressed.");
        GameAudioSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Panels ----------

    public void OpenPanel(string id)
    {
        if (id == "career") BuildCareerPanel();   // built on first use
        if (!panels.TryGetValue(id, out var p)) return;
        if (openPanel == p) return;
        if (openPanel != null)
        {
            if (panels.TryGetValue("settings", out var s) && openPanel == s) GameAudioSettings.Save();   // leaving Settings for another panel
            openPanel.gameObject.SetActive(false);
        }
        if (id == "settings") LoadSettingsIntoUI();
        if (id == "resetprogress") PrepareResetProgress();
        if (id == "career") { careerTab = 0; FillCareerPanel(); }   // always opens on OVERVIEW
        openPanel = p;
        panelDim.gameObject.SetActive(true);
        p.gameObject.SetActive(true);
        StartCoroutine(PanelIn(p));
        var icon = p.GetComponentInChildren<PopupIcon>();   // confirmation panels (quit, reset) have a status icon
        if (icon != null) icon.Play();
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
        if (playSub != null) playSub.text = PlaySubtitle();

        if (!has) { progressChip.text = "No saved progress yet  ·  press PLAY to start your first case"; RefreshCareerCard(); return; }
        var sb = new StringBuilder("Saved progress  ");
        foreach (var l in catalog.levels)
        {
            if (l == null) continue;
            var p = LocalDataManager.Peek(l.levelId);
            if (p == null || !p.completed) continue;
            sb.Append($"·  Level {l.levelNumber}: <b>{p.bestScore}</b> pts  <color={UIKit.HexWarning}>{Stars(p.bestStars)}</color>  ");
        }
        progressChip.text = sb.ToString();
        RefreshCareerCard();
    }

    /// <summary>What PLAY opens, under its label: "Case 1 · The First Case", "Case 3 · The Digital Trap", "Replay case 5 · ...".</summary>
    string PlaySubtitle()
    {
        if (catalog == null || catalog.Count == 0) return "";
        int i = CurrentRoomIndex();
        var lvl = catalog.levels[i];
        if (lvl == null) return "";
        var p = LocalDataManager.Peek(lvl.levelId);
        bool done = p != null && p.completed;
        return (done ? "Replay case " : "Case ") + lvl.levelNumber + "  ·  " + lvl.displayName;
    }

    /// <summary>Three stars: earned ones gold, the rest dim (plus "n/3" so it never relies on colour alone).</summary>
    static string Stars(int n)
    {
        n = Mathf.Clamp(n, 0, 3);
        return $"<color={UIKit.HexWarning}>{new string('★', n)}</color><color=#FFFFFF33>{new string('★', 3 - n)}</color> <size=80%><color={UIKit.HexSecondary}>{n}/3</color></size>";
    }

    /// <summary>
    /// Footer: the saved-progress line spans the bottom on one line and shrinks to fit (it used to wrap and run
    /// into the offline note on narrower screens); the offline note sits on its own line underneath.
    /// </summary>
    public static void LayoutFooter(TMP_Text progress, TMP_Text offline)
    {
        var p = progress.rectTransform;
        p.anchorMin = new Vector2(0, 0); p.anchorMax = new Vector2(1, 0); p.pivot = new Vector2(0, 0);
        p.offsetMin = new Vector2(120, 44); p.offsetMax = new Vector2(-80, 44 + 34);
        progress.textWrappingMode = TextWrappingModes.NoWrap;
        progress.overflowMode = TextOverflowModes.Ellipsis;
        progress.enableAutoSizing = true;
        progress.fontSizeMax = 22f;
        progress.fontSizeMin = 15f;

        var o = offline.rectTransform;
        o.anchorMin = new Vector2(0, 0); o.anchorMax = new Vector2(1, 0); o.pivot = new Vector2(1, 0);
        o.offsetMin = new Vector2(120, 14); o.offsetMax = new Vector2(-80, 14 + 26);
        offline.fontSize = 18f;
        offline.textWrappingMode = TextWrappingModes.NoWrap;
        offline.overflowMode = TextOverflowModes.Ellipsis;
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

    // ---------- Progression ----------

    /// <summary>The room PLAY opens: the first unlocked room not completed yet, else the furthest unlocked one.</summary>
    int CurrentRoomIndex()
    {
        if (catalog == null) return 0;
        for (int i = 0; i < catalog.Count; i++)
        {
            var l = catalog.levels[i];
            if (l == null || !catalog.IsUnlocked(i) || !LevelCatalog.SceneAvailable(l)) continue;
            var p = LocalDataManager.Peek(l.levelId);
            if (p == null || !p.completed) return i;
        }
        return catalog.ContinueIndex();
    }

    static Sprite padlock;

    /// <summary>A small white padlock, drawn once (no texture asset needed). Used by the locked achievement cards.</summary>
    static Sprite PadlockSprite()
    {
        if (padlock != null) return padlock;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                bool body = fx > 12 && fx < 52 && fy > 6 && fy < 36;
                float dx = fx - 32, dy = fy - 36;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                bool shackle = (fy >= 36 && r > 10 && r < 17) || (fy > 30 && fy < 36 && ((fx > 15 && fx < 22) || (fx > 42 && fx < 49)));
                bool keyhole = (Mathf.Pow(fx - 32, 2) + Mathf.Pow(fy - 24, 2) < 20f) || (fx > 30 && fx < 34 && fy > 12 && fy < 24);
                px[y * n + x] = (body && !keyhole) || shackle ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        padlock = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return padlock;
    }

    // ---------- Career profile (main menu card + dashboard) ----------

    RectTransform careerCard, careerBarFill, careerPanelBody;
    Image careerCardBadge;
    TMP_Text careerCardRank, careerCardStats, careerCardProgress;

    /// <summary>Top-right "CAREER PROFILE" card: rank insignia, XP bar and key stats. Click = career dashboard.</summary>
    void RefreshCareerCard()
    {
        if (root == null) return;
        if (careerCard == null)
        {
            careerCard = UIKit.Rect("CareerProfile", root);
            UIKit.Place(careerCard, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-120, -104), new Vector2(580, 176));
            var bg = UIKit.PanelImage(careerCard, new Color(UIKit.Panel.r, UIKit.Panel.g, UIKit.Panel.b, 0.9f));
            UIKit.AccentStrip(careerCard);
            var b = careerCard.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => OpenPanel("career"));
            careerCard.gameObject.AddComponent<ButtonPressFeedback>();
            AddFx(b);

            var badge = UIKit.Rect("Badge", careerCard);
            UIKit.Place(badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(112, 112));
            careerCardBadge = UIKit.AddImage(badge, Color.white, RankBadge.For(0));
            careerCardBadge.preserveAspect = true;
            careerCardBadge.raycastTarget = false;

            var head = UIKit.Text(careerCard, "Header", "CAREER PROFILE", 17, UIKit.Accent, TextAlignmentOptions.TopLeft, bold: true);
            head.characterSpacing = 3f;
            UIKit.Place(head.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -14), new Vector2(260, 22));
            var open = UIKit.Text(careerCard, "Open", "VIEW CAREER  ›", 17, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.TopRight, bold: true);
            open.characterSpacing = 2f;
            UIKit.Place(open.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -15), new Vector2(200, 22));
            careerCardRank = UIKit.Text(careerCard, "Rank", "", 26, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
            UIKit.Place(careerCardRank.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -40), new Vector2(414, 36));
            careerCardRank.textWrappingMode = TextWrappingModes.NoWrap;
            careerCardRank.overflowMode = TextOverflowModes.Ellipsis;
            careerCardRank.enableAutoSizing = true;   // long ranks ("Senior Cybersecurity Investigator") fit instead of being cut
            careerCardRank.fontSizeMin = 20f;
            careerCardRank.fontSizeMax = 26f;

            var bar = UIKit.Rect("XpBar", careerCard);
            UIKit.Place(bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -84), new Vector2(410, 12));
            UIKit.AddImage(bar, new Color(1f, 1f, 1f, 0.1f)).raycastTarget = false;
            careerBarFill = UIKit.Rect("Fill", bar);
            careerBarFill.anchorMin = Vector2.zero;
            careerBarFill.anchorMax = new Vector2(0f, 1f);
            careerBarFill.offsetMin = careerBarFill.offsetMax = Vector2.zero;
            UIKit.AddImage(careerBarFill, UIKit.Accent).raycastTarget = false;
            careerCardProgress = UIKit.Text(careerCard, "Progress", "", 18, CpMuted, TextAlignmentOptions.TopLeft);
            UIKit.Place(careerCardProgress.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -104), new Vector2(420, 26));
            careerCardProgress.textWrappingMode = TextWrappingModes.NoWrap;
            careerCardStats = UIKit.Text(careerCard, "Stats", "", 18, UIKit.TextPrimary, TextAlignmentOptions.TopLeft);
            UIKit.Place(careerCardStats.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -136), new Vector2(420, 26));
            careerCardStats.textWrappingMode = TextWrappingModes.NoWrap;
            careerCard.SetSiblingIndex(menuColumn != null ? menuColumn.GetSiblingIndex() : careerCard.GetSiblingIndex());   // under the panels
        }

        int xp = CareerSystem.TotalXp;
        CareerSystem.ProgressInRank(xp, out int rank, out int into, out int span);
        var rk = CareerSystem.RankAt(rank);
        careerCardBadge.sprite = RankBadge.For(rank);
        careerCardBadge.color = rk.color;
        careerCardRank.text = rk.title;
        careerBarFill.anchorMax = new Vector2(span > 0 ? Mathf.Clamp01((float)into / span) : 1f, 1f);
        careerBarFill.GetComponent<Image>().color = rk.color;
        careerCardProgress.text = span > 0 ? $"{into:N0} / {span:N0} XP to {CareerSystem.RankAt(rank + 1).title}" : "Highest rank reached";
        float acc = CareerSystem.BestRunAccuracy;
        int cases = catalog != null ? catalog.Count : CareerSystem.Config.casesInGame;
        careerCardStats.text = $"<b>{xp:N0}</b> XP   ·   <b>{CareerSystem.CasesSolved}/{cases}</b> solved   ·   " +
                               (acc >= 0f ? $"<b>{Mathf.RoundToInt(acc * 100f)}%</b> accuracy" : "accuracy -") +
                               $"   ·   <b>{LocalDataManager.Achievements.Count}</b> <color={UIKit.HexWarning}>★</color>";
    }

    // Career dashboard type scale (same scale as the rest of the menu: body 27, small 23 on a 1920x1080 canvas).
    const float CpRank = 42f, CpValue = 36f, CpHeading = 24f, CpBody = 22f, CpLabel = 19f;
    static readonly Color CpMuted = new Color32(0xC9, 0xD4, 0xE3, 255);   // supporting text: softer than white, still high contrast
    RectTransform careerTabs;
    int careerTab;   // 0 = overview, 1 = achievements

    /// <summary>Career dashboard: OVERVIEW (rank, XP, stats, career path, best XP per case) and ACHIEVEMENTS tabs.</summary>
    void BuildCareerPanel()
    {
        if (panels.ContainsKey("career")) return;
        var p = MakePanel("career", "CAREER PROFILE", 1600);
        careerTabs = UIKit.Rect("Tabs", p);
        var th = UIKit.Horizontal(careerTabs, 14, TextAnchor.MiddleLeft);
        th.childForceExpandWidth = false;
        UIKit.Size(careerTabs, -1, 60);
        careerPanelBody = UIKit.Rect("Body", p);
        UIKit.Size(careerPanelBody, -1, 660);
        FooterButtons(p, ("BACK", (UnityEngine.Events.UnityAction)ClosePanel, UIKit.HudVariant.Secondary));
    }

    /// <summary>The body's single vertical layout (created once; the tabs only change its spacing).</summary>
    void BodyLayout(float spacing)
    {
        var v = careerPanelBody.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = UIKit.Vertical(careerPanelBody, 0, spacing);
        v.spacing = spacing;
        v.childForceExpandHeight = false;
    }

    void ShowCareerTab(int tab)
    {
        if (careerTab == tab) return;
        careerTab = tab;
        FillCareerPanel();
    }

    void FillCareerPanel()
    {
        // Never wider than the screen (the canvas matches height, so 4:3 screens are only 1440 wide).
        var panel = (RectTransform)careerPanelBody.parent;
        float canvasW = root != null ? root.rect.width : 1920f;
        panel.sizeDelta = new Vector2(Mathf.Min(1600f, canvasW - 80f), panel.sizeDelta.y);
        float innerW = panel.sizeDelta.x - 104f;   // MakePanel's side padding

        foreach (Transform c in careerTabs) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        foreach (Transform c in careerPanelBody) { c.gameObject.SetActive(false); Destroy(c.gameObject); }

        int got = LocalDataManager.Achievements.Count, all = CareerSystem.Config.achievements.Count;
        Tab("OVERVIEW", 0);
        Tab($"ACHIEVEMENTS  {got}/{all}", 1);

        if (careerTab == 0) FillCareerOverview(innerW);
        else FillCareerAchievements(innerW);
    }

    void Tab(string label, int index)
    {
        bool on = careerTab == index;
        var b = UIKit.HudButton(careerTabs, (on ? "›  " : "") + label, () => ShowCareerTab(index), on ? UIKit.HudVariant.Primary : UIKit.HudVariant.Secondary, 22);
        b.name = "Tab_" + index;
        UIKit.Size(b, index == 0 ? 260 : 340, 56);
        AddFx(b);
    }

    void FillCareerOverview(float width)
    {
        BodyLayout(18f);
        int xp = CareerSystem.TotalXp;
        CareerSystem.ProgressInRank(xp, out int rank, out int into, out int span);
        var rk = CareerSystem.RankAt(rank);

        // 1) Rank header: insignia + rank | XP to next rank with a large bar
        var head = UIKit.Rect("RankHeader", careerPanelBody);
        UIKit.Size(head, -1, 196);
        UIKit.AddImage(head, UIKit.PanelRaised, rounded: true).raycastTarget = false;
        var badge = UIKit.Rect("Badge", head);
        UIKit.Place(badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(160, 160));
        UIKit.AddImage(badge, rk.color, RankBadge.For(rank)).preserveAspect = true;
        float leftW = Mathf.Max(420f, width * 0.5f - 210f);
        var rankLabel = UIKit.Text(head, "RankLabel", $"CURRENT RANK  ·  {rank + 1} OF {CareerSystem.Ranks.Count}", CpLabel, UIKit.Accent, TextAlignmentOptions.TopLeft, bold: true);
        rankLabel.characterSpacing = 3f;
        UIKit.Place(rankLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(208, -26), new Vector2(leftW, 28));
        var rankTitle = UIKit.Text(head, "RankTitle", rk.title, CpRank, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(rankTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(208, -58), new Vector2(leftW, 56));
        rankTitle.enableAutoSizing = true; rankTitle.fontSizeMin = 30f; rankTitle.fontSizeMax = CpRank;
        rankTitle.textWrappingMode = TextWrappingModes.NoWrap;
        var summary = UIKit.Text(head, "Summary", rk.summary, CpBody, CpMuted, TextAlignmentOptions.TopLeft);
        UIKit.Place(summary.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(208, -118), new Vector2(leftW, 64));

        float xpW = Mathf.Min(620f, width - leftW - 260f);
        var xpValue = UIKit.Text(head, "XpValue", span > 0 ? $"{into:N0} <size=60%><color={UIKit.HexSecondary}>/ {span:N0} XP</color></size>" : "MAX RANK",
                                 CpValue, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(xpValue.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -26), new Vector2(xpW, 46));
        var xpNext = UIKit.Text(head, "XpNext", span > 0 ? $"to <b>{CareerSystem.RankAt(rank + 1).title}</b>" : "You hold the highest rank in Scam Sense.",
                                CpBody, CpMuted, TextAlignmentOptions.TopLeft);
        UIKit.Place(xpNext.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -76), new Vector2(xpW, 32));
        xpNext.textWrappingMode = TextWrappingModes.NoWrap; xpNext.overflowMode = TextOverflowModes.Ellipsis;
        var bar = UIKit.Rect("XpBar", head);
        UIKit.Place(bar, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -120), new Vector2(xpW, 20));
        UIKit.AddImage(bar, new Color(1f, 1f, 1f, 0.12f), rounded: true).raycastTarget = false;
        var fill = UIKit.Rect("Fill", bar);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(span > 0 ? Mathf.Clamp01((float)into / span) : 1f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        UIKit.AddImage(fill, rk.color, rounded: true).raycastTarget = false;
        var total = UIKit.Text(head, "Total", $"<b>{xp:N0}</b> total career XP", CpLabel, CpMuted, TextAlignmentOptions.TopLeft);
        UIKit.Place(total.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -150), new Vector2(xpW, 28));

        // 2) Stat tiles
        float acc = CareerSystem.BestRunAccuracy;
        int cases = catalog != null ? catalog.Count : CareerSystem.Config.casesInGame;
        var tiles = UIKit.Rect("Stats", careerPanelBody);
        var tl = UIKit.Horizontal(tiles, 14);
        tl.childForceExpandWidth = true;
        UIKit.Size(tiles, -1, 104);
        StatTile(tiles, $"{xp:N0}", "TOTAL XP");
        StatTile(tiles, $"{CareerSystem.CasesSolved} / {cases}", "CASES SOLVED");
        StatTile(tiles, $"{CareerSystem.PerfectCases}", "PERFECT CASES");
        StatTile(tiles, acc >= 0f ? $"{Mathf.RoundToInt(acc * 100f)}%" : "-", "BEST-RUN ACCURACY");
        StatTile(tiles, $"{LocalDataManager.Achievements.Count} / {CareerSystem.Config.achievements.Count}", "ACHIEVEMENTS");

        // 3) Career path: every rank left to right
        UIKit.Header(careerPanelBody, "Career path", CpLabel).characterSpacing = 3f;
        var path = UIKit.Rect("CareerPath", careerPanelBody);
        var ph = UIKit.Horizontal(path, 8, TextAnchor.UpperCenter);
        ph.childForceExpandWidth = true;
        ph.childForceExpandHeight = false;
        UIKit.Size(path, -1, 182);
        for (int i = 0; i < CareerSystem.Ranks.Count; i++)
        {
            var r = CareerSystem.RankAt(i);
            bool reached = i <= rank, current = i == rank;
            var step = UIKit.Rect("Rank_" + i, path);
            UIKit.Size(step, 60, 182).flexibleWidth = 1f;
            if (current) UIKit.AddImage(step, new Color(UIKit.AccentDark.r, UIKit.AccentDark.g, UIKit.AccentDark.b, 0.45f), rounded: true).raycastTarget = false;
            var ib = UIKit.Rect("Badge", step);
            UIKit.Place(ib, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(60, 60));
            UIKit.AddImage(ib, reached ? r.color : new Color(0.5f, 0.56f, 0.64f, 0.75f), RankBadge.For(i)).preserveAspect = true;
            var t = UIKit.Text(step, "Title", r.title, 18, reached ? UIKit.TextPrimary : CpMuted, TextAlignmentOptions.Top, bold: current);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -74), new Vector2(160, 48));
            t.rectTransform.anchorMin = new Vector2(0, 1); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = new Vector2(4, -142); t.rectTransform.offsetMax = new Vector2(-4, -74);   // up to three lines
            t.enableAutoSizing = true; t.fontSizeMin = 15f; t.fontSizeMax = 18f;
            t.lineSpacing = -6f;
            string state = current ? "YOU ARE HERE" : reached ? "✓ EARNED" : $"{r.minXp:N0} XP";
            var s = UIKit.Text(step, "State", state, 16, current ? UIKit.Accent : reached ? UIKit.Safe : CpMuted, TextAlignmentOptions.Top, bold: true);
            s.rectTransform.anchorMin = new Vector2(0, 1); s.rectTransform.anchorMax = new Vector2(1, 1);
            s.rectTransform.offsetMin = new Vector2(2, -174); s.rectTransform.offsetMax = new Vector2(-2, -146);
            s.textWrappingMode = TextWrappingModes.NoWrap;
            s.enableAutoSizing = true; s.fontSizeMin = 12f; s.fontSizeMax = 16f;
        }

        // 4) Best XP per case
        if (catalog != null)
        {
            var row = UIKit.Rect("Cases", careerPanelBody);
            var rh = UIKit.Horizontal(row, 14);
            rh.childForceExpandWidth = true;
            UIKit.Size(row, -1, 56);
            foreach (var l in catalog.levels)
            {
                if (l == null) continue;
                int best = CareerSystem.CaseXp(LocalDataManager.Peek(l.levelId));
                var chip = UIKit.Rect("Case_" + l.levelNumber, row);
                UIKit.AddImage(chip, UIKit.PanelRaised, rounded: true).raycastTarget = false;
                var ct = UIKit.Text(chip, "Text", $"<color={UIKit.HexAccent}><b>{l.levelNumber:00}</b></color>  {l.displayName}   " +
                                    (best > 0 ? $"<b>{best} XP</b>" : $"<color={UIKit.HexSecondary}>not played</color>"),
                                    CpLabel, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft);
                UIKit.Stretch(ct.rectTransform);
                ct.margin = new Vector4(16, 0, 12, 0);
                ct.textWrappingMode = TextWrappingModes.NoWrap;
                ct.enableAutoSizing = true; ct.fontSizeMin = 15f; ct.fontSizeMax = CpLabel;
            }
        }
    }

    void StatTile(RectTransform parent, string value, string label)
    {
        var tile = UIKit.Rect("Stat_" + label, parent);
        UIKit.AddImage(tile, UIKit.PanelRaised, rounded: true).raycastTarget = false;
        var v = UIKit.Text(tile, "Value", value, CpValue, UIKit.TextPrimary, TextAlignmentOptions.Top, bold: true);
        v.rectTransform.anchorMin = new Vector2(0, 1); v.rectTransform.anchorMax = new Vector2(1, 1);
        v.rectTransform.offsetMin = new Vector2(8, -60); v.rectTransform.offsetMax = new Vector2(-8, -12);
        v.textWrappingMode = TextWrappingModes.NoWrap;
        var l = UIKit.Text(tile, "Label", label, 17, CpMuted, TextAlignmentOptions.Top, bold: true);
        l.characterSpacing = 2f;
        l.rectTransform.anchorMin = new Vector2(0, 1); l.rectTransform.anchorMax = new Vector2(1, 1);
        l.rectTransform.offsetMin = new Vector2(8, -94); l.rectTransform.offsetMax = new Vector2(-8, -66);
        l.textWrappingMode = TextWrappingModes.NoWrap;
        l.enableAutoSizing = true; l.fontSizeMin = 13f; l.fontSizeMax = 17f;
    }

    /// <summary>Achievement cards in two columns; scrolls (wheel/drag) only if the list outgrows the panel.</summary>
    void FillCareerAchievements(float width)
    {
        BodyLayout(12f);
        int got = LocalDataManager.Achievements.Count, all = CareerSystem.Config.achievements.Count;
        UIKit.Text(careerPanelBody, "Summary", $"<b>{got}</b> of <b>{all}</b> unlocked  ·  achievements come from what you do in cases and are kept until RESET PROGRESS.",
                   CpLabel, CpMuted);

        var viewport = UIKit.Rect("Viewport", careerPanelBody);
        UIKit.Size(viewport, -1, 600);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);   // catches wheel/drag between cards
        var content = UIKit.Rect("Cards", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        float cellW = Mathf.Floor((width - 16f) / 2f);
        grid.cellSize = new Vector2(cellW, 108f);
        grid.spacing = new Vector2(16f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // Unlocked first, then the ones in progress, then locked - in the configured order within each group.
        var defs = CareerSystem.Config.achievements;
        var ordered = defs.Where(a => LocalDataManager.HasAchievement(a.id))
                          .Concat(defs.Where(a => !LocalDataManager.HasAchievement(a.id) && CareerSystem.AchievementProgress(a.id, out int c, out _) && c > 0))
                          .Concat(defs.Where(a => !LocalDataManager.HasAchievement(a.id) && !(CareerSystem.AchievementProgress(a.id, out int c, out _) && c > 0)));
        foreach (var a in ordered) AchievementCard(content, a);
    }

    void AchievementCard(RectTransform parent, CareerConfig.Achievement a)
    {
        bool unlocked = LocalDataManager.HasAchievement(a.id);
        bool counted = CareerSystem.AchievementProgress(a.id, out int cur, out int target);
        bool inProgress = !unlocked && counted && cur > 0;

        var card = UIKit.Rect("Ach_" + a.id, parent);
        UIKit.AddImage(card, unlocked ? UIKit.PanelRaised : new Color(0.08f, 0.11f, 0.18f, 1f), rounded: true).raycastTarget = false;
        var edge = card.gameObject.AddComponent<Outline>();
        edge.effectColor = unlocked ? new Color(UIKit.Warning.r, UIKit.Warning.g, UIKit.Warning.b, 0.55f) : UIKit.Border;
        edge.effectDistance = new Vector2(2f, -2f);

        // Icon: gold star when unlocked, padlock when not (shape differs, not only colour).
        var icon = UIKit.Rect("Icon", card);
        UIKit.Place(icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(56, 56));
        UIKit.AddImage(icon, unlocked ? UIKit.Warning : new Color(0.62f, 0.68f, 0.76f, 1f), unlocked ? UIKit.StarSprite : PadlockSprite()).preserveAspect = true;

        var title = UIKit.Text(card, "Title", a.title, CpHeading, unlocked ? UIKit.TextPrimary : new Color32(0xE2, 0xE8, 0xF0, 255), TextAlignmentOptions.TopLeft, bold: true);
        title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = new Vector2(1, 1);
        title.rectTransform.offsetMin = new Vector2(94, -46); title.rectTransform.offsetMax = new Vector2(-190, -14);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        var desc = UIKit.Text(card, "Description", a.description, 20f, CpMuted, TextAlignmentOptions.TopLeft);
        desc.rectTransform.anchorMin = new Vector2(0, 0); desc.rectTransform.anchorMax = new Vector2(1, 1);
        desc.rectTransform.offsetMin = new Vector2(94, 10); desc.rectTransform.offsetMax = new Vector2(-24, -50);
        desc.enableAutoSizing = true; desc.fontSizeMin = 16f; desc.fontSizeMax = 20f;
        desc.lineSpacing = 0f;

        // Status, top right: words + symbol, plus a count where the achievement has one.
        string status = unlocked ? "✓  UNLOCKED" : inProgress ? "IN PROGRESS" : "LOCKED";
        Color statusColor = unlocked ? UIKit.Safe : inProgress ? UIKit.Accent : new Color(0.72f, 0.77f, 0.84f, 1f);
        var st = UIKit.Text(card, "Status", status, 18f, statusColor, TextAlignmentOptions.TopRight, bold: true);
        st.characterSpacing = 1.5f;
        UIKit.Place(st.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -16), new Vector2(170, 26));
        st.textWrappingMode = TextWrappingModes.NoWrap;
        if (inProgress)
        {
            var bar = UIKit.Rect("Progress", card);
            UIKit.Place(bar, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -44), new Vector2(150, 8));
            UIKit.AddImage(bar, new Color(1f, 1f, 1f, 0.12f)).raycastTarget = false;
            var f = UIKit.Rect("Fill", bar);
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(target > 0 ? Mathf.Clamp01((float)cur / target) : 0f, 1f);
            f.offsetMin = f.offsetMax = Vector2.zero;
            UIKit.AddImage(f, UIKit.Accent).raycastTarget = false;
            var count = UIKit.Text(card, "Count", $"{cur:N0} / {target:N0}", 18f, UIKit.TextPrimary, TextAlignmentOptions.TopRight, bold: true);
            UIKit.Place(count.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -58), new Vector2(170, 26));
            count.textWrappingMode = TextWrappingModes.NoWrap;
            desc.rectTransform.offsetMax = new Vector2(-190, -50);   // keep clear of the bar and count
        }
    }

    // ---------- 3D background (Level 1 room) ----------

    const string BackdropScene = "MenuBackdrop_Room1";
    bool backdropShown;

    /// <summary>
    /// Loads the Level 1 room backdrop (MenuBackdrop_Room1) additively behind the menu and fades the still shots
    /// out. If that scene isn't built or isn't in Build Settings, the still shots simply stay.
    /// The backdrop unloads by itself when a level loads (levels load as single scenes).
    /// </summary>
    IEnumerator LoadBackdrop()
    {
        if (!Application.CanStreamedLevelBeLoaded(BackdropScene)) yield break;
        if (!SceneManager.GetSceneByName(BackdropScene).isLoaded)
        {
            var op = SceneManager.LoadSceneAsync(BackdropScene, LoadSceneMode.Additive);
            if (op == null) yield break;
            while (!op.isDone) yield return null;
        }
        yield return null;
        if (MenuBackdrop.Active == null) { Debug.LogWarning("MainMenuUI: backdrop scene loaded without an active MenuBackdrop; keeping the still shots.", this); yield break; }

        backdropShown = true;
        var tint = root != null ? root.Find("Tint")?.GetComponent<Image>() : null;
        float tint0 = tint != null ? tint.color.a : 0f;
        float a0 = bgA != null ? bgA.color.a : 0f, b0 = bgB != null ? bgB.color.a : 0f;
        if (bgLayer != null) bgLayer.anchoredPosition = Vector2.zero;
        for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.4f);
            if (bgA != null) bgA.color = new Color(1, 1, 1, a0 * (1f - k));
            if (bgB != null) bgB.color = new Color(1, 1, 1, b0 * (1f - k));
            if (tint != null) tint.color = new Color(tint.color.r, tint.color.g, tint.color.b, Mathf.Lerp(tint0, BackdropTintAlpha, k));
            yield return null;
        }
        if (bgA != null) bgA.gameObject.SetActive(false);
        if (bgB != null) bgB.gameObject.SetActive(false);
        backdropTint = tint;   // from now on Update() dips it at each camera cut
    }

    const float BackdropTintAlpha = 0.34f;
    Image backdropTint;

    /// <summary>
    /// Cuts between backdrop shots: the Tint layer (behind all menu UI) darkens briefly, so the camera can jump
    /// to the next shot - even another room - without a visible jump. The UI itself never fades.
    /// </summary>
    void DipBackdropForCuts()
    {
        if (!backdropShown || backdropTint == null) return;
        var c = backdropTint.color;
        c.a = Mathf.Lerp(BackdropTintAlpha, 0.97f, MenuBackdrop.CutFade);
        backdropTint.color = c;
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
        if (bgA == null || backdropShown) return;   // the 3D room backdrop animates itself
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
        UIKit.Place(menuColumn, new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -350), new Vector2(520, 0));
        var col = UIKit.Vertical(menuColumn, 0, 12);
        col.childForceExpandWidth = false;   // each entry keeps its own width (QUIT GAME is narrower)
        UIKit.FitHeight(menuColumn);
        columnGroup = menuColumn.gameObject.AddComponent<CanvasGroup>();

        // One obvious first action: a large PLAY that says which case it opens. Then three quiet options, and QUIT
        // set apart at the bottom so it is never hit by accident.
        playButton = PlayMenuButton();
        Wire(playButton, Play);
        Gap(menuColumn, 14f);
        classroom = GetComponent<ClassroomUI>();
        if (classroom == null) classroom = gameObject.AddComponent<ClassroomUI>();
        Wire(MenuButton("CLASSROOM", ""), classroom.Open);
        Wire(MenuButton("SETTINGS", ""), OpenPanel, "settings");
        Wire(MenuButton("ABOUT", ""), OpenPanel, "about");
        Gap(menuColumn, 22f);
        Wire(MenuButton("QUIT GAME", "", quiet: true), OpenPanel, "quit");

        statusText = UIKit.Text(menuColumn, "Status", "", UIKit.SizeSmall, UIKit.Warning);
        statusText.gameObject.SetActive(false);

        // Footer: saved progress + offline note
        progressChip = UIKit.Text(root, "Progress", "", 22, UIKit.TextSecondary, TextAlignmentOptions.BottomLeft);
        var offline = UIKit.Text(root, "Offline", "Offline training  ·  all messages are fictional  ·  no real links",
                                 20, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.BottomRight);
        LayoutFooter(progressChip, offline);

        BuildFeed();

        // Panels
        panelDim = UIKit.Dimmer(root, 0.7f).gameObject.AddComponent<CanvasGroup>();
        panelDim.name = "PanelDim";
        Wire(panelDim.gameObject.AddComponent<Button>(), ClosePanel);   // click outside closes
        panelDim.gameObject.SetActive(false);
        panelHost = UIKit.Rect("Panels", root);
        UIKit.Stretch(panelHost);

        BuildSettings();
        BuildAbout();
        BuildQuit();
        BuildResetProgress();

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

    /// <summary>Height of the secondary menu buttons.</summary>
    const float MenuButtonHeight = 64f;

    /// <summary>
    /// The primary action: a large PLAY (idle light sweep every 5 s) with the case it opens underneath
    /// ("Case 2 · The Investigation"), so a first-time player knows exactly where to start.
    /// </summary>
    Button PlayMenuButton()
    {
        var b = UIKit.HudButton(menuColumn, "PLAY", null, UIKit.HudVariant.Primary, 44, 5f);
        b.name = "PLAY Button";
        UIKit.Size(b, 520, 124);
        var lbl = b.transform.Find("Label").GetComponent<TMP_Text>();
        lbl.alignment = TextAlignmentOptions.TopLeft;
        lbl.margin = new Vector4(56, 18, 20, 0);
        lbl.characterSpacing = 10f;
        playSub = UIKit.Text(b.transform, "Sub", "", 21, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.BottomLeft);
        UIKit.Stretch(playSub.rectTransform);
        playSub.margin = new Vector4(58, 0, 24, 20);
        playSub.textWrappingMode = TextWrappingModes.NoWrap;
        playSub.overflowMode = TextOverflowModes.Ellipsis;
        var arrow = UIKit.Text(b.transform, "Arrow", "▶", 34, Color.white, TextAlignmentOptions.MidlineRight);
        UIKit.Stretch(arrow.rectTransform);
        arrow.margin = new Vector4(0, 0, 34, 0);
        AddFx(b);
        return b;
    }

    /// <summary>A secondary menu entry; <paramref name="quiet"/> = smaller and dimmer (QUIT GAME).</summary>
    Button MenuButton(string label, string subLabel, bool quiet = false)
    {
        var b = UIKit.HudButton(menuColumn, label, null, UIKit.HudVariant.Secondary, quiet ? 22 : 26);
        UIKit.Size(b, quiet ? 340 : 520, quiet ? 54 : MenuButtonHeight);
        var lbl = b.transform.Find("Label").GetComponent<TMP_Text>();
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        lbl.margin = new Vector4(52, 0, 20, 0);
        lbl.characterSpacing = 4f;
        if (quiet) lbl.color = UIKit.TextSecondary;

        var sub = UIKit.Text(b.transform, "Sub", subLabel, 20, UIKit.TextSecondary, TextAlignmentOptions.MidlineRight);
        UIKit.Stretch(sub.rectTransform);
        sub.margin = new Vector4(20, 0, 26, 0);
        sub.textWrappingMode = TextWrappingModes.NoWrap;
        AddFx(b);
        return b;
    }

    static void Gap(RectTransform parent, float height)
    {
        var g = UIKit.Rect("Gap", parent);
        UIKit.Size(g, -1, height);
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
        UIKit.TintPanel(p, UIKit.PopupTone.Warning);   // a confirmation: amber strip and border
        var q = UIKit.IconHeader(p, UIKit.PopupTone.Warning, out _, 64f);
        UIKit.Text(q, "Question", "Are you sure you want to quit Scam Sense?", UIKit.SizeBody, UIKit.TextPrimary);
        UIKit.Text(p, "Saved", "Your scores, stars and unlocked rooms are saved.", UIKit.SizeSmall, UIKit.TextSecondary);
        FooterButtons(p,
            ("CANCEL", (UnityEngine.Events.UnityAction)ClosePanel, UIKit.HudVariant.Secondary),
            ("YES, QUIT", (UnityEngine.Events.UnityAction)QuitGame, UIKit.HudVariant.Danger));
    }

    // ---------- Reset progress (Settings > Saved progress) ----------

    const string ResetQuestion = "Are you sure you want to reset your game progress? Your completed levels, scores, stars, " +
                                 "and unlocked levels will be reset. This action cannot be undone.";
    bool resetting;

    /// <summary>
    /// Adds a "Saved progress" section with RESET PROGRESS to Settings, plus the confirmation and result panels.
    /// Built with the rest of the menu; a menu baked into the scene before this existed gets it at runtime.
    /// </summary>
    void BuildResetProgress()
    {
        if (!panels.TryGetValue("settings", out var settings) || settings == null) return;

        var section = UIKit.Rect("SavedProgress", settings);
        var footer = settings.Find("Footer");
        if (footer != null) section.SetSiblingIndex(footer.GetSiblingIndex());   // above RESET TO DEFAULTS / APPLY / BACK
        UIKit.Vertical(section, 0, 8);
        UIKit.Header(section, "Saved progress");
        var row = UIKit.Rect("Row", section);
        var h = UIKit.Horizontal(row, 24, TextAnchor.MiddleLeft);
        h.childForceExpandWidth = false;
        UIKit.Size(row, -1, 64);
        var info = UIKit.Text(row, "Info", "Start every room again from the beginning. Your settings and account are kept.",
                              20, UIKit.TextSecondary, TextAlignmentOptions.MidlineLeft);
        UIKit.Size(info, 600, 64).flexibleWidth = 1f;
        var open = UIKit.HudButton(row, "RESET PROGRESS", null, UIKit.HudVariant.Danger, 22);   // red: never mistaken for PLAY
        open.name = "ResetProgressButton";
        UIKit.Size(open, 330, 60);
        Wire(open, OpenPanel, "resetprogress");
        AddFx(open);

        var p = MakePanel("resetprogress", "RESET PROGRESS", 860);
        UIKit.TintPanel(p, UIKit.PopupTone.Error);   // destructive and cannot be undone: red strip and border
        var q = UIKit.IconHeader(p, UIKit.PopupTone.Error, out _, 64f);
        UIKit.Text(q, "Question", ResetQuestion, UIKit.SizeBody, UIKit.TextPrimary);
        resetScopeText = UIKit.Text(p, "Scope", "", UIKit.SizeSmall, UIKit.TextSecondary);
        var buttons = UIKit.Rect("Footer", p);
        UIKit.Horizontal(buttons, 16);
        UIKit.Size(buttons, -1, 72);
        resetCancelButton = UIKit.HudButton(buttons, "CANCEL", null, UIKit.HudVariant.Secondary, 26);
        Wire(resetCancelButton, BackToSettings);
        AddFx(resetCancelButton);
        resetConfirmButton = UIKit.HudButton(buttons, "RESET EVERYTHING", null, UIKit.HudVariant.Danger, 26);
        Wire(resetConfirmButton, ConfirmResetProgress);
        AddFx(resetConfirmButton);

        var r = MakePanel("resetresult", "PROGRESS RESET SUCCESSFULLY", 860);
        resetResultTitle = r.Find("Header/Title").GetComponent<TMP_Text>();
        resetResultBody = UIKit.Text(r, "Message", "", UIKit.SizeBody, UIKit.TextPrimary);
        FooterButtons(r, ("OK", (UnityEngine.Events.UnityAction)BackToSettings, UIKit.HudVariant.Primary));
    }

    void PrepareResetProgress()
    {
        resetting = false;
        UIKit.SetInteractable(resetConfirmButton, true);
        UIKit.SetInteractable(resetCancelButton, true);
        if (resetScopeText != null) resetScopeText.text = ResetScope();
    }

    /// <summary>Which save is affected and what happens to the cloud copy (shown before confirming).</summary>
    static string ResetScope()
    {
        var cloud = CloudAccountService.Instance;
        if (cloud.SyncsActiveSave)
            return $"This resets the progress of <b>{cloud.DisplayName}</b> on this PC and in its cloud save. " +
                   "Your account, your settings and other players' progress are kept.";
        if (!string.IsNullOrEmpty(LocalDataManager.ProfileId))
            return $"This resets the progress of <b>{cloud.DisplayName}</b> on this PC now; its cloud save is updated the next " +
                   "time you're online. Your account, your settings and other players' progress are kept.";
        return "This resets the progress saved on this PC. Your settings are kept.";
    }

    /// <summary>CANCEL / OK: back to Settings. Nothing is changed by cancelling.</summary>
    public void BackToSettings() => OpenPanel("settings");

    /// <summary>RESET EVERYTHING: resets through LocalDataManager, then shows what actually happened.</summary>
    public void ConfirmResetProgress()
    {
        if (resetting) return;   // one reset per confirmation
        resetting = true;
        UIKit.SetInteractable(resetConfirmButton, false);
        UIKit.SetInteractable(resetCancelButton, false);

        bool ok = LocalDataManager.ResetProgress(out string error);
        RefreshProgress();   // Continue, saved-progress line; level cards rebuild when Level Selection opens

        var cloud = CloudAccountService.Instance;
        if (ok)
        {
            resetResultTitle.text = "PROGRESS RESET SUCCESSFULLY";
            resetResultBody.text = "Your game progress has been reset. You can start your investigation again." +
                (cloud.SyncsActiveSave ? $"\n<size=80%><color={UIKit.HexSecondary}>Your cloud save is being updated - the account status shows SYNCING, then ONLINE.</color></size>"
                 : !string.IsNullOrEmpty(LocalDataManager.ProfileId) ? $"\n<size=80%><color={UIKit.HexSecondary}>Your cloud save will be updated the next time you're online.</color></size>"
                 : "");
        }
        else
        {
            resetResultTitle.text = "RESET FAILED";
            resetResultBody.text = error;
        }
        OpenPanel("resetresult");
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
