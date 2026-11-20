using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Level 1 guide. Teaches the game on the first question, one step at a time, while the player actually plays:
///   welcome (what the game is, scam vs legitimate, the goal) -> wait for the first question (press E)
///   -> 1 investigate (click an underlined detail) -> 2 hint (press HINT or skip) -> 3 answer -> 4 read the feedback
///   -> "you know the basics" -> a short tip the first time a SCAM / LEGITIMATE message appears.
/// Each step frames the part of the screen to use (pulsing cyan frame + label) and waits for the player to do it.
/// SKIP TUTORIAL ends it at any time.
///
/// Added by QuizManager only when the level is marked as the tutorial (LevelDefinition.tutorial), so no other room
/// ever runs it. It only observes the game (QuizManager, MessageInvestigation, ScoringSystem): answering, scoring
/// and progression are untouched.
/// </summary>
public class TutorialCoach : MonoBehaviour
{
    enum Step { Off, Welcome, Move, Interact, Menus, WaitForQuestion, WarningSigns, Investigate, ClueFound, Hint, Answer, Feedback, CaseReview, Practice, VerdictIntro }

    // Movement / look check for the controls step
    Transform player, cam;
    Vector3 lastPos;
    Quaternion startLook;
    float walked;

    /// <summary>A key drawn as a highlighted key cap in card text.</summary>
    static string K(string key) => $"<mark=#38BDF840><b> {key} </b></mark>";

    const int SortingOrder = 37;   // above the investigation UI (35) and wrong-answer card (36); below results (40) and pause (100)

    QuizManager quiz;
    Step step = Step.Off;
    ScamMessage guided;            // the question the guide walks through
    bool verdictTipDone, cardHidden, gateHeld;
    float stepStarted, nextPlace;
    string shownBody;

    RectTransform root, cardRt, frameRt, pillRt;
    Image dim;
    Image[] frameBars;
    TMP_Text kicker, title, body, pillText;
    Button nextButton, skipButton;
    PopupIcon icon;
    PopupAnimator cardAnim;
    WrongAnswerConsequences consequences;
    System.Action onNext;

    public bool Running => step != Step.Off;

    // ---------- Lifecycle (QuizManager) ----------

    public void Begin(QuizManager q)
    {
        quiz = q;
        if (root == null) Build();
        consequences = GetComponent<WrongAnswerConsequences>();
        guided = null;
        verdictTipDone = false;
        Go(Step.Welcome);
    }

    public void Stop() => Go(Step.Off);

    void OnDisable() => ReleaseGate();

    void ReleaseGate()
    {
        if (!gateHeld) return;
        gateHeld = false;
        InteractionGate.Release(this);
    }

    // ---------- Steps ----------

    void Go(Step next)
    {
        step = next;
        stepStarted = Time.unscaledTime;
        cardHidden = false;
        shownBody = null;
        onNext = null;
        if (root == null) return;

        bool modal = next == Step.Welcome;
        dim.gameObject.SetActive(modal);
        if (modal && !gateHeld) { gateHeld = true; InteractionGate.Acquire(this); }   // free cursor, pause walking
        if (!modal) ReleaseGate();

        if (next == Step.Off) { root.gameObject.SetActive(false); return; }
        root.gameObject.SetActive(true);

        int unlock = quiz != null && quiz.Level != null ? quiz.Level.minScoreToUnlockNext : 0;
        bool choice = guided != null && guided.HasChoices;
        switch (next)
        {
            case Step.Welcome:
                Show(UIKit.PopupTone.Info, "TRAINING CASE", "Welcome to Scam Sense",
                    "You are a new <b>cybercrime investigator</b>. Scammers send fake texts, emails and posts to steal <b>money</b>, <b>passwords</b> and <b>personal details</b>. " +
                    "Spotting them protects you and the people around you.\n\n" +
                    $"Your goal: decide if each message is a <color={UIKit.HexDanger}><b>SCAM</b></color> (it tries to trick you) " +
                    $"or <color={UIKit.HexSafe}><b>LEGITIMATE</b></color> (it is real and safe), and answer each case question. " +
                    $"Score <b>{unlock}</b> or more to unlock the next room.\n" +
                    $"<color={UIKit.HexSecondary}>This guide teaches the controls, then walks you through your first question.</color>",
                    "LET'S START", () => Go(Step.Move));
                break;
            case Step.Move:
                BeginMoveCheck();
                Show(UIKit.PopupTone.Info, "CONTROLS 1 OF 3  ·  MOVE AND LOOK", "Walk and look around",
                    $"Walk with {K("W")} {K("A")} {K("S")} {K("D")}  and look around by moving the <b>mouse</b>. {K("Space")} jumps.\n" +
                    $"<color={UIKit.HexSecondary}>Try it now. This step finishes when you have walked and looked around.</color>",
                    "NEXT  (Enter)", () => Go(Step.Interact));
                break;
            case Step.Interact:
                Show(UIKit.PopupTone.Info, "CONTROLS 2 OF 3  ·  INTERACT", "Use things in the room",
                    $"Look at the door, a chair, the phone or a computer. When a label with {K("E")} appears, press {K("E")} to use it.\n" +
                    $"Sitting on a chair? Press {K("E")} or {K("Space")} to stand up.",
                    "NEXT  (Enter)", () => Go(Step.Menus));
                break;
            case Step.Menus:
                Show(UIKit.PopupTone.Info, "CONTROLS 3 OF 3  ·  MENUS", "Menus and windows",
                    $"{K("Tab")} hides or shows your task list (top left).\n" +
                    $"{K("Esc")} or {K("P")} opens the pause menu. {K("Esc")} also closes a computer or a card.\n" +
                    "When a window is open, the mouse pointer appears: <b>click</b> buttons and underlined words. Click the game again to go back to looking around.",
                    "NEXT  (Enter)", () => Go(Step.WaitForQuestion));
                break;
            case Step.WarningSigns:
                Show(UIKit.PopupTone.Info, "STEP 1 OF 5  ·  WARNING SIGNS", "What to look for",
                    "Check every message for warning signs:\n" +
                    "•  <b>Sender</b> - unknown, or an address that only looks real\n" +
                    "•  <b>Links</b> - odd or unofficial web addresses\n" +
                    "•  <b>Pressure</b> - urgent deadlines or threats\n" +
                    "•  <b>Requests</b> - passwords, codes, money or personal details\n" +
                    "•  <b>Mistakes</b> - strange spelling or grammar\n" +
                    $"<color={UIKit.HexSecondary}>One sign alone doesn't prove a scam. Real messages can have links or deadlines too, so weigh all the evidence.</color>",
                    "NEXT", () => Go(MessageInvestigation.Investigates(guided) ? Step.Investigate : guided != null && guided.HasHint ? Step.Hint : Step.Answer));
                break;
            case Step.WaitForQuestion:
                RefreshWaiting(force: true);
                break;
            case Step.Investigate:
                Show(UIKit.PopupTone.Info, "STEP 2 OF 5  ·  UNDERLINED CLUES", "Check the underlined details",
                    $"Words <color=#7DD3FC><u>underlined in blue</u></color> are details worth checking. Click one to see what it tells you.\n<b>Click one now.</b>",
                    null, null);
                break;
            case Step.ClueFound:
                Show(UIKit.PopupTone.Success, "STEP 2 OF 5  ·  UNDERLINED CLUES", "Nice find!",
                    $"The card explains what you found. A gold <color=#FBBF24>★</color> marks a <b>key clue</b>. " +
                    "Check the other underlined words too - the more clues you find, the surer your answer.",
                    "NEXT", () => Go(guided != null && guided.HasHint ? Step.Hint : Step.Answer));
                break;
            case Step.Hint:
                Show(UIKit.PopupTone.Info, "STEP 3 OF 5  ·  HINTS", "Need a nudge?",
                    "Press <b>HINT</b> to see what to focus on. A hint never gives away the answer. Try it now, or skip it.",
                    "SKIP HINT", () => Go(Step.Answer));
                break;
            case Step.Answer:
                Show(UIKit.PopupTone.Info, "STEP 4 OF 5  ·  ANSWER", choice ? "Choose the best answer" : "Scam or legitimate?",
                    choice ? "Click the <b>safest, most accurate</b> choice. Your answer is submitted as soon as you click it. The A-D order changes every time you play, so read them all."
                           : $"Click <color={UIKit.HexDanger}><b>SCAM</b></color> if it tries to trick you, or <color={UIKit.HexSafe}><b>LEGITIMATE</b></color> if it is real and safe. Your answer is submitted as soon as you click.",
                    null, null);
                break;
            case Step.Feedback:
                Show(UIKit.PopupTone.Info, "STEP 5 OF 5  ·  FEEDBACK", "Read the feedback",
                    $"<color={UIKit.HexSafe}>✓</color> means correct, <color={UIKit.HexDanger}>×</color> means not quite. " +
                    "<b>Why</b> explains the warning signs, and the clue list shows what you found and what you missed.",
                    "GOT IT", () => { if (guided != null && guided.HasChoices) Go(Step.CaseReview); else HideCard(); });
                break;
            case Step.CaseReview:
                Show(UIKit.PopupTone.Info, "CASE REVIEW", "Your case report",
                    "On the left, the <b>Case Review</b> now sums up the case:\n" +
                    "•  the <b>correct answer</b>\n" +
                    $"•  the <b>clues to notice</b> (<color={UIKit.HexSafe}>✓</color> found, <color={UIKit.HexWarning}>•</color> missed)\n" +
                    "•  <b>what you learned</b>\n" +
                    "Every case question in this room ends with one. Press <b>CONTINUE</b> when you're done.",
                    "GOT IT", HideCard);
                break;
            case Step.Practice:
                Show(UIKit.PopupTone.Success, "TRAINING COMPLETE", "You know the basics",
                    "Answer the other questions the same way: <b>investigate</b>, use a <b>hint</b> if you need one, then <b>answer</b>. Good luck, investigator!",
                    "GOT IT", HideCard);
                break;
            case Step.VerdictIntro:
                Show(UIKit.PopupTone.Info, "NEW TYPE OF QUESTION", "Scam or legitimate?",
                    "This one is a real message to judge. Check the underlined details and use HINT if you need it, then press " +
                    $"<color={UIKit.HexDanger}><b>SCAM</b></color> or <color={UIKit.HexSafe}><b>LEGITIMATE</b></color>.",
                    "GOT IT", HideCard);
                break;
        }
    }

    void Update()
    {
        if (step == Step.Off || quiz == null || root == null) return;
        if (quiz.IsComplete) { Go(Step.Off); return; }

        var open = quiz.OpenMessage;
        bool answering = quiz.IsAnswering;
        bool feedback = open != null && !answering;
        var session = MessageInvestigation.Active != null ? MessageInvestigation.Active.Session : null;

        // A card with a NEXT button also continues with Enter (the cursor is locked while walking around).
        var kb = Keyboard.current;
        if (kb != null && onNext != null && nextButton.gameObject.activeInHierarchy && Time.unscaledTime - stepStarted > 0.4f &&
            (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !PauseMenu.IsPaused)
        {
            Next();
            return;
        }

        // The first question can be opened during the controls steps: move straight on to it.
        if ((step == Step.Welcome || step == Step.Move || step == Step.Interact || step == Step.Menus || step == Step.WaitForQuestion) && open != null && answering)
        {
            guided = open;
            Go(Step.WarningSigns);
            return;
        }

        switch (step)
        {
            case Step.Move:
                if (MoveCheckDone()) Go(Step.Interact);
                break;
            case Step.Interact:
                if (kb != null && kb.eKey.wasPressedThisFrame && Time.unscaledTime - stepStarted > 0.4f) Go(Step.Menus);
                break;
            case Step.WaitForQuestion:
                RefreshWaiting(false);
                break;
            case Step.WarningSigns:
                if (open != guided) Go(Step.Practice);
                else if (feedback) Go(Step.Feedback);
                break;
            case Step.CaseReview:
                if (open != guided) Go(Step.Practice);
                break;
            case Step.Investigate:
                if (open != guided) Go(Step.Practice);
                else if (feedback) Go(Step.Feedback);
                else if (session != null && session.Message == open && session.InspectedIds.Count > 0) Go(Step.ClueFound);
                break;
            case Step.ClueFound:
            case Step.Answer:
                if (open != guided) Go(Step.Practice);
                else if (feedback) Go(Step.Feedback);
                break;
            case Step.Hint:
                if (open != guided) Go(Step.Practice);
                else if (feedback) Go(Step.Feedback);
                else if (quiz.Scoring != null && quiz.Scoring.HintUsedFor(open.id)) Go(Step.Answer);
                break;
            case Step.Feedback:
                if (open != guided) Go(Step.Practice);
                break;
            case Step.Practice:
                if (open != null && answering && !open.HasChoices && !verdictTipDone) { verdictTipDone = true; Go(Step.VerdictIntro); }
                else if (!cardHidden && Time.unscaledTime - stepStarted > 12f) HideCard();
                else if (cardHidden && verdictTipDone) Go(Step.Off);
                break;
            case Step.VerdictIntro:
                if (open == null) Go(Step.Off);
                break;
        }
        if (step == Step.Off) return;

        // Never cover the pause menu or the wrong-answer card; never cover what the player must click.
        bool blocked = PauseMenu.IsPaused || (consequences != null && consequences.IsShowing);
        cardRt.gameObject.SetActive(!cardHidden && !blocked);
        // Presenters animate open: settle the card's position against their final layout during the first moment of a step.
        if (step != Step.Welcome && !cardHidden && !blocked && Time.unscaledTime - stepStarted < 1f && Time.unscaledTime >= nextPlace)
        {
            nextPlace = Time.unscaledTime + 0.15f;
            PlaceCard(false);
        }
        UpdateFrame(blocked);
    }

    // ---------- Move-and-look check ----------

    void BeginMoveCheck()
    {
        if (player == null)
        {
            GameObject tagged = null;
            try { tagged = GameObject.FindWithTag("Player"); } catch { /* tag may not exist */ }
            if (tagged != null) player = tagged.transform;
        }
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        walked = 0f;
        if (player != null) lastPos = player.position;
        if (cam != null) startLook = cam.rotation;
    }

    /// <summary>True once the player has walked about 1.5 m and turned the view by 25° or more.</summary>
    bool MoveCheckDone()
    {
        if (player == null || cam == null) return false;
        var d = player.position - lastPos; d.y = 0f;
        walked += d.magnitude;
        lastPos = player.position;
        return walked >= 1.5f && Quaternion.Angle(startLook, cam.rotation) >= 25f;
    }

    void RefreshWaiting(bool force)
    {
        bool arrived = quiz.PendingMessage != null;
        string bodyText = arrived ? "Press <b>E</b> to open it. Text messages open on your phone; emails and web pages open on a computer - look at the monitor and press <b>E</b>."
                                  : "Explore the room. When the alert appears at the bottom of the screen, press <b>E</b> to open it.";
        if (!force && bodyText == shownBody) return;
        Show(UIKit.PopupTone.Info, "GET READY", arrived ? "Your first question has arrived" : "Your first question is on its way", bodyText, null, null);
    }

    void HideCard()
    {
        cardHidden = true;
        onNext = null;
    }

    // ---------- Card ----------

    void Show(UIKit.PopupTone tone, string kick, string head, string text, string nextLabel, System.Action next)
    {
        shownBody = text;
        icon.SetTone(tone);
        UIKit.TintPanel(cardRt, tone == UIKit.PopupTone.Success ? tone : UIKit.PopupTone.Info);
        kicker.text = kick;
        kicker.color = UIKit.ToneColor(tone);
        title.text = head;
        body.text = text;
        onNext = next;
        nextButton.gameObject.SetActive(nextLabel != null);
        if (nextLabel != null) { UIKit.SetLabel(nextButton, nextLabel); UIKit.SetInteractable(nextButton, true); }
        skipButton.gameObject.SetActive(step != Step.Practice);
        cardHidden = false;
        cardRt.gameObject.SetActive(true);
        PlaceCard(step == Step.Welcome);
        cardAnim.Open();
        icon.Play();
    }

    /// <summary>
    /// Welcome: screen centre. With a message open: at the bottom, under the message column (never over the
    /// answer buttons or the framed control); top of the screen if the bottom would cover them. Otherwise bottom centre.
    /// </summary>
    void PlaceCard(bool centre)
    {
        if (centre) { UIKit.Place(cardRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 0)); return; }
        float k = ScaleFactor, screenW = Screen.width / k, w = 780f, x = 0f;
        var targets = quiz != null ? quiz.OpenPresenter as ITutorialTargets : null;
        if (targets != null && targets.TryGetTutorialRect(TutorialFocus.Message, out var msg))
        {
            w = Mathf.Clamp(msg.width / k, 620f, 780f);
            x = Mathf.Clamp(msg.center.x / k - screenW * 0.5f, -screenW * 0.5f + w * 0.5f + 16f, screenW * 0.5f - w * 0.5f - 16f);
        }
        UIKit.Place(cardRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 26), new Vector2(w, 0));
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardRt);
        if (!MessageInvestigation.ScreenRect(cardRt, out var card)) return;
        bool covers = TargetRect(out var target) && card.Overlaps(target);
        if (!covers && targets != null && targets.TryGetTutorialRect(TutorialFocus.Answers, out var answers)) covers = card.Overlaps(answers);
        if (covers) UIKit.Place(cardRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -26), new Vector2(w, 0));
    }

    float ScaleFactor
    {
        get
        {
            var canvas = root != null ? root.GetComponent<Canvas>() : null;
            return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        }
    }

    void Next()
    {
        var a = onNext;
        if (a == null) return;
        if (quiz != null && quiz.Level != null) { var la = FindFirstObjectByType<LevelAudio>(); if (la != null) la.PlayClick(); }
        a();
    }

    void Skip()
    {
        var la = FindFirstObjectByType<LevelAudio>();
        if (la != null) la.PlayClick();
        Go(Step.Off);
    }

    // ---------- Frame around the control to use ----------

    TutorialFocus? Focus =>
        step == Step.Investigate || step == Step.WarningSigns || (step == Step.CaseReview && !cardHidden) ? TutorialFocus.Message
        : step == Step.Hint ? TutorialFocus.Hint
        : step == Step.Answer || step == Step.VerdictIntro ? TutorialFocus.Answers
        : step == Step.Feedback && !cardHidden ? TutorialFocus.Feedback
        : (TutorialFocus?)null;

    string FocusLabel =>
        step == Step.Investigate ? "CLICK AN UNDERLINED WORD"
        : step == Step.WarningSigns ? "READ THE MESSAGE"
        : step == Step.CaseReview ? "CASE REVIEW"
        : step == Step.Hint ? "PRESS HINT"
        : step == Step.Answer ? "ANSWER HERE"
        : step == Step.VerdictIntro ? "SCAM OR LEGITIMATE"
        : "READ WHY";

    bool TargetRect(out Rect screen)
    {
        screen = default;
        var f = Focus;
        return f.HasValue && quiz != null && quiz.OpenPresenter is ITutorialTargets t && t.TryGetTutorialRect(f.Value, out screen);
    }

    void UpdateFrame(bool blocked)
    {
        if (blocked || !TargetRect(out var s))
        {
            frameRt.gameObject.SetActive(false);
            pillRt.gameObject.SetActive(false);
            return;
        }
        var canvas = root.GetComponent<Canvas>();
        float k = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        const float pad = 10f;
        frameRt.gameObject.SetActive(true);
        frameRt.anchorMin = frameRt.anchorMax = Vector2.zero;
        frameRt.pivot = Vector2.zero;
        frameRt.anchoredPosition = new Vector2(s.xMin / k - pad, s.yMin / k - pad);
        frameRt.sizeDelta = new Vector2(s.width / k + pad * 2f, s.height / k + pad * 2f);

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (var b in frameBars) b.color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, pulse);

        pillRt.gameObject.SetActive(true);
        pillText.text = FocusLabel;
        // Label above the frame; below it when the frame touches the top of the screen.
        float top = frameRt.anchoredPosition.y + frameRt.sizeDelta.y;
        bool roomAbove = top + 44f < Screen.height / k;
        pillRt.anchoredPosition = roomAbove ? new Vector2(frameRt.anchoredPosition.x, top + 6f)
                                            : new Vector2(frameRt.anchoredPosition.x, frameRt.anchoredPosition.y - 48f);
    }

    // ---------- Build ----------

    void Build()
    {
        root = UIKit.CreateCanvas(transform, "Tutorial Canvas", SortingOrder);

        dim = UIKit.Dimmer(root, 0.6f).GetComponent<Image>();

        // Pulsing frame (four bars) + label above it. Display only: clicks pass through to the game.
        frameRt = UIKit.Rect("Frame", root);
        frameBars = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var bar = UIKit.Rect("Bar" + i, frameRt);
            bool horizontal = i < 2;
            bar.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
            bar.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
            bar.pivot = new Vector2(horizontal ? 0.5f : i - 2, horizontal ? i : 0.5f);
            bar.sizeDelta = horizontal ? new Vector2(0, 4) : new Vector2(4, 0);
            bar.anchoredPosition = Vector2.zero;
            frameBars[i] = UIKit.AddImage(bar, UIKit.Accent);
            frameBars[i].raycastTarget = false;
        }
        pillRt = UIKit.Rect("Label", root);
        pillRt.anchorMin = pillRt.anchorMax = Vector2.zero;
        pillRt.pivot = Vector2.zero;
        pillText = UIKit.Badge(pillRt, "", UIKit.AccentDark, Color.white, 18f);
        var pillFit = pillRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        pillFit.childControlWidth = pillFit.childControlHeight = true;
        pillFit.childForceExpandWidth = pillFit.childForceExpandHeight = false;
        pillRt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var g in pillRt.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        // Coach card
        cardRt = UIKit.Rect("Card", root);
        UIKit.PanelImage(cardRt);
        UIKit.AccentStrip(cardRt, null, 6f);
        var v = UIKit.Vertical(cardRt, 26, 10);
        v.padding.left = 34;
        v.childForceExpandHeight = false;
        UIKit.FitHeight(cardRt);
        cardAnim = cardRt.gameObject.AddComponent<PopupAnimator>();
        cardAnim.drift = 0f;   // placed by anchors per step

        var head = UIKit.IconHeader(cardRt, UIKit.PopupTone.Info, out icon, 56f, 18f);
        kicker = UIKit.Text(head, "Kicker", "", 18, UIKit.Accent, bold: true);
        kicker.characterSpacing = 3f;
        title = UIKit.Text(head, "Title", "", 30, UIKit.TextPrimary, bold: true);
        body = UIKit.Text(cardRt, "Body", "", 23, UIKit.TextPrimary);
        body.lineSpacing = 6f;

        var row = UIKit.Rect("Buttons", cardRt);
        var h = UIKit.Horizontal(row, 14, TextAnchor.MiddleRight);
        h.childForceExpandWidth = false;
        UIKit.Size(row, -1, 56);
        skipButton = UIKit.HudButton(row, "SKIP TUTORIAL", Skip, UIKit.HudVariant.Secondary, 18);
        UIKit.Size(skipButton, 230, 52);
        nextButton = UIKit.HudButton(row, "NEXT", Next, UIKit.HudVariant.Primary, 22);
        UIKit.Size(nextButton, 230, 52);

        root.gameObject.SetActive(false);
    }
}
