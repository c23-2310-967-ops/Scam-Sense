using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen message view used for text messages (and for emails if the scene has no ComputerTerminal).
/// Left: a smartphone (SMS) or a mail window (Email). Right: the decision card with the response timer
/// and SCAM / LEGITIMATE buttons, which turns into the feedback card after answering.
/// Builds itself at runtime. QuizManager drives it through IMessagePresenter.
/// </summary>
public class MessagePopupUI : MonoBehaviour, IMessagePresenter, IPresenterTiming, IInvestigationDock
{
    [SerializeField] private int sortingOrder = 20;
    [Tooltip("Text messages are read on a first-person phone pulled out of the pocket (PocketPhone). " +
             "Off = the original full-screen phone view.")]
    [SerializeField] private bool textsOnHandheldPhone = true;

    public event Action<MessageVerdict> AnswerChosen;
    public event Action ContinueClicked;

    public bool IsOpen => root != null && root.gameObject.activeSelf;

    /// <summary>Investigation UI goes in the gap between the phone (or mail window) and the decision card.</summary>
    public bool TryGetDockArea(out Rect area)
    {
        area = default;
        if (!IsOpen || decisionCard == null || !decisionCard.gameObject.activeInHierarchy) return false;
        var left = phone != null && phone.gameObject.activeInHierarchy ? phone : emailWindow != null && emailWindow.gameObject.activeInHierarchy ? emailWindow : null;
        if (left == null || !MessageInvestigation.ScreenRect(left, out var l) || !MessageInvestigation.ScreenRect(decisionCard, out var d)) return false;
        area = Rect.MinMaxRect(l.xMax + 20f, d.yMin, d.xMin - 20f, d.yMax);
        return area.width > 0f && area.height > 0f;
    }

    private RectTransform root, content;
    private CanvasGroup group;

    // SMS view
    private RectTransform phone;
    private TMP_Text smsSender, smsDetail, smsBody, smsLink, smsAvatar;

    // Email view
    private RectTransform emailWindow;
    private TMP_Text emailSubject, emailFrom, emailBody, emailLink;

    // Decision card
    private GameObject decisionGroup, feedbackGroup;
    private TMP_Text channelHeader, timerText, worthText;
    private Button scamButton, legitButton, detailsButton;
    private TMP_Text hintText, detailsText, detailsMoreHint;
    private RectTransform detailsBox;
    private LayoutElement detailsLayout;
    private ScrollRect detailsScroll;
    private bool detailsOverflow;
    private const float DetailsMaxHeight = 260f, DetailsPadding = 16f, DetailsTextWidth = 468f;
    private bool detailsOpen;
    private QuizManager quiz;

    // Feedback
    private TMP_Text verdictTitle, pointsLine, answerLine, explanationText, scoreLine;
    private Button continueButton;

    // Handheld phone mode (text messages)
    private RectTransform decisionCard;
    private Image dimmer;
    private HorizontalLayoutGroup row;
    private PocketPhone pocket;
    private LevelHUD hud;
    private bool usingPocket, phoneReady, hudHiddenByPhone;

    /// <summary>The response timer waits until the phone is up and the message is readable.</summary>
    public bool TimerRunning => !usingPocket || phoneReady;

    void Awake()
    {
        Build();
        root.gameObject.SetActive(false);
    }

    // ---------- IMessagePresenter ----------

    public void ShowMessage(ScamMessage msg)
    {
        bool sms = MessageChannels.OnPhone(msg.channel);   // text messages and chats use the phone layout
        phone.gameObject.SetActive(sms);
        emailWindow.gameObject.SetActive(!sms);

        bool hasLink = !string.IsNullOrEmpty(msg.link);
        if (sms)
        {
            // Bind/Mark: inspectable details become clickable while the message is investigated (plain text otherwise).
            MessageInvestigation.Bind(smsSender, () => MessageInvestigation.Mark(msg, InspectField.Sender, msg.sender));
            MessageInvestigation.Bind(smsDetail, () => MessageInvestigation.Mark(msg, InspectField.SenderDetail, msg.senderDetail));
            smsDetail.gameObject.SetActive(!string.IsNullOrEmpty(msg.senderDetail));
            smsAvatar.text = string.IsNullOrEmpty(msg.sender) ? "?" : msg.sender.Substring(0, 1).ToUpperInvariant();
            MessageInvestigation.Bind(smsBody, () => MessageInvestigation.Mark(msg, InspectField.Body, msg.body));
            MessageInvestigation.Bind(smsLink, () => hasLink ? $"<u>{MessageInvestigation.Mark(msg, InspectField.Link, msg.link)}</u>" : "");
            smsLink.gameObject.SetActive(hasLink);
        }
        else
        {
            MessageInvestigation.Bind(emailSubject, () => MessageInvestigation.Mark(msg, InspectField.Subject, msg.subject));
            MessageInvestigation.Bind(emailFrom, () => $"From: <b>{MessageInvestigation.Mark(msg, InspectField.Sender, msg.sender)}</b>" +
                (string.IsNullOrEmpty(msg.senderDetail) ? "" : $"  <color=#64748B><noparse><</noparse>{MessageInvestigation.Mark(msg, InspectField.SenderDetail, msg.senderDetail)}<noparse>></noparse></color>"));
            MessageInvestigation.Bind(emailBody, () => MessageInvestigation.Mark(msg, InspectField.Body, msg.body));
            MessageInvestigation.Bind(emailLink, () => hasLink ? $"Link in email: <u>{MessageInvestigation.Mark(msg, InspectField.Link, msg.link)}</u>" : "");
            emailLink.gameObject.SetActive(hasLink);
        }

        channelHeader.text = msg.channel == MessageChannel.Chat ? "Incoming chat message" : sms ? "Incoming text message" : "Incoming email";
        decisionGroup.SetActive(true);

        // Details start collapsed for every new message.
        if (quiz == null) quiz = FindFirstObjectByType<QuizManager>();
        string details = UIKit.MessageDetailsText(msg, quiz != null && quiz.Level != null ? quiz.Level.caseNotes : null);
        detailsText.text = details;
        // Fit the box to short details; cap it (and scroll) for long ones.
        float needed = string.IsNullOrEmpty(details) ? 0f
            : detailsText.GetPreferredValues(details, DetailsTextWidth, 0f).y + DetailsPadding * 2f;
        detailsOverflow = needed > DetailsMaxHeight;
        detailsLayout.preferredHeight = Mathf.Min(needed, DetailsMaxHeight);
        detailsOpen = false;
        detailsBox.gameObject.SetActive(false);
        detailsMoreHint.gameObject.SetActive(false);
        hintText.gameObject.SetActive(true);
        detailsButton.gameObject.SetActive(!string.IsNullOrEmpty(details));
        UIKit.SetLabel(detailsButton, "SHOW DETAILS");
        feedbackGroup.SetActive(false);
        UIKit.SetInteractable(scamButton, true);
        UIKit.SetInteractable(legitButton, true);
        SetTimer(0f, 0);

        if (sms && textsOnHandheldPhone && EnsurePocket())
        {
            ShowOnHandheld();
            return;
        }
        RestoreDesktopLayout();
        root.gameObject.SetActive(true);
        StartCoroutine(PopIn());
    }

    // ---------- Handheld phone ----------

    bool EnsurePocket()
    {
        if (pocket == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            pocket = PocketPhone.For(cam);
        }
        return pocket != null && pocket.ScreenRoot != null;
    }

    /// <summary>
    /// Moves the phone screen onto the 3D phone, pulls it out of the pocket, and shows the decision
    /// card on the right once it is held up. No dimmer: the phone is part of the 3D view.
    /// </summary>
    void ShowOnHandheld()
    {
        usingPocket = true;
        phoneReady = false;

        phone.SetParent(pocket.ScreenRoot, false);
        phone.anchorMin = phone.anchorMax = phone.pivot = new Vector2(0.5f, 0.5f);
        phone.anchoredPosition3D = Vector3.zero;
        phone.localRotation = Quaternion.identity;
        phone.localScale = Vector3.one;
        phone.sizeDelta = new Vector2(PocketPhone.ScreenUnitsW, PocketPhone.ScreenUnitsH);

        dimmer.enabled = false;
        row.childAlignment = TextAnchor.MiddleRight;
        decisionCard.gameObject.SetActive(false);
        root.gameObject.SetActive(true);

        // The objectives card would sit on top of the phone; it comes back when the phone is put away.
        if (hud == null) hud = FindFirstObjectByType<LevelHUD>();
        if (hud != null && !hudHiddenByPhone) { hud.SetVisible(false); hudHiddenByPhone = true; }

        pocket.PullOut(() =>
        {
            phoneReady = true;   // QuizManager starts counting response time from here
            decisionCard.gameObject.SetActive(true);
            StartCoroutine(PopIn());
        });
    }

    /// <summary>Back to the original full-screen layout (phone view in the row, dimmer on).</summary>
    void RestoreDesktopLayout()
    {
        if (phone.parent != content)
        {
            phone.SetParent(content, false);
            phone.SetSiblingIndex(0);
            phone.localScale = Vector3.one;
            phone.sizeDelta = new Vector2(PocketPhone.ScreenUnitsW, PocketPhone.ScreenUnitsH);
        }
        dimmer.enabled = true;
        row.childAlignment = TextAnchor.MiddleCenter;
        decisionCard.gameObject.SetActive(true);
        usingPocket = false;
        phoneReady = false;
        if (hudHiddenByPhone && hud != null) hud.SetVisible(true);
        hudHiddenByPhone = false;
    }

    public void SetTimer(float seconds, int pointsIfCorrect)
    {
        timerText.text = UIKit.FormatSeconds(seconds);
        worthText.text = $"A correct answer now earns <color={UIKit.HexAccent}><b>{pointsIfCorrect} pts</b></color>";
    }

    public void ShowFeedback(ScoringSystem.AnswerResult r, int score, int maxScore, int reviewed, int total)
    {
        decisionGroup.SetActive(false);
        feedbackGroup.SetActive(true);

        string word = UIKit.VerdictWord(r.message.correctVerdict);
        string hex = r.message.correctVerdict == MessageVerdict.Scam ? UIKit.HexDanger : UIKit.HexSafe;
        if (r.isCorrect)
        {
            verdictTitle.text = $"<color={UIKit.HexSafe}>✓  CORRECT</color>";
            answerLine.text = $"This message is <color={hex}><b>{word}</b></color>.";
        }
        else
        {
            verdictTitle.text = $"<color={UIKit.HexWarning}>×  NOT QUITE</color>";
            answerLine.text = $"You chose {UIKit.VerdictWord(r.chosen)}, but this message is <color={hex}><b>{word}</b></color>. Here is what to look for:";
        }

        pointsLine.text = $"<color={UIKit.HexAccent}><b>+{r.points} points</b></color>   ·   answered in {UIKit.FormatSeconds(r.seconds)}";
        explanationText.text = r.message.explanation + MessageInvestigation.FeedbackFor(r.message);   // + selected / missed evidence
        scoreLine.text = $"Score <b>{score}</b> / {maxScore}   ·   Messages reviewed <b>{reviewed}/{total}</b>";
        UIKit.SetLabel(continueButton, reviewed >= total ? "SEE RESULTS" : "CONTINUE");
        UIKit.SetInteractable(continueButton, true);
    }

    public void Hide()
    {
        if (root == null) return;
        root.gameObject.SetActive(false);

        // Handheld: the card goes now; the phone (still showing the message) lowers back into the
        // pocket, and only then does the player get movement and mouse-look back.
        if (usingPocket && pocket != null && pocket.IsOut)
        {
            pocket.PutAway(RestoreDesktopLayout);
            return;
        }
        RestoreDesktopLayout();
    }

    // ---------- Button handlers ----------

    void ToggleDetails()
    {
        detailsOpen = !detailsOpen;
        detailsBox.gameObject.SetActive(detailsOpen);
        detailsMoreHint.gameObject.SetActive(detailsOpen && detailsOverflow);
        hintText.gameObject.SetActive(!detailsOpen);   // keeps the card height in check
        if (detailsOpen) detailsScroll.verticalNormalizedPosition = 1f;   // start at the top
        UIKit.SetLabel(detailsButton, detailsOpen ? "HIDE DETAILS" : "SHOW DETAILS");
    }

    void Choose(MessageVerdict v)
    {
        // Disable both at once so a double click can never submit twice.
        if (!scamButton.interactable) return;
        UIKit.SetInteractable(scamButton, false);
        UIKit.SetInteractable(legitButton, false);
        AnswerChosen?.Invoke(v);
    }

    void Continue()
    {
        if (!continueButton.interactable) return;
        UIKit.SetInteractable(continueButton, false);
        ContinueClicked?.Invoke();
    }

    IEnumerator PopIn()
    {
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.18f;
            group.alpha = k;
            content.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k);
            yield return null;
        }
        group.alpha = 1f;
        content.localScale = Vector3.one;
    }

    // ---------- Build ----------

    void Build()
    {
        root = UIKit.CreateCanvas(transform, "MessagePopup Canvas", sortingOrder);
        group = root.gameObject.AddComponent<CanvasGroup>();
        dimmer = UIKit.Dimmer(root, 0.8f).GetComponent<Image>();

        content = UIKit.Rect("Content", root);
        UIKit.Stretch(content, 40);
        row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 48;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        BuildPhone(content);
        BuildEmail(content);
        BuildDecisionCard(content);
    }

    void BuildPhone(RectTransform parent)
    {
        Color ink = new Color32(0x0F, 0x17, 0x2A, 255);
        Color grey = new Color32(0x47, 0x55, 0x69, 255);

        phone = UIKit.Rect("Phone", parent);
        phone.sizeDelta = new Vector2(500, 920);
        UIKit.AddImage(phone, new Color32(0x05, 0x08, 0x10, 255), rounded: true);
        var bezel = phone.gameObject.AddComponent<Outline>();
        bezel.effectColor = UIKit.Border;
        bezel.effectDistance = new Vector2(3, -3);

        var screen = UIKit.Rect("Screen", phone);
        UIKit.Stretch(screen, 16);
        UIKit.AddImage(screen, new Color32(0xF8, 0xFA, 0xFC, 255), rounded: true);

        var status = UIKit.Text(screen, "Status", "9:41", 20, ink, bold: true);
        UIKit.Place(status.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -14), new Vector2(120, 28));
        var signal = UIKit.Text(screen, "Signal", "LTE  100%", 18, ink, TextAlignmentOptions.TopRight, bold: true);
        UIKit.Place(signal.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -15), new Vector2(180, 28));

        var avatar = UIKit.Rect("Avatar", screen);
        UIKit.Place(avatar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(72, 72));
        UIKit.AddImage(avatar, new Color32(0x64, 0x74, 0x8B, 255), UIKit.CircleSprite);
        smsAvatar = UIKit.Text(avatar, "Initial", "?", 34, Color.white, TextAlignmentOptions.Center, bold: true);
        UIKit.Stretch(smsAvatar.rectTransform);

        smsSender = UIKit.Text(screen, "Sender", "", 28, ink, TextAlignmentOptions.Top, bold: true);
        UIKit.Place(smsSender.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -134), new Vector2(440, 36));
        smsDetail = UIKit.Text(screen, "Detail", "", 21, grey, TextAlignmentOptions.Top);
        UIKit.Place(smsDetail.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -172), new Vector2(440, 28));

        var line = UIKit.Rect("Divider", screen);
        UIKit.Place(line, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(436, 2));
        UIKit.AddImage(line, new Color32(0xCB, 0xD5, 0xE1, 255));

        var chat = UIKit.Rect("Chat", screen);
        chat.anchorMin = new Vector2(0, 0);
        chat.anchorMax = new Vector2(1, 1);
        chat.offsetMin = new Vector2(22, 96);
        chat.offsetMax = new Vector2(-22, -222);
        var cv = chat.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 12;
        cv.childAlignment = TextAnchor.UpperLeft;
        cv.childControlWidth = cv.childControlHeight = true;
        cv.childForceExpandWidth = cv.childForceExpandHeight = false;

        var stamp = UIKit.Text(chat, "Time", "Text Message · Today 9:41 AM", 19, grey, TextAlignmentOptions.Center);
        UIKit.Size(stamp, 424);

        var bubble = UIKit.Rect("Bubble", chat);
        UIKit.AddImage(bubble, new Color32(0xE2, 0xE8, 0xF0, 255), rounded: true);
        UIKit.Vertical(bubble, 20, 12);
        UIKit.Size(bubble, 400);

        smsBody = UIKit.Text(bubble, "Body", "", 28, ink);
        smsBody.lineSpacing = 8f;
        smsLink = UIKit.Text(bubble, "Link", "", 24, new Color32(0x1D, 0x4E, 0xD8, 255));

        var input = UIKit.Rect("InputBar", screen);
        UIKit.Place(input, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(430, 52));
        UIKit.AddImage(input, new Color32(0xE2, 0xE8, 0xF0, 255), rounded: true);
        var ph = UIKit.Text(input, "Placeholder", "Text Message", 21, grey, TextAlignmentOptions.MidlineLeft);
        UIKit.Stretch(ph.rectTransform, 0);
        ph.margin = new Vector4(22, 0, 22, 0);
    }

    void BuildEmail(RectTransform parent)
    {
        Color ink = new Color32(0x0F, 0x17, 0x2A, 255);

        emailWindow = UIKit.Rect("EmailWindow", parent);
        emailWindow.sizeDelta = new Vector2(900, 0);
        UIKit.AddImage(emailWindow, new Color32(0xF8, 0xFA, 0xFC, 255), rounded: true);
        UIKit.Vertical(emailWindow, 0, 0);
        UIKit.FitHeight(emailWindow);

        var bar = UIKit.Rect("TitleBar", emailWindow);
        UIKit.AddImage(bar, UIKit.PanelRaised, rounded: true);
        UIKit.Size(bar, -1, 54);
        var barText = UIKit.Text(bar, "Title", "Mail  ·  Inbox", 21, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        UIKit.Stretch(barText.rectTransform);

        var body = UIKit.Rect("Body", emailWindow);
        UIKit.Vertical(body, 38, 16);

        emailSubject = UIKit.Text(body, "Subject", "", 34, ink, bold: true);
        emailFrom = UIKit.Text(body, "From", "", 25, ink);
        UIKit.Text(body, "To", "To: me   ·   Today", 21, new Color32(0x47, 0x55, 0x69, 255));
        var div = UIKit.Rect("Divider", body);
        UIKit.AddImage(div, new Color32(0xCB, 0xD5, 0xE1, 255));
        UIKit.Size(div, -1, 2);
        emailBody = UIKit.Text(body, "Message", "", 27, ink);
        emailBody.lineSpacing = 10f;
        emailLink = UIKit.Text(body, "Link", "", 24, new Color32(0x1D, 0x4E, 0xD8, 255));
        UIKit.Text(body, "Footer", "Simulated message · links are disabled in this training.", 19, new Color32(0x64, 0x74, 0x8B, 255)).fontStyle = FontStyles.Italic;
    }

    void BuildDecisionCard(RectTransform parent)
    {
        var card = UIKit.Rect("DecisionCard", parent);
        decisionCard = card;
        card.sizeDelta = new Vector2(580, 0);
        UIKit.PanelImage(card);
        UIKit.Vertical(card, 0, 0);
        UIKit.FitHeight(card);
        UIKit.AccentStrip(card);

        // ----- Decision state -----
        var d = UIKit.Rect("Decision", card);
        var dv = UIKit.Vertical(d, 36, 18);
        dv.padding.left = 44;
        decisionGroup = d.gameObject;

        channelHeader = UIKit.Header(d, "Incoming");
        UIKit.Text(d, "Question", "Is this message a scam or legitimate?", 34, UIKit.TextPrimary, bold: true);
        hintText = UIKit.Text(d, "Hint", "Check who sent it, whether it matches something you did, any pressure to act fast, and where links lead.",
            UIKit.SizeSmall, UIKit.TextSecondary);

        // Investigation details (collapsed by default; opening them doesn't reveal the answer)
        detailsButton = UIKit.HudButton(d, "SHOW DETAILS", ToggleDetails, UIKit.HudVariant.Secondary, 22);
        UIKit.Size(detailsButton, -1, 56);
        // Scrollable box with a capped height, so long details never push the answer buttons off screen.
        detailsBox = UIKit.Rect("Details", d);
        UIKit.AddImage(detailsBox, UIKit.Background, rounded: true);
        detailsLayout = detailsBox.gameObject.AddComponent<LayoutElement>();
        detailsLayout.preferredHeight = DetailsMaxHeight;
        var viewport = UIKit.Rect("Viewport", detailsBox);
        UIKit.Stretch(viewport, DetailsPadding);
        viewport.gameObject.AddComponent<RectMask2D>();
        detailsText = UIKit.Text(viewport, "Text", "", 20, UIKit.TextPrimary);
        detailsText.lineSpacing = 4f;
        var trt = detailsText.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        detailsText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        detailsScroll = detailsBox.gameObject.AddComponent<ScrollRect>();
        detailsScroll.viewport = viewport;
        detailsScroll.content = trt;
        detailsScroll.horizontal = false;
        detailsScroll.movementType = ScrollRect.MovementType.Clamped;
        detailsScroll.scrollSensitivity = 30f;
        detailsBox.gameObject.SetActive(false);
        detailsMoreHint = UIKit.Text(d, "MoreHint", "Scroll the details for more", 18, UIKit.TextSecondary, TextAlignmentOptions.Right);
        detailsMoreHint.gameObject.SetActive(false);

        var timerRow = UIKit.Rect("TimerRow", d);
        UIKit.AddImage(timerRow, UIKit.Background, rounded: true);
        var tv = UIKit.Vertical(timerRow, 18, 4);
        tv.childAlignment = TextAnchor.MiddleCenter;
        var tLabel = UIKit.Text(timerRow, "Label", "RESPONSE TIME", 20, UIKit.TextSecondary, TextAlignmentOptions.Center, bold: true);
        tLabel.characterSpacing = 4f;
        timerText = UIKit.Text(timerRow, "Timer", "0.0s", 52, UIKit.TextPrimary, TextAlignmentOptions.Center, bold: true);
        worthText = UIKit.Text(timerRow, "Worth", "", UIKit.SizeSmall, UIKit.TextSecondary, TextAlignmentOptions.Center);

        scamButton = UIKit.HudButton(d, "×  SCAM", () => Choose(MessageVerdict.Scam), UIKit.HudVariant.Danger, 32);
        UIKit.Size(scamButton, -1, 80);
        legitButton = UIKit.HudButton(d, "✓  LEGITIMATE", () => Choose(MessageVerdict.Legitimate), UIKit.HudVariant.Safe, 32);
        UIKit.Size(legitButton, -1, 80);

        // ----- Feedback state -----
        var f = UIKit.Rect("Feedback", card);
        var fv = UIKit.Vertical(f, 36, 16);
        fv.padding.left = 44;
        feedbackGroup = f.gameObject;

        verdictTitle = UIKit.Text(f, "Verdict", "", UIKit.SizeDisplay, UIKit.TextPrimary, bold: true);
        verdictTitle.characterSpacing = 3f;
        pointsLine = UIKit.Text(f, "Points", "", 26, UIKit.TextPrimary);
        answerLine = UIKit.Text(f, "Answer", "", 26, UIKit.TextPrimary);
        UIKit.Header(f, "Why");
        explanationText = UIKit.Text(f, "Explanation", "", 25, UIKit.TextPrimary);
        explanationText.lineSpacing = 8f;
        UIKit.Divider(f);
        scoreLine = UIKit.Text(f, "Score", "", UIKit.SizeSmall, UIKit.TextSecondary);
        continueButton = UIKit.HudButton(f, "CONTINUE", Continue, UIKit.HudVariant.Primary, 30);
        UIKit.Size(continueButton, -1, 80);

        feedbackGroup.SetActive(false);
    }
}
