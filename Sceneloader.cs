using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scam Sense loading screen. Builds its own UI at runtime.
///   SceneLoader.LoadScene("Game");
///   SceneLoader.Transition(() => { ... });
///
/// Assets (put them in Assets/Resources/):
///   GoldropspersonaluseRegular-9Ynn5.otf  -> your font (loaded automatically)
///   LoadingBackground                     -> your menu art (optional, Texture Type: Sprite (2D and UI))
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Timing")]
    public float fadeDuration = 0.5f;
    [Tooltip("Long enough to read one tip.")]
    public float minimumLoadingTime = 3f;
    public float barSpeed = 1.5f;

    [Header("Work with the scene's FadeOverlay")]
    public bool waitForSceneFadeOverlay = true;
    public string fadeOverlayName = "FadeOverlay";
    [Tooltip("Max seconds to wait for the overlay to reach black.")]
    public float fadeOverlayTimeout = 2f;

    [Header("Font")]
    [Tooltip("File name (no extension) of the .otf/.ttf in Assets/Resources")]
    public string fontResourceName = "GoldropspersonaluseRegular-9Ynn5";

    [Header("Look (Scam Sense palette)")]
    public Color backgroundColor = new Color32(0x1A, 0x12, 0x10, 255);
    public Color paperColor      = new Color32(0xE8, 0xDC, 0xC0, 255);
    public Color alertRed        = new Color32(0xB8, 0x28, 0x1E, 255);
    public Color amber           = new Color32(0xE8, 0xA0, 0x30, 255);
    public Color borderColor     = new Color32(0x4A, 0x3A, 0x30, 255);
    public Color inkColor        = new Color32(0x2A, 0x1F, 0x18, 255);
    [Range(0f, 1f)] public float backgroundDim = 0.55f;

    [Header("Tips (shown randomly)")]
    public string[] tips =
    {
        "Urgency is a weapon. \"Act within 24 hrs!\" is designed to stop you from thinking.",
        "Check the sender's real email address, not just the display name.",
        "Hover before you click. If the link doesn't match the claimed site, it's a trap.",
        "Banks never ask for your PIN, OTP, or password by text or call.",
        "Never share your OTP. Not with \"support\", not with anyone.",
        "Too good to be true? A free phone or instant prize is bait.",
        "Short links hide the real destination. Treat them with suspicion.",
        "Surprise parcel fee? Check directly with the courier, not the text.",
        "Scammers copy logos perfectly. Look at the URL instead.",
        "Gift cards or crypto as payment? Big red flag.",
        "\"Your account is locked\"? Hang up and call the official hotline.",
        "Report and block. Your report may protect the next victim."
    };

    CanvasGroup group;
    GameObject loadingRoot;
    RectTransform barFill;
    TMP_Text loadingText;
    TMP_Text tipText;
    TMP_Text tipHeader;
    TMP_FontAsset font;
    readonly List<TMP_Text> allTexts = new List<TMP_Text>();
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
        ApplyMenuFont();                   // fallback: copy the menu's font if ours failed to load
        group.blocksRaycasts = true;       // block menu clicks right away
        loadingRoot.SetActive(true);
        PickTip();
        SetProgress(0f);

        // 1) Let the scene's FadeOverlay play first (screen goes black)
        yield return WaitForSceneFadeOverlay();
        // 2) Then fade our loading screen in over the black
        yield return Fade(1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float elapsed = 0f;
        float shown = 0f;

        while (op.progress < 0.9f || shown < 1f)
        {
            elapsed += Time.unscaledDeltaTime;
            float timeTarget = minimumLoadingTime > 0f ? elapsed / minimumLoadingTime : 1f;
            float target = Mathf.Min(op.progress / 0.9f, timeTarget);
            shown = Mathf.MoveTowards(shown, Mathf.Clamp01(target), Time.unscaledDeltaTime * barSpeed);
            SetProgress(shown);
            yield return null;
        }

        SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.25f);

        Time.timeScale = 1f;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;

        yield return Fade(0f);
        loadingRoot.SetActive(false);
        busy = false;
    }

    IEnumerator TransitionRoutine(Action whileHidden, bool showLoading)
    {
        busy = true;
        ApplyMenuFont();
        group.blocksRaycasts = true;
        loadingRoot.SetActive(showLoading);
        PickTip();
        SetProgress(0f);

        yield return WaitForSceneFadeOverlay();
        yield return Fade(1f);

        whileHidden?.Invoke();
        yield return null;

        if (showLoading)
        {
            for (float t = 0f; t < minimumLoadingTime; t += Time.unscaledDeltaTime)
            {
                SetProgress(t / minimumLoadingTime);
                yield return null;
            }
            SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        yield return Fade(0f);
        loadingRoot.SetActive(false);
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
            yield return null;
        }
        group.alpha = to;
        group.blocksRaycasts = to > 0.5f;
    }

    // ---------------- Font ----------------

    /// <summary>Loads a TMP Font Asset, or builds one at runtime from a raw .otf/.ttf in Resources.</summary>
    TMP_FontAsset LoadFont()
    {
        var prebuilt = Resources.Load<TMP_FontAsset>(fontResourceName);
        if (prebuilt != null) return prebuilt;

        var raw = Resources.Load<Font>(fontResourceName);
        if (raw == null)
        {
            Debug.LogWarning("SceneLoader: font '" + fontResourceName + "' not found in a Resources folder.");
            return null;
        }

        var fa = TMP_FontAsset.CreateFontAsset(raw);
        if (fa == null) return null;

        fa.name = fontResourceName + " (runtime)";

        // If the font is missing a character, fall back to TMP's default so you don't get boxes
        if (fa.fallbackFontAssetTable == null)
            fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (TMP_Settings.defaultFontAsset != null)
            fa.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);

        return fa;
    }

    /// <summary>Only used if LoadFont() found nothing: copies the font from the menu's TMP text.</summary>
    void ApplyMenuFont()
    {
        if (font == null)
        {
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.transform.IsChildOf(transform)) continue;   // skip our own texts
                if (t.font == null) continue;
                font = t.font;
                break;
            }
        }

        if (font == null)
        {
            Debug.LogWarning("SceneLoader: no TMP font found. Put '" + fontResourceName + ".otf' in Assets/Resources.");
            return;
        }

        foreach (var t in allTexts)
        {
            t.font = font;
            t.fontSharedMaterial = font.material;
        }
    }

    // ---------------- UI ----------------

    void PickTip()
    {
        if (tips == null || tips.Length == 0) { tipText.text = ""; tipHeader.text = ""; return; }
        int i = UnityEngine.Random.Range(0, tips.Length);
        tipText.text = tips[i];
        tipHeader.text = "TIP #" + (i + 1).ToString("00");
    }

    void SetProgress(float p)
    {
        p = Mathf.Clamp01(p);
        barFill.anchorMax = new Vector2(p, 1f);
        int dots = (int)(Time.unscaledTime * 3f) % 4;
        loadingText.text = "LOADING CASE FILE" + new string('.', dots) + "  " + Mathf.RoundToInt(p * 100f) + "%";
    }

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        font = LoadFont();
        var art = Resources.Load<Sprite>("LoadingBackground");

        // Solid background (always there during fades)
        var bg = MakeRect("Background", transform);
        Stretch(bg);
        bg.gameObject.AddComponent<Image>().color = backgroundColor;

        var root = MakeRect("LoadingRoot", bg);
        Stretch(root);
        loadingRoot = root.gameObject;

        // Menu art, covers the screen without stretching
        if (art != null)
        {
            var artRect = MakeRect("Art", root);
            artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
            artRect.anchoredPosition = Vector2.zero;
            var artImg = artRect.gameObject.AddComponent<Image>();
            artImg.sprite = art;
            var fitter = artRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = art.rect.width / art.rect.height;
        }

        // Dark dim so text is readable
        var dim = MakeRect("Dim", root);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, backgroundDim);

        // Title top-left with red underline (same as the menu)
        var title = MakeText("Title", root, "SCAM SENSE", 76, paperColor, TextAlignmentOptions.TopLeft);
        title.characterSpacing = 10f;
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -80), new Vector2(900, 100));

        var line = MakeRect("TitleLine", root);
        Place(line, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -180), new Vector2(480, 6));
        line.gameObject.AddComponent<Image>().color = alertRed;

        var sub = MakeText("Subtitle", root, "CYBERCRIME INVESTIGATION UNIT", 22, new Color(paperColor.r, paperColor.g, paperColor.b, 0.7f), TextAlignmentOptions.TopLeft);
        sub.characterSpacing = 6f;
        Place(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -200), new Vector2(900, 40));

        // Tip card: cream paper, slightly tilted, red pin
        var card = MakeRect("TipCard", root);
        Place(card, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 360), new Vector2(980, 230));
        card.localRotation = Quaternion.Euler(0, 0, -1.5f);
        card.gameObject.AddComponent<Image>().color = paperColor;

        var pin = MakeRect("Pin", card);
        Place(pin, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(34, 34));
        var pinImg = pin.gameObject.AddComponent<Image>();
        pinImg.sprite = MakeCircleSprite();
        pinImg.color = alertRed;

        tipHeader = MakeText("TipHeader", card, "TIP #01", 26, alertRed, TextAlignmentOptions.TopLeft);
        tipHeader.fontStyle = FontStyles.Bold;
        tipHeader.characterSpacing = 6f;
        Place(tipHeader.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -34), new Vector2(400, 40));

        tipText = MakeText("TipText", card, "", 36, inkColor, TextAlignmentOptions.TopLeft);
        tipText.enableWordWrapping = true;
        Place(tipText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -80), new Vector2(900, 130));

        // Loading text
        loadingText = MakeText("LoadingText", root, "", 26, paperColor, TextAlignmentOptions.Center);
        loadingText.characterSpacing = 6f;
        Place(loadingText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 175), new Vector2(900, 40));

        // Progress bar: border, dark back, red fill, accent on left edge
        var barBorder = MakeRect("BarBorder", root);
        Place(barBorder, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(900, 34));
        barBorder.gameObject.AddComponent<Image>().color = borderColor;

        var barBack = MakeRect("BarBack", barBorder);
        Stretch(barBack);
        barBack.offsetMin = new Vector2(3, 3);
        barBack.offsetMax = new Vector2(-3, -3);
        barBack.gameObject.AddComponent<Image>().color = new Color32(0x14, 0x0E, 0x0C, 255);

        barFill = MakeRect("BarFill", barBack);
        barFill.anchorMin = new Vector2(0, 0);
        barFill.anchorMax = new Vector2(0, 1);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        barFill.gameObject.AddComponent<Image>().color = alertRed;

        var accent = MakeRect("BarAccent", barBorder);
        accent.anchorMin = new Vector2(0, 0);
        accent.anchorMax = new Vector2(0, 1);
        accent.pivot = new Vector2(0, 0.5f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(8, 0);
        accent.gameObject.AddComponent<Image>().color = amber;

        loadingRoot.SetActive(false);
    }

    // ---------------- Helpers ----------------

    RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    TMP_Text MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var rect = MakeRect(name, parent);
        var t = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        allTexts.Add(t);
        return t;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static Sprite MakeCircleSprite()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float c = (s - 1) / 2f, r = s / 2f - 1f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 1f)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }
}