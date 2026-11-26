using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a question's interactive activity (<see cref="MessageActivity"/>): a simulated app - mail client, browser,
/// chat, call transcript, social post, form, Wi-Fi list, file explorer or evidence folder - with an evidence board
/// underneath. Underlined details are inspected with the normal clue system (<see cref="MessageInvestigation"/>):
/// key clues land on the board, and the latest finding is explained under it.
///
/// Reusable: any presenter can host it (<see cref="Build"/> into a vertical layout, <see cref="Show"/> per message).
/// Everything is simulated - nothing is opened, sent or typed, and no real data is asked for.
/// </summary>
public class ActivityView : MonoBehaviour
{
    // Dark navy app colours; cyan = inspect, green = safe / done, red = suspicious, amber = key clue.
    static readonly Color Frame = new Color32(0x0A, 0x14, 0x26, 255);
    static readonly Color Bar = new Color32(0x0B, 0x2A, 0x45, 255);
    static readonly Color Row = new Color32(0x13, 0x22, 0x3A, 255);
    static readonly Color BubbleIn = new Color32(0x1E, 0x2D, 0x48, 255);
    static readonly Color BubbleOut = new Color32(0x0E, 0x4A, 0x6B, 255);
    static readonly Color BannerBg = new Color32(0x3A, 0x2C, 0x07, 255);
    static readonly Color SlotEmpty = new Color32(0x12, 0x1C, 0x2E, 255);
    static readonly Color SlotFound = new Color32(0x0E, 0x3B, 0x27, 255);   // collected = green
    const string HexLabel = "#8FA3BF", HexClue = "#FBBF24", HexMuted = "#94A3B8";

    RectTransform root, screen, board, slots;
    TMP_Text barTitle, kindBadge, instruction, boardCount, finding;
    readonly List<GameObject> built = new List<GameObject>();
    readonly List<(MessageEvidence clue, Image bg, TMP_Text text)> slotViews = new List<(MessageEvidence, Image, TMP_Text)>();
    ScamMessage msg;

    public bool IsShowing => root != null && root.gameObject.activeSelf && msg != null;
    public ScamMessage Message => msg;

    /// <summary>Creates the view inside <paramref name="parent"/> (a vertical layout). Hidden until <see cref="Show"/>.</summary>
    public static ActivityView Build(RectTransform parent, float width)
    {
        var r = UIKit.Rect("Activity", parent);
        var view = r.gameObject.AddComponent<ActivityView>();
        view.root = r;
        UIKit.Vertical(r, 0, 10).childForceExpandHeight = false;
        if (width > 0f) UIKit.Size(r, width);

        // The simulated app: title bar + screen
        var app = UIKit.Rect("App", r);
        UIKit.AddImage(app, Frame, rounded: true).raycastTarget = false;
        var outline = app.gameObject.AddComponent<Outline>();
        outline.effectColor = UIKit.Border;
        outline.effectDistance = new Vector2(2f, -2f);
        UIKit.Vertical(app, 0, 0).childForceExpandHeight = false;

        var bar = UIKit.Rect("TitleBar", app);
        UIKit.AddImage(bar, Bar, rounded: true).raycastTarget = false;
        var bh = UIKit.Horizontal(bar, 12, TextAnchor.MiddleLeft);
        bh.padding = new RectOffset(16, 16, 8, 8);
        bh.childForceExpandWidth = false;
        UIKit.Size(bar, -1, 54);
        view.kindBadge = UIKit.Badge(bar, "MAIL", UIKit.AccentDark, UIKit.TextPrimary, 15);
        view.barTitle = UIKit.Text(bar, "Title", "", 18, UIKit.TextSecondary, TextAlignmentOptions.MidlineLeft, bold: true);
        view.barTitle.textWrappingMode = TextWrappingModes.NoWrap;
        view.barTitle.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Size(view.barTitle, 600, 38).flexibleWidth = 1f;

        view.screen = UIKit.Rect("Screen", app);
        var sv = UIKit.Vertical(view.screen, 18, 12);
        sv.padding.left = sv.padding.right = 22;
        sv.childForceExpandHeight = false;

        view.instruction = UIKit.Text(view.screen, "Instruction", "", 18, UIKit.Accent);   // first line of the screen (dark, readable on any sheet)

        // Evidence board: one slot per key clue (★ when found), then the latest finding.
        view.board = UIKit.Rect("EvidenceBoard", r);
        UIKit.AddImage(view.board, Frame, rounded: true).raycastTarget = false;
        var bv = UIKit.Vertical(view.board, 16, 10);
        bv.padding.left = bv.padding.right = 20;
        bv.childForceExpandHeight = false;
        var head = UIKit.Rect("Head", view.board);
        var hh = UIKit.Horizontal(head, 8, TextAnchor.MiddleLeft);
        hh.childForceExpandWidth = false;
        UIKit.Size(head, -1, 32);
        var title = UIKit.Header(head, "Evidence board", 17);
        UIKit.Size(title, 300, 32).flexibleWidth = 1f;
        view.boardCount = UIKit.Text(head, "Count", "", 17, UIKit.TextSecondary, TextAlignmentOptions.MidlineRight, bold: true);
        UIKit.Size(view.boardCount, 460, 32);
        view.slots = UIKit.Rect("Slots", view.board);
        var sh = UIKit.Horizontal(view.slots, 10, TextAnchor.MiddleLeft);
        sh.childForceExpandHeight = true;
        view.finding = UIKit.Text(view.board, "Finding", "", 18, UIKit.TextPrimary);
        view.finding.lineSpacing = 4f;

        r.gameObject.SetActive(false);
        return view;
    }

    // ---------- Show / hide ----------

    /// <summary>Draws <paramref name="m"/>'s activity. Call after the investigation has begun, so details bind as clickable.</summary>
    public void Show(ScamMessage m)
    {
        Clear();
        msg = m;
        if (m == null || !m.HasActivity) { root.gameObject.SetActive(false); return; }
        var a = m.activity;

        kindBadge.text = KindLabel(a.kind);
        barTitle.text = Literal(a.title) + "   <color=#7C8BA1><size=80%>SIMULATED · FICTIONAL</size></color>";
        instruction.text = string.IsNullOrEmpty(a.instruction) ? DefaultInstruction(a.kind) : a.instruction;
        instruction.text = "<b>TASK</b>   <color=#E2E8F0>" + instruction.text + "</color>";

        foreach (var item in a.items) built.Add(BuildItem(screen, m, item));

        // Board slots: one per key clue, in the authored order.
        if (m.evidence != null)
            foreach (var c in m.evidence)
            {
                if (!c.supportsVerdict) continue;
                var slot = UIKit.Rect("Slot", slots);
                var bg = UIKit.AddImage(slot, SlotEmpty, rounded: true);
                bg.raycastTarget = false;
                UIKit.Vertical(slot, 8, 0).childAlignment = TextAnchor.MiddleCenter;
                var t = UIKit.Text(slot, "Text", "", 16, UIKit.TextSecondary, TextAlignmentOptions.Center, bold: true);
                UIKit.Size(slot, -1, 66).flexibleWidth = 1f;
                slotViews.Add((c, bg, t));
                built.Add(slot.gameObject);
            }
        board.gameObject.SetActive(slotViews.Count > 0 && MessageInvestigation.Investigates(m));
        finding.text = $"<color={HexMuted}>Click the <color=#7DD3FC><u>underlined details</u></color> above. Key clues are pinned here with a <color={HexClue}>★</color>.</color>";
        RefreshBoard();
        root.gameObject.SetActive(true);
        ApplyScale(1f);
    }

    // ---------- Text size ----------

    /// <summary>All activity text is authored at a compact size and drawn this much larger, for readability.</summary>
    public const float TextBoost = 1.25f;
    readonly Dictionary<TMP_Text, float> baseSizes = new Dictionary<TMP_Text, float>();
    public float Scale { get; private set; } = 1f;

    /// <summary>
    /// Sets every text in the view to its authored size x <see cref="TextBoost"/> x <paramref name="k"/>. The host
    /// lowers k a step at a time only when a long case wouldn't fit on screen.
    /// </summary>
    public void ApplyScale(float k)
    {
        Scale = k;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!baseSizes.TryGetValue(t, out float size)) baseSizes[t] = size = t.fontSize;
            t.fontSize = Mathf.Round(size * TextBoost * k);
        }
    }

    public void Hide()
    {
        Clear();
        msg = null;
        if (root != null) root.gameObject.SetActive(false);
    }

    /// <summary>Hides the app but keeps nothing else (used when a case report needs the room).</summary>
    public void Collapse() { if (root != null) root.gameObject.SetActive(false); }

    void Clear()
    {
        foreach (var go in built) if (go != null) { go.SetActive(false); Destroy(go); }
        built.Clear();
        slotViews.Clear();
        var dead = new List<TMP_Text>();
        foreach (var t in baseSizes.Keys) if (t == null) dead.Add(t);   // texts of earlier activities (destroyed)
        foreach (var t in dead) baseSizes.Remove(t);
    }

    // ---------- Evidence ----------

    /// <summary>Key clues found so far / in total (0 / 0 when the activity isn't investigated).</summary>
    public int KeyFound => Session != null ? Session.KeyFound : 0;
    public int KeyTotal => Session != null ? Session.KeyTotal : 0;

    InvestigationSession Session
    {
        get
        {
            var a = MessageInvestigation.Active;
            return a != null && a.Session != null && a.Session.Message == msg ? a.Session : null;
        }
    }

    /// <summary>Shows an inspection result on the board. Returns false if this view isn't showing that message.</summary>
    public bool ShowFinding(ScamMessage m, MessageInspectable part, MessageEvidence clue, InvestigationSession.InspectOutcome outcome)
    {
        if (!IsShowing || m != msg || part == null) return false;
        var s = Session;
        string status =
            s != null && s.Submitted ? $"<color={HexMuted}><b>REVIEWING</b></color>" :
            outcome == InvestigationSession.InspectOutcome.NewClue && clue != null && clue.supportsVerdict ? $"<color={UIKit.HexSafe}><b>★ KEY CLUE FOUND</b></color>  <b>{clue.title}</b>" :
            outcome == InvestigationSession.InspectOutcome.NewClue && clue != null ? $"<color={UIKit.HexAccent}><b>NOTED</b></color>  <b>{clue.title}</b>" :
            outcome == InvestigationSession.InspectOutcome.KnownClue && clue != null ? $"<color={UIKit.HexAccent}><b>ALREADY ON THE BOARD</b></color>  {clue.title}" :
            $"<color={HexMuted}><b>✓ CHECKED</b>  · not evidence on its own</color>";
        var sb = new StringBuilder();
        sb.Append(status).Append('\n');
        sb.Append($"<color={UIKit.HexAccent}>{Literal(part.label)}:</color> ").Append(Literal(part.finding));
        if (part.IsLink && !string.IsNullOrEmpty(part.linkAddress))
            sb.Append($"\n<color={HexMuted}><size=90%>Simulated link · not opened: </size></color><color=#7DD3FC>{Literal(part.linkAddress)}</color>");
        finding.text = sb.ToString();
        RefreshBoard();
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(Pulse(board));
        return true;
    }

    /// <summary>After answering: the board shows which key clues were missed. False if nothing is on a board.</summary>
    public bool RevealMissed()
    {
        if (!IsShowing || !board.gameObject.activeSelf || slotViews.Count == 0) return false;
        RefreshBoard();
        finding.text = $"<color={HexMuted}>Case closed. Missed clues are marked in red - you can still click any detail to review it.</color>";
        return true;
    }
    static readonly Color MissedBg = new Color32(0x3B, 0x13, 0x18, 255);

    void RefreshBoard()
    {
        var s = Session;
        bool closed = s != null && s.Submitted;   // after answering, unfound clues are shown as missed
        int found = 0;
        foreach (var (clue, bg, text) in slotViews)
        {
            bool got = s != null && s.IsDiscovered(clue.id);
            if (got) found++;
            bg.color = got ? SlotFound : closed ? MissedBg : SlotEmpty;
            text.text = got ? $"<color={UIKit.HexSafe}>✓</color> {Literal(clue.title)}"
                      : closed ? $"<color={UIKit.HexDanger}>✗</color> {Literal(clue.title)} <color={HexMuted}><size=85%>(missed)</size></color>"
                      : $"<color={HexMuted}>?  not found yet</color>";
            text.color = got || closed ? UIKit.TextPrimary : UIKit.TextSecondary;
        }
        int total = slotViews.Count;
        boardCount.text = total == 0 ? ""
            : found >= total ? $"<color={UIKit.HexSafe}>✓ ALL {total} KEY CLUES · INVESTIGATION COMPLETE</color>"
            : closed ? $"<color={HexClue}>★</color> {found} / {total} FOUND  ·  <color={UIKit.HexDanger}>{total - found} MISSED</color>"
            : $"<color={HexClue}>★</color> {found} / {total} KEY CLUES";
    }

    Coroutine popRoutine;

    static System.Collections.IEnumerator Pulse(RectTransform r)
    {
        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Sin(t / 0.22f * Mathf.PI);
            r.localScale = Vector3.one * (1f + 0.015f * k);
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    // ---------- Items ----------

    static GameObject BuildItem(RectTransform parent, ScamMessage m, ActivityItem item)
    {
        switch (item.role)
        {
            case ActivityRole.Field:
            {
                var t = UIKit.Text(parent, "Field_" + item.key, "", 20, UIKit.TextPrimary);
                string label = string.IsNullOrEmpty(item.label) ? "" : $"<color={HexLabel}>{Literal(item.label)}:</color>  ";
                MessageInvestigation.Bind(t, () => label + MessageInvestigation.MarkActivity(m, item));
                return t.gameObject;
            }
            case ActivityRole.Incoming:
            case ActivityRole.Outgoing:
            {
                bool mine = item.role == ActivityRole.Outgoing;
                var line = UIKit.Rect("Bubble_" + item.key, parent);
                var lv = UIKit.Vertical(line, 0, 2);
                lv.padding = new RectOffset(mine ? 140 : 0, mine ? 0 : 140, 0, 0);
                lv.spacing = 4;
                lv.childForceExpandHeight = false;
                if (!string.IsNullOrEmpty(item.label))
                    UIKit.Text(line, "Who", Literal(item.label), 15, new Color32(0x93, 0xC5, 0xFD, 255), mine ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft, bold: true);
                var bubble = UIKit.Rect("Bubble", line);
                UIKit.AddImage(bubble, mine ? BubbleOut : BubbleIn, rounded: true).raycastTarget = false;
                var bv = UIKit.Vertical(bubble, 14, 0);
                bv.padding.left = bv.padding.right = 18;
                var t = UIKit.Text(bubble, "Text", "", 20, UIKit.TextPrimary);
                MessageInvestigation.Bind(t, () => MessageInvestigation.MarkActivity(m, item));
                return line.gameObject;
            }
            case ActivityRole.File:
            case ActivityRole.Row:
            {
                bool file = item.role == ActivityRole.File;
                var row = UIKit.Rect((file ? "File_" : "Row_") + item.key, parent);
                UIKit.AddImage(row, Row, rounded: true).raycastTarget = false;
                var rh = UIKit.Horizontal(row, 12, TextAnchor.MiddleLeft);
                rh.padding = new RectOffset(14, 16, 12, 12);
                rh.childForceExpandWidth = false;
                rh.childForceExpandHeight = false;
                if (file)
                {
                    var icon = UIKit.Badge(row, FileTag(item.text), new Color32(0x1E, 0x3A, 0x5F, 255), UIKit.TextPrimary, 14);
                    UIKit.Size(icon.transform.parent, 84, 36);
                }
                var t = UIKit.Text(row, "Text", "", 20, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft);
                UIKit.Size(t, 500).flexibleWidth = 1f;
                MessageInvestigation.Bind(t, () => MessageInvestigation.MarkActivity(m, item));
                if (!string.IsNullOrEmpty(item.label))
                {
                    var tag = UIKit.Text(row, "Tag", Literal(item.label).ToUpperInvariant(), 14, new Color32(0x93, 0xC5, 0xFD, 255), TextAlignmentOptions.MidlineRight, bold: true);
                    tag.characterSpacing = 1.5f;
                    UIKit.Size(tag, 200);
                }
                return row.gameObject;
            }
            case ActivityRole.Banner:
            {
                var box = UIKit.Rect("Banner_" + item.key, parent);
                UIKit.AddImage(box, BannerBg, rounded: true).raycastTarget = false;
                var bv = UIKit.Vertical(box, 12, 0);
                bv.padding.left = bv.padding.right = 18;
                var t = UIKit.Text(box, "Text", "", 20, new Color32(0xFD, 0xE6, 0x8A, 255));
                string label = string.IsNullOrEmpty(item.label) ? "" : $"<b>{Literal(item.label)}</b>  ";
                MessageInvestigation.Bind(t, () => label + MessageInvestigation.MarkActivity(m, item));
                return box.gameObject;
            }
            case ActivityRole.Button:
            {
                var holder = UIKit.Rect("Button_" + item.key, parent);
                var hh = UIKit.Horizontal(holder, 0, TextAnchor.MiddleLeft);
                hh.childForceExpandWidth = false;
                var pill = UIKit.Rect("Pill", holder);
                UIKit.AddImage(pill, UIKit.AccentDark, rounded: true).raycastTarget = false;
                var pv = UIKit.Vertical(pill, 8, 0);
                pv.padding.left = pv.padding.right = 22;
                pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                var t = UIKit.Text(pill, "Text", "", 19, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                MessageInvestigation.Bind(t, () => MessageInvestigation.MarkActivity(m, item));
                return holder.gameObject;
            }
            case ActivityRole.Note:
            {
                var t = UIKit.Text(parent, "Note_" + item.key, "", 17, UIKit.TextSecondary);
                t.fontStyle = FontStyles.Italic;
                MessageInvestigation.Bind(t, () => MessageInvestigation.MarkActivity(m, item));
                return t.gameObject;
            }
            default:
            {
                var t = UIKit.Text(parent, "Text_" + item.key, "", 20, UIKit.TextPrimary);
                t.lineSpacing = 4f;
                string label = string.IsNullOrEmpty(item.label) ? "" : $"<color={HexLabel}><b>{Literal(item.label)}</b></color>\n";
                MessageInvestigation.Bind(t, () => label + MessageInvestigation.MarkActivity(m, item));
                return t.gameObject;
            }
        }
    }

    // ---------- Labels ----------

    public static string KindLabel(ActivityKind k) =>
        k == ActivityKind.Email ? "MAIL" : k == ActivityKind.Browser ? "BROWSER" : k == ActivityKind.Chat ? "CHAT"
        : k == ActivityKind.Call ? "CALL" : k == ActivityKind.Social ? "SOCIAL" : k == ActivityKind.Form ? "FORM"
        : k == ActivityKind.Network ? "WI-FI" : k == ActivityKind.Files ? "FILES" : k == ActivityKind.Evidence ? "EVIDENCE" : "";

    public static string DefaultInstruction(ActivityKind k) =>
        k == ActivityKind.Email ? "Inspect the email: sender, subject, links and attachments."
        : k == ActivityKind.Browser ? "Inspect the page: the address bar, the padlock and what it asks for."
        : k == ActivityKind.Chat ? "Read the chat. Click anything that looks off."
        : k == ActivityKind.Call ? "Read the call transcript. Find the red flags."
        : k == ActivityKind.Social ? "Inspect the post and its comments."
        : k == ActivityKind.Form ? "Inspect the form. Which fields go too far?"
        : k == ActivityKind.Network ? "Inspect the networks and the sign-in page."
        : k == ActivityKind.Files ? "Inspect the files and the note."
        : "Inspect each piece of evidence.";

    /// <summary>A short type tag for a file row: the file's extension ("DOCM"), or "FILE".</summary>
    public static string FileTag(string name)
    {
        if (string.IsNullOrEmpty(name)) return "FILE";
        string first = name.Split(' ')[0];
        int dot = first.LastIndexOf('.');
        if (dot < 0 || dot == first.Length - 1) return "FILE";
        string ext = first.Substring(dot + 1).Trim(',', ';', ')').ToUpperInvariant();
        return ext.Length > 0 && ext.Length <= 6 ? ext : "FILE";
    }

    static string Literal(string s) => string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "") + "</noparse>";
}
