using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HINT button + hint box for a presenter's decision area (phone pop-up and computer). Visible only for messages
/// that carry a hint (ScamMessage.hint - currently the Level 1 tutorial questions), so every other room looks and
/// plays exactly as before. Pressing HINT shows the hint (which never states the answer), counts it once in the
/// case evaluation (ScoringSystem.RecordHint, shown as "Hints" on the results) and locks the button.
/// Scoring is unchanged: hints don't cost points on a first answer.
/// </summary>
public class HintControl
{
    public Button Button { get; private set; }
    public RectTransform Box { get; private set; }
    public bool Used { get; private set; }

    private TMP_Text text;
    private ScamMessage message;
    private System.Func<ScoringSystem> scoring;

    /// <summary>Builds the button (into <paramref name="buttonParent"/>) and the hint box (into <paramref name="boxParent"/>).</summary>
    public static HintControl Build(Transform buttonParent, Transform boxParent, System.Func<ScoringSystem> scoring,
                                    float buttonHeight = 56f, float fontSize = 22f, float buttonWidth = -1f)
    {
        var h = new HintControl { scoring = scoring };
        h.Button = UIKit.HudButton(buttonParent, "?  HINT", h.Reveal, UIKit.HudVariant.Secondary, fontSize);
        h.Button.name = "Hint Button";
        UIKit.Size(h.Button, buttonWidth, buttonHeight);

        h.Box = UIKit.Rect("HintBox", boxParent);
        UIKit.Callout(h.Box, UIKit.PopupTone.Warning);
        var v = UIKit.Vertical(h.Box, 14, 4);
        v.padding.left = v.padding.right = 18;
        h.text = UIKit.Text(h.Box, "Hint", "", fontSize - 1f, UIKit.TextPrimary);
        h.text.lineSpacing = 4f;
        h.Hide();
        return h;
    }

    /// <summary>A message opened: offer the button if it has a hint (and show it again if it was already used for this message).</summary>
    public void Show(ScamMessage msg)
    {
        message = msg;
        bool has = msg != null && msg.HasHint;
        Button.gameObject.SetActive(has);
        var sc = scoring?.Invoke();
        Used = has && sc != null && sc.HintUsedFor(msg.id);
        if (Used) Open(); else Box.gameObject.SetActive(false);
        UIKit.SetLabel(Button, Used ? "HINT SHOWN" : "?  HINT");
        UIKit.SetInteractable(Button, has && !Used);
    }

    public void Hide()
    {
        message = null;
        Used = false;
        Button.gameObject.SetActive(false);
        Box.gameObject.SetActive(false);
    }

    void Reveal()
    {
        if (message == null || !message.HasHint || Used) return;
        Used = true;
        scoring?.Invoke()?.RecordHint(message.id);
        Open();
        UIKit.SetLabel(Button, "HINT SHOWN");
        UIKit.SetInteractable(Button, false);
    }

    void Open()
    {
        text.text = $"<color={UIKit.HexWarning}><b>HINT</b></color>   {message.hint}";
        Box.gameObject.SetActive(true);
    }
}
