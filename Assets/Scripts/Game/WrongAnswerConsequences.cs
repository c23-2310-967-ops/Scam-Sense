using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What happens after a wrong answer, on top of the normal feedback. QuizManager calls it with the real result
/// from ScoringSystem (it never decides correctness itself):
///   trusted a scam            -> "Investigation Error" with a simulated security incident ("security risk missed")
///   flagged a genuine message -> investigation accuracy warning: the details that support legitimacy, verify first
///   wrong final case          -> the evidence that was overlooked (records not opened at the evidence stations)
/// Every card explains why the answer was wrong and names the clue that was overlooked. For investigated messages
/// it offers a follow-up: the message opens again, a checklist asks the player to re-check the key details, and
/// only then can the verdict be given again (once) for recovery points.
///
/// Per-message settings live in ScamMessage.consequence (empty = generated from the message's own data).
/// Created by QuizManager at runtime (no Inspector setup). Everything shown is fictional: nothing real is
/// opened, sent or changed. Effects are short (a red frame and edge pulse under a second, a quiet alert).
/// </summary>
public class WrongAnswerConsequences : MonoBehaviour
{
    public enum Reaction { SecurityIncident, AccuracyWarning, MissedClue, FeedbackOnly }

    const int SortingOrder = 36;          // above the investigation UI (35), below the document reader, results and pause menu
    const int TrackerSortingOrder = 34;   // checklist sits under the investigation UI, so finding cards open over it
    const int MaxRecheckDetails = 3;

    /// <summary>The warning / recovery card is on screen.</summary>
    public bool IsShowing => root != null && cardGroup.gameObject.activeSelf;
    /// <summary>A follow-up re-check is running.</summary>
    public bool RecheckActive => recheckMsg != null;
    /// <summary>The re-check checklist is complete: the verdict may be given again.</summary>
    public bool RecheckReady { get; private set; }

    ScoringSystem scoring;
    LevelAudio levelAudio;
    LevelHUD hud;

    // Card
    RectTransform root, card, alertBox, edges;
    CanvasGroup cardGroup;
    Image strip, alertBg;
    Outline alertOutline;
    TMP_Text kicker, title, alertCaption, alertHeadline, alertBody, whyHeader, whyText, clueHeader, clueText, statusText, followUpNote;
    Button primaryButton, secondaryButton;
    Action primaryAction, secondaryAction;
    readonly HashSet<string> reacted = new HashSet<string>();   // answer events already reacted to (no double reactions)
    readonly List<GameObject> frames = new List<GameObject>();
    readonly List<Image> frameImages = new List<Image>();
    Image[] edgeImages;
    Coroutine effects;

    // Follow-up re-check
    RectTransform trackerRoot, tracker;
    IInvestigationDock recheckDock;
    TMP_Text trackerObjective, trackerList, trackerHint, trackerReward;
    Button hintButton;
    ScamMessage recheckMsg;
    IRecheckPresenter recheckPresenter;
    MessageInvestigation investigation;
    readonly List<MessageInspectable> recheckParts = new List<MessageInspectable>();
    int shownProgress = -1;
    Action skipAction;

    void Awake()
    {
        scoring = GetComponent<ScoringSystem>();
        levelAudio = GetComponent<LevelAudio>();
        hud = GetComponent<LevelHUD>();
        Build();
        cardGroup.gameObject.SetActive(false);
        tracker.gameObject.SetActive(false);
        edges.gameObject.SetActive(false);
    }

    void OnDestroy() => InteractionGate.RemoveEscapeHandler(this);

    // ---------- Scenario rules ----------

    /// <summary>The reaction for a wrong answer. A genuine message never gets a fake security incident.</summary>
    public static Reaction ReactionFor(ScamMessage msg)
    {
        var kind = msg.consequence != null ? msg.consequence.kind : ConsequenceKind.Auto;
        bool scam = msg.correctVerdict == MessageVerdict.Scam;
        switch (kind)
        {
            case ConsequenceKind.SecurityIncident: return scam ? Reaction.SecurityIncident : Reaction.AccuracyWarning;
            case ConsequenceKind.AccuracyWarning:  return Reaction.AccuracyWarning;
            case ConsequenceKind.MissedClue:       return Reaction.MissedClue;
            case ConsequenceKind.FeedbackOnly:     return Reaction.FeedbackOnly;
        }
        if (msg.channel == MessageChannel.Case) return Reaction.MissedClue;
        return scam ? Reaction.SecurityIncident : Reaction.AccuracyWarning;
    }

    /// <summary>True if a wrong answer to this message unlocks the follow-up re-check (investigated messages only).</summary>
    public static bool OffersFollowUp(ScamMessage msg) =>
        msg != null && msg.channel != MessageChannel.Case && MessageInvestigation.Investigates(msg) &&
        (msg.consequence == null || msg.consequence.followUp == FollowUpMode.Auto) && RecheckParts(msg).Count > 0;

    /// <summary>Details to re-check: the configured ids, or up to three that reveal key clues (links and senders first).</summary>
    public static List<MessageInspectable> RecheckParts(ScamMessage m)
    {
        var list = new List<MessageInspectable>();
        if (m == null || m.inspectables == null) return list;
        var ids = m.consequence != null ? m.consequence.recheckDetails : null;
        if (ids != null && ids.Length > 0)
        {
            foreach (var id in ids)
            {
                var p = m.inspectables.Find(i => i.id == id);
                if (p != null && !list.Contains(p)) list.Add(p);
            }
            return list;
        }
        int Rank(MessageInspectable p) => p.IsLink ? 0 : p.field == InspectField.Sender || p.field == InspectField.SenderDetail ? 1 : p.field == InspectField.Subject ? 2 : 3;
        var candidates = m.inspectables.FindAll(p => IsKeyClue(m, p.clueId));
        for (int rank = 0; rank <= 3 && list.Count < MaxRecheckDetails; rank++)
            foreach (var p in candidates)
                if (Rank(p) == rank && list.Count < MaxRecheckDetails) list.Add(p);
        return list;
    }

    static bool IsKeyClue(ScamMessage m, string clueId)
    {
        if (string.IsNullOrEmpty(clueId) || m.evidence == null) return false;
        var c = m.evidence.Find(e => e.id == clueId);
        return c != null && c.supportsVerdict;
    }

    // ---------- Wrong answer ----------

    /// <summary>
    /// Reacts to a wrong first answer (once per message: repeated calls for the same answer are ignored).
    /// <paramref name="onFollowUp"/> is offered only when <paramref name="canFollowUp"/>; Continue / Esc runs <paramref name="onContinue"/>.
    /// </summary>
    public bool ShowMistake(ScoringSystem.AnswerResult r, IMessagePresenter presenter, InvestigationSession.Result eval,
                            bool canFollowUp, Action onFollowUp, Action onContinue)
    {
        if (r.message == null || r.isCorrect || !reacted.Add(r.message.id + "#answer")) return false;
        var m = r.message;
        var reaction = ReactionFor(m);
        string noun = MessageChannels.Noun(m.channel);
        bool scam = m.correctVerdict == MessageVerdict.Scam;

        Color tone = reaction == Reaction.SecurityIncident ? UIKit.Danger : UIKit.Warning;
        strip.color = tone;
        kicker.text = "INVESTIGATION ERROR";
        kicker.color = tone;
        title.text = reaction == Reaction.SecurityIncident ? $"You trusted a scam {noun}"
                   : reaction == Reaction.AccuracyWarning ? $"A genuine {noun} was reported as a scam"
                   : reaction == Reaction.MissedClue ? "The case conclusion missed key evidence"
                   : "That verdict was not right";

        // Simulated consequence (none for FeedbackOnly)
        alertBox.gameObject.SetActive(reaction != Reaction.FeedbackOnly);
        if (reaction != Reaction.FeedbackOnly)
        {
            var (caption, headline, body) = AlertFor(m, reaction);
            alertCaption.text = caption;
            alertHeadline.text = headline;
            alertBody.text = body;
            alertBg.color = new Color(tone.r * 0.18f, tone.g * 0.18f, tone.b * 0.18f, 0.96f);
            alertOutline.effectColor = tone;
            alertCaption.color = tone;
        }

        whyHeader.text = "Why this answer was wrong";
        whyText.text = $"You chose <b>{UIKit.VerdictWord(r.chosen)}</b>, but this {noun} is " +
                       $"<color={(scam ? UIKit.HexDanger : UIKit.HexSafe)}><b>{UIKit.VerdictWord(m.correctVerdict)}</b></color>. {m.explanation}";

        if (reaction == Reaction.AccuracyWarning)
        {
            clueHeader.text = "Details that support legitimacy";
            clueText.text = LegitimacyDetails(m, eval) +
                $"\n<color={UIKit.HexSecondary}>Verify through a channel you already trust - the official app, a bookmarked site or a known phone number - instead of judging a message by how it looks.</color>";
        }
        else if (reaction == Reaction.MissedClue)
        {
            clueHeader.text = "Evidence to look at again";
            clueText.text = MissedEvidence(m);
        }
        else
        {
            clueHeader.text = "The clue you overlooked";
            clueText.text = OverlookedClue(m, eval);
        }

        string status = StatusLabel(reaction);
        statusText.text = $"Case status  <color={StatusHex(reaction)}><b>{status}</b></color>   ·   {ScoringSystem.PointsSummary(r)}   ·   " +
                          $"Accuracy <b>{scoring.CorrectCount}/{scoring.Results.Count}</b>";

        followUpNote.gameObject.SetActive(canFollowUp);
        if (canFollowUp)
            followUpNote.text = $"<color={UIKit.HexAccent}><b>FOLLOW-UP AVAILABLE</b></color>   Re-check the key details of this {noun} and give your verdict again " +
                                $"to recover <b>{scoring.RecoveryPointsFor(false)} points</b>.";

        SetButtons(canFollowUp ? "REVIEW EVIDENCE" : "CONTINUE INVESTIGATION", canFollowUp ? onFollowUp : onContinue,
                   canFollowUp ? "CONTINUE WITHOUT REVIEW" : null, canFollowUp ? onContinue : null);
        UpdateHudStatus();
        Open(presenter as IWarningTarget, warning: true, tone);
        return true;
    }

    // ---------- Follow-up re-check ----------

    /// <summary>The message has been shown again for the follow-up: lock the verdict until the checklist is done.</summary>
    public void BeginRecheck(ScamMessage msg, IMessagePresenter presenter, MessageInvestigation inv, Action onSkip)
    {
        CloseCard();
        recheckMsg = msg;
        recheckPresenter = presenter as IRecheckPresenter;
        recheckDock = presenter as IInvestigationDock;
        investigation = inv;
        skipAction = onSkip;
        recheckParts.Clear();
        recheckParts.AddRange(RecheckParts(msg));
        RecheckReady = false;
        shownProgress = -1;

        bool scam = msg.correctVerdict == MessageVerdict.Scam;
        string noun = MessageChannels.Noun(msg.channel);
        trackerObjective.text = scam
            ? $"The simulated incident started with this {noun}. Re-check its warning signs, then give your verdict again."
            : $"Re-check the details that show whether this {noun} is genuine, then give your verdict again.";
        trackerHint.text = "";
        UIKit.SetInteractable(hintButton, true);
        tracker.gameObject.SetActive(true);
        RefreshTracker(force: true);
    }

    /// <summary>The follow-up is over (answered or skipped).</summary>
    public void EndRecheck()
    {
        recheckMsg = null;
        recheckPresenter = null;
        recheckDock = null;
        investigation = null;
        skipAction = null;
        RecheckReady = false;
        if (tracker != null) tracker.gameObject.SetActive(false);
    }

    void Update()
    {
        if (RecheckActive) RefreshTracker(force: false);
    }

    /// <summary>
    /// The checklist goes at the bottom of the presenter's investigation dock (the computer's inbox column, not needed
    /// while re-checking), so it never covers the message details the player must click. Otherwise bottom-left.
    /// </summary>
    void LateUpdate()
    {
        if (!RecheckActive || !tracker.gameObject.activeSelf) return;
        var canvas = trackerRoot.GetComponent<Canvas>();
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        tracker.anchorMin = tracker.anchorMax = Vector2.zero;
        tracker.pivot = Vector2.zero;
        if (recheckDock != null && recheckDock.TryGetDockArea(out var area) && area.width / scale >= 300f)
        {
            float w = Mathf.Min(area.width / scale, 620f);
            tracker.anchoredPosition = new Vector2(area.x / scale + (area.width / scale - w) * 0.5f, area.y / scale);
            tracker.sizeDelta = new Vector2(w, tracker.sizeDelta.y);
        }
        else
        {
            tracker.anchoredPosition = new Vector2(24f, 24f);
            tracker.sizeDelta = new Vector2(560f, tracker.sizeDelta.y);
        }
    }

    /// <summary>Rebuilds the checklist only when its progress changes (no per-frame strings).</summary>
    void RefreshTracker(bool force)
    {
        var s = investigation != null ? investigation.Session : null;
        int inspected = 0;
        foreach (var p in recheckParts) if (s != null && s.WasInspected(p.id)) inspected++;
        bool hint = scoring.HintUsedFor(recheckMsg.id);
        int progress = inspected * 2 + (hint ? 1 : 0);
        if (!force && progress == shownProgress) return;
        shownProgress = progress;

        bool ready = inspected >= recheckParts.Count;
        var sb = new StringBuilder();
        foreach (var p in recheckParts)
        {
            bool done = s != null && s.WasInspected(p.id);
            sb.Append(Check(done)).Append($"Inspect: <b>{p.label}</b>").Append(done ? "" : $" <color={UIKit.HexSecondary}>({Where(p)})</color>").Append('\n');
        }
        sb.Append(Check(false)).Append(ready ? "<b>Give your verdict again</b>" : $"<color={UIKit.HexSecondary}>Give your verdict again</color>");
        trackerList.text = sb.ToString();
        trackerReward.text = $"Recovery worth <color={UIKit.HexAccent}><b>{scoring.RecoveryPointsFor(hint)} pts</b></color>" + (hint ? "  (hint used)" : "");

        if (ready && !RecheckReady && !force && levelAudio != null) levelAudio.PlayClick();
        RecheckReady = ready;
        recheckPresenter?.SetRecheck(ready, ready ? "Checks complete - answer again" : $"Follow-up checklist  {inspected}/{recheckParts.Count}");
    }

    static string Check(bool done) => done ? $"<color={UIKit.HexSafe}>✓</color>  " : $"<color={UIKit.HexAccent}>•</color>  ";

    static string Where(MessageInspectable p) =>
        p.IsLink ? "the link" : p.field == InspectField.Sender ? "the sender" : p.field == InspectField.SenderDetail ? "the sender's address"
        : p.field == InspectField.Subject ? "the subject line" : "the message text";

    void UseHint()
    {
        if (!RecheckActive) return;
        scoring.RecordHint(recheckMsg.id);   // counted once per message
        var s = investigation != null ? investigation.Session : null;
        var next = recheckParts.Find(p => s == null || !s.WasInspected(p.id));
        trackerHint.text = next != null
            ? $"<color={UIKit.HexWarning}>Hint:</color> in {Where(next)}, click <color=#7DD3FC><u>{Literal(string.IsNullOrEmpty(next.match) ? next.label : next.match)}</u></color>."
            : $"<color={UIKit.HexWarning}>Hint:</color> every detail is checked - give your verdict again.";
        if (levelAudio != null) levelAudio.PlayClick();
        RefreshTracker(force: true);
    }

    void Skip()
    {
        var a = skipAction;
        EndRecheck();
        a?.Invoke();
    }

    // ---------- Follow-up result ----------

    /// <summary>Feedback for the follow-up answer (once per message).</summary>
    public bool ShowRecheckResult(ScoringSystem.AnswerResult r, InvestigationSession.Result eval, IMessagePresenter presenter, Action onContinue)
    {
        EndRecheck();
        if (r.message == null || !reacted.Add(r.message.id + "#recheck")) return false;
        var m = r.message;
        string noun = MessageChannels.Noun(m.channel);
        bool scam = m.correctVerdict == MessageVerdict.Scam;
        Color tone = r.isCorrect ? UIKit.Safe : UIKit.Warning;
        strip.color = tone;
        kicker.color = tone;
        kicker.text = r.isCorrect ? "FOLLOW-UP COMPLETE" : "FOLLOW-UP CLOSED";
        title.text = r.isCorrect ? "✓  Evidence confirmed - case recovered" : "Still not right";

        alertBox.gameObject.SetActive(r.isCorrect);
        if (r.isCorrect)
        {
            alertCaption.text = scam ? "THREAT CONTAINED  ·  SIMULATION" : "RECORD CORRECTED  ·  SIMULATION";
            alertCaption.color = tone;
            alertHeadline.text = scam ? $"This {noun} is now reported as a scam" : $"This {noun} is now marked as genuine";
            alertBody.text = scam ? "In a real incident: change the password at the official site, turn on two-step sign-in and report the message to your IT or security team."
                                  : "In real life: if a genuine message is ever in doubt, confirm it through the official app or a known contact - then act on it.";
            alertBg.color = new Color(tone.r * 0.16f, tone.g * 0.16f, tone.b * 0.16f, 0.96f);
            alertOutline.effectColor = tone;
        }

        whyHeader.text = r.isCorrect ? "The clues that decide it" : "Why";
        whyText.text = r.isCorrect ? KeyClueList(m, eval) : $"This {noun} is <b>{UIKit.VerdictWord(m.correctVerdict)}</b>. {m.explanation}";
        clueHeader.text = "Remember";
        clueText.text = scam
            ? "Check who really sent it, where any link really goes, and what it asks you to do - before you act."
            : "Genuine messages match something you did, ask for nothing risky, and point you to the official app or site.";
        statusText.text = $"Case status  <color={(r.isCorrect ? UIKit.HexSafe : UIKit.HexWarning)}><b>{(r.isCorrect ? "RISK RESOLVED" : "NEEDS REVIEW")}</b></color>   ·   " +
                          $"{ScoringSystem.PointsSummary(r)}   ·   Total score <b>{scoring.TotalScore}</b>";
        followUpNote.gameObject.SetActive(false);
        SetButtons("CONTINUE INVESTIGATION", onContinue, null, null);
        UpdateHudStatus();
        Open(presenter as IWarningTarget, warning: !r.isCorrect, tone);
        return true;
    }

    // ---------- HUD ----------

    /// <summary>"Case status  On track · Accuracy 3/4" on the HUD; unresolved mistakes are named.</summary>
    public void UpdateHudStatus()
    {
        if (hud == null || scoring == null) return;
        int risks = 0, alarms = 0, wrong = 0;
        foreach (var r in scoring.Results)
        {
            if (r.isCorrect || r.message == null) continue;
            wrong++;
            if (scoring.WasRecovered(r.message.id)) continue;
            if (r.message.correctVerdict == MessageVerdict.Scam) risks++; else alarms++;
        }
        string state = risks > 0 ? $"<color={UIKit.HexDanger}>{risks} security risk{(risks > 1 ? "s" : "")} missed</color>"
                     : alarms > 0 ? $"<color={UIKit.HexWarning}>{alarms} false alarm{(alarms > 1 ? "s" : "")}</color>"
                     : wrong > 0 ? $"<color={UIKit.HexSafe}>Recovered - on track</color>"
                     : $"<color={UIKit.HexSafe}>On track</color>";
        hud.SetCaseStatus($"Case status   {state}   <color={UIKit.HexSecondary}>·   accuracy {scoring.CorrectCount}/{scoring.Results.Count}</color>");
    }

    // ---------- Closing ----------

    /// <summary>Hides everything (the message is finished or the level ended).</summary>
    public void Close()
    {
        CloseCard();
        EndRecheck();
    }

    void CloseCard()
    {
        if (root == null) return;
        if (effects != null) { StopCoroutine(effects); effects = null; }
        ClearFrames();
        edges.gameObject.SetActive(false);
        cardGroup.gameObject.SetActive(false);
        InteractionGate.RemoveEscapeHandler(this);
        primaryAction = secondaryAction = null;
    }

    void SetButtons(string primary, Action onPrimary, string secondary, Action onSecondary)
    {
        primaryAction = onPrimary;
        secondaryAction = onSecondary;
        UIKit.SetLabel(primaryButton, primary);
        UIKit.SetInteractable(primaryButton, true);
        secondaryButton.gameObject.SetActive(secondary != null);
        if (secondary != null) { UIKit.SetLabel(secondaryButton, secondary); UIKit.SetInteractable(secondaryButton, true); }
    }

    void Click(bool primary)
    {
        var a = primary ? primaryAction : secondaryAction;
        if (a == null) return;
        if (levelAudio != null) levelAudio.PlayClick();
        CloseCard();   // clears the actions first, so a double click can't run one twice
        a();
    }

    /// <summary>Esc = the safe choice: continue (or skip the review).</summary>
    bool OnEscape()
    {
        if (!IsShowing || PauseMenu.IsPaused) return false;
        Click(secondaryAction == null);
        return true;
    }

    // ---------- Effects ----------

    void Open(IWarningTarget target, bool warning, Color tone)
    {
        if (effects != null) StopCoroutine(effects);
        ClearFrames();
        cardGroup.gameObject.SetActive(true);
        cardGroup.alpha = 0f;
        InteractionGate.PushEscapeHandler(this, OnEscape);
        var t = target != null ? target.WarningTarget : null;
        if (warning && t != null)
        {
            var f = Frame(t, tone);
            frames.Add(f);
            frameImages.AddRange(f.GetComponentsInChildren<Image>());
        }
        effects = StartCoroutine(Effects(warning, tone));
    }

    /// <summary>Fade the card in, then (warnings) a quiet alert, a short shake and two soft red pulses. Under a second.</summary>
    IEnumerator Effects(bool warning, Color tone)
    {
        edges.gameObject.SetActive(warning);
        foreach (var img in edgeImages) img.color = new Color(tone.r, tone.g, tone.b, 0f);
        if (warning && levelAudio != null) levelAudio.PlayWarning();
        const float duration = 0.9f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            cardGroup.alpha = Mathf.Clamp01(t / 0.15f);
            if (warning)
            {
                float pulse = Mathf.Sin(t / duration * Mathf.PI * 2f);   // two soft pulses
                float a = pulse * pulse * (1f - t / duration);
                foreach (var img in edgeImages) img.color = new Color(tone.r, tone.g, tone.b, 0.32f * a);
                foreach (var img in frameImages) if (img != null) img.color = new Color(tone.r, tone.g, tone.b, img.name == "Fill" ? 0.12f * a : 0.9f * Mathf.Max(a, 0.25f));
                float shake = t < 0.35f ? Mathf.Sin(t * 70f) * 9f * (1f - t / 0.35f) : 0f;
                card.anchoredPosition = new Vector2(shake, 0f);
            }
            yield return null;
        }
        cardGroup.alpha = 1f;
        card.anchoredPosition = Vector2.zero;
        edges.gameObject.SetActive(false);
        ClearFrames();
        effects = null;
    }

    /// <summary>A thin red frame laid over a presenter's panel (ignored by its layout, removed after the pulse).</summary>
    static GameObject Frame(RectTransform target, Color tone)
    {
        var f = UIKit.Rect("WarningFrame", target);
        f.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        UIKit.Stretch(f);
        f.SetAsLastSibling();
        var fill = UIKit.AddImage(UIKit.Rect("Fill", f), new Color(tone.r, tone.g, tone.b, 0f));
        UIKit.Stretch((RectTransform)fill.transform);
        fill.raycastTarget = false;
        Edge(f, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), Vector2.zero, tone);
        Edge(f, "Bottom", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 6), tone);
        Edge(f, "Left", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(6, 0), tone);
        Edge(f, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-6, 0), Vector2.zero, tone);
        return f.gameObject;
    }

    static void Edge(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color tone)
    {
        var e = UIKit.Rect(name, parent);
        e.anchorMin = aMin; e.anchorMax = aMax; e.offsetMin = oMin; e.offsetMax = oMax;
        UIKit.AddImage(e, new Color(tone.r, tone.g, tone.b, 0f)).raycastTarget = false;
    }

    void ClearFrames()
    {
        foreach (var f in frames) if (f != null) Destroy(f);
        frames.Clear();
        frameImages.Clear();
    }

    // ---------- Text ----------

    static (string caption, string headline, string body) AlertFor(ScamMessage m, Reaction reaction)
    {
        var c = m.consequence;
        string noun = MessageChannels.Noun(m.channel);
        string caption, headline, body;
        if (reaction == Reaction.SecurityIncident)
        {
            caption = "SIMULATED SECURITY INCIDENT";
            bool link = !string.IsNullOrEmpty(m.link);
            if (m.channel == MessageChannel.Website)
            {
                headline = "Login details sent to a fake website";
                body = "The page you trusted copied what was typed into it. Minutes later: <b>new sign-in to your account from an unrecognised device</b>.";
            }
            else if (link && MessageChannels.OnPhone(m.channel))
            {
                headline = "Suspicious payment attempt on your account";
                body = $"The link in the {noun} opened a look-alike page that captured your details. Your bank has flagged an <b>unrecognised payment attempt</b>.";
            }
            else if (link)
            {
                headline = "New sign-in from an unrecognised device";
                body = $"The link in this {noun} led to a look-alike sign-in page. The password entered there was used moments later to <b>sign in to your account from an unknown location</b>.";
            }
            else
            {
                headline = "Request completed for an impostor";
                body = string.IsNullOrEmpty(m.requestedAction)
                    ? $"Acting on this {noun} gave the sender what they wanted. Money, codes or information sent to an impostor usually cannot be recovered."
                    : $"The request was carried out: <i>{Literal(m.requestedAction.TrimEnd('.', ' '))}</i>. Money, codes or information sent to an impostor usually cannot be recovered.";
            }
            body += $"\n<size=85%><color={UIKit.HexSecondary}>Sender: {Literal(m.sender)}{(string.IsNullOrEmpty(m.senderDetail) ? "" : "  " + Literal(m.senderDetail))}</color></size>";
        }
        else if (reaction == Reaction.AccuracyWarning)
        {
            caption = "INVESTIGATION ACCURACY WARNING";
            headline = $"Genuine {noun} blocked";
            body = "Reporting a real message as a scam has costs too: a missed delivery, a late payment or an ignored colleague. " +
                   "Good investigators verify before they judge.";
        }
        else
        {
            caption = "EVIDENCE OVERLOOKED";
            headline = "The conclusion does not match the evidence";
            body = "The case file was submitted before every piece of evidence pointed the same way. Compare the records again before you conclude.";
        }
        if (c != null && !string.IsNullOrEmpty(c.alertTitle)) headline = c.alertTitle;
        if (c != null && !string.IsNullOrEmpty(c.alertBody)) body = c.alertBody;
        body += $"\n<size=80%><color={UIKit.HexSecondary}><i>Training simulation - no real account, message or file was affected.</i></color></size>";
        return (caption, headline, body);
    }

    static string StatusLabel(Reaction r) =>
        r == Reaction.SecurityIncident ? "SECURITY RISK MISSED" : r == Reaction.AccuracyWarning ? "FALSE ALARM RAISED"
        : r == Reaction.MissedClue ? "EVIDENCE OVERLOOKED" : "NEEDS REVIEW";

    static string StatusHex(Reaction r) => r == Reaction.SecurityIncident ? UIKit.HexDanger : UIKit.HexWarning;

    /// <summary>The configured clue, else the first key clue the player missed (or found but misjudged), else the Details clues.</summary>
    static string OverlookedClue(ScamMessage m, InvestigationSession.Result eval)
    {
        if (m.consequence != null && !string.IsNullOrEmpty(m.consequence.overlookedClue)) return m.consequence.overlookedClue;
        MessageEvidence c = null;
        if (eval != null) c = eval.missed.Count > 0 ? eval.missed[0] : eval.found.Count > 0 ? eval.found[0] : null;
        if (c == null && m.evidence != null) c = m.evidence.Find(e => e.supportsVerdict);
        if (c != null) return $"<b>{c.title}</b>  -  {c.whyItMatters}";
        if (m.clues != null && m.clues.Length > 0) return m.clues[0];
        if (!string.IsNullOrEmpty(m.linkNote)) return m.linkNote;
        if (!string.IsNullOrEmpty(m.senderDetail)) return $"Who really sent it: {Literal(m.senderDetail)}";
        return "Compare the message with your case notes: did you expect it, and does it ask for something risky?";
    }

    /// <summary>Up to three details that show a genuine message is genuine.</summary>
    static string LegitimacyDetails(ScamMessage m, InvestigationSession.Result eval)
    {
        if (m.consequence != null && !string.IsNullOrEmpty(m.consequence.overlookedClue)) return m.consequence.overlookedClue;
        var list = new List<MessageEvidence>();
        if (eval != null) { list.AddRange(eval.missed); list.AddRange(eval.found); }
        else if (m.evidence != null) list.AddRange(m.evidence.FindAll(e => e.supportsVerdict));
        var sb = new StringBuilder();
        int n = 0;
        foreach (var c in list)
        {
            if (n++ >= 3) break;
            sb.Append($"<color={UIKit.HexSafe}>✓</color>  <b>{c.title}</b>  -  {c.whyItMatters}\n");
        }
        if (n == 0 && m.clues != null)
            foreach (var clue in m.clues) { if (n++ >= 3) break; sb.Append($"<color={UIKit.HexSafe}>✓</color>  {clue}\n"); }
        return n == 0 ? "It matches something you actually did and asks for nothing risky." : sb.ToString().TrimEnd();
    }

    /// <summary>Final case: required evidence records the player never opened (from the evidence stations).</summary>
    static string MissedEvidence(ScamMessage m)
    {
        if (m.consequence != null && !string.IsNullOrEmpty(m.consequence.overlookedClue)) return m.consequence.overlookedClue;
        var terminal = FindFirstObjectByType<FinalCaseTerminal>();
        var tracker = FindFirstObjectByType<EvidenceTracker>();
        var notOpened = new List<string>();
        if (terminal != null && terminal.RequiredEvidence != null)
            foreach (var e in terminal.RequiredEvidence)
                if (tracker == null || !tracker.IsInspected(e)) notOpened.Add(e);
        if (notOpened.Count > 0)
            return $"Records you did not open: <b>{string.Join(", ", notOpened)}</b>. Open them at the evidence stations before concluding a case.";
        if (m.clues != null && m.clues.Length > 0) return m.clues[0];
        return "You opened every record - compare the sender, the domain and the website capture side by side: do they all point to the same conclusion?";
    }

    static string KeyClueList(ScamMessage m, InvestigationSession.Result eval)
    {
        var list = new List<MessageEvidence>();
        if (eval != null) list.AddRange(eval.found);
        var sb = new StringBuilder();
        foreach (var c in list) sb.Append($"<color={UIKit.HexSafe}>✓</color>  <b>{c.title}</b>  -  {c.whyItMatters}\n");
        if (eval != null && eval.missed.Count > 0)
            sb.Append($"<color={UIKit.HexSecondary}>Also worth checking: {string.Join(", ", eval.missed.ConvertAll(c => c.title))}</color>\n");
        return list.Count == 0 && sb.Length == 0 ? m.explanation : sb.ToString().TrimEnd();
    }

    static string Literal(string s) => string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "") + "</noparse>";

    // ---------- Build ----------

    void Build()
    {
        root = UIKit.CreateCanvas(transform, "Consequences Canvas", SortingOrder);

        // Screen-edge pulse: four thin bars, only during the warning (never a full-screen tint).
        edges = UIKit.Rect("EdgePulse", root);
        UIKit.Stretch(edges);
        Edge(edges, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -22), Vector2.zero, UIKit.Danger);
        Edge(edges, "Bottom", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 22), UIKit.Danger);
        Edge(edges, "Left", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(22, 0), UIKit.Danger);
        Edge(edges, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-22, 0), Vector2.zero, UIKit.Danger);
        edgeImages = edges.GetComponentsInChildren<Image>(true);

        // Modal card
        var modal = UIKit.Rect("Modal", root);
        UIKit.Stretch(modal);
        cardGroup = modal.gameObject.AddComponent<CanvasGroup>();
        UIKit.Dimmer(modal, 0.55f);

        card = UIKit.Rect("Card", modal);
        UIKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 0));
        UIKit.PanelImage(card);
        var v = UIKit.Vertical(card, 34, 12);
        v.padding.left = 48;
        v.childForceExpandHeight = false;
        UIKit.FitHeight(card);
        var stripRt = UIKit.Rect("Strip", card);
        stripRt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        stripRt.anchorMin = new Vector2(0, 0); stripRt.anchorMax = new Vector2(0, 1); stripRt.pivot = new Vector2(0, 0.5f);
        stripRt.offsetMin = Vector2.zero; stripRt.offsetMax = new Vector2(10, 0);
        strip = UIKit.AddImage(stripRt, UIKit.Danger);
        strip.raycastTarget = false;

        kicker = UIKit.Text(card, "Kicker", "", 20, UIKit.Danger, bold: true);
        kicker.characterSpacing = 5f;
        title = UIKit.Text(card, "Title", "", 38, UIKit.TextPrimary, bold: true);

        alertBox = UIKit.Rect("Alert", card);
        alertBg = UIKit.AddImage(alertBox, new Color(0.2f, 0.05f, 0.05f, 0.96f), rounded: true);
        alertBg.raycastTarget = false;
        alertOutline = alertBox.gameObject.AddComponent<Outline>();
        alertOutline.effectDistance = new Vector2(2, -2);
        var av = UIKit.Vertical(alertBox, 20, 6);
        av.childForceExpandHeight = false;
        alertCaption = UIKit.Text(alertBox, "Caption", "", 17, UIKit.Danger, bold: true);
        alertCaption.characterSpacing = 3f;
        alertHeadline = UIKit.Text(alertBox, "Headline", "", 27, UIKit.TextPrimary, bold: true);
        alertBody = UIKit.Text(alertBox, "Body", "", 21, UIKit.TextPrimary);
        alertBody.lineSpacing = 4f;

        whyHeader = UIKit.Header(card, "Why this answer was wrong");
        whyText = UIKit.Text(card, "Why", "", 22, UIKit.TextPrimary);
        whyText.lineSpacing = 4f;
        clueHeader = UIKit.Header(card, "The clue you overlooked");
        clueText = UIKit.Text(card, "Clue", "", 22, UIKit.TextPrimary);
        clueText.lineSpacing = 4f;

        UIKit.Divider(card);
        statusText = UIKit.Text(card, "Status", "", 20, UIKit.TextSecondary);
        followUpNote = UIKit.Text(card, "FollowUp", "", 20, UIKit.TextPrimary);

        var row = UIKit.Rect("Buttons", card);
        UIKit.Horizontal(row, 16);
        UIKit.Size(row, -1, 70);
        primaryButton = UIKit.HudButton(row, "CONTINUE", () => Click(true), UIKit.HudVariant.Primary, 24);
        secondaryButton = UIKit.HudButton(row, "CONTINUE", () => Click(false), UIKit.HudVariant.Secondary, 22);

        // Follow-up checklist (top centre, not modal: the message stays usable underneath)
        trackerRoot = UIKit.CreateCanvas(transform, "FollowUp Canvas", TrackerSortingOrder);
        tracker = UIKit.Rect("FollowUp", trackerRoot);
        UIKit.Place(tracker, Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(560, 0));
        UIKit.PanelImage(tracker);
        UIKit.AccentStrip(tracker, UIKit.Warning, 6f);
        var tv = UIKit.Vertical(tracker, 16, 5);
        tv.padding.left = 26;
        tv.childForceExpandHeight = false;
        UIKit.FitHeight(tracker);
        var head = UIKit.Text(tracker, "Title", "FOLLOW-UP INVESTIGATION", 18, UIKit.Warning, bold: true);
        head.characterSpacing = 3f;
        trackerObjective = UIKit.Text(tracker, "Objective", "", 18, UIKit.TextPrimary);
        trackerList = UIKit.Text(tracker, "Checklist", "", 18, UIKit.TextPrimary);
        trackerList.lineSpacing = 3f;
        trackerHint = UIKit.Text(tracker, "Hint", "", 17, UIKit.TextPrimary);
        trackerReward = UIKit.Text(tracker, "Reward", "", 17, UIKit.TextSecondary);
        var trow = UIKit.Rect("Row", tracker);
        UIKit.Horizontal(trow, 10);
        UIKit.Size(trow, -1, 44);
        hintButton = UIKit.HudButton(trow, "HINT", UseHint, UIKit.HudVariant.Secondary, 17);
        UIKit.HudButton(trow, "SKIP FOLLOW-UP", Skip, UIKit.HudVariant.Secondary, 17);
    }
}
