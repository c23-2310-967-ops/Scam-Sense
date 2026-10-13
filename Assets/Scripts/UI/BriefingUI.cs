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
    private TMP_Text title, subtitle, body, notes, objectives, rules;
    private GameObject notesBlock;
    private Button beginButton;
    private Action onBegin;

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
        subtitle.text = $"LEVEL {level.levelNumber}  ·  {level.displayName.ToUpperInvariant()}  ·  CYBERCRIME INVESTIGATION UNIT";
        body.text = level.briefingText;

        bool hasNotes = level.caseNotes != null && level.caseNotes.Length > 0;
        notesBlock.SetActive(hasNotes);
        if (hasNotes)
        {
            var sb = new StringBuilder();
            foreach (var n in level.caseNotes) sb.Append($"<color={UIKit.HexAccent}>•</color>  ").AppendLine(n);
            notes.text = sb.ToString().TrimEnd();
        }

        var ob = new StringBuilder();
        if (level.objectives != null)
            for (int i = 0; i < level.objectives.Length; i++)
                ob.Append($"<color={UIKit.HexAccent}><b>{i + 1:00}</b></color>   ").AppendLine(level.objectives[i]);
        objectives.text = ob.ToString().TrimEnd();

        rules.text = $"Faster correct answers earn more points (up to {maxScore}). " +
                     $"Score at least <color={UIKit.HexAccent}><b>{level.minScoreToUnlockNext}</b></color> to unlock the next room.";

        UIKit.SetInteractable(beginButton, true);
        root.gameObject.SetActive(true);
        StartCoroutine(FadeIn());
    }

    void Begin()
    {
        if (!beginButton.interactable) return;
        UIKit.SetInteractable(beginButton, false);
        root.gameObject.SetActive(false);
        var cb = onBegin;
        onBegin = null;
        cb?.Invoke();
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

        var panel = UIKit.Rect("Panel", root);
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 0));
        UIKit.PanelImage(panel);
        var v = UIKit.Vertical(panel, 40, 12);
        v.padding.left = 60;
        v.padding.right = 56;
        UIKit.FitHeight(panel);
        UIKit.AccentStrip(panel, null, 8f);

        title = UIKit.Text(panel, "Title", "CASE BRIEFING", 44, UIKit.TextPrimary, bold: true);
        title.characterSpacing = 8f;
        subtitle = UIKit.Text(panel, "Subtitle", "", 21, UIKit.TextSecondary, bold: true);
        subtitle.characterSpacing = 2f;
        UIKit.Divider(panel, 3f).GetComponent<Image>().color = UIKit.Accent;

        body = UIKit.Text(panel, "Body", "", 28, UIKit.TextPrimary);
        body.lineSpacing = 10f;

        var nb = UIKit.Rect("NotesBlock", panel);
        UIKit.Vertical(nb, 0, 6);
        notesBlock = nb.gameObject;
        UIKit.Header(nb, "Case notes");
        notes = UIKit.Text(nb, "Notes", "", 25, UIKit.TextPrimary);
        notes.lineSpacing = 6f;

        UIKit.Header(panel, "Objectives");
        objectives = UIKit.Text(panel, "Objectives", "", 26, UIKit.TextPrimary);
        objectives.lineSpacing = 10f;

        UIKit.Header(panel, "Controls");
        UIKit.Text(panel, "Controls",
            "<b>WASD</b> move   ·   <b>Mouse</b> look   ·   <b>E</b> interact (phone, computer, chairs, doors)   ·   <b>Esc</b> menu\n" +
            "Text messages open on your phone with <b>E</b>. Emails are read on a <b>computer</b>: look at a monitor and press <b>E</b>. Open <b>Details</b> to investigate before answering.",
            UIKit.SizeSmall, UIKit.TextSecondary).lineSpacing = 6f;

        UIKit.Divider(panel);
        rules = UIKit.Text(panel, "Rules", "", UIKit.SizeSmall, UIKit.TextSecondary);

        var row = UIKit.Rect("ButtonRow", panel);
        var h = UIKit.Horizontal(row, 0);
        h.childForceExpandWidth = false;
        UIKit.Size(row, -1, 80);
        beginButton = UIKit.HudButton(row, "BEGIN INVESTIGATION", Begin, UIKit.HudVariant.Primary, 30);
        UIKit.Size(beginButton, 480, 80);
    }
}
