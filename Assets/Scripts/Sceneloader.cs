using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scam Sense loading screen. Builds its own UI at runtime (UIKit: the same navy panels, cyan accent and Inter
/// font as the menu, briefing and HUD) and lives across scenes.
///   SceneLoader.LoadScene("Room2_InvestigationOffice");
///   SceneLoader.Transition(() => { ... });
///
/// While a level loads it shows that room's own menu preview as the backdrop, the case number, name and
/// description, a status line and a progress bar that follow the real async load, and rotating investigator
/// tips. The menu and every level register their LevelDefinitions (<see cref="RegisterLevels"/>), so the screen
/// knows which room is coming; an unknown scene gets a neutral headquarters screen.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Timing")]
    public float fadeDuration = 0.45f;
    [Tooltip("The screen stays at least this long, so the case and one tip can be read. The bar never runs ahead of the real load.")]
    public float minimumLoadingTime = 2.5f;
    [Tooltip("Seconds between investigator tips while the screen is up.")]
    public float tipInterval = 7f;

    [Header("Work with the scene's FadeOverlay")]
    public bool waitForSceneFadeOverlay = true;
    public string fadeOverlayName = "FadeOverlay";
    [Tooltip("Max seconds to wait for the overlay to reach black.")]
    public float fadeOverlayTimeout = 2f;

    [Header("Investigator tips (rotated while loading)")]
    public string[] tips =
    {
        "Read the sender's full address, not just the display name. A familiar name is easy to fake.",
        "Urgency is a tactic. \"Act within 24 hours\" is written to stop you from checking.",
        "Before you click, check where a link really goes. A look-alike domain is a trap.",
        "No real bank, school or IT team asks for your password or a one-time code by message.",
        "Unexpected prizes, refunds or grants that need a fee are scams.",
        "A file ending in .exe is a program, not a document - and never click \"Enable Content\" on a surprise file.",
        "Changed bank details by email? Call the office on a number you already know.",
        "Genuine messages match something you did and ask for nothing risky.",
        "Gift cards and crypto as payment are a major red flag.",
        "When in doubt, open the official app or website yourself instead of using the message's link.",
    };

    const string MenuSceneName = "Menu";
    static readonly Dictionary<string, LevelDefinition> levelsByScene = new Dictionary<string, LevelDefinition>();

    CanvasGroup group;
    GameObject content;
    Image preview;
    AspectRatioFitter previewFit;
    RectTransform layoutRoot, caseCard, tipCard, scanLine, barRoot, barFill, barSheen;
    Image statusDot;
    TMP_Text caseKicker, caseTitle, caseDescription, statusText, tipHeader, tipText;
    CanvasGroup tipGroup;
    int tipIndex = -1;
    float progressShown;
    bool busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("SceneLoader").AddComponent<SceneLoader>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    // ---------------- Public API ----------------

    /// <summary>Tells the loading screen which room each scene is (the menu registers the whole catalog, each level itself).</summary>
    public static void RegisterLevels(IEnumerable<LevelDefinition> levels)
    {
        if (levels == null) return;
        foreach (var l in levels) RegisterLevel(l);
    }

    public static void RegisterLevel(LevelDefinition level)
    {
        if (level != null && !string.IsNullOrEmpty(level.sceneName)) levelsByScene[level.sceneName] = level;
    }

    public static void LoadScene(string sceneName)
    {
        if (Instance == null || Instance.busy) return;
        Instance.StartCoroutine(Instance.LoadRoutine(sceneName));
    }

    public static void Transition(Action whileHidden, bool showLoading = true)
    {
        if (Instance == null || Instance.busy) return;
        Instance.StartCoroutine(Instance.TransitionRoutine(whileHidden, showLoading));
    }

    // ---------------- Routines ----------------

    IEnumerator LoadRoutine(string sceneName)
    {
        busy = true;
        bool toMenu = sceneName == MenuSceneName;
        group.blocksRaycasts = true;       // block menu clicks right away
        ShowCase(sceneName);
        SetStatus(toMenu ? "RETURNING TO HEADQUARTERS" : "INITIALIZING INVESTIGATION");
        SetProgress(0f);
        barRoot.gameObject.SetActive(true);
        content.SetActive(true);
        NextTip();

        // 1) Let the scene's FadeOverlay play first (screen goes black), 2) then fade the loading screen in.
        yield return WaitForSceneFadeOverlay();
        yield return Fade(1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            // Not in the build: say so instead of hanging on a frozen screen, then hand control back.
            Debug.LogError($"SceneLoader: scene '{sceneName}' could not be loaded (is it in the Build Profile scene list?).");
            SetStatus("COULD NOT OPEN THIS CASE");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Fade(0f);
            content.SetActive(false);
            busy = false;
            yield break;
        }
        op.allowSceneActivation = false;

        // The bar follows the real load (Unity reports 0-0.9 until the scene is ready to switch on) and never
        // runs ahead of it; the minimum time only keeps the screen up long enough to read the case.
        float elapsed = 0f;
        while (op.progress < 0.9f || elapsed < minimumLoadingTime || progressShown < 0.999f)
        {
            elapsed += Time.unscaledDeltaTime;
            float real = Mathf.Clamp01(op.progress / 0.9f);
            SetProgress(Mathf.MoveTowards(progressShown, real, Time.unscaledDeltaTime * 2.5f));
            SetStatus(op.progress < 0.9f ? (toMenu ? "LOADING MAIN MENU" : "LOADING INVESTIGATION ENVIRONMENT")
                                         : (toMenu ? "MAIN MENU READY" : "CASE FILE READY"));
            Animate();
            yield return null;
        }

        SetStatus(toMenu ? "OPENING MAIN MENU" : "ENTERING INVESTIGATION ROOM");
        Time.timeScale = 1f;
        op.allowSceneActivation = true;
        while (!op.isDone) { Animate(); yield return null; }
        yield return null;

        yield return Fade(0f);
        content.SetActive(false);
        busy = false;
    }

    IEnumerator TransitionRoutine(Action whileHidden, bool showLoading)
    {
        busy = true;
        group.blocksRaycasts = true;
        ShowCase(null);
        SetStatus("PREPARING CASE FILE");
        barRoot.gameObject.SetActive(false);   // nothing is loaded here, so there is no real progress to show
        content.SetActive(showLoading);
        NextTip();

        yield return WaitForSceneFadeOverlay();
        yield return Fade(1f);

        whileHidden?.Invoke();
        yield return null;

        if (showLoading)
            for (float t = 0f; t < minimumLoadingTime; t += Time.unscaledDeltaTime) { Animate(); yield return null; }

        yield return Fade(0f);
        content.SetActive(false);
        busy = false;
    }

    /// <summary>Waits until the scene's FadeOverlay is (almost) fully opaque, or times out.</summary>
    IEnumerator WaitForSceneFadeOverlay()
    {
        if (!waitForSceneFadeOverlay) yield break;

        GameObject go = GameObject.Find(fadeOverlayName);
        if (go == null) yield break;       // no overlay in this scene, nothing to wait for

        var cg = go.GetComponent<CanvasGroup>();
        var gfx = go.GetComponent<Graphic>();

        for (float t = 0f; t < fadeOverlayTimeout; t += Time.unscaledDeltaTime)
        {
            float a = 1f;
            if (cg != null) a = cg.alpha;
            else if (gfx != null) a = gfx.color.a;

            if (a >= 0.98f && t > 0.05f) break;
            yield return null;
        }
        yield return null;
    }

    IEnumerator Fade(float to)
    {
        float from = group.alpha;
        group.blocksRaycasts = true;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / fadeDuration));
            if (content.activeSelf) Animate();
            yield return null;
        }
        group.alpha = to;
        group.blocksRaycasts = to > 0.5f;
    }

    // ---------------- Content ----------------

    /// <summary>The destination room's case card and backdrop (or the headquarters card for the menu / unknown scenes).</summary>
    void ShowCase(string sceneName)
    {
        LevelDefinition level = null;
        if (sceneName != null) levelsByScene.TryGetValue(sceneName, out level);
        Sprite art = level != null ? level.previewImage : sceneName == MenuSceneName ? FirstRoomPreview() : null;
        preview.sprite = art;
        preview.enabled = art != null;
        if (art != null) previewFit.aspectRatio = art.rect.width / art.rect.height;

        if (level != null)
        {
            caseKicker.text = $"CASE {level.levelNumber:00}   ·   NOW OPENING";
            caseTitle.text = level.displayName;
            caseDescription.text = level.description;
        }
        else if (sceneName == MenuSceneName)
        {
            caseKicker.text = "CYBERCRIME INVESTIGATION UNIT";
            caseTitle.text = "Headquarters";
            caseDescription.text = "Returning to the main menu. Your scores, stars and unlocked rooms are saved.";
        }
        else
        {
            caseKicker.text = "CYBERCRIME INVESTIGATION UNIT";
            caseTitle.text = "Next case";
            caseDescription.text = "Preparing the investigation room.";
        }
    }

    /// <summary>Preview of the lowest-numbered registered room (Room 1, which the menu's backdrop also starts on).</summary>
    static Sprite FirstRoomPreview()
    {
        LevelDefinition first = null;
        foreach (var l in levelsByScene.Values)
            if (l != null && l.previewImage != null && (first == null || l.levelNumber < first.levelNumber)) first = l;
        return first != null ? first.previewImage : null;
    }

    void SetStatus(string s)
    {
        if (statusText.text != s) statusText.text = s;
    }

    void SetProgress(float p)
    {
        progressShown = Mathf.Clamp01(p);
        barFill.anchorMax = new Vector2(progressShown, 1f);
    }

    void NextTip()
    {
        if (tips == null || tips.Length == 0) { tipText.text = ""; tipHeader.text = ""; return; }
        int next = tips.Length == 1 ? 0 : (tipIndex + 1 + UnityEngine.Random.Range(0, tips.Length - 1)) % tips.Length;
        tipIndex = next;
        tipText.text = tips[next];
        tipHeader.text = $"INVESTIGATOR TIP   ·   {next + 1:00}";
        tipTimer = 0f;
    }

    float tipTimer;

    /// <summary>
    /// Wide screens: tip card bottom right. Narrow (4:3, 5:4) screens don't have room for both cards side by side,
    /// so the tip sits above the case card instead.
    /// </summary>
    void LayoutCards()
    {
        bool wide = layoutRoot.rect.width >= caseCard.sizeDelta.x + tipCard.sizeDelta.x + 120f * 2f + 60f;
        if (wide)
        {
            tipCard.anchorMin = tipCard.anchorMax = tipCard.pivot = new Vector2(1, 0);
            tipCard.anchoredPosition = new Vector2(-120, 110);
        }
        else
        {
            tipCard.anchorMin = tipCard.anchorMax = tipCard.pivot = new Vector2(0, 0);
            tipCard.anchoredPosition = new Vector2(120, 110 + caseCard.sizeDelta.y + 24);
        }
    }

    /// <summary>Quiet motion: a slow scan line over the room, a blinking status light, a sheen on the bar, tips that change.</summary>
    void Animate()
    {
        LayoutCards();
        float t = Time.unscaledTime;
        float h = ((RectTransform)scanLine.parent).rect.height;
        scanLine.anchoredPosition = new Vector2(0f, -Mathf.Repeat(t * 0.22f, 1f) * h);
        statusDot.color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 3f)));
        float w = barRoot.rect.width;
        barSheen.anchoredPosition = new Vector2(Mathf.Repeat(t * 0.6f, 1.4f) * w - 0.2f * w, 0f);

        tipTimer += Time.unscaledDeltaTime;
        if (tipInterval > 0f && tipTimer >= tipInterval) StartCoroutine(SwapTip());
    }

    IEnumerator SwapTip()
    {
        tipTimer = -1f;   // not again while this runs
        for (float k = 0f; k < 0.25f; k += Time.unscaledDeltaTime) { tipGroup.alpha = 1f - k / 0.25f; yield return null; }
        NextTip();
        for (float k = 0f; k < 0.25f; k += Time.unscaledDeltaTime) { tipGroup.alpha = k / 0.25f; yield return null; }
        tipGroup.alpha = 1f;
    }

    // ---------------- UI ----------------

    static Sprite gradientH, gradientV;

    /// <summary>White-to-clear gradient (opaque at the left, or at the bottom), tinted by the Image colour.</summary>
    static Sprite Gradient(bool horizontal)
    {
        if (horizontal && gradientH != null) return gradientH;
        if (!horizontal && gradientV != null) return gradientV;
        const int n = 128;
        var tex = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.SmoothStep(1f, 0f, i / (n - 1f));
            tex.SetPixel(horizontal ? i : 0, horizontal ? 0 : i, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        if (horizontal) gradientH = sprite; else gradientV = sprite;
        return sprite;
    }

    void BuildUI()
    {
        var root = UIKit.CreateCanvas(transform, "Loading Canvas", 999);   // above every menu and HUD
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // Solid navy behind everything (also what fades to and from black between scenes)
        var bg = UIKit.Rect("Background", root);
        UIKit.Stretch(bg);
        UIKit.AddImage(bg, UIKit.Background);

        content = UIKit.Rect("Content", root).gameObject;
        var c = (RectTransform)content.transform;
        UIKit.Stretch(c);

        // The destination room itself (its menu preview), covering the screen without stretching
        var art = UIKit.Rect("RoomPreview", c);
        art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
        art.anchoredPosition = Vector2.zero;
        preview = art.gameObject.AddComponent<Image>();
        preview.raycastTarget = false;
        previewFit = art.gameObject.AddComponent<AspectRatioFitter>();
        previewFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        previewFit.aspectRatio = 16f / 9f;

        // Navy grade so text reads: an even tint, plus soft gradients from the left (title, case card) and the
        // bottom (cards) - no hard edges over the room.
        Color navy = UIKit.Background;
        var grade = UIKit.Rect("Grade", c);
        UIKit.Stretch(grade);
        UIKit.AddImage(grade, new Color(navy.r, navy.g, navy.b, 0.5f)).raycastTarget = false;
        var left = UIKit.Rect("LeftShade", c);
        left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.75f, 1);
        left.offsetMin = left.offsetMax = Vector2.zero;
        UIKit.AddImage(left, new Color(navy.r, navy.g, navy.b, 0.75f), Gradient(true)).raycastTarget = false;
        var bottom = UIKit.Rect("BottomShade", c);
        bottom.anchorMin = new Vector2(0, 0); bottom.anchorMax = new Vector2(1, 0.55f);
        bottom.offsetMin = bottom.offsetMax = Vector2.zero;
        UIKit.AddImage(bottom, new Color(navy.r, navy.g, navy.b, 0.7f), Gradient(false)).raycastTarget = false;

        // A slow, faint scan line across the room (the only "digital" effect; no neon)
        var scanArea = UIKit.Rect("ScanArea", c);
        UIKit.Stretch(scanArea);
        scanArea.gameObject.AddComponent<RectMask2D>();
        scanLine = UIKit.Rect("ScanLine", scanArea);
        scanLine.anchorMin = new Vector2(0, 1); scanLine.anchorMax = new Vector2(1, 1); scanLine.pivot = new Vector2(0.5f, 1f);
        scanLine.sizeDelta = new Vector2(0, 2);
        UIKit.AddImage(scanLine, new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.16f)).raycastTarget = false;
        var glow = UIKit.Rect("Glow", scanLine);
        glow.anchorMin = new Vector2(0, 1); glow.anchorMax = new Vector2(1, 1); glow.pivot = new Vector2(0.5f, 0f);
        glow.sizeDelta = new Vector2(0, 140);
        UIKit.AddImage(glow, new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.05f), Gradient(false)).raycastTarget = false;   // soft trail behind the line

        // Title block - the same as the main menu's
        var title = UIKit.Rect("Title", c);
        UIKit.Place(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -90), new Vector2(900, 190));
        var big = UIKit.Text(title, "Name", "SCAM SENSE", 84, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        big.characterSpacing = 12f;
        big.textWrappingMode = TextWrappingModes.NoWrap;
        UIKit.Place(big.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(900, 100));
        var line = UIKit.Rect("Line", title);
        UIKit.Place(line, new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -104), new Vector2(360, 4));
        UIKit.AddImage(line, UIKit.Accent).raycastTarget = false;
        var tag = UIKit.Text(title, "Tagline", "THINK BEFORE YOU CLICK", 26, UIKit.Accent, TextAlignmentOptions.TopLeft, bold: true);
        tag.characterSpacing = 10f;
        UIKit.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -122), new Vector2(900, 36));

        // Case card (bottom left): which case is opening, its status and the real load progress
        layoutRoot = c;
        var card = UIKit.Rect("CaseCard", c);
        caseCard = card;
        UIKit.Place(card, new Vector2(0, 0), new Vector2(0, 0), new Vector2(120, 110), new Vector2(860, 300));
        UIKit.PanelImage(card, new Color(UIKit.Panel.r, UIKit.Panel.g, UIKit.Panel.b, 0.92f));
        UIKit.AccentStrip(card, null, 6f);
        caseKicker = UIKit.Text(card, "Kicker", "", 20, UIKit.Accent, TextAlignmentOptions.TopLeft, bold: true);
        caseKicker.characterSpacing = 4f;
        UIKit.Place(caseKicker.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -30), new Vector2(780, 30));
        caseTitle = UIKit.Text(card, "CaseTitle", "", 46, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        caseTitle.textWrappingMode = TextWrappingModes.NoWrap;
        caseTitle.enableAutoSizing = true;   // long case names shrink to fit instead of being cut off
        caseTitle.fontSizeMin = 30f;
        caseTitle.fontSizeMax = 46f;
        caseTitle.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Place(caseTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -64), new Vector2(780, 60));
        caseDescription = UIKit.Text(card, "Description", "", 22, UIKit.TextSecondary, TextAlignmentOptions.TopLeft);
        caseDescription.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Place(caseDescription.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -128), new Vector2(780, 62));

        var dot = UIKit.Rect("StatusDot", card);
        UIKit.Place(dot, new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(44, 84), new Vector2(14, 14));
        statusDot = UIKit.AddImage(dot, UIKit.Accent, UIKit.CircleSprite);
        statusDot.raycastTarget = false;
        statusText = UIKit.Text(card, "Status", "", 20, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, bold: true);
        statusText.characterSpacing = 3f;
        UIKit.Place(statusText.rectTransform, new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(70, 84), new Vector2(740, 30));

        barRoot = UIKit.Rect("ProgressBar", card);
        UIKit.Place(barRoot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(44, 40), new Vector2(772, 10));
        UIKit.AddImage(barRoot, UIKit.PanelRaised).raycastTarget = false;
        barRoot.gameObject.AddComponent<RectMask2D>();
        barFill = UIKit.Rect("Fill", barRoot);
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0, 1);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        UIKit.AddImage(barFill, UIKit.Accent).raycastTarget = false;
        barSheen = UIKit.Rect("Sheen", barFill);
        barSheen.anchorMin = new Vector2(0, 0); barSheen.anchorMax = new Vector2(0, 1); barSheen.pivot = new Vector2(0, 0.5f);
        barSheen.sizeDelta = new Vector2(120, 0);
        UIKit.AddImage(barSheen, new Color(1f, 1f, 1f, 0.35f)).raycastTarget = false;
        barFill.gameObject.AddComponent<RectMask2D>();

        // Investigator tip (bottom right), in the style of the game's notification cards
        var tip = UIKit.Rect("TipCard", c);
        tipCard = tip;
        UIKit.Place(tip, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-120, 110), new Vector2(640, 200));
        UIKit.PanelImage(tip, new Color(UIKit.Panel.r, UIKit.Panel.g, UIKit.Panel.b, 0.92f));
        UIKit.AccentStrip(tip, UIKit.Warning, 6f);
        tipGroup = tip.gameObject.AddComponent<CanvasGroup>();
        tipHeader = UIKit.Text(tip, "TipHeader", "", 18, UIKit.Warning, TextAlignmentOptions.TopLeft, bold: true);
        tipHeader.characterSpacing = 4f;
        UIKit.Place(tipHeader.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -28), new Vector2(570, 28));
        tipText = UIKit.Text(tip, "TipText", "", 25, UIKit.TextPrimary, TextAlignmentOptions.TopLeft);
        tipText.lineSpacing = 6f;
        tipText.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Place(tipText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -64), new Vector2(570, 120));

        content.SetActive(false);
    }
}
