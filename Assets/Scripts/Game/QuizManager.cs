using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the message sequence for one level:
///   wait (time or player activity) -> notification -> player opens it -> response timer runs while reading
///   -> answer is scored once -> feedback -> next message.
/// A wrong answer also gets its consequence (WrongAnswerConsequences: investigation error, simulated incident or
/// accuracy warning) and, for investigated messages, an optional follow-up re-check: the message opens again
/// (Recheck phase), and one more verdict is scored as a recovery - the first answer's score is never changed.
/// Text messages open on the phone pop-up (E anywhere). Emails are read on a computer (ComputerTerminal)
/// when the scene has one; otherwise they also use the pop-up. Scoring is always done here, once.
/// Runs early in the frame so that when E opens a message, doors and chairs ignore that press.
/// </summary>
[DefaultExecutionOrder(-100)]
public class QuizManager : MonoBehaviour
{
    private enum Phase { Idle, Waiting, Notified, Reading, Feedback, Recheck, Complete }

    [Header("References (auto-found on this object if empty)")]
    [SerializeField] private ScoringSystem scoring;
    [Tooltip("Phone pop-up. Also used for emails if the scene has no ComputerTerminal.")]
    [SerializeField] private MessagePopupUI popup;
    [SerializeField] private LevelHUD hud;
    [SerializeField] private LevelAudio levelAudio;
    [Tooltip("Defaults to the object tagged Player.")]
    [SerializeField] private Transform player;
    [Tooltip("Defaults to Camera.main.")]
    [SerializeField] private Camera playerCamera;

    [Header("Input")]
    [SerializeField] private Key interactKey = Key.E;
    [SerializeField] private float lookDistance = 3f;

    [Header("Activity Triggers")]
    [Tooltip("Metres walked since the last message that count as 'exploring'.")]
    [SerializeField] private float walkDistanceTrigger = 6f;
    [Tooltip("How close counts as 'near' the phone or a computer.")]
    [SerializeField] private float nearDeviceRadius = 3f;

    [Header("Reminders")]
    [Tooltip("Replay the notification sound if a message is ignored this long (0 = never).")]
    [SerializeField] private float reminderInterval = 20f;

    public event Action Completed;
    /// <summary>Raised whenever a message arrives, opens, is answered or is closed (computer screens refresh on it).</summary>
    public event Action StateChanged;

    public bool IsComplete => phase == Phase.Complete;
    public bool IsAnswering => phase == Phase.Reading || phase == Phase.Recheck;
    /// <summary>The message waiting to be opened, or null.</summary>
    public ScamMessage PendingMessage => phase == Phase.Notified ? Current : null;
    /// <summary>The message currently open (reading or feedback), or null.</summary>
    public ScamMessage OpenMessage => (phase == Phase.Reading || phase == Phase.Feedback || phase == Phase.Recheck) ? Current : null;
    public IMessagePresenter OpenPresenter => current;
    public ScoringSystem Scoring => scoring;
    public LevelDefinition Level => level;

    private LevelDefinition level;
    private Phase phase = Phase.Idle;
    private int index;
    private float waitTimer, waitTarget, walked, reminderTimer, responseTime;
    private int shownTenths = -1, shownPoints = -1;
    private Vector3 lastPlayerPos;
    private MessageDevice[] devices = new MessageDevice[0];
    private bool hasComputers, hasCaseTerminal;
    private IMessagePresenter current;

    private int Total => level != null ? level.MessageCount : 0;
    private ScamMessage Current => level.messages[index];
    private MessageInvestigation investigation;
    private WrongAnswerConsequences consequences;
    private float recheckTime;

    void Awake()
    {
        if (scoring == null) scoring = GetComponent<ScoringSystem>();
        if (popup == null) popup = GetComponent<MessagePopupUI>();
        if (hud == null) hud = GetComponent<LevelHUD>();
        if (levelAudio == null) levelAudio = GetComponent<LevelAudio>();
        // Clickable details + Evidence Notebook for messages that have investigation data.
        investigation = GetComponent<MessageInvestigation>();
        if (investigation == null) investigation = gameObject.AddComponent<MessageInvestigation>();
        // Wrong-answer consequences and the follow-up re-check (needs the scoring system on this object).
        consequences = GetComponent<WrongAnswerConsequences>();
        if (consequences == null && scoring != null) consequences = gameObject.AddComponent<WrongAnswerConsequences>();
    }

    void OnEnable()
    {
        if (hud != null) hud.NotificationClicked += OnNotificationClicked;
    }

    void OnDisable()
    {
        if (hud != null) hud.NotificationClicked -= OnNotificationClicked;
        Unsubscribe();
        InteractionGate.Release(this);
    }

    // ---------- Lifecycle ----------

    /// <summary>Starts (or restarts) the sequence from the first message with a clean score.</summary>
    public void Begin(LevelDefinition definition)
    {
        level = definition;
        index = 0;
        phase = Phase.Idle;
        if (scoring != null) scoring.ResetRun();
        if (popup != null) popup.Hide();
        if (hud != null) hud.HideNotification();
        if (consequences != null) consequences.Close();

        if (player == null)
        {
            GameObject tagged = null;
            try { tagged = GameObject.FindWithTag("Player"); } catch { /* tag may not exist */ }
            if (tagged != null) player = tagged.transform;
        }
        if (playerCamera == null) playerCamera = Camera.main;
        devices = FindObjectsByType<MessageDevice>(FindObjectsSortMode.None);
        hasComputers = FindObjectsByType<ComputerTerminal>(FindObjectsSortMode.None).Length > 0;
        hasCaseTerminal = FindObjectsByType<FinalCaseTerminal>(FindObjectsSortMode.None).Length > 0;

        if (level == null || scoring == null || popup == null)
        {
            Debug.LogError("QuizManager: Level, ScoringSystem or MessagePopupUI is missing - cannot run the level.", this);
            return;
        }
        if (Total == 0)
        {
            Finish();
            return;
        }
        Schedule(level.firstMessageDelay);
        StateChanged?.Invoke();   // lets computer screens fill their inbox right away
    }

    void Update()
    {
        if (level == null) return;

        switch (phase)
        {
            case Phase.Waiting:
                TickWaiting();
                UpdateLookPrompt();
                break;

            case Phase.Notified:
                TickNotified();
                break;

            case Phase.Reading:
                // Scaled time: the pause menu (timeScale 0) also pauses the response timer.
                // A presenter that is still opening (phone coming out of the pocket) holds the timer at 0.
                // Reading a newly inspected detail's finding is free for a few seconds (MessageInvestigation).
                if ((!(current is IPresenterTiming timing) || timing.TimerRunning) && !MessageInvestigation.PausesTimer)
                    responseTime += Time.deltaTime;
                // The timer shows tenths of a second: rebuild its text only when what it shows changes
                // (not every frame - each update allocates strings and re-meshes the text).
                int tenths = Mathf.FloorToInt(responseTime * 10f + 0.5f);
                int points = scoring.PointsFor(true, responseTime);
                if (tenths != shownTenths || points != shownPoints)
                {
                    shownTenths = tenths;
                    shownPoints = points;
                    current?.SetTimer(responseTime, points);
                }
                break;

            case Phase.Recheck:
                recheckTime += Time.deltaTime;   // not timed for points; only recorded with the recovery answer
                break;
        }
    }

    // ---------- Phases ----------

    void Schedule(float delay)
    {
        phase = Phase.Waiting;
        waitTimer = 0f;
        waitTarget = Mathf.Max(0f, delay);
        walked = 0f;
        if (player != null) lastPlayerPos = player.position;
    }

    void TickWaiting()
    {
        waitTimer += Time.deltaTime;

        if (player != null)
        {
            Vector3 delta = player.position - lastPlayerPos;
            delta.y = 0f;
            walked += delta.magnitude;
            lastPlayerPos = player.position;
        }

        bool activityReady = waitTimer >= level.minimumActivityDelay && ActivityMet(Current.trigger);
        if (waitTimer >= waitTarget || activityReady)
            Notify();
    }

    void Notify()
    {
        phase = Phase.Notified;
        reminderTimer = 0f;
        if (hud != null) hud.ShowNotification(Current, NeedsComputer(Current) ? "Read it at a computer"
                                                     : NeedsCaseTerminal(Current) ? "Submit at the final case terminal"
                                                     : $"Press [{interactKey}] to read");
        if (levelAudio != null) levelAudio.PlayNotification(Current.channel);
        StateChanged?.Invoke();
    }

    void TickNotified()
    {
        UpdateLookPrompt();

        if (reminderInterval > 0f)
        {
            reminderTimer += Time.deltaTime;
            if (reminderTimer >= reminderInterval)
            {
                reminderTimer = 0f;
                if (hud != null) hud.NudgeNotification();
                if (levelAudio != null) levelAudio.PlayNotification(Current.channel);
            }
        }

        var kb = Keyboard.current;
        if (kb != null && kb[interactKey].wasPressedThisFrame && !InteractionGate.InteractBlocked)
        {
            if (NeedsCaseTerminal(Current))
            {
                // The final case terminal opens it (once the evidence has been reviewed).
                if (LookedAt<FinalCaseTerminal>() == null && hud != null)
                    hud.ShowToast("Final case: review the evidence, then go to the final case terminal and press E.");
                return;
            }
            if (NeedsComputer(Current))
            {
                // Looking at a monitor? Let the ComputerTerminal handle this press.
                if (!LookingAtComputer() && hud != null)
                    hud.ShowToast(Current.channel == MessageChannel.Website
                        ? "Web pages are reviewed on a computer - look at a monitor and press E."
                        : "Emails are read on a computer - look at a monitor and press E.");
                return;
            }
            InteractionGate.ConsumeInteract();   // doors/chairs ignore this press
            OpenPending(popup);
        }
    }

    bool NeedsComputer(ScamMessage msg) => MessageChannels.OnComputer(msg.channel) && hasComputers;
    bool NeedsCaseTerminal(ScamMessage msg) => msg.channel == MessageChannel.Case && hasCaseTerminal;
    /// <summary>Opened on a station (computer or final case terminal) rather than the phone pop-up.</summary>
    bool NeedsStation(ScamMessage msg) => NeedsComputer(msg) || NeedsCaseTerminal(msg);

    void OnNotificationClicked()
    {
        if (phase == Phase.Notified && !NeedsStation(Current)) OpenPending(popup);
    }

    /// <summary>
    /// Opens the waiting message on the given presenter. The response timer starts now, once.
    /// Returns false if nothing is waiting.
    /// </summary>
    public bool OpenPending(IMessagePresenter presenter)
    {
        if (phase != Phase.Notified || presenter == null) return false;

        phase = Phase.Reading;
        responseTime = 0f;
        shownTenths = shownPoints = -1;
        current = presenter;
        current.AnswerChosen += OnAnswer;
        current.ContinueClicked += OnContinue;

        if (hud != null) hud.HideNotification();
        InteractionPrompt.Clear(this);
        InteractionGate.Acquire(this);
        if (investigation != null) investigation.Begin(Current, current as IInvestigationDock);   // before ShowMessage, so its details bind as clickable
        current.ShowMessage(Current);
        StateChanged?.Invoke();
        return true;
    }

    void OnAnswer(MessageVerdict chosen)
    {
        if (phase == Phase.Recheck) { OnRecheckAnswer(chosen); return; }
        if (phase != Phase.Reading) return;   // ignores late or repeated clicks
        phase = Phase.Feedback;

        if (!scoring.TryRecord(Current, chosen, responseTime, out var result))
        {
            // Only possible if two messages share an id: skip it rather than score twice or get stuck.
            Debug.LogError($"QuizManager: message id '{Current.id}' was already scored - check for duplicate ids.", this);
            OnContinue();
            return;
        }

        if (investigation != null) investigation.Submit(Current, chosen);   // evaluates the selected evidence once
        RecordEvidence();
        int max = scoring.MaxScoreFor(Total);
        if (levelAudio != null) levelAudio.PlayAnswer(result.isCorrect);
        current.ShowFeedback(result, scoring.TotalScore, max, index + 1, Total);
        if (hud != null) hud.SetProgress(index + 1, Total, scoring.TotalScore, max);
        if (consequences != null)
        {
            if (result.isCorrect) consequences.UpdateHudStatus();   // a correct answer never triggers a failure reaction
            else consequences.ShowMistake(result, current, Evaluation(),
                                          WrongAnswerConsequences.OffersFollowUp(Current) && scoring.CanRecover(Current.id),
                                          BeginRecheck, OnContinue);
        }
        StateChanged?.Invoke();
    }

    InvestigationSession.Result Evaluation() =>
        investigation != null && investigation.Session != null && investigation.Session.Message == Current ? investigation.Session.Evaluation : null;

    /// <summary>Key clues found in this message, for the case evaluation (best count per message, never doubled).</summary>
    void RecordEvidence()
    {
        var r = Evaluation();
        if (r != null) scoring.RecordEvidence(Current.id, r.selectedCorrectly.Count + r.foundNotSelected.Count);
    }

    // ---------- Follow-up re-check (after a wrong answer) ----------

    /// <summary>REVIEW EVIDENCE: the same message opens again with a fresh investigation; the verdict waits for the checklist.</summary>
    void BeginRecheck()
    {
        if (phase != Phase.Feedback || current == null || !scoring.CanRecover(Current.id)) { OnContinue(); return; }
        phase = Phase.Recheck;
        recheckTime = 0f;
        if (investigation != null) investigation.Begin(Current, current as IInvestigationDock);   // before ShowMessage (clickable details)
        current.ShowMessage(Current);
        if (consequences != null) consequences.BeginRecheck(Current, current, investigation, SkipRecheck);
        StateChanged?.Invoke();
    }

    void OnRecheckAnswer(MessageVerdict chosen)
    {
        if (consequences != null && !consequences.RecheckReady)
        {
            // Buttons are locked until the checklist is done; if a click still gets through, keep them locked.
            (current as IRecheckPresenter)?.SetRecheck(false, "Finish the follow-up checklist first");
            return;
        }
        phase = Phase.Feedback;
        bool hint = scoring.HintUsedFor(Current.id);
        if (!scoring.TryRecordRecovery(Current, chosen, recheckTime, hint, out var result))
        {
            OnContinue();   // no recovery attempt left (cannot normally happen)
            return;
        }
        if (investigation != null) investigation.Submit(Current, chosen);
        RecordEvidence();
        int max = scoring.MaxScoreFor(Total);
        if (levelAudio != null) levelAudio.PlayAnswer(result.isCorrect);
        current.ShowFeedback(result, scoring.TotalScore, max, index + 1, Total);
        if (hud != null) hud.SetProgress(index + 1, Total, scoring.TotalScore, max);
        if (consequences != null) consequences.ShowRecheckResult(result, Evaluation(), current, OnContinue);
        StateChanged?.Invoke();
    }

    /// <summary>SKIP: the recovery attempt is used up and the investigation moves on.</summary>
    void SkipRecheck()
    {
        if (phase != Phase.Recheck) return;
        scoring.ForfeitRecovery(Current.id);
        phase = Phase.Feedback;
        OnContinue();
    }

    void OnContinue()
    {
        if (phase != Phase.Feedback) return;
        if (consequences != null) consequences.Close();

        var finished = current;
        Unsubscribe();
        finished?.Hide();
        if (investigation != null) investigation.End();
        InteractionGate.Release(this);
        index++;

        if (index >= Total) Finish();
        else Schedule(UnityEngine.Random.Range(level.delayBetweenMessages.x, level.delayBetweenMessages.y));
        StateChanged?.Invoke();
    }

    void Unsubscribe()
    {
        if (current == null) return;
        current.AnswerChosen -= OnAnswer;
        current.ContinueClicked -= OnContinue;
        current = null;
    }

    void Finish()
    {
        phase = Phase.Complete;
        InteractionPrompt.Clear(this);
        if (hud != null) hud.HideNotification();
        Completed?.Invoke();
    }

    // ---------- Activity & devices ----------

    bool ActivityMet(MessageTrigger trigger)
    {
        switch (trigger)
        {
            case MessageTrigger.PlayerWalked: return walked >= walkDistanceTrigger;
            case MessageTrigger.NearComputer: return NearDevice(MessageChannel.Email);
            case MessageTrigger.NearPhone:    return NearDevice(MessageChannel.SMS);
            default:                          return false;
        }
    }

    bool NearDevice(MessageChannel channel)
    {
        if (player == null) return false;
        foreach (var d in devices)
        {
            if (d == null || d.channel != channel) continue;
            Vector3 a = player.position; a.y = 0f;
            Vector3 b = d.transform.position; b.y = 0f;
            if (Mathf.Abs(player.position.y - d.transform.position.y) < 2.5f && Vector3.Distance(a, b) <= nearDeviceRadius)
                return true;
        }
        return false;
    }

    /// <summary>Prompt for simple devices (the phone). Computers show their own prompt.</summary>
    void UpdateLookPrompt()
    {
        MessageDevice device = LookedAt<MessageDevice>();
        if (device == null || device.GetComponentInParent<ComputerTerminal>() != null)
        {
            InteractionPrompt.Clear(this);
            return;
        }

        if (phase == Phase.Notified && !NeedsStation(Current))
            InteractionPrompt.Set(this, InteractionPrompt.KeyName(interactKey),
                Current.channel == MessageChannel.Chat ? "Read new chat message" : Current.channel == MessageChannel.SMS ? "Read new text message" : "Read new email", priority: 3);
        else
            InteractionPrompt.Set(this, "", device.idleLabel, priority: 1);
    }

    bool LookingAtComputer() => LookedAt<ComputerTerminal>() != null;

    T LookedAt<T>() where T : Component
    {
        if (playerCamera == null) return null;
        // First thing in the way decides (player's own collider skipped); shared, allocation-free ray - see LookRaycast.
        return LookRaycast.TryGetFirstHit(playerCamera, player, lookDistance, out RaycastHit hit)
            ? hit.collider.GetComponentInParent<T>()
            : null;
    }
}
