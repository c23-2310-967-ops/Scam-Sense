using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// End-of-level results: score, accuracy, answering time, stars (revealed one by one),
/// room unlock status, lessons, and Continue / Replay / Main Menu buttons.
/// Builds itself at runtime. GameManager listens to the three button events.
/// </summary>
public class ResultsUI : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 40;
    [SerializeField] private LevelAudio levelAudio;

    public event Action ContinueClicked;
    public event Action ReplayClicked;
    public event Action MenuClicked;

    private RectTransform root;
    private CanvasGroup group;
    private TMP_Text title, scoreText, statsText, performanceText, unlockText, lessonsText;
    private Image unlockBox;
    private Outline unlockOutline;
    private readonly Image[] stars = new Image[3];
    private Button continueButton, replayButton, menuButton;

    static readonly Color StarOff = new Color(1f, 1f, 1f, 0.14f);

    void Awake()
    {
        Build();
        root.gameObject.SetActive(false);
    }

    public void Show(GameManager.LevelResult r)
    {
        title.text = $"CASE CLOSED  ·  LEVEL {r.levelNumber}: {r.levelName.ToUpperInvariant()}";
        scoreText.text = $"<size=76><b>{r.score}</b></size><size=36><color={UIKit.HexSecondary}> / {r.maxScore} points</color></size>";
        int total = r.totalMessages > 0 ? r.totalMessages : r.correct + r.incorrect;
        statsText.text =
            $"<color={UIKit.HexSafe}>✓</color> Correct <b>{r.correct} / {total}</b>     " +
            $"<color={UIKit.HexWarning}>×</color> Incorrect <b>{r.incorrect} / {total}</b>     " +
            $"Stars <b>{r.stars} / 3</b>     Best score <b>{r.bestScore}</b>\n" +
            $"Response time <b>{UIKit.FormatSeconds(r.totalAnswerTime)}</b> total  ·  <b>{UIKit.FormatSeconds(r.averageAnswerTime)}</b> average per message     " +
            $"Passing score <b>{r.requiredScore}</b>";
        performanceText.text = r.performanceMessage;

        string icon = r.nextRoomUnlocked ? $"<color={UIKit.HexSafe}>✓</color>  " : $"<color={UIKit.HexDanger}>×</color>  ";
        unlockText.text = icon + r.unlockMessage;
        Color c = r.nextRoomUnlocked ? UIKit.Safe : UIKit.Danger;
        unlockBox.color = new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.95f);
        unlockOutline.effectColor = c;

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(r.learningSummary))
            sb.Append($"<color={UIKit.HexSecondary}>{r.learningSummary}</color>\n");
        if (r.lessons != null)
            foreach (var l in r.lessons) sb.Append($"<color={UIKit.HexAccent}>•</color>  ").AppendLine(l);
        lessonsText.text = sb.ToString().TrimEnd();

        // Continue only when the next room is unlocked AND its scene can actually be loaded.
        continueButton.gameObject.SetActive(r.hasNextRoom);
        UIKit.SetInteractable(continueButton, r.nextRoomUnlocked && r.nextSceneAvailable);
        UIKit.SetLabel(continueButton, !r.nextRoomUnlocked ? "NEXT ROOM: LOCKED"
                                     : !r.nextSceneAvailable ? "NEXT ROOM: COMING SOON" : "NEXT ROOM");
        UIKit.SetInteractable(replayButton, true);
        UIKit.SetInteractable(menuButton, true);

        foreach (var s in stars) { s.color = StarOff; s.rectTransform.localScale = Vector3.one; }

        root.gameObject.SetActive(true);
        StartCoroutine(Reveal(r));
    }

    string ScoreLabel(int score, int max) =>
        $"<size=76><b>{score}</b></size><size=36><color={UIKit.HexSecondary}> / {max} points</color></size>";

    public void Hide() => root.gameObject.SetActive(false);

    void Click(Action evt)
    {
        // One click only - the scene is about to change.
        UIKit.SetInteractable(continueButton, false);
        UIKit.SetInteractable(replayButton, false);
        UIKit.SetInteractable(menuButton, false);
        if (levelAudio != null) levelAudio.PlayClick();
        evt?.Invoke();
    }

    IEnumerator Reveal(GameManager.LevelResult r)
    {
        int earned = r.stars;
        scoreText.text = ScoreLabel(0, r.maxScore);
        if (levelAudio != null) levelAudio.PlayLevelComplete();
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.35f; yield return null; }
        group.alpha = 1f;

        // Score counts up (ease-out) so the result lands with a little weight.
        const float countTime = 0.9f;
        for (float t = 0f; t < countTime; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / countTime, 3f);
            scoreText.text = ScoreLabel(Mathf.RoundToInt(r.score * k), r.maxScore);
            yield return null;
        }
        scoreText.text = ScoreLabel(r.score, r.maxScore);

        for (int i = 0; i < earned && i < stars.Length; i++)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (levelAudio != null) levelAudio.PlayStar();
            var s = stars[i];
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                s.color = Color.Lerp(StarOff, UIKit.Warning, k);
                float scale = k < 0.6f ? Mathf.Lerp(0.4f, 1.3f, k / 0.6f) : Mathf.Lerp(1.3f, 1f, (k - 0.6f) / 0.4f);
                s.rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }
            s.color = UIKit.Warning;
            s.rectTransform.localScale = Vector3.one;
        }

        // Unlocking is decided by score (GameManager); this only celebrates it.
        if (r.nextRoomUnlocked && r.hasNextRoom)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            if (levelAudio != null) levelAudio.PlayRoomUnlocked();
            var box = unlockBox.rectTransform;
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.45f;
                box.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.03f);
                yield return null;
            }
            box.localScale = Vector3.one;
        }
    }

    // ---------- Build ----------

    void Build()
    {
        root = UIKit.CreateCanvas(transform, "Results Canvas", sortingOrder);
        group = root.gameObject.AddComponent<CanvasGroup>();
        UIKit.Dimmer(root, 0.85f);

        var panel = UIKit.Rect("Panel", root);
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 0));
        UIKit.PanelImage(panel);
        var v = UIKit.Vertical(panel, 36, 12, TextAnchor.UpperCenter);
        v.padding.left = v.padding.right = 60;
        UIKit.FitHeight(panel);
        UIKit.AccentStrip(panel, null, 8f);

        title = UIKit.Text(panel, "Title", "", 30, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        title.characterSpacing = 3f;

        var starRow = UIKit.Rect("Stars", panel);
        var h = UIKit.Horizontal(starRow, 26);
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        UIKit.Size(starRow, -1, 100);
        for (int i = 0; i < 3; i++)
        {
            var s = UIKit.Rect("Star" + i, starRow);
            s.sizeDelta = i == 1 ? new Vector2(100, 100) : new Vector2(80, 80);
            stars[i] = UIKit.AddImage(s, StarOff, UIKit.StarSprite);
            stars[i].raycastTarget = false;
        }

        scoreText = UIKit.Text(panel, "Score", "", 40, UIKit.TextPrimary, TextAlignmentOptions.Center);
        statsText = UIKit.Text(panel, "Stats", "", 25, UIKit.TextPrimary, TextAlignmentOptions.Center);
        performanceText = UIKit.Text(panel, "Performance", "", 28, UIKit.Accent, TextAlignmentOptions.Center, bold: true);

        var box = UIKit.Rect("UnlockBox", panel);
        unlockBox = UIKit.AddImage(box, Color.clear, rounded: true);
        unlockOutline = box.gameObject.AddComponent<Outline>();
        unlockOutline.effectDistance = new Vector2(2, -2);
        UIKit.Vertical(box, 18, 0, TextAnchor.MiddleCenter);
        unlockText = UIKit.Text(box, "Unlock", "", 25, UIKit.TextPrimary, TextAlignmentOptions.Center);

        var lessons = UIKit.Rect("Lessons", panel);
        UIKit.Vertical(lessons, 0, 6);
        UIKit.Header(lessons, "What you learned");
        lessonsText = UIKit.Text(lessons, "LessonsText", "", UIKit.SizeSmall, UIKit.TextPrimary);
        lessonsText.lineSpacing = 6f;

        var row = UIKit.Rect("Buttons", panel);
        UIKit.Horizontal(row, 18);
        UIKit.Size(row, -1, 80);
        // Same HUD buttons + Digital Light Sweep as the main menu.
        continueButton = UIKit.HudButton(row, "NEXT ROOM", () => Click(ContinueClicked), UIKit.HudVariant.Primary);
        replayButton = UIKit.HudButton(row, "REPLAY LEVEL", () => Click(ReplayClicked), UIKit.HudVariant.Secondary);
        menuButton = UIKit.HudButton(row, "MAIN MENU", () => Click(MenuClicked), UIKit.HudVariant.Secondary);
    }
}
