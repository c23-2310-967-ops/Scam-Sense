using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Message investigation inside the normal gameplay. While a message with investigation data is open on its
/// presenter (phone pop-up or computer), the presenter's texts show clickable details (see <see cref="Bind"/> and
/// <see cref="Mark"/>). Inspecting shows a finding card and may add a clue to the Evidence Notebook, where the
/// player selects the clues that support their verdict. The verdict is still given with the presenter's
/// SCAM / LEGITIMATE buttons; QuizManager then calls <see cref="Submit"/> and the presenter's feedback includes
/// <see cref="FeedbackFor"/> (selected, unselected and missed evidence). Scoring is unchanged.
///
/// Created by QuizManager at runtime (no Inspector setup). Messages without investigation data behave exactly
/// as before. Links are never opened.
/// </summary>
public class MessageInvestigation : MonoBehaviour
{
    public static MessageInvestigation Active { get; private set; }

    /// <summary>Response time pauses briefly after each newly inspected detail (reading the finding is free).</summary>
    public static bool PausesTimer => Active != null && Active.session != null && Time.time < Active.pauseUntil;
    const float ReadingAllowance = 6f;

    const string HexLink = "#7DD3FC", HexClue = "#FBBF24", HexSeen = "#C9D4E3";
    static readonly Color Muted = new Color32(0xC9, 0xD4, 0xE3, 255);

    InvestigationSession session;
    IInvestigationDock dock;
    float pauseUntil;
    readonly List<(TMP_Text text, Func<string> build)> binds = new List<(TMP_Text, Func<string>)>();

    RectTransform root, tab, card, panel, linkBox, panelList;
    LayoutElement listLayout;
    TMP_Text hint, cardLabel, cardStatus, cardFinding, linkText, panelTitle, panelIntro;
    Button tabButton;

    void Awake()
    {
        Active = this;
        Build();
        Hide();
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        InteractionGate.RemoveEscapeHandler(this);
    }

    // ---------- Lifecycle (called by QuizManager) ----------

    /// <summary>
    /// Only emails are investigated. Texts and chats stay plain read-and-answer (their data in the
    /// Level assets is kept but unused; widen this check to bring it back).
    /// </summary>
    public static bool Investigates(ScamMessage msg) => msg != null && msg.HasInvestigation && msg.channel == MessageChannel.Email;

    /// <summary>A message is being opened. Call before the presenter shows it, so its texts bind as clickable.</summary>
    public void Begin(ScamMessage msg, IInvestigationDock dockArea = null)
    {
        End();
        if (!Investigates(msg)) return;
        dock = dockArea;
        foreach (var p in InvestigationSession.Validate(msg)) Debug.LogWarning($"Investigation data of '{msg.id}': {p}");
        session = new InvestigationSession(msg);
        pauseUntil = 0f;
        root.gameObject.SetActive(true);
        tab.gameObject.SetActive(true);
        hint.gameObject.SetActive(true);
        card.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
        RefreshTab();
    }

    /// <summary>The player answered with the presenter's buttons: evaluate the evidence once.</summary>
    public void Submit(ScamMessage msg, MessageVerdict verdict)
    {
        if (session == null || session.Message != msg || session.Submitted) return;
        session.Submit(verdict);
        card.gameObject.SetActive(false);
        hint.gameObject.SetActive(false);
        RefreshTab();
        if (panel.gameObject.activeSelf) FillPanel();
    }

    /// <summary>The message is finished (feedback closed).</summary>
    public void End()
    {
        session = null;
        dock = null;
        binds.Clear();
        Hide();
    }

    void Hide()
    {
        if (root == null) return;
        root.gameObject.SetActive(false);
        InteractionGate.RemoveEscapeHandler(this);
    }

    // ---------- Layout: dock next to the presenter's content ----------

    void LateUpdate()
    {
        if (session == null || root == null || !root.gameObject.activeSelf) return;
        var canvas = root.GetComponent<Canvas>();
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        if (dock != null && dock.TryGetDockArea(out var area) && area.width / scale >= 320f && area.height / scale >= 300f)
            Layout(new Rect(area.x / scale, area.y / scale, area.width / scale, area.height / scale));
        else
            Layout(null);
    }

    /// <summary>Places hint, tab, card and notebook inside <paramref name="area"/> (canvas units), or on the right of the screen.</summary>
    void Layout(Rect? area)
    {
        if (area.HasValue)
        {
            var a = area.Value;
            float w = Mathf.Min(a.width, 540f), x = a.x + (a.width - w) * 0.5f, top = a.yMax;
            Put(hint.rectTransform, x, top, w, 28f);
            Put(tab, x, top - 34f, w, 60f);
            Put(card, x, top - 106f, w, -1f);
            float pw = Mathf.Min(a.width, 600f);
            Put(panel, a.x + (a.width - pw) * 0.5f, top, pw, -1f);
            listLayout.preferredHeight = listLayout.minHeight = Mathf.Clamp(a.height - 230f, 160f, 560f);
        }
        else
        {
            Dock(hint.rectTransform, new Vector2(-28, 190), new Vector2(520, 30), new Vector2(1, 0));
            Dock(tab, new Vector2(-28, 150), new Vector2(420, 64), new Vector2(1, 0.5f));
            Dock(card, new Vector2(-28, 104), new Vector2(520, card.sizeDelta.y), new Vector2(1, 1));
            Dock(panel, new Vector2(-28, 0), new Vector2(600, panel.sizeDelta.y), new Vector2(1, 0.5f));
            listLayout.preferredHeight = listLayout.minHeight = 560f;
        }
    }

    static void Put(RectTransform r, float x, float top, float width, float height)
    {
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, top);
        r.sizeDelta = new Vector2(width, height >= 0f ? height : r.sizeDelta.y);
    }

    static void Dock(RectTransform r, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    /// <summary>Screen-pixel rectangle of a UI element (overlay or world-space canvas).</summary>
    public static bool ScreenRect(RectTransform rt, out Rect rect)
    {
        rect = default;
        var canvas = rt != null ? rt.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return false;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;   // e.g. the 3D phone's screen has no camera assigned
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && cam == null) return false;
        rt.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in corners)
        {
            Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, c);
            min = Vector2.Min(min, s); max = Vector2.Max(max, s);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.width > 0f && rect.height > 0f;
    }
    static readonly Vector3[] corners = new Vector3[4];

    // ---------- Text binding for presenters ----------

    /// <summary>
    /// Sets a presenter text from <paramref name="build"/>. While the message is being investigated, the text is
    /// rebuilt after every inspection (so inspected details change colour) and its links are clickable.
    /// </summary>
    public static void Bind(TMP_Text text, Func<string> build)
    {
        if (text == null || build == null) return;
        text.text = build();
        var a = Active;
        if (a == null || a.session == null) return;
        a.binds.Add((text, build));
        var link = text.GetComponent<InvestigationLinkText>();
        if (link == null) link = text.gameObject.AddComponent<InvestigationLinkText>();
        link.clicked = id => { if (Active != null) Active.Inspect(id); };
        text.raycastTarget = true;
    }

    /// <summary>
    /// Rich text for one field of <paramref name="msg"/>: literal text (never parsed as tags), with the
    /// inspectable details of that field as underlined links when the message is being investigated.
    /// Not yet inspected = cyan; inspected clue = gold with ★; inspected neutral detail = grey with ✓.
    /// </summary>
    public static string Mark(ScamMessage msg, InspectField field, string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var a = Active;
        var s = a != null && a.session != null && a.session.Message == msg ? a.session : null;
        if (s == null) return Literal(text);

        // Find each inspectable's span in this field (whole field when no match is given).
        var spans = new List<(int start, int length, MessageInspectable part)>();
        foreach (var p in msg.inspectables)
        {
            if (p.field != field) continue;
            int start = string.IsNullOrEmpty(p.match) ? 0 : text.IndexOf(p.match, StringComparison.Ordinal);
            int length = string.IsNullOrEmpty(p.match) ? text.Length : p.match.Length;
            if (start < 0) continue;
            if (spans.Exists(o => start < o.start + o.length && o.start < start + length)) continue;   // no overlaps
            spans.Add((start, length, p));
        }
        spans.Sort((x, y) => x.start.CompareTo(y.start));

        var sb = new StringBuilder();
        int i = 0;
        foreach (var (start, length, part) in spans)
        {
            sb.Append(Literal(text.Substring(i, start - i)));
            bool seen = s.WasInspected(part.id);
            bool clue = seen && !string.IsNullOrEmpty(part.clueId);
            string color = !seen ? HexLink : clue ? HexClue : HexSeen;
            sb.Append($"<link=\"{part.id}\"><color={color}><u>{Literal(text.Substring(start, length))}</u></color></link>");
            if (seen) sb.Append(clue ? $"<color={HexClue}><size=75%> ★</size></color>" : "<color=#94A3B8><size=75%> ✓</size></color>");
            i = start + length;
        }
        sb.Append(Literal(text.Substring(i)));
        return sb.ToString();
    }

    static string Literal(string s) => string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "") + "</noparse>";

    /// <summary>Evidence summary appended to the presenter's feedback explanation ("" if the message wasn't investigated).</summary>
    public static string FeedbackFor(ScamMessage msg)
    {
        var a = Active;
        var r = a != null && a.session != null && a.session.Message == msg ? a.session.Evaluation : null;
        if (r == null) return "";
        string Names(List<MessageEvidence> l) => l.Count == 0 ? "none" : string.Join(", ", l.ConvertAll(c => c.title));
        var sb = new StringBuilder();
        sb.Append($"\n\n<color={UIKit.HexAccent}><b>YOUR EVIDENCE</b></color>   key clues selected <b>{r.selectedCorrectly.Count} of {r.SupportingTotal}</b>");
        sb.Append($"\n<color={UIKit.HexSafe}>✓ Selected:</color> {Names(r.selectedCorrectly)}");
        if (r.foundNotSelected.Count > 0) sb.Append($"\n<color={UIKit.HexAccent}>• Found, not selected:</color> {Names(r.foundNotSelected)}");
        if (r.missed.Count > 0) sb.Append($"\n<color={UIKit.HexDanger}>✗ Missed (never found):</color> {Names(r.missed)}");
        if (r.selectedNotSupporting.Count > 0) sb.Append($"\n<color={UIKit.HexWarning}>! Selected, but not real evidence:</color> {Names(r.selectedNotSupporting)}");
        return sb.ToString();
    }

    // ---------- Inspecting ----------

    void Inspect(string id)
    {
        if (session == null) return;
        var outcome = session.Inspect(id, out var part, out var clue, out bool first);
        if (outcome == InvestigationSession.InspectOutcome.Unknown) return;
        if (first && !session.Submitted) pauseUntil = Time.time + ReadingAllowance;
        hint.gameObject.SetActive(false);   // the player has found the highlighted details; free the space for the card
        foreach (var (text, build) in binds) if (text != null) text.text = build();

        cardLabel.text = part.label;
        cardStatus.text =
            session.Submitted ? $"<color={HexSeen}>ANSWER GIVEN  ·  REVIEWING</color>" :
            outcome == InvestigationSession.InspectOutcome.NewClue ? $"<color={UIKit.HexSafe}>★ NEW CLUE ADDED TO YOUR NOTEBOOK</color>\n<b>{clue.title}</b>" :
            outcome == InvestigationSession.InspectOutcome.KnownClue ? $"<color={UIKit.HexAccent}>ALREADY IN YOUR NOTEBOOK</color>  ·  {clue.title}" :
            $"<color={HexSeen}>✓ CHECKED  ·  NOT EVIDENCE ON ITS OWN</color>";
        cardFinding.text = part.finding;
        linkBox.gameObject.SetActive(part.IsLink);
        if (part.IsLink)
        {
            string address = !string.IsNullOrEmpty(part.linkAddress) ? part.linkAddress : session.Message.link;
            linkText.text = $"<b>SIMULATED LINK  ·  NOT OPENED</b>\n<color={HexLink}>{Literal(address)}</color>";
        }
        if (!panel.gameObject.activeSelf) ShowCard(true);
        RefreshTab();
        if (panel.gameObject.activeSelf) FillPanel();
    }

    void ShowCard(bool on)
    {
        card.gameObject.SetActive(on);
        UpdateEscape();
    }

    void RefreshTab()
    {
        if (session == null) return;
        int n = session.Discovered.Count;
        UIKit.SetLabel(tabButton, session.Submitted ? "EVIDENCE REVIEW" : n > 0 ? $"EVIDENCE NOTEBOOK  ·  {n}" : "EVIDENCE NOTEBOOK");
    }

    // ---------- Notebook ----------

    void TogglePanel()
    {
        if (panel.gameObject.activeSelf) { panel.gameObject.SetActive(false); tab.gameObject.SetActive(true); UpdateEscape(); return; }
        card.gameObject.SetActive(false);
        tab.gameObject.SetActive(false);
        panel.gameObject.SetActive(true);
        FillPanel();
        UpdateEscape();
    }

    void UpdateEscape()
    {
        InteractionGate.RemoveEscapeHandler(this);
        if (panel.gameObject.activeSelf || card.gameObject.activeSelf) InteractionGate.PushEscapeHandler(this, OnEscape);
    }

    bool OnEscape()
    {
        if (panel.gameObject.activeSelf) { TogglePanel(); return true; }
        if (card.gameObject.activeSelf) { ShowCard(false); return true; }
        return false;
    }

    void FillPanel()
    {
        if (session == null) return;
        for (int i = panelList.childCount - 1; i >= 0; i--) { var c = panelList.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
        var r = session.Evaluation;
        panelTitle.text = r == null ? "EVIDENCE NOTEBOOK" : "EVIDENCE REVIEW";
        panelIntro.text = r == null
            ? "Clues you found in this message. Click a clue to select it as evidence for your verdict (click again to deselect), then answer SCAM or LEGITIMATE as usual."
            : $"Correct answer: <b>{UIKit.VerdictWord(r.correct)}</b>   ·   your answer: <b>{UIKit.VerdictWord(r.submitted)}</b>   ·   key clues selected <b>{r.selectedCorrectly.Count} of {r.SupportingTotal}</b>";

        if (session.Discovered.Count == 0 && r == null)
            UIKit.Text(panelList, "Empty", "No clues yet. Click or tap the highlighted details in the message - real clues are added here automatically.", 20, Muted);

        foreach (var c in session.Discovered)
        {
            string id = c.id;
            bool on = session.IsSelected(id);
            var row = UIKit.Rect("Clue_" + id, panelList);
            var img = UIKit.AddImage(row, on ? new Color(0.05f, 0.29f, 0.42f, 1f) : UIKit.PanelRaised, rounded: true);
            var outline = row.gameObject.AddComponent<Outline>();
            outline.effectColor = on ? UIKit.Accent : UIKit.Border;
            outline.effectDistance = new Vector2(2f, -2f);
            var v = UIKit.Vertical(row, 16, 4);
            v.padding.left = 70;
            v.childForceExpandHeight = false;
            string state = r == null ? (on ? "SELECTED" : "NOT SELECTED")
                         : !c.supportsVerdict ? (on ? "SELECTED · NOT REAL EVIDENCE" : "NOT REAL EVIDENCE")
                         : on ? "✓ SELECTED · KEY CLUE" : "FOUND · NOT SELECTED";
            Color stateColor = r == null ? (on ? UIKit.Accent : Muted) : !c.supportsVerdict ? UIKit.Warning : on ? UIKit.Safe : UIKit.Accent;
            UIKit.Text(row, "State", state, 16, stateColor, bold: true).characterSpacing = 1.5f;
            UIKit.Text(row, "Title", c.title, 22, UIKit.TextPrimary, bold: true);
            UIKit.Text(row, "Why", c.whyItMatters, 18, Muted);

            var box = UIKit.Rect("Box", row);
            UIKit.Place(box, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(36, 36));
            box.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UIKit.AddImage(box, on ? UIKit.Accent : new Color(0f, 0f, 0f, 0.35f), rounded: true).raycastTarget = false;
            var be = box.gameObject.AddComponent<Outline>();
            be.effectColor = on ? UIKit.Accent : Muted;
            be.effectDistance = new Vector2(2f, -2f);
            if (on) { var tick = UIKit.Text(box, "Tick", "✓", 26, UIKit.Background, TextAlignmentOptions.Center, bold: true); UIKit.Stretch(tick.rectTransform); }

            if (r == null)
            {
                var b = row.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() => { session.ToggleSelected(id); FillPanel(); });
            }
        }

        if (r != null && r.missed.Count > 0)
        {
            UIKit.Text(panelList, "MissedHeader", "✗  MISSED  ·  NEVER DISCOVERED", 18, UIKit.Danger, bold: true).characterSpacing = 1.5f;
            foreach (var c in r.missed)
                UIKit.Text(panelList, "Missed", $"<b>{c.title}</b>  <color={HexSeen}>-  {c.whyItMatters}</color>", 18, UIKit.TextPrimary);
        }
    }

    // ---------- Build ----------

    void Build()
    {
        root = UIKit.CreateCanvas(transform, "Investigation Canvas", 35);   // above the HUD and phone pop-up, below results

        hint = UIKit.Text(root, "Hint", $"Click or tap the <color={HexLink}><u>highlighted details</u></color> to inspect them", 19, Muted, TextAlignmentOptions.BottomRight);
        UIKit.Place(hint.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0f), new Vector2(-28, 190), new Vector2(520, 30));

        tab = UIKit.Rect("NotebookTab", root);
        UIKit.Place(tab, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 150), new Vector2(420, 64));
        tabButton = UIKit.HudButton(tab, "EVIDENCE NOTEBOOK", TogglePanel, UIKit.HudVariant.Primary, 21);
        UIKit.Stretch((RectTransform)tabButton.transform);

        // Finding card (under the tab)
        card = UIKit.Rect("FindingCard", root);
        UIKit.Place(card, new Vector2(1, 0.5f), new Vector2(1, 1f), new Vector2(-28, 104), new Vector2(520, 0));
        UIKit.PanelImage(card);
        UIKit.AccentStrip(card, null, 6f);
        var cv = UIKit.Vertical(card, 22, 8);
        cv.padding.left = 30;
        cv.childForceExpandHeight = false;
        UIKit.FitHeight(card);
        var head = UIKit.Rect("Head", card);
        var hh = UIKit.Horizontal(head, 8, TextAnchor.MiddleLeft);
        hh.childForceExpandWidth = false;
        UIKit.Size(head, -1, 40);
        cardLabel = UIKit.Text(head, "Label", "", 26, UIKit.Accent, TextAlignmentOptions.MidlineLeft, bold: true);
        UIKit.Size(cardLabel, 360, 40).flexibleWidth = 1f;
        var close = UIKit.Button(head, "×", () => ShowCard(false), UIKit.Secondary, 30);
        UIKit.Size(close, 44, 40);
        close.transform.Find("Accent")?.gameObject.SetActive(false);
        cardStatus = UIKit.Text(card, "Status", "", 19, UIKit.TextPrimary, bold: true);
        linkBox = UIKit.Rect("LinkBox", card);
        UIKit.AddImage(linkBox, new Color(0.02f, 0.05f, 0.09f, 1f), rounded: true).raycastTarget = false;
        UIKit.Vertical(linkBox, 14, 4).childForceExpandHeight = false;
        linkText = UIKit.Text(linkBox, "Address", "", 18, UIKit.TextPrimary);
        cardFinding = UIKit.Text(card, "Finding", "", 22, UIKit.TextPrimary);
        cardFinding.lineSpacing = 4f;

        // Notebook panel (right side)
        panel = UIKit.Rect("NotebookPanel", root);
        UIKit.Place(panel, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(600, 0));
        UIKit.PanelImage(panel);
        UIKit.AccentStrip(panel, UIKit.Warning, 8f);
        var pv = UIKit.Vertical(panel, 28, 12);
        pv.padding.left = 36;
        pv.childForceExpandHeight = false;
        UIKit.FitHeight(panel);
        panelTitle = UIKit.Text(panel, "Title", "EVIDENCE NOTEBOOK", 28, UIKit.TextPrimary, bold: true);
        panelTitle.characterSpacing = 3f;
        panelIntro = UIKit.Text(panel, "Intro", "", 19, Muted);
        var viewport = UIKit.Rect("ListViewport", panel);
        listLayout = UIKit.Size(viewport, -1, 560);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        panelList = UIKit.Rect("List", viewport);
        panelList.anchorMin = new Vector2(0, 1); panelList.anchorMax = new Vector2(1, 1);
        panelList.pivot = new Vector2(0.5f, 1f);
        panelList.anchoredPosition = Vector2.zero;
        panelList.sizeDelta = Vector2.zero;
        UIKit.Vertical(panelList, 0, 10).childForceExpandHeight = false;
        panelList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = viewport.gameObject.AddComponent<ScrollRect>();
        sr.viewport = viewport;
        sr.content = panelList;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        var closeRow = UIKit.Rect("Buttons", panel);
        UIKit.Horizontal(closeRow, 0);
        UIKit.Size(closeRow, -1, 64);
        UIKit.HudButton(closeRow, "CLOSE NOTEBOOK", TogglePanel, UIKit.HudVariant.Secondary, 22);
    }
}
