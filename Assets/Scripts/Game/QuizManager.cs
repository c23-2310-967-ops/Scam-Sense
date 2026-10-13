using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the message sequence for one level:
///   wait (time or player activity) -> notification -> player opens it -> response timer runs while reading
///   -> answer is scored once -> feedback -> next message.
/// Text messages open on the phone pop-up (E anywhere). Emails are read on a computer (ComputerTerminal)
/// when the scene has one; otherwise they also use the pop-up. Scoring is always done here, once.
/// Runs early in the frame so that when E opens a message, doors and chairs ignore that press.
/// </summary>
[DefaultExecutionOrder(-100)]
public class QuizManager : MonoBehaviour
{
    private enum Phase { Idle, Waiting, Notified, Reading, Feedback, Complete }

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
    public bool IsAnswering => phase == Phase.Reading;
    /// <summary>The message waiting to be opened, or null.</summary>
    public ScamMessage PendingMessage => phase == Phase.Notified ? Current : null;
    /// <summary>The message currently open (reading or feedback), or null.</summary>
    public ScamMessage OpenMessage => (phase == Phase.Reading || phase == Phase.Feedback) ? Current : null;
    public IMessagePresenter OpenPresenter => current;
    public ScoringSystem Scoring => scoring;
    public LevelDefinition Level => level;

    private LevelDefinition level;
    private Phase phase = Phase.Idle;
    private int index;
    private float waitTimer, waitTarget, walked, reminderTimer, responseTime;
    private Vector3 lastPlayerPos;
    private MessageDevice[] devices = new MessageDevice[0];
    private bool hasComputers;
    private IMessagePresenter current;

    private int Total => level != null ? level.MessageCount : 0;
    private ScamMessage Current => level.messages[index];

    void Awake()
    {
        if (scoring == null) scoring = GetComponent<ScoringSystem>();
        if (popup == null) popup = GetComponent<MessagePopupUI>();
        if (hud == null) hud = GetComponent<LevelHUD>();
        if (levelAudio == null) levelAudio = GetComponent<LevelAudio>();
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

        if (player == null)
        {
            GameObject tagged = null;
            try { tagged = GameObject.FindWithTag("Player"); } catch { /* tag may not exist */ }
            if (tagged != null) player = tagged.transform;
        }
        if (playerCamera == null) playerCamera = Camera.main;
        devices = FindObjectsByType<MessageDevice>(FindObjectsSortMode.None);
        hasComputers = FindObjectsByType<ComputerTerminal>(FindObjectsSortMode.None).Length > 0;

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
                if (!(current is IPresenterTiming timing) || timing.TimerRunning)
                    responseTime += Time.deltaTime;
                current?.SetTimer(responseTime, scoring.PointsFor(true, responseTime));
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
        if (hud != null) hud.ShowNotification(Current, NeedsComputer(Current) ? "Read it at a computer" : $"Press [{interactKey}] to read");
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
            if (NeedsComputer(Current))
            {
                // Looking at a monitor? Let the ComputerTerminal handle this press.
                if (!LookingAtComputer() && hud != null)
                    hud.ShowToast("Emails are read on a computer - look at a monitor and press E.");
                return;
            }
            InteractionGate.ConsumeInteract();   // doors/chairs ignore this press
            OpenPending(popup);
        }
    }

    bool NeedsComputer(ScamMessage msg) => msg.channel == MessageChannel.Email && hasComputers;

    void OnNotificationClicked()
    {
        if (phase == Phase.Notified && !NeedsComputer(Current)) OpenPending(popup);
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
        current = presenter;
        current.AnswerChosen += OnAnswer;
        current.ContinueClicked += OnContinue;

        if (hud != null) hud.HideNotification();
        InteractionPrompt.Clear(this);
        InteractionGate.Acquire(this);
        current.ShowMessage(Current);
        StateChanged?.Invoke();
        return true;
    }

    void OnAnswer(MessageVerdict chosen)
    {
        if (phase != Phase.Reading) return;   // ignores late or repeated clicks
        phase = Phase.Feedback;

        if (!scoring.TryRecord(Current, chosen, responseTime, out var result))
        {
            // Only possible if two messages share an id: skip it rather than score twice or get stuck.
            Debug.LogError($"QuizManager: message id '{Current.id}' was already scored - check for duplicate ids.", this);
            OnContinue();
            return;
        }

        int max = scoring.MaxScoreFor(Total);
        if (levelAudio != null) levelAudio.PlayAnswer(result.isCorrect);
        current.ShowFeedback(result, scoring.TotalScore, max, index + 1, Total);
        if (hud != null) hud.SetProgress(index + 1, Total, scoring.TotalScore, max);
        StateChanged?.Invoke();
    }

    void OnContinue()
    {
        if (phase != Phase.Feedback) return;

        var finished = current;
        Unsubscribe();
        finished?.Hide();
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

        if (phase == Phase.Notified && !NeedsComputer(Current))
            InteractionPrompt.Set(this, interactKey.ToString(),
                Current.channel == MessageChannel.SMS ? "Read new text message" : "Read new email", priority: 3);
        else
            InteractionPrompt.Set(this, "", device.idleLabel, priority: 1);
    }

    bool LookingAtComputer() => LookedAt<ComputerTerminal>() != null;

    T LookedAt<T>() where T : Component
    {
        if (playerCamera == null) return null;
        Transform cam = playerCamera.transform;
        var hits = Physics.RaycastAll(cam.position, cam.forward, lookDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (player != null && hit.transform.root == player.root) continue;   // skip the player's own collider
            return hit.collider.GetComponentInParent<T>();                     // first thing in the way decides
        }
        return null;
    }
}
