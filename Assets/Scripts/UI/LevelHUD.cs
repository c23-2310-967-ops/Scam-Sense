using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// In-game HUD: level name, objectives, case notes, messages reviewed, score,
/// incoming-message notifications (phone banner or desktop email toast), short info toasts and a crosshair.
/// Builds itself at runtime. QuizManager / GameManager drive it through the public methods.
/// (Interaction prompts are shown by InteractionPrompt.)
/// </summary>
public class LevelHUD : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 10;
    [SerializeField] private bool showCrosshair = true;

    [Header("Task panel toggle")]
    [Tooltip("Hides / shows the objectives card for a clearer view. Only the UI changes; the camera FOV is untouched.")]
    [SerializeField] private Key toggleTasksKey = Key.Tab;
    [SerializeField] private float toggleAnimTime = 0.25f;

    /// <summary>Raised when the player clicks the notification (only possible while the cursor is free).</summary>
    public event System.Action NotificationClicked;

    /// <summary>True while the objectives card is shown.</summary>
    public bool TasksVisible => tasksVisible;

    // Remembered for the session, so the choice carries over from Level 1 to Level 2 and on replay.
    private static bool sessionTasksHidden;
    private bool tasksVisible = true;
    private RectTransform tasksCard, tasksReminder;
    private CanvasGroup tasksGroup, reminderGroup;
    private Vector2 tasksRest;
    private Coroutine tasksRoutine;

    private RectTransform root;
    private TMP_Text levelTitle, objectivesText, notesText, reviewedText, scoreText, toastText;
    private RectTransform phoneBanner, emailToast;
    private TMP_Text phoneSender, phonePreview, phoneHint, emailSender, emailSubject, emailHint;
    private CanvasGroup phoneGroup, emailGroup, toastGroup;
    private Coroutine toastRoutine, bannerRoutine, nudgeRoutine;
    private Vector2 phoneRest, emailRest;

    private string[] objectives;
    private bool allReviewed;

    void Awake()
    {
        Build();
        SetTasksVisible(!sessionTasksHidden, instant: true);
    }

    void Update()
    {
        var kb = Keyboard.current;
        // Not while paused, or while the whole HUD is hidden (briefing / results).
        if (kb == null || PauseMenu.IsPaused || !root.gameObject.activeInHierarchy) return;
        if (kb[toggleTasksKey].wasPressedThisFrame) SetTasksVisible(!tasksVisible);
    }

    // ---------- Public API ----------

    /// <summary>
    /// Shows or hides the objectives card (slide + fade). Purely visual: progress, score and the
    /// message sequence are untouched, and notifications, toasts and the crosshair stay on screen.
    /// </summary>
    public void SetTasksVisible(bool visible, bool instant = false)
    {
        tasksVisible = visible;
        sessionTasksHidden = !visible;
        if (tasksRoutine != null) StopCoroutine(tasksRoutine);
        tasksRoutine = null;

        if (instant || !isActiveAndEnabled)
        {
            tasksCard.anchoredPosition = visible ? tasksRest : HiddenTasksPos();
            tasksGroup.alpha = visible ? 1f : 0f;
            tasksCard.gameObject.SetActive(visible);
            reminderGroup.alpha = visible ? 0f : 1f;
            tasksReminder.gameObject.SetActive(!visible);
            return;
        }
        tasksRoutine = StartCoroutine(AnimateTasks(visible));
    }

    public void Setup(LevelDefinition level)
    {
        levelTitle.text = $"LEVEL {level.levelNumber}  ·  {level.displayName.ToUpperInvariant()}";
        objectives = level.objectives;
        allReviewed = false;
        RefreshObjectives();

        bool hasNotes = level.caseNotes != null && level.caseNotes.Length > 0;
        notesText.transform.parent.gameObject.SetActive(hasNotes);
        if (hasNotes)
        {
            var sb = new StringBuilder();
            foreach (var n in level.caseNotes) sb.Append("•  ").AppendLine(n);
            notesText.text = sb.ToString().TrimEnd();
        }
    }

    public void SetProgress(int reviewed, int total, int score, int maxScore)
    {
        reviewedText.text = $"Messages reviewed   <color={UIKit.HexAccent}><b>{reviewed} / {total}</b></color>";
        scoreText.text = $"Score   <color={UIKit.HexAccent}><b>{score}</b></color> / {maxScore}";
        allReviewed = reviewed >= total && total > 0;
        RefreshObjectives();
    }

    public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

    /// <summary>Shows the incoming-message notification. <paramref name="hint"/> says how to open it.</summary>
    public void ShowNotification(ScamMessage msg, string hint)
    {
        HideNotification();
        bool sms = msg.channel == MessageChannel.SMS;
        if (sms)
        {
            phoneSender.text = msg.sender;
            phonePreview.text = msg.body;
            phoneHint.text = hint;
        }
        else
        {
            emailSender.text = msg.sender;
            emailSubject.text = msg.subject;
            emailHint.text = hint;
        }
        var group = sms ? phoneGroup : emailGroup;
        group.gameObject.SetActive(true);
        bannerRoutine = StartCoroutine(SlideIn(sms ? phoneBanner : emailToast, group, sms));
    }

    /// <summary>Gives an ignored notification a small shake to remind the player it is waiting.</summary>
    public void NudgeNotification()
    {
        if (bannerRoutine != null || nudgeRoutine != null) return;
        RectTransform target = phoneGroup.gameObject.activeSelf ? phoneBanner :
                               emailGroup.gameObject.activeSelf ? emailToast : null;
        if (target != null) nudgeRoutine = StartCoroutine(Nudge(target));
    }

    public void HideNotification()
    {
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        if (nudgeRoutine != null) StopCoroutine(nudgeRoutine);
        bannerRoutine = nudgeRoutine = null;
        phoneBanner.anchoredPosition = phoneRest;
        emailToast.anchoredPosition = emailRest;
        phoneGroup.gameObject.SetActive(false);
        emailGroup.gameObject.SetActive(false);
    }

    public void ShowToast(string text, float seconds = 3f)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(Toast(text, seconds));
    }

    // ---------- Internals ----------

    void RefreshObjectives()
    {
        if (objectives == null || objectives.Length == 0) { objectivesText.text = ""; return; }
        var sb = new StringBuilder();
        for (int i = 0; i < objectives.Length; i++)
        {
            // Objectives 1 and 2 complete when every message is answered; exploring stays open.
            bool done = allReviewed && i < 2;
            string box = done ? $"<color={UIKit.HexSafe}>✓</color>" : $"<color={UIKit.HexAccent}>•</color>";
            string line = done ? $"<color={UIKit.HexSecondary}><s>{objectives[i]}</s></color>" : objectives[i];
            sb.Append(box).Append("  ").AppendLine(line);
        }
        objectivesText.text = sb.ToString().TrimEnd();
    }

    Vector2 HiddenTasksPos() => tasksRest - new Vector2(tasksCard.rect.width + 60f, 0f);

    IEnumerator AnimateTasks(bool show)
    {
        tasksCard.gameObject.SetActive(true);
        tasksReminder.gameObject.SetActive(true);
        Vector2 fromPos = tasksCard.anchoredPosition, toPos = show ? tasksRest : HiddenTasksPos();
        float fromA = tasksGroup.alpha, toA = show ? 1f : 0f;
        float fromR = reminderGroup.alpha, toR = show ? 0f : 1f;

        // Unscaled time: works the same whether or not a message is open.
        for (float t = 0f; t < toggleAnimTime; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / toggleAnimTime, 3f);
            tasksCard.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, k);
            tasksGroup.alpha = Mathf.Lerp(fromA, toA, k);
            reminderGroup.alpha = Mathf.Lerp(fromR, toR, k);
            yield return null;
        }
        tasksCard.anchoredPosition = toPos;
        tasksGroup.alpha = toA;
        reminderGroup.alpha = toR;
        tasksCard.gameObject.SetActive(show);
        tasksReminder.gameObject.SetActive(!show);
        tasksRoutine = null;
    }

    IEnumerator SlideIn(RectTransform r, CanvasGroup g, bool fromTop)
    {
        Vector2 target = r.anchoredPosition;
        Vector2 from = target + (fromTop ? new Vector2(0f, 200f) : new Vector2(640f, 0f));
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 0.35f, 3f);
            r.anchoredPosition = Vector2.LerpUnclamped(from, target, k);
            g.alpha = k;
            yield return null;
        }
        r.anchoredPosition = target;
        g.alpha = 1f;
        bannerRoutine = null;
    }

    IEnumerator Nudge(RectTransform r)
    {
        Vector2 origin = r.anchoredPosition;
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            r.anchoredPosition = origin + new Vector2(Mathf.Sin(t * 60f) * 10f * (1f - t / 0.4f), 0f);
            yield return null;
        }
        r.anchoredPosition = origin;
        nudgeRoutine = null;
    }

    IEnumerator Toast(string text, float seconds)
    {
        toastText.text = text;
        toastGroup.gameObject.SetActive(true);
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) { toastGroup.alpha = t / 0.2f; yield return null; }
        toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(seconds);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { toastGroup.alpha = 1f - t / 0.4f; yield return null; }
        toastGroup.gameObject.SetActive(false);
        toastRoutine = null;
    }

    // ---------- Build ----------

    void Build()
    {
        root = UIKit.CreateCanvas(transform, "LevelHUD Canvas", sortingOrder);

        // Objectives card (top-left)
        var card = UIKit.Rect("ObjectivesCard", root);
        UIKit.Place(card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(560, 0));   // ends before the centred phone banner
        UIKit.PanelImage(card).raycastTarget = false;
        var v = UIKit.Vertical(card, 24, 10);
        v.padding.left = 32;
        UIKit.FitHeight(card);
        UIKit.AccentStrip(card);

        levelTitle = UIKit.Text(card, "LevelTitle", "LEVEL", 26, UIKit.TextPrimary, bold: true);
        levelTitle.characterSpacing = 2f;

        UIKit.Header(card, "Objectives", 21);
        objectivesText = UIKit.Text(card, "Objectives", "", 25, UIKit.TextPrimary);
        objectivesText.lineSpacing = 10f;

        var notesBlock = UIKit.Rect("NotesBlock", card);
        UIKit.Vertical(notesBlock, 0, 6);
        UIKit.Header(notesBlock, "Case notes", 21);
        notesText = UIKit.Text(notesBlock, "Notes", "", UIKit.SizeSmall, UIKit.TextSecondary);

        UIKit.Divider(card);
        reviewedText = UIKit.Text(card, "Reviewed", "", 25, UIKit.TextPrimary);
        scoreText = UIKit.Text(card, "Score", "", 25, UIKit.TextPrimary);
        UIKit.Text(card, "ToggleHint", "TAB  —  HIDE TASKS", 18, UIKit.TextSecondary, bold: true).characterSpacing = 2f;

        tasksCard = card;
        tasksRest = card.anchoredPosition;
        tasksGroup = card.gameObject.AddComponent<CanvasGroup>();
        tasksGroup.blocksRaycasts = false;

        // Small reminder shown while the tasks are hidden (top-left, same panel style)
        tasksReminder = UIKit.Rect("TasksReminder", root);
        UIKit.Place(tasksReminder, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(300, 50));
        UIKit.PanelImage(tasksReminder).raycastTarget = false;
        UIKit.AccentStrip(tasksReminder);
        var remText = UIKit.Text(tasksReminder, "Text", "<color=" + UIKit.HexAccent + ">TAB</color>  —  SHOW TASKS", 20,
                                 UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        remText.characterSpacing = 2f;
        UIKit.Stretch(remText.rectTransform, 6);
        reminderGroup = tasksReminder.gameObject.AddComponent<CanvasGroup>();
        reminderGroup.blocksRaycasts = false;
        tasksReminder.gameObject.SetActive(false);

        // Phone banner (top-centre, smartphone style: light card, dark text)
        phoneBanner = UIKit.Rect("PhoneBanner", root);
        UIKit.Place(phoneBanner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(720, 176));
        phoneRest = phoneBanner.anchoredPosition;
        UIKit.AddImage(phoneBanner, new Color32(0xF8, 0xFA, 0xFC, 250), rounded: true);
        phoneGroup = phoneBanner.gameObject.AddComponent<CanvasGroup>();
        phoneBanner.gameObject.AddComponent<Button>().onClick.AddListener(() => NotificationClicked?.Invoke());

        var appIcon = UIKit.Rect("AppIcon", phoneBanner);
        UIKit.Place(appIcon, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -20), new Vector2(36, 36));
        UIKit.AddImage(appIcon, UIKit.SafeButton, rounded: true).raycastTarget = false;
        var appName = UIKit.Text(phoneBanner, "AppName", "MESSAGES  ·  now", 20, new Color32(0x47, 0x55, 0x69, 255), bold: true);
        UIKit.Place(appName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, -24), new Vector2(500, 30));
        phoneSender = UIKit.Text(phoneBanner, "Sender", "", 26, new Color32(0x0F, 0x17, 0x2A, 255), bold: true);
        UIKit.Place(phoneSender.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -64), new Vector2(676, 34));
        phonePreview = UIKit.Text(phoneBanner, "Preview", "", 23, new Color32(0x1E, 0x29, 0x3B, 255));
        phonePreview.textWrappingMode = TextWrappingModes.NoWrap;
        phonePreview.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Place(phonePreview.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -100), new Vector2(676, 32));
        phoneHint = AddKeyHint(phoneBanner, UIKit.AccentDark);
        phoneGroup.gameObject.SetActive(false);

        // Email toast (bottom-right, desktop style)
        emailToast = UIKit.Rect("EmailToast", root);
        UIKit.Place(emailToast, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 40), new Vector2(620, 176));
        emailRest = emailToast.anchoredPosition;
        UIKit.PanelImage(emailToast, UIKit.PanelRaised);
        emailGroup = emailToast.gameObject.AddComponent<CanvasGroup>();
        emailToast.gameObject.AddComponent<Button>().onClick.AddListener(() => NotificationClicked?.Invoke());

        var mailIcon = UIKit.Rect("MailIcon", emailToast);
        UIKit.Place(mailIcon, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -22), new Vector2(36, 28));
        UIKit.AddImage(mailIcon, UIKit.Link, rounded: true).raycastTarget = false;
        var mailApp = UIKit.Text(emailToast, "AppName", "MAIL  ·  New email", 20, UIKit.TextSecondary, bold: true);
        UIKit.Place(mailApp.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, -22), new Vector2(500, 30));
        emailSender = UIKit.Text(emailToast, "Sender", "", 26, UIKit.TextPrimary, bold: true);
        emailSender.textWrappingMode = TextWrappingModes.NoWrap;
        emailSender.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Place(emailSender.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -62), new Vector2(576, 34));
        emailSubject = UIKit.Text(emailToast, "Subject", "", 23, UIKit.TextSecondary);
        emailSubject.textWrappingMode = TextWrappingModes.NoWrap;
        emailSubject.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Place(emailSubject.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -98), new Vector2(576, 32));
        emailHint = AddKeyHint(emailToast, UIKit.Accent);
        emailGroup.gameObject.SetActive(false);

        // Toast (lower-centre, short info lines)
        var toast = UIKit.Rect("Toast", root);
        UIKit.Place(toast, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(980, 76));
        UIKit.PanelImage(toast, new Color(UIKit.Background.r, UIKit.Background.g, UIKit.Background.b, 0.94f)).raycastTarget = false;
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastText = UIKit.Text(toast, "Text", "", 26, UIKit.TextPrimary, TextAlignmentOptions.Center);
        UIKit.Stretch(toastText.rectTransform, 10);
        toast.gameObject.SetActive(false);

        if (showCrosshair)
        {
            var dot = UIKit.Rect("Crosshair", root);
            UIKit.Place(dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
            UIKit.AddImage(dot, new Color(1f, 1f, 1f, 0.85f), UIKit.CircleSprite).raycastTarget = false;
            dot.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
        }
    }

    static TMP_Text AddKeyHint(RectTransform parent, Color color)
    {
        var hint = UIKit.Text(parent, "KeyHint", "", 22, color, TextAlignmentOptions.BottomRight, bold: true);
        UIKit.Place(hint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 14), new Vector2(520, 32));
        return hint;
    }
}
