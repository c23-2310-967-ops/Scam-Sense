using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Shared look for every runtime-built in-game UI (HUD, messages, computer screen, menus).
/// Dark navy panels, near-white text, cyan highlights, green = legitimate, red = scam.
/// Text uses Inter (Assets/Fonts/Resources) with Liberation Sans as fallback.
///
/// Type scale is in reference pixels (1920x1080). At 1366x768 everything scales by ~0.71,
/// so nothing important goes below SizeSmall (22 -> ~16 px on screen).
/// </summary>
public static class UIKit
{
    // ---------- Palette ----------
    public static readonly Color Background   = new Color32(0x0B, 0x12, 0x20, 255);
    public static readonly Color Panel        = new Color32(0x11, 0x1A, 0x2B, 248);
    public static readonly Color PanelRaised  = new Color32(0x1A, 0x27, 0x3E, 255);
    public static readonly Color Border       = new Color32(0x2E, 0x42, 0x63, 255);
    public static readonly Color TextPrimary  = new Color32(0xF1, 0xF5, 0xF9, 255);
    public static readonly Color TextSecondary= new Color32(0xB9, 0xC6, 0xD8, 255);
    public static readonly Color Accent       = new Color32(0x38, 0xBD, 0xF8, 255);   // cyan
    public static readonly Color AccentDark   = new Color32(0x0E, 0x74, 0x9E, 255);
    public static readonly Color Safe         = new Color32(0x22, 0xC5, 0x5E, 255);   // legitimate (text/icons)
    public static readonly Color SafeButton   = new Color32(0x15, 0x80, 0x3D, 255);   // white text passes contrast
    public static readonly Color Danger       = new Color32(0xF8, 0x71, 0x71, 255);   // scam (text/icons)
    public static readonly Color DangerButton = new Color32(0xC0, 0x26, 0x26, 255);
    public static readonly Color Warning      = new Color32(0xFB, 0xBF, 0x24, 255);
    public static readonly Color Link         = new Color32(0x60, 0xA5, 0xFA, 255);
    public static readonly Color Dim          = new Color(0.02f, 0.04f, 0.08f, 0.78f);

    // Rich-text hex versions for inline colouring.
    public const string HexAccent = "#38BDF8", HexSafe = "#22C55E", HexDanger = "#F87171",
                        HexWarning = "#FBBF24", HexSecondary = "#B9C6D8";

    // ---------- Type scale (reference px) ----------
    public const float SizeDisplay = 46f, SizeH1 = 36f, SizeH2 = 30f, SizeBody = 27f, SizeSmall = 23f, SizeButton = 28f;

    const string BodyFontResource = "Inter-Regular SDF";
    const string BoldFontResource = "Inter-SemiBold SDF";

    static TMP_FontAsset bodyFont, boldFont;
    static Sprite roundedSprite, starSprite, circleSprite;

    // ---------- Fonts ----------

    public static TMP_FontAsset BodyFont
    {
        get
        {
            if (bodyFont == null) bodyFont = Resources.Load<TMP_FontAsset>(BodyFontResource);
            if (bodyFont == null) bodyFont = TMP_Settings.defaultFontAsset;
            return bodyFont;
        }
    }

    /// <summary>SemiBold face for titles and buttons (falls back to the body font).</summary>
    public static TMP_FontAsset BoldFont
    {
        get
        {
            if (boldFont == null) boldFont = Resources.Load<TMP_FontAsset>(BoldFontResource);
            if (boldFont == null) boldFont = BodyFont;
            return boldFont;
        }
    }

    // ---------- Canvas & rects ----------

    public static RectTransform CreateCanvas(Transform owner, string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(owner, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvas.pixelPerfect = false;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // Match height: on 16:10 / 4:3 screens the layout gets more width, never less height,
        // so tall panels (briefing, results) always fit vertically.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        return (RectTransform)go.transform;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    public static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    public static Image AddImage(RectTransform r, Color color, Sprite sprite = null, bool rounded = false)
    {
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        if (sprite == null && rounded) sprite = RoundedSprite;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    /// <summary>Rounded panel with a subtle border, separating text from the 3D scene.</summary>
    public static Image PanelImage(RectTransform r, Color? color = null)
    {
        var img = AddImage(r, color ?? Panel, rounded: true);
        var outline = r.gameObject.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(2f, -2f);
        return img;
    }

    /// <summary>Full-screen dark overlay that also blocks clicks to anything behind it.</summary>
    public static RectTransform Dimmer(Transform parent, float alpha = 0.78f)
    {
        var r = Rect("Dim", parent);
        Stretch(r);
        AddImage(r, new Color(Dim.r, Dim.g, Dim.b, alpha));
        return r;
    }

    /// <summary>Thin cyan strip on the left edge of a panel (ignored by layout groups).</summary>
    public static void AccentStrip(RectTransform panel, Color? color = null, float width = 6f)
    {
        var strip = Rect("Accent", panel);
        strip.anchorMin = new Vector2(0, 0);
        strip.anchorMax = new Vector2(0, 1);
        strip.pivot = new Vector2(0, 0.5f);
        strip.offsetMin = new Vector2(0, 14);
        strip.offsetMax = new Vector2(width, -14);
        AddImage(strip, color ?? Accent).raycastTarget = false;
        strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    public static RectTransform Divider(Transform parent, float thickness = 2f)
    {
        var d = Rect("Divider", parent);
        AddImage(d, Border).raycastTarget = false;
        Size(d, -1, thickness);
        return d;
    }

    // ---------- Layout ----------

    public static VerticalLayoutGroup Vertical(RectTransform r, int padding, float spacing, TextAnchor align = TextAnchor.UpperLeft)
    {
        var v = r.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(padding, padding, padding, padding);
        v.spacing = spacing;
        v.childAlignment = align;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return v;
    }

    public static HorizontalLayoutGroup Horizontal(RectTransform r, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = align;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        return h;
    }

    /// <summary>Makes a rect grow to fit its children vertically (panels whose text varies).</summary>
    public static void FitHeight(RectTransform r)
    {
        var f = r.gameObject.AddComponent<ContentSizeFitter>();
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    public static LayoutElement Size(Component c, float preferredWidth = -1f, float preferredHeight = -1f)
    {
        var le = c.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        if (preferredWidth >= 0f) { le.preferredWidth = preferredWidth; le.minWidth = preferredWidth; }
        if (preferredHeight >= 0f) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
        return le;
    }

    // ---------- Text ----------

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool bold = false)
    {
        var r = Rect(name, parent);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        var font = bold ? BoldFont : BodyFont;
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.lineSpacing = 4f;
        t.richText = true;
        return t;
    }

    /// <summary>Small all-caps cyan section label ("OBJECTIVES").</summary>
    public static TextMeshProUGUI Header(Transform parent, string text, float size = SizeSmall)
    {
        var t = Text(parent, "Header_" + text, text.ToUpperInvariant(), size, Accent, bold: true);
        t.characterSpacing = 3f;
        return t;
    }

    /// <summary>Rounded pill with a label, e.g. a "SCAM" / "LEGITIMATE" / "NEW" tag. Never colour alone.</summary>
    public static TextMeshProUGUI Badge(Transform parent, string label, Color background, Color textColor, float size = 20f)
    {
        var r = Rect("Badge_" + label, parent);
        AddImage(r, background, rounded: true).raycastTarget = false;
        var t = Text(r, "Label", label, size, textColor, TextAlignmentOptions.Center, bold: true);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.characterSpacing = 2f;
        Stretch(t.rectTransform);
        var fitter = r.gameObject.AddComponent<HorizontalLayoutGroup>();
        fitter.padding = new RectOffset(14, 14, 4, 4);
        fitter.childControlWidth = fitter.childControlHeight = true;
        fitter.childForceExpandWidth = fitter.childForceExpandHeight = false;   // size to the label, don't stretch
        return t;
    }

    // ---------- Buttons ----------

    public class ButtonStyle
    {
        public Color background = PanelRaised;
        public Color text = TextPrimary;
        public Color accent = Accent;       // thin strip on the left edge (set alpha 0 for none)
        public Color outline = Border;
    }

    public static ButtonStyle Primary => new ButtonStyle { background = AccentDark, accent = Accent, outline = Accent };
    public static ButtonStyle Secondary => new ButtonStyle();
    public static ButtonStyle ScamStyle => new ButtonStyle { background = DangerButton, accent = new Color(1, 1, 1, 0.35f), outline = Danger };
    public static ButtonStyle LegitStyle => new ButtonStyle { background = SafeButton, accent = new Color(1, 1, 1, 0.35f), outline = Safe };

    public static Button Button(Transform parent, string label, UnityAction onClick, ButtonStyle style = null, float fontSize = SizeButton)
    {
        style = style ?? Secondary;
        var r = Rect(label + " Button", parent);
        var img = AddImage(r, style.background, rounded: true);
        var outline = r.gameObject.AddComponent<Outline>();
        outline.effectColor = style.outline;
        outline.effectDistance = new Vector2(2f, -2f);

        var btn = r.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);   // brighten on hover
        colors.selectedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.7f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        if (style.accent.a > 0f)
        {
            var strip = Rect("Accent", r);
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(0f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.offsetMin = new Vector2(6f, 12f);
            strip.offsetMax = new Vector2(12f, -12f);
            AddImage(strip, style.accent, rounded: false).raycastTarget = false;
        }

        var t = Text(r, "Label", label, fontSize, style.text, TextAlignmentOptions.Center, bold: true);
        t.characterSpacing = 1.5f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;            // long labels shrink instead of overflowing
        t.fontSizeMax = fontSize;
        t.fontSizeMin = Mathf.Min(fontSize, SizeSmall - 2f);
        Stretch(t.rectTransform);
        t.margin = new Vector4(20, 4, 20, 4);

        r.gameObject.AddComponent<ButtonPressFeedback>();
        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    /// <summary>
    /// HUD-style button inspired by flat sci-fi interface sets: cut (chamfered) corners, a thin frame,
    /// segmented tick marks and a small top tab, with the cyan DigitalLightSweep on hover.
    /// The root is the clickable fill (Image + Mask + Button); everything else is display only.
    /// Children: "Frame", "Ticks", "Tab", "Label" (same "Label" name as UIKit.Button, so SetLabel works).
    /// </summary>
    public enum HudVariant { Secondary, Primary, Danger, Safe }

    public static Button HudButton(Transform parent, string label, UnityAction onClick, bool primary = false,
        float fontSize = SizeButton, float idleSweepEvery = 0f) =>
        HudButton(parent, label, onClick, primary ? HudVariant.Primary : HudVariant.Secondary, fontSize, idleSweepEvery);

    public static Button HudButton(Transform parent, string label, UnityAction onClick, HudVariant variant,
        float fontSize = SizeButton, float idleSweepEvery = 0f)
    {
        bool primary = variant == HudVariant.Primary, danger = variant == HudVariant.Danger, safe = variant == HudVariant.Safe;
        bool strong = primary || danger || safe;
        var r = Rect(label + " Button", parent);
        var fill = r.gameObject.AddComponent<Image>();
        fill.sprite = ChamferSprite;
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 0.7f;            // larger corner cuts, closer to the reference
        fill.color = primary ? new Color(AccentDark.r, AccentDark.g, AccentDark.b, 0.92f)
                   : danger ? new Color(DangerButton.r, DangerButton.g, DangerButton.b, 0.95f)
                   : safe ? new Color(SafeButton.r, SafeButton.g, SafeButton.b, 0.95f)
                             : new Color(PanelRaised.r, PanelRaised.g, PanelRaised.b, 0.88f);
        var mask = r.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;                    // the fill is visible and clips the sweep to the shape

        var btn = r.gameObject.AddComponent<Button>();
        btn.targetGraphic = fill;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.3f, 1f);
        colors.selectedColor = new Color(1.12f, 1.12f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.7f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        // Light sweep first, so its band sits just above the fill and below the frame/text.
        var sweep = r.gameObject.AddComponent<DigitalLightSweep>();

        var frame = Rect("Frame", r);
        Stretch(frame);
        var frameImg = frame.gameObject.AddComponent<Image>();
        frameImg.sprite = ChamferOutlineSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.pixelsPerUnitMultiplier = 0.7f;        // must match the fill so the outline follows the cut
        frameImg.raycastTarget = false;

        // Segmented ticks on the left (reference: stacked bar accents)
        var ticks = Rect("Ticks", r);
        ticks.anchorMin = ticks.anchorMax = new Vector2(0f, 0.5f);
        ticks.pivot = new Vector2(0f, 0.5f);
        ticks.anchoredPosition = new Vector2(16f, 0f);
        ticks.sizeDelta = new Vector2(16f, 22f);
        for (int i = 0; i < 3; i++)
        {
            var t = Rect("Tick" + i, ticks);
            t.anchorMin = t.anchorMax = new Vector2(0f, 0.5f);
            t.pivot = new Vector2(0f, 0.5f);
            t.anchoredPosition = new Vector2(i * 6f, 0f);
            t.sizeDelta = new Vector2(3f, i == 1 ? 22f : 14f);
            AddImage(t, strong ? new Color(1f, 1f, 1f, 0.85f) : new Color(Accent.r, Accent.g, Accent.b, 0.85f)).raycastTarget = false;
        }

        // Small tab on the top edge, right side (reference: inset header tabs)
        var tab = Rect("Tab", r);
        tab.anchorMin = tab.anchorMax = new Vector2(1f, 1f);
        tab.pivot = new Vector2(1f, 1f);
        tab.anchoredPosition = new Vector2(-30f, -3f);
        tab.sizeDelta = new Vector2(48f, 3f);
        AddImage(tab, strong ? new Color(1f, 1f, 1f, 0.8f) : Accent).raycastTarget = false;

        var txt = Text(r, "Label", label, fontSize, TextPrimary, TextAlignmentOptions.Center, bold: true);
        txt.characterSpacing = 2f;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.enableAutoSizing = true;
        txt.fontSizeMax = fontSize;
        txt.fontSizeMin = Mathf.Min(fontSize, SizeSmall - 2f);
        Stretch(txt.rectTransform);
        txt.margin = new Vector4(48, 4, 24, 4);

        sweep.Setup(frameImg,
            primary ? new Color(Accent.r, Accent.g, Accent.b, 0.9f) : danger ? Danger : safe ? Safe : Border,
            primary ? new Color(0.75f, 0.95f, 1f, 1f) : danger ? new Color(1f, 0.8f, 0.8f, 1f) : safe ? new Color(0.8f, 1f, 0.85f, 1f) : Accent,
            idleSweepEvery,
            danger ? new Color(1f, 0.9f, 0.88f, 0.45f)          // warm white sweep on red
            : safe ? new Color(0.88f, 1f, 0.9f, 0.4f)           // pale sweep on green
            : (Color?)null);

        r.gameObject.AddComponent<ButtonPressFeedback>();
        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    public static void SetLabel(Button b, string label)
    {
        var t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = label;
    }

    /// <summary>Dims a button's label too when it is not interactable (tint alone is easy to miss).</summary>
    public static void SetInteractable(Button b, bool on)
    {
        if (b == null) return;
        b.interactable = on;
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.alpha = on ? 1f : 0.45f;
    }

    // ---------- Sliders ----------

    /// <summary>
    /// Label + value text + large slider (used by the pause menu and the main menu settings).
    /// The value text is a child named "Value" of the returned slider's parent.
    /// </summary>
    public static Slider LabeledSlider(Transform panel, string label, float min, float max, UnityAction<float> onChange)
    {
        var block = Rect("Slider_" + label, panel);
        Size(block, -1, 92);

        var name = Text(block, "Label", label, SizeBody, TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(460, 36));
        var value = Text(block, "Value", "", SizeBody, Accent, TextAlignmentOptions.TopRight, bold: true);
        Place(value.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(160, 36));

        var sr = Rect("Slider", block);
        sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(1, 0); sr.pivot = new Vector2(0.5f, 0);
        sr.sizeDelta = new Vector2(0, 40); sr.anchoredPosition = new Vector2(0, 4);

        var track = Rect("Track", sr);
        track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f);
        track.sizeDelta = new Vector2(0, 12);
        AddImage(track, Border, rounded: true);

        var fillArea = Rect("FillArea", sr);
        fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
        fillArea.sizeDelta = new Vector2(-20, 12);
        var fill = Rect("Fill", fillArea);
        Stretch(fill);
        fill.offsetMin = new Vector2(-10, 0); fill.offsetMax = new Vector2(10, 0);
        AddImage(fill, Accent, rounded: true);

        var handleArea = Rect("HandleArea", sr);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(16, 0); handleArea.offsetMax = new Vector2(-16, 0);
        var handle = Rect("Handle", handleArea);
        handle.sizeDelta = new Vector2(36, 0);
        handle.anchorMin = new Vector2(0, 0); handle.anchorMax = new Vector2(0, 1);
        var himg = AddImage(handle, TextPrimary, CircleSprite);

        var slider = sr.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = himg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        if (onChange != null) slider.onValueChanged.AddListener(onChange);
        var colors = slider.colors;
        colors.highlightedColor = new Color(0.75f, 0.92f, 1f, 1f);
        slider.colors = colors;
        return slider;
    }

    public static void SetSliderValueLabel(Slider s, string text)
    {
        var label = s.transform.parent.Find("Value");
        var t = label != null ? label.GetComponent<TMP_Text>() : null;
        if (t != null) t.text = text;
    }

    // ---------- Sprites ----------
    // Each sprite is loaded from Assets/Resources/UIKit/<name>.png when that asset exists (so UI saved in
    // scenes can reference it), otherwise generated procedurally exactly as before.
    // Tools > Scam Sense > Build Menu UI In Scene writes the assets from GenerateSpriteSet().

    const string SpriteFolder = "UIKit/";

    static Sprite LoadOrMake(string name, System.Func<Sprite> make)
    {
        var s = Resources.Load<Sprite>(SpriteFolder + name);
        if (s != null) return s;
        s = make();
        s.name = name;
        return s;
    }

    /// <summary>Freshly generated copies of every UIKit sprite (used to write the sprite assets).</summary>
    public static (string name, Sprite sprite)[] GenerateSpriteSet() => new[]
    {
        ("UIKit_Rounded", MakeRounded()),
        ("UIKit_Chamfer", MakeChamfer(false)),
        ("UIKit_ChamferOutline", MakeChamfer(true)),
        ("UIKit_Sweep", MakeSweep()),
        ("UIKit_Circle", MakeCircle()),
        ("UIKit_Star", MakeStar()),
    };

    /// <summary>Forget cached sprites so the next access re-loads them (after the assets were written).</summary>
    public static void ClearSpriteCache() =>
        roundedSprite = starSprite = circleSprite = chamferSprite = chamferOutlineSprite = sweepSprite = null;

    /// <summary>White 9-sliced rounded rectangle; tint it with Image.color.</summary>
    public static Sprite RoundedSprite => roundedSprite != null ? roundedSprite : (roundedSprite = LoadOrMake("UIKit_Rounded", MakeRounded));

    static Sprite MakeRounded()
    {
        {
            const int s = 64, radius = 14;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, s - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, s - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
                }
            tex.Apply();
            var rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            rounded.name = "UIKit_Rounded";
            return rounded;
        }
    }

    // ---------- HUD shapes (cut-corner frame + light band) ----------

    static Sprite chamferSprite, chamferOutlineSprite, sweepSprite;
    const int ChamferSize = 64, ChamferCut = 16, ChamferBorder = 22;

    /// <summary>Signed distance (in pixels, positive inside) to a rect with the top-left and bottom-right corners cut.</summary>
    static float ChamferDistance(float x, float y)
    {
        float s = ChamferSize;
        float d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(s - x, s - y));
        float topLeft = (x + (s - y) - ChamferCut) / 1.41421f;
        float bottomRight = ((s - x) + y - ChamferCut) / 1.41421f;
        return Mathf.Min(d, Mathf.Min(topLeft, bottomRight));
    }

    static Sprite MakeChamfer(bool outline)
    {
        var tex = new Texture2D(ChamferSize, ChamferSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < ChamferSize; y++)
            for (int x = 0; x < ChamferSize; x++)
            {
                float d = ChamferDistance(x + 0.5f, y + 0.5f);
                float a = Mathf.Clamp01(d + 0.5f);
                if (outline) a *= 1f - Mathf.Clamp01(d - 2f + 0.5f);   // 2 px ring along the edge
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, ChamferSize, ChamferSize), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(ChamferBorder, ChamferBorder, ChamferBorder, ChamferBorder));
        s.name = outline ? "UIKit_ChamferOutline" : "UIKit_Chamfer";
        return s;
    }

    /// <summary>9-sliced HUD panel shape with cut top-left and bottom-right corners (white; tint with Image.color).</summary>
    public static Sprite ChamferSprite => chamferSprite != null ? chamferSprite : (chamferSprite = LoadOrMake("UIKit_Chamfer", () => MakeChamfer(false)));

    /// <summary>2 px outline of ChamferSprite.</summary>
    public static Sprite ChamferOutlineSprite => chamferOutlineSprite != null ? chamferOutlineSprite : (chamferOutlineSprite = LoadOrMake("UIKit_ChamferOutline", () => MakeChamfer(true)));

    /// <summary>Soft vertical band of light with a bright core, for DigitalLightSweep.</summary>
    public static Sprite SweepSprite => sweepSprite != null ? sweepSprite : (sweepSprite = LoadOrMake("UIKit_Sweep", MakeSweep));

    static Sprite MakeSweep()
    {
        {
            const int w = 128, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f - w / 2f);
                float a = Mathf.Exp(-(u / 26f) * (u / 26f)) * 0.75f + Mathf.Exp(-(u / 3.5f) * (u / 3.5f)) * 0.6f;
                for (int y = 0; y < h; y++) tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
            }
            tex.Apply();
            var sweep = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            sweep.name = "UIKit_Sweep";
            return sweep;
        }
    }

    public static Sprite CircleSprite => circleSprite != null ? circleSprite : (circleSprite = LoadOrMake("UIKit_Circle", MakeCircle));

    static Sprite MakeCircle()
    {
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
            var circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            circle.name = "UIKit_Circle";
            return circle;
        }
    }

    /// <summary>White five-pointed star; tint with Image.color.</summary>
    public static Sprite StarSprite => starSprite != null ? starSprite : (starSprite = LoadOrMake("UIKit_Star", MakeStar));

    static Sprite MakeStar()
    {
        {
            const int s = 128;
            var pts = new Vector2[10];
            Vector2 centre = new Vector2(s / 2f, s / 2f - 4f);
            for (int i = 0; i < 10; i++)
            {
                float radius = (i % 2 == 0) ? s * 0.48f : s * 0.2f;
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                pts[i] = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            }

            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int inside = 0;   // 2x2 supersampling for smooth edges
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (InPolygon(new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f), pts)) inside++;
                    tex.SetPixel(x, y, new Color(1, 1, 1, inside / 4f));
                }
            tex.Apply();
            var star = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            star.name = "UIKit_Star";
            return star;
        }
    }

    static bool InPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    public static string FormatSeconds(float s) => s.ToString("0.0") + "s";

    /// <summary>
    /// Investigation details for a message (sender info, requested action, link note, observations, and the
    /// player's own recent activity from the level's case notes). Neutral facts only - never the answer.
    /// Returns "" if there is nothing to show.
    /// </summary>
    public static string MessageDetailsText(ScamMessage m, string[] recentActivity)
    {
        if (m == null) return "";
        var sb = new System.Text.StringBuilder();
        string channel = m.channel == MessageChannel.SMS ? "Text message" : m.channel == MessageChannel.Website ? "Web page (captured)"
                       : m.channel == MessageChannel.Case ? "Multi-evidence case" : m.channel == MessageChannel.Chat ? "Work chat" : "Email";
        sb.Append($"<color={HexAccent}><b>SENDER</b></color>  {m.sender}");
        if (!string.IsNullOrEmpty(m.senderDetail)) sb.Append($"  <color={HexSecondary}>({m.senderDetail})</color>");
        sb.Append($"\n<color={HexAccent}><b>CHANNEL</b></color>  {channel}");
        if (!string.IsNullOrEmpty(m.requestedAction)) sb.Append($"\n<color={HexAccent}><b>ASKS YOU TO</b></color>  {m.requestedAction}");
        if (!string.IsNullOrEmpty(m.linkNote)) sb.Append($"\n<color={HexAccent}><b>LINK</b></color>  {m.linkNote}");
        if (m.clues != null && m.clues.Length > 0)
        {
            sb.Append($"\n<color={HexAccent}><b>OBSERVATIONS</b></color>");
            foreach (var c in m.clues) if (!string.IsNullOrEmpty(c)) sb.Append($"\n•  {c}");
        }
        if (recentActivity != null && recentActivity.Length > 0)
        {
            sb.Append($"\n<color={HexAccent}><b>YOUR RECENT ACTIVITY</b></color>");
            foreach (var a in recentActivity) if (!string.IsNullOrEmpty(a)) sb.Append($"\n•  {a}");
        }
        return sb.ToString();
    }

    public static string VerdictWord(MessageVerdict v) => v == MessageVerdict.Scam ? "SCAM" : "LEGITIMATE";
}
