using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Full-screen reader for case folders, evidence documents and the investigation board (see InspectableObject).
/// Builds itself on first use with UIKit (no scene setup). While open it holds the InteractionGate, so player
/// movement, mouse-look and other E interactions are frozen and the cursor is free; it never opens over another
/// panel. CLOSE, E or Esc closes it and hands control back. Left / Right (or A / D) turn pages.
/// </summary>
public class DocumentReaderUI : MonoBehaviour
{
    public struct Page
    {
        public string title, body;
        public Page(string title, string body) { this.title = title; this.body = body; }
    }

    const int SortingOrder = 38;   // above the HUD / briefing, below results and the pause menu

    static DocumentReaderUI instance;

    /// <summary>True while a document is open.</summary>
    public static bool IsOpen => instance != null && instance.root != null && instance.root.gameObject.activeSelf;

    RectTransform root;
    TMP_Text kindText, titleText, pageTitleText, bodyText, pageCounter;
    ScrollRect scroll;
    Button prevButton, nextButton, closeButton;
    readonly List<Page> pages = new List<Page>();
    int page, openedFrame;
    LevelAudio levelAudio;
    LevelHUD hud;

    /// <summary>
    /// Opens a document. Returns false (and opens nothing) if another panel is already open, so two interfaces
    /// can never be open at once.
    /// </summary>
    public static bool Open(string kind, string title, List<Page> content)
    {
        if (content == null || content.Count == 0 || InteractionGate.UIOpen || PauseMenu.IsPaused) return false;
        if (instance == null)
        {
            var go = new GameObject("DocumentReaderUI");
            instance = go.AddComponent<DocumentReaderUI>();
        }
        instance.Show(kind, title, content);
        return true;
    }

    /// <summary>Closes the reader if it is open.</summary>
    public static void CloseIfOpen()
    {
        if (IsOpen) instance.Close();
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
        root.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        InteractionGate.Release(this);
        InteractionGate.RemoveEscapeHandler(this);
    }

    void Show(string kind, string title, List<Page> content)
    {
        pages.Clear();
        pages.AddRange(content);
        kindText.text = (kind ?? "").ToUpperInvariant();
        titleText.text = title ?? "";
        page = 0;
        openedFrame = Time.frameCount;   // the E press that opened it must not close it in the same frame
        root.gameObject.SetActive(true);
        InteractionGate.Acquire(this);
        InteractionGate.PushEscapeHandler(this, () => { Close(); return true; });
        if (levelAudio == null) levelAudio = FindFirstObjectByType<LevelAudio>();
        if (levelAudio != null) levelAudio.PlayClick();
        if (hud == null) hud = FindFirstObjectByType<LevelHUD>();
        if (hud != null) hud.SetVisible(false);   // like the computer and phone: nothing else on screen while reading
        ShowPage(0);
    }

    void ShowPage(int index)
    {
        page = Mathf.Clamp(index, 0, pages.Count - 1);
        pageTitleText.text = pages[page].title ?? "";
        pageTitleText.gameObject.SetActive(!string.IsNullOrEmpty(pages[page].title));
        bodyText.text = pages[page].body ?? "";
        bool many = pages.Count > 1;
        pageCounter.text = many ? $"Page {page + 1} / {pages.Count}" : "";
        prevButton.gameObject.SetActive(many);
        nextButton.gameObject.SetActive(many);
        UIKit.SetInteractable(prevButton, page > 0);
        UIKit.SetInteractable(nextButton, page < pages.Count - 1);
        scroll.verticalNormalizedPosition = 1f;   // start at the top of each page
    }

    void Close()
    {
        if (!IsOpen) return;
        root.gameObject.SetActive(false);
        InteractionGate.RemoveEscapeHandler(this);
        InteractionGate.Release(this);
        if (hud != null) hud.SetVisible(true);
        if (levelAudio != null) levelAudio.PlayClick();
    }

    void Update()
    {
        if (!IsOpen || PauseMenu.IsPaused) return;
        var kb = Keyboard.current;
        if (kb == null || Time.frameCount == openedFrame) return;
        if (kb.eKey.wasPressedThisFrame) { InteractionGate.ConsumeInteract(); Close(); return; }
        if ((kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) && page < pages.Count - 1) ShowPage(page + 1);
        if ((kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) && page > 0) ShowPage(page - 1);
    }

    // ---------- Build ----------

    void Build()
    {
        root = UIKit.CreateCanvas(transform, "DocumentReader Canvas", SortingOrder);
        UIKit.Dimmer(root, 0.8f);

        var panel = UIKit.Rect("Panel", root);
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 820));
        UIKit.PanelImage(panel);
        UIKit.AccentStrip(panel, null, 8f);

        kindText = UIKit.Text(panel, "Kind", "", 20, UIKit.Accent, TextAlignmentOptions.TopLeft, bold: true);
        kindText.characterSpacing = 4f;
        UIKit.Place(kindText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -34), new Vector2(900, 28));
        titleText = UIKit.Text(panel, "Title", "", 40, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -64), new Vector2(1070, 56));
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.overflowMode = TextOverflowModes.Ellipsis;

        var divider = UIKit.Rect("Divider", panel);
        UIKit.Place(divider, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -128), new Vector2(1070, 3));
        UIKit.AddImage(divider, UIKit.Accent);

        pageTitleText = UIKit.Text(panel, "PageTitle", "", 28, UIKit.Accent, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(pageTitleText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -146), new Vector2(1070, 40));

        // Scrollable body (long evidence lists fit at any length).
        var viewport = UIKit.Rect("Viewport", panel);
        UIKit.Place(viewport, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -196), new Vector2(1070, 500));
        viewport.gameObject.AddComponent<RectMask2D>();
        UIKit.AddImage(viewport, new Color(0, 0, 0, 0));   // catches the mouse wheel
        var content = UIKit.Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.padding = new RectOffset(0, 16, 0, 12);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        bodyText = UIKit.Text(content, "Body", "", 25, UIKit.TextPrimary, TextAlignmentOptions.TopLeft);
        bodyText.lineSpacing = 8f;
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;

        // Footer: page controls and CLOSE.
        prevButton = UIKit.HudButton(panel, "< PREV", () => ShowPage(page - 1), UIKit.HudVariant.Secondary, 22);
        UIKit.Place((RectTransform)prevButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 40), new Vector2(170, 64));
        nextButton = UIKit.HudButton(panel, "NEXT >", () => ShowPage(page + 1), UIKit.HudVariant.Secondary, 22);
        UIKit.Place((RectTransform)nextButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(240, 40), new Vector2(170, 64));
        pageCounter = UIKit.Text(panel, "PageCounter", "", 20, UIKit.TextSecondary, TextAlignmentOptions.MidlineLeft);
        UIKit.Place(pageCounter.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(430, 40), new Vector2(240, 64));
        closeButton = UIKit.HudButton(panel, "CLOSE   [E] / Esc", Close, UIKit.HudVariant.Primary, 22);
        UIKit.Place((RectTransform)closeButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 40), new Vector2(300, 64));
    }
}
