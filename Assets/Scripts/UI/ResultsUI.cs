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

    // Career section + promotion card
    private RectTransform careerRow, barFill, promo, promoBadgeRt, promoGlow;
    private Image careerBadge, promoBadge;
    private TMP_Text careerOutcome, careerRank, careerProgress, careerLines, careerXp, achievementsText, promoTitle, promoText;
    private CanvasGroup promoGroup;
    private Button promoContinue;

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
            $"Stars <b>{r.stars} / 3</b>     Best score <b>{r.bestScore}</b>     Passing score <b>{r.requiredScore}</b>\n" +
            $"Case grade <color={UIKit.HexAccent}><b>{r.caseGrade}</b></color>   ·   Accuracy <b>{Mathf.RoundToInt(r.accuracy * 100f)}%</b>   ·   " +
            $"Evidence found <b>{r.evidenceFound}</b>   ·   Corrected <b>{r.recovered}/{r.incorrect}</b>   ·   Hints <b>{r.hintsUsed}</b>   ·   " +
            $"Avg response <b>{UIKit.FormatSeconds(r.averageAnswerTime)}</b>";
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
        PrepareCareer(r.career);

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

        if (r.career != null) yield return AnimateCareer(r.career);
    }

    // ---------- Career ----------

    /// <summary>Lowest quality level = reduced effects: the career section appears without animation.</summary>
    static bool ReducedEffects => QualitySettings.GetQualityLevel() == 0;

    void PrepareCareer(CareerSystem.CaseCareer c)
    {
        careerRow.gameObject.SetActive(c != null);
        promo.gameObject.SetActive(false);
        if (c == null) return;
        careerOutcome.text = $"<color={(c.solved ? UIKit.HexSafe : UIKit.HexWarning)}>{c.outcome}</color>   <color={UIKit.HexSecondary}>·   CASE {c.caseNumber:00}  {c.caseName.ToUpperInvariant()}</color>";
        var sb = new StringBuilder();
        foreach (var l in c.lines) sb.Append($"{l.label}  <color={UIKit.HexAccent}>+{l.xp}</color>     ");
        if (c.awardedXp < c.runXp)
            sb.Append(c.awardedXp == 0
                ? $"\n<color={UIKit.HexSecondary}>This run: {c.runXp} XP. Your best for this case is {c.previousBestXp} XP - beat it to earn more.</color>"
                : $"\n<color={UIKit.HexSecondary}>This run: {c.runXp} XP - {c.awardedXp} more than your previous best ({c.previousBestXp} XP).</color>");
        careerLines.text = sb.ToString().TrimEnd();
        if (c.newAchievements.Count == 0) achievementsText.text = $"<color={UIKit.HexSecondary}>None this time.</color>";
        else
        {
            var a = new StringBuilder();
            foreach (var d in c.newAchievements)
                a.Append($"<color={UIKit.HexWarning}>★</color>  <b>{d.title}</b>\n<size=80%><color={UIKit.HexSecondary}>{d.description}</color></size>\n");
            achievementsText.text = a.ToString().TrimEnd();
        }
        ShowCareerState(c.previousTotalXp, 0);
    }

    /// <summary>Badge, rank name, bar and "x / y XP" for a career total; <paramref name="shownAward"/> is the XP counter.</summary>
    void ShowCareerState(int totalXp, int shownAward)
    {
        CareerSystem.ProgressInRank(totalXp, out int rank, out int into, out int span);
        var rk = CareerSystem.RankAt(rank);
        careerBadge.sprite = RankBadge.For(rank);
        careerBadge.color = rk.color;
        careerRank.text = $"<size=60%><color={UIKit.HexSecondary}>CURRENT RANK</color></size>\n{rk.title}";
        float k = span > 0 ? Mathf.Clamp01((float)into / span) : 1f;
        barFill.anchorMax = new Vector2(k, 1f);
        barFill.GetComponent<Image>().color = rk.color;
        careerProgress.text = span > 0
            ? $"<b>{into:N0} / {span:N0} XP</b> to <b>{CareerSystem.RankAt(rank + 1).title}</b>   <color={UIKit.HexSecondary}>·   {totalXp:N0} career XP</color>"
            : $"<b>Highest rank reached</b>   <color={UIKit.HexSecondary}>·   {totalXp:N0} career XP</color>";
        careerXp.text = $"+{shownAward} XP";
    }

    IEnumerator AnimateCareer(CareerSystem.CaseCareer c)
    {
        if (ReducedEffects || c.awardedXp <= 0)
        {
            ShowCareerState(c.totalXp, c.awardedXp);
            if (c.Promoted) ShowPromotion(c, !ReducedEffects);
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.3f);
        int shownRank = c.previousRank;
        float duration = Mathf.Clamp(0.9f + 0.4f * (c.rank - c.previousRank), 0.9f, 2.4f);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / duration, 2f);
            int xp = c.previousTotalXp + Mathf.RoundToInt(c.awardedXp * k);
            ShowCareerState(xp, Mathf.RoundToInt(c.awardedXp * k));
            int now = CareerSystem.RankIndexFor(xp);
            if (now != shownRank)
            {
                shownRank = now;
                StartCoroutine(Pulse(careerBadge.rectTransform));
                if (levelAudio != null) levelAudio.PlayStar();
            }
            yield return null;
        }
        ShowCareerState(c.totalXp, c.awardedXp);
        if (c.Promoted)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            ShowPromotion(c, true);
        }
    }

    IEnumerator Pulse(RectTransform rt)
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            rt.localScale = Vector3.one * (1f + Mathf.Sin(t / 0.35f * Mathf.PI) * 0.18f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    /// <summary>Promotion card - only when a new rank was actually earned. Explains what earned it.</summary>
    void ShowPromotion(CareerSystem.CaseCareer c, bool animate)
    {
        var rk = CareerSystem.RankAt(c.rank);
        promoBadge.sprite = RankBadge.For(c.rank);
        promoBadge.color = rk.color;
        promoGlow.GetComponent<Image>().color = new Color(rk.color.r, rk.color.g, rk.color.b, 0.35f);
        promoTitle.text = $"<size=55%><color={UIKit.HexAccent}>PROMOTION</color></size>\n{rk.title.ToUpperInvariant()}";
        var sb = new StringBuilder();
        sb.Append($"Solving <b>Case {c.caseNumber:00}: {c.caseName}</b> took your career to <b>{c.totalXp:N0} XP</b>, ");
        sb.Append($"past the <b>{rk.minXp:N0} XP</b> needed for this rank.\n<color={UIKit.HexSecondary}>{rk.summary}</color>");
        if (c.rank - c.previousRank > 1)
        {
            sb.Append($"\n\n<size=85%>You rose {c.rank - c.previousRank} ranks: ");
            for (int i = c.previousRank; i <= c.rank; i++) sb.Append(i == c.previousRank ? "" : "  ›  ").Append(CareerSystem.RankAt(i).title);
            sb.Append("</size>");
        }
        if (!CareerSystem.IsTopRank(c.rank))
            sb.Append($"\n\n<size=85%><color={UIKit.HexSecondary}>Next: {CareerSystem.RankAt(c.rank + 1).title} at {CareerSystem.RankAt(c.rank + 1).minXp:N0} XP</color></size>");
        else
            sb.Append($"\n\n<size=85%><color={UIKit.HexWarning}>This is the highest rank in Scam Sense.</color></size>");
        promoText.text = sb.ToString();
        promo.gameObject.SetActive(true);
        UIKit.SetInteractable(promoContinue, true);
        if (levelAudio != null) levelAudio.PlayRoomUnlocked();
        StartCoroutine(PromotionIn(animate));
    }

    IEnumerator PromotionIn(bool animate)
    {
        if (!animate) { promoGroup.alpha = 1f; promoBadgeRt.localScale = Vector3.one; yield break; }
        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.7f;
            promoGroup.alpha = Mathf.Clamp01(k * 2f);
            float s = k < 0.6f ? Mathf.Lerp(0.5f, 1.12f, k / 0.6f) : Mathf.Lerp(1.12f, 1f, (k - 0.6f) / 0.4f);
            promoBadgeRt.localScale = Vector3.one * s;
            yield return null;
        }
        promoGroup.alpha = 1f;
        promoBadgeRt.localScale = Vector3.one;
        while (promo.gameObject.activeSelf)   // slow, subtle glow while the card is open
        {
            promoGlow.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 1.6f) * 0.06f);
            yield return null;
        }
    }

    void ClosePromotion()
    {
        if (levelAudio != null) levelAudio.PlayClick();
        promo.gameObject.SetActive(false);
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
        UIKit.Size(starRow, -1, 84);   // compact: leaves room for the career section
        for (int i = 0; i < 3; i++)
        {
            var s = UIKit.Rect("Star" + i, starRow);
            s.sizeDelta = i == 1 ? new Vector2(84, 84) : new Vector2(68, 68);
            stars[i] = UIKit.AddImage(s, StarOff, UIKit.StarSprite);
            stars[i].raycastTarget = false;
        }

        scoreText = UIKit.Text(panel, "Score", "", 40, UIKit.TextPrimary, TextAlignmentOptions.Center);
        statsText = UIKit.Text(panel, "Stats", "", 25, UIKit.TextPrimary, TextAlignmentOptions.Center);
        performanceText = UIKit.Text(panel, "Performance", "", 28, UIKit.Accent, TextAlignmentOptions.Center, bold: true);
        BuildCareer(panel);

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
        BuildPromotion();
    }

    void BuildCareer(RectTransform panel)
    {
        careerRow = UIKit.Rect("Career", panel);
        var h = UIKit.Horizontal(careerRow, 20, TextAnchor.UpperCenter);
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        UIKit.Size(careerRow, -1, 200);

        var card = UIKit.Rect("CareerCard", careerRow);
        UIKit.Size(card, 920, 200);
        UIKit.PanelImage(card, UIKit.PanelRaised);
        var badgeRt = UIKit.Rect("Badge", card);
        UIKit.Place(badgeRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(140, 140));
        careerBadge = UIKit.AddImage(badgeRt, Color.white, RankBadge.For(0));
        careerBadge.raycastTarget = false;
        careerBadge.preserveAspect = true;

        careerOutcome = UIKit.Text(card, "Outcome", "", 19, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(careerOutcome.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -16), new Vector2(520, 28));
        careerOutcome.characterSpacing = 1.5f;
        careerOutcome.textWrappingMode = TextWrappingModes.NoWrap;
        careerOutcome.overflowMode = TextOverflowModes.Ellipsis;
        careerXp = UIKit.Text(card, "XpEarned", "+0 XP", 40, UIKit.Accent, TextAlignmentOptions.TopRight, bold: true);
        UIKit.Place(careerXp.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -10), new Vector2(200, 50));
        careerRank = UIKit.Text(card, "Rank", "", 30, UIKit.TextPrimary, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(careerRank.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -44), new Vector2(600, 74));
        careerRank.lineSpacing = -6f;

        var bar = UIKit.Rect("XpBar", card);
        UIKit.Place(bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -120), new Vector2(700, 14));
        UIKit.AddImage(bar, new Color(1f, 1f, 1f, 0.1f)).raycastTarget = false;
        barFill = UIKit.Rect("Fill", bar);
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        UIKit.AddImage(barFill, UIKit.Accent).raycastTarget = false;
        careerProgress = UIKit.Text(card, "Progress", "", 18, UIKit.TextPrimary, TextAlignmentOptions.TopLeft);
        UIKit.Place(careerProgress.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -140), new Vector2(710, 26));
        careerProgress.textWrappingMode = TextWrappingModes.NoWrap;
        careerLines = UIKit.Text(card, "XpLines", "", 15, UIKit.TextSecondary, TextAlignmentOptions.TopLeft);
        UIKit.Place(careerLines.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -164), new Vector2(712, 36));
        careerLines.lineSpacing = -4f;

        var ach = UIKit.Rect("Achievements", careerRow);
        UIKit.Size(ach, 440, 200);
        UIKit.PanelImage(ach, UIKit.PanelRaised);
        var av = UIKit.Vertical(ach, 18, 6);
        av.childForceExpandHeight = false;
        UIKit.Header(ach, "New achievements");
        achievementsText = UIKit.Text(ach, "List", "", 19, UIKit.TextPrimary);
        achievementsText.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Size(achievementsText, -1, 140);
    }

    void BuildPromotion()
    {
        promo = UIKit.Rect("Promotion", root);
        UIKit.Stretch(promo);
        promoGroup = promo.gameObject.AddComponent<CanvasGroup>();
        UIKit.AddImage(promo, new Color(0.01f, 0.02f, 0.05f, 0.72f));   // blocks clicks to the results behind

        var card = UIKit.Rect("Card", promo);
        UIKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 0));
        UIKit.PanelImage(card);
        UIKit.AccentStrip(card, null, 8f);
        var v = UIKit.Vertical(card, 40, 14, TextAnchor.UpperCenter);
        v.padding.left = v.padding.right = 70;
        UIKit.FitHeight(card);

        var badgeHolder = UIKit.Rect("BadgeHolder", card);
        UIKit.Size(badgeHolder, -1, 220);
        promoGlow = UIKit.Rect("Glow", badgeHolder);
        UIKit.Place(promoGlow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 380));
        UIKit.AddImage(promoGlow, new Color(1, 1, 1, 0.3f), RankBadge.Glow).raycastTarget = false;
        promoBadgeRt = UIKit.Rect("Badge", badgeHolder);
        UIKit.Place(promoBadgeRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 200));
        promoBadge = UIKit.AddImage(promoBadgeRt, Color.white, RankBadge.For(0));
        promoBadge.preserveAspect = true;
        promoBadge.raycastTarget = false;

        promoTitle = UIKit.Text(card, "Title", "", 44, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        promoTitle.characterSpacing = 3f;
        promoText = UIKit.Text(card, "Text", "", 24, UIKit.TextPrimary, TextAlignmentOptions.Center);
        var row = UIKit.Rect("Buttons", card);
        UIKit.Horizontal(row, 0);
        UIKit.Size(row, -1, 76);
        promoContinue = UIKit.HudButton(row, "CONTINUE", ClosePromotion, UIKit.HudVariant.Primary);
        promo.gameObject.SetActive(false);
    }
}
