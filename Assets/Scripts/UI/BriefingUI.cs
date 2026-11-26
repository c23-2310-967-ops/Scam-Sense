using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Mission briefing shown when the level starts. Builds itself at runtime.
/// Press the button (or Enter) to begin.
/// </summary>
public class BriefingUI : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 30;

    private RectTransform root;
    private CanvasGroup group;
    private TMP_Text title, subtitle, body, notes, objectives, rules, goal;
    private GameObject notesBlock;
    private Button beginButton;
    private Action onBegin;
    private PopupAnimator panelAnimator;
    private PopupIcon infoIcon;

    void Awake()
    {
        Build();
        root.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!root.gameObject.activeSelf || Keyboard.current == null || PauseMenu.IsPaused) return;
        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            Begin();
    }

    public void Show(LevelDefinition level, int maxScore, Action begin)
    {
        onBegin = begin;
        title.text = level.briefingTitle;
        // Don't repeat the level name when the title already says it.
        bool titleHasName = !string.IsNullOrEmpty(level.briefingTitle) && !string.IsNullOrEmpty(level.displayName) &&
                            level.briefingTitle.IndexOf(level.displayName, StringComparison.OrdinalIgnoreCase) >= 0;
        subtitle.text = titleHasName ? "CASE BRIEFING" : $"LEVEL {level.levelNumber}  ·  {level.displayName.ToUpperInvariant()}";
        body.text = level.briefingText;

        bool hasNotes = level.caseNotes != null && level.caseNotes.Length > 0;
        notesBlock.SetActive(hasNotes);
        if (hasNotes)
        {
            var sb = new StringBuilder();
            foreach (var n in level.caseNotes) sb.Append($"<color={UIKit.HexAccent}>•</color>  ").AppendLine(n);
            notes.text = sb.ToString().TrimEnd();
        }

        // Mission = the objectives as short numbered steps.
        var ob = new StringBuilder();
        if (level.objectives != null)
            for (int i = 0; i < level.objectives.Length; i++)
                ob.AppendLine(UIKit.Step(i + 1, level.objectives[i]));
        objectives.text = ob.ToString().TrimEnd();

        // One goal line the player must not miss, then the scoring rules in small print.
        bool last = string.IsNullOrEmpty(level.nextLevelId);
        goal.text = $"<b>GOAL</b>   Score <color={UIKit.HexAccent}><b>{level.minScoreToUnlockNext}</b></color> of {maxScore} points to " +
                    (last ? "finish the investigation." : "unlock the next room.");
        rules.text = level.scoring != null ? level.scoring.Summary() : "";
        rules.gameObject.SetActive(!string.IsNullOrEmpty(rules.text));

        UIKit.SetInteractable(beginButton, true);
        group.blocksRaycasts = true;
        root.gameObject.SetActive(true);
        StartCoroutine(FadeIn());
        panelAnimator.Open();
        infoIcon.Play();
    }

    void Begin()
    {
        if (!beginButton.interactable) return;
        UIKit.SetInteractable(beginButton, false);
        // The level starts right away (same as before); only the overlay fades out over a few frames, without blocking clicks.
        group.blocksRaycasts = false;
        StartCoroutine(FadeOut());
        var cb = onBegin;
        onBegin = null;
        cb?.Invoke();
    }

    IEnumerator FadeOut()
    {
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.18f;
            yield return null;
        }
        group.alpha = 1f;
        root.gameObject.SetActive(false);
    }

    IEnumerator FadeIn()
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / 0.35f;
            yield return null;
        }
        group.alpha = 1f;
    }

    // ---------- Build ----------

    void Build()
    {
        root = UIKit.CreateCanvas(transform, "Briefing Canvas", sortingOrder);
        group = root.gameObject.AddComponent<CanvasGroup>();
        UIKit.Dimmer(root, 0.82f);

        // Clear hierarchy, top to bottom: title -> short story -> what to remember -> mission steps -> controls -> goal -> START.
        var panel = UIKit.Rect("Panel", root);
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 0));
        UIKit.PanelImage(panel);
        var v = UIKit.Vertical(panel, 40, 16);
        v.padding.left = 60;
        v.padding.right = 56;
        UIKit.FitHeight(panel);
        UIKit.AccentStrip(panel, null, 8f);

        panelAnimator = panel.gameObject.AddComponent<PopupAnimator>();

        // Header: info icon + title + level line (an information pop-up)
        var head = UIKit.IconHeader(panel, UIKit.PopupTone.Info, out infoIcon, 72f, 24f);
        title = UIKit.Text(head, "Title", "CASE BRIEFING", 42, UIKit.TextPrimary, bold: true);
        title.characterSpacing = 6f;
        subtitle = UIKit.Text(head, "Subtitle", "", 21, UIKit.Accent, bold: true);
        subtitle.characterSpacing = 3f;
        UIKit.Divider(panel, 3f).GetComponent<Image>().color = UIKit.Accent;

        body = UIKit.Text(panel, "Body", "", UIKit.SizeBody, UIKit.TextPrimary);
        body.lineSpacing = 8f;

        // Case notes: a soft callout, only when the room has notes.
        var nb = UIKit.Rect("NotesBlock", panel);
        UIKit.Callout(nb, UIKit.PopupTone.Info);
        var nv = UIKit.Vertical(nb, 18, 6);
        nv.padding.left = 24;
        notesBlock = nb.gameObject;
        UIKit.Header(nb, "Remember", 20);
        notes = UIKit.Text(nb, "Notes", "", UIKit.SizeSmall, UIKit.TextPrimary);
        notes.lineSpacing = 6f;

        UIKit.Header(panel, "Your mission");
        objectives = UIKit.Text(panel, "Objectives", "", UIKit.SizeBody - 1f, UIKit.TextPrimary);
        objectives.lineSpacing = 12f;

        UIKit.Header(panel, "Controls");
        UIKit.Text(panel, "Controls",
            UIKit.ControlStrip() + "\n" +
            $"<color={UIKit.HexSecondary}>Texts open on your <b>phone</b>. Emails open on a <b>computer</b>: look at the screen and press {UIKit.Key("E")}.</color>",
            UIKit.SizeSmall, UIKit.TextPrimary).lineSpacing = 12f;

        // Goal: the one line the player must not miss, with the scoring rules under it in small print.
        var gb = UIKit.Rect("GoalBlock", panel);
        UIKit.Callout(gb, UIKit.PopupTone.Success);
        var gv = UIKit.Vertical(gb, 16, 4);
        gv.padding.left = 24;
        goal = UIKit.Text(gb, "Goal", "", UIKit.SizeBody - 2f, UIKit.TextPrimary);
        rules = UIKit.Text(gb, "Rules", "", 20, UIKit.TextSecondary);

        var row = UIKit.Rect("ButtonRow", panel);
        var h = UIKit.Horizontal(row, 24, TextAnchor.MiddleLeft);
        h.childForceExpandWidth = false;
        UIKit.Size(row, -1, 80);
        beginButton = UIKit.HudButton(row, "BEGIN INVESTIGATION", Begin, UIKit.HudVariant.Primary, 30);
        UIKit.Size(beginButton, 480, 80);
        var enter = UIKit.Text(row, "EnterHint", $"or press {UIKit.Key("Enter")}", UIKit.SizeSmall, UIKit.TextSecondary, TextAlignmentOptions.MidlineLeft);
        UIKit.Size(enter, 260, 80);
    }
}
