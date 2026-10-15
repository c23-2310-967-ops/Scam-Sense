using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The final case review terminal (Level 4). Its screen always shows what is still needed:
///   - every earlier message classified (QuizManager only delivers the final case after them), and
///   - each required evidence record inspected at an evidence station (EvidenceTracker).
/// When both are done, E opens the case: the player reviews the summary and findings, picks SCAM or
/// LEGITIMATE and presses SUBMIT FINDINGS. It is an IMessagePresenter, so QuizManager still owns the
/// response timer and scores the answer once, exactly like the computer and the phone.
///
/// The screen UI is a World Space canvas built in the scene (Tools > Scam Sense > Level 4 > ...);
/// buttons call the public methods below as persistent onClick events.
/// </summary>
[DefaultExecutionOrder(-90)]
public class FinalCaseTerminal : MonoBehaviour, IMessagePresenter
{
    [Header("Screen (scene objects)")]
    [SerializeField] private Canvas screenCanvas;
    [SerializeField] private Transform viewPoint;
    [SerializeField] private GameObject lockedView, caseView, feedbackView;

    [Header("Locked view")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text checklistText;

    [Header("Case view")]
    [SerializeField] private TMP_Text caseTitle;
    [SerializeField] private TMP_Text caseBody;
    [SerializeField] private TMP_Text findingsText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text selectionText;
    [SerializeField] private Button scamButton, legitButton, submitButton;

    [Header("Feedback view")]
    [SerializeField] private TMP_Text verdictText;
    [SerializeField] private TMP_Text explanationText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Button finishButton;

    [SerializeField] private Button exitButton;

    [Header("Prerequisites")]
    [Tooltip("Evidence record titles (as shown on the evidence stations) that must be inspected before the case can be submitted.")]
    [SerializeField] private string[] requiredEvidence = new string[0];

    [Header("Interaction")]
    [SerializeField] private string displayName = "Final case review";
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Key useKey = Key.E;
    [SerializeField] private float cameraMoveTime = 0.45f;

    [Header("References (auto-found if empty)")]
    [SerializeField] private QuizManager quiz;
    [SerializeField] private EvidenceTracker evidence;

    public event Action<MessageVerdict> AnswerChosen;
    public event Action ContinueClicked;

    public bool InUse { get; private set; }

    Camera cam;
    Transform player;
    FloatingHand hands;
    LevelHUD hud;
    GraphicRaycaster raycaster;
    Vector3 camLocalPos;
    Quaternion camLocalRot;
    float camFov;
    Coroutine camRoutine;
    bool camAtScreen;

    ScamMessage shown;
    bool answering;                 // case open, not submitted yet
    bool submitted;                 // SUBMIT FINDINGS pressed for this case (blocks repeats)
    bool hasSelection;
    MessageVerdict selected;
    GameManager gameManager;

    // ---------- Unity ----------

    void Awake()
    {
        if (quiz == null) quiz = FindFirstObjectByType<QuizManager>();
        if (evidence == null) evidence = FindFirstObjectByType<EvidenceTracker>();
        if (screenCanvas == null || viewPoint == null || lockedView == null || caseView == null || feedbackView == null)
        {
            Debug.LogError($"{name}: FinalCaseTerminal is missing screen references (rebuild with Tools > Scam Sense > Level 4).", this);
            enabled = false;
            return;
        }
        raycaster = screenCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;
        ShowView(lockedView);
    }

    void OnEnable()
    {
        if (quiz != null) quiz.StateChanged += RefreshStatus;
        if (evidence != null) evidence.Changed += RefreshStatus;
    }

    void OnDisable()
    {
        if (quiz != null) quiz.StateChanged -= RefreshStatus;
        if (evidence != null) evidence.Changed -= RefreshStatus;
        InteractionPrompt.Clear(this);
        InteractionGate.RemoveEscapeHandler(this);
        InteractionGate.Release(this);
    }

    void Start()
    {
        cam = Camera.main;
        GameObject p = null;
        try { p = GameObject.FindWithTag("Player"); } catch { /* tag may not exist */ }
        player = p != null ? p.transform : (cam != null ? cam.transform.root : null);
        hands = cam != null ? cam.GetComponent<FloatingHand>() : null;
        screenCanvas.worldCamera = cam;
        RefreshStatus();
    }

    void Update()
    {
        if (InUse || cam == null) return;
        bool focused = !InteractionGate.UIOpen && !PauseMenu.IsPaused &&
                       LookRaycast.TryGetFirstHit(cam, player, interactDistance, out RaycastHit hit) && hit.transform.IsChildOf(transform);
        if (!focused)
        {
            InteractionPrompt.Clear(this);
            return;
        }

        if (Ready)
        {
            InteractionPrompt.Set(this, InteractionPrompt.KeyName(useKey), "Open the final case", priority: 4);
            var kb = Keyboard.current;
            if (kb != null && kb[useKey].wasPressedThisFrame && !InteractionGate.InteractBlocked)
            {
                InteractionGate.ConsumeInteract();
                Enter();
            }
        }
        else InteractionPrompt.Set(this, "", CaseClosed ? "Final case submitted" : displayName + "  -  locked (see the screen)", priority: 1);
    }

    void LateUpdate()
    {
        if (InUse && camAtScreen && cam != null)
            cam.transform.SetPositionAndRotation(viewPoint.position, viewPoint.rotation);
    }

    // ---------- Prerequisites ----------

    ScamMessage PendingCase => quiz != null && quiz.PendingMessage != null && quiz.PendingMessage.channel == MessageChannel.Case ? quiz.PendingMessage : null;
    bool CaseClosed => quiz != null && quiz.IsComplete;

    int EvidenceDone()
    {
        int n = 0;
        foreach (var e in requiredEvidence) if (evidence != null && evidence.IsInspected(e)) n++;
        return n;
    }

    bool EvidenceComplete => EvidenceDone() >= requiredEvidence.Length;

    /// <summary>The final case is waiting (all earlier messages answered) and the required evidence was inspected.</summary>
    public bool Ready => PendingCase != null && EvidenceComplete;

    void RefreshStatus()
    {
        if (checklistText == null || quiz == null) return;
        // QuizManager only gets its level at BEGIN; during the briefing read it from the GameManager.
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        var level = quiz.Level != null ? quiz.Level : (gameManager != null ? gameManager.Level : null);
        if (level == null) return;
        int earlier = Mathf.Max(0, level.MessageCount - 1);
        int answered = quiz.Scoring != null && quiz.Scoring.Results != null ? Mathf.Min(quiz.Scoring.Results.Count, earlier) : 0;

        var sb = new StringBuilder();
        sb.AppendLine(Line(answered >= earlier, $"Classify the reported communications  ({answered}/{earlier})"));
        foreach (var e in requiredEvidence)
            sb.AppendLine(Line(evidence != null && evidence.IsInspected(e), "Inspect evidence: " + e));
        checklistText.text = sb.ToString().TrimEnd();

        if (statusText != null)
        {
            statusText.text = CaseClosed ? $"<color={UIKit.HexSafe}>CASE CLOSED · FINDINGS SUBMITTED</color>"
                            : Ready ? $"<color={UIKit.HexSafe}>READY · PRESS E TO OPEN THE FINAL CASE</color>"
                            : PendingCase != null ? $"<color={UIKit.HexWarning}>LOCKED · INSPECT THE REMAINING EVIDENCE</color>"
                            : $"<color={UIKit.HexWarning}>LOCKED · COMPLETE THE CHECKLIST</color>";
        }
    }

    static string Line(bool done, string text) =>
        done ? $"<color={UIKit.HexSafe}>✓ DONE</color>   {text}" : $"<color={UIKit.HexWarning}>• TO DO</color>   <color={UIKit.HexSecondary}>{text}</color>";

    // ---------- Enter / exit ----------

    public void Enter()
    {
        if (InUse || cam == null || !Ready) return;
        InUse = true;
        InteractionPrompt.Clear(this);
        InteractionGate.Acquire(this);
        InteractionGate.PushEscapeHandler(this, OnEscape);

        camLocalPos = cam.transform.localPosition;
        camLocalRot = cam.transform.localRotation;
        camFov = cam.fieldOfView;
        if (hands != null) hands.SetHandsVisible(false);
        if (hud == null) hud = FindFirstObjectByType<LevelHUD>();
        if (hud != null) hud.SetVisible(false);
        if (raycaster != null) raycaster.enabled = true;

        quiz.OpenPending(this);   // QuizManager calls ShowMessage and starts the response timer
        if (camRoutine != null) StopCoroutine(camRoutine);
        camRoutine = StartCoroutine(MoveCamera(true));
    }

    /// <summary>EXIT / Esc: only after the findings were submitted (the case must be answered once opened).</summary>
    public void Exit()
    {
        if (!InUse || !camAtScreen || answering) return;
        if (quiz != null && quiz.OpenPresenter == (IMessagePresenter)this)
            ContinueClicked?.Invoke();   // leaving the feedback counts as "finish case"
        if (raycaster != null) raycaster.enabled = false;
        InteractionGate.RemoveEscapeHandler(this);
        if (camRoutine != null) StopCoroutine(camRoutine);
        camRoutine = StartCoroutine(MoveCamera(false));
    }

    bool OnEscape()
    {
        if (!InUse || PauseMenu.IsPaused || answering) return false;   // answering: Esc opens the pause menu instead
        Exit();
        return true;
    }

    IEnumerator MoveCamera(bool toScreen)
    {
        camAtScreen = false;
        Transform t = cam.transform;
        Vector3 p0 = t.position;
        Quaternion r0 = t.rotation;
        float f0 = cam.fieldOfView;
        float targetFov = toScreen ? 60f : camFov;
        for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / Mathf.Max(0.01f, cameraMoveTime))
        {
            float s = Mathf.SmoothStep(0f, 1f, k);
            Vector3 pEnd = toScreen ? viewPoint.position : t.parent.TransformPoint(camLocalPos);
            Quaternion rEnd = toScreen ? viewPoint.rotation : t.parent.rotation * camLocalRot;
            t.SetPositionAndRotation(Vector3.Lerp(p0, pEnd, s), Quaternion.Slerp(r0, rEnd, s));
            cam.fieldOfView = Mathf.Lerp(f0, targetFov, s);
            yield return null;
        }
        if (toScreen)
        {
            camAtScreen = true;
            t.SetPositionAndRotation(viewPoint.position, viewPoint.rotation);
            cam.fieldOfView = targetFov;
        }
        else
        {
            t.localPosition = camLocalPos;
            t.localRotation = camLocalRot;
            cam.fieldOfView = camFov;
            if (hands != null) hands.SetHandsVisible(true);
            if (hud != null) hud.SetVisible(true);
            InUse = false;
            InteractionGate.Release(this);
            RefreshStatus();
        }
        camRoutine = null;
    }

    // ---------- Buttons (persistent onClick targets) ----------

    public void SelectScam() => Select(MessageVerdict.Scam);
    public void SelectLegitimate() => Select(MessageVerdict.Legitimate);

    void Select(MessageVerdict v)
    {
        if (!answering || submitted) return;
        hasSelection = true;
        selected = v;
        selectionText.text = $"Your classification: <b>{UIKit.VerdictWord(v)}</b>   ·   press SUBMIT FINDINGS to confirm";
        UIKit.SetLabel(scamButton, v == MessageVerdict.Scam ? "›  SCAM" : "×  SCAM");
        UIKit.SetLabel(legitButton, v == MessageVerdict.Legitimate ? "›  LEGITIMATE" : "✓  LEGITIMATE");
        UIKit.SetInteractable(submitButton, true);
    }

    public void SubmitFindings()
    {
        if (!answering || submitted || !hasSelection) return;
        submitted = true;                                   // one submission per case
        UIKit.SetInteractable(submitButton, false);
        UIKit.SetInteractable(scamButton, false);
        UIKit.SetInteractable(legitButton, false);
        AnswerChosen?.Invoke(selected);
    }

    public void FinishCase()
    {
        if (!finishButton.interactable) return;
        UIKit.SetInteractable(finishButton, false);
        Exit();
    }

    // ---------- IMessagePresenter (driven by QuizManager) ----------

    public void ShowMessage(ScamMessage msg)
    {
        shown = msg;
        answering = true;
        submitted = hasSelection = false;
        ShowView(caseView);

        caseTitle.text = string.IsNullOrEmpty(msg.subject) ? msg.sender : msg.subject;
        caseBody.text = msg.body;
        var sb = new StringBuilder();
        sb.AppendLine($"<color={UIKit.HexAccent}><b>EVIDENCE YOU REVIEWED</b></color>");
        foreach (var e in requiredEvidence) sb.AppendLine("•  " + e);
        if (msg.clues != null && msg.clues.Length > 0)
        {
            sb.AppendLine($"<color={UIKit.HexAccent}><b>KEY FACTS FROM THE RECORDS</b></color>");
            foreach (var c in msg.clues) if (!string.IsNullOrEmpty(c)) sb.AppendLine("•  " + c);
        }
        findingsText.text = sb.ToString().TrimEnd();
        selectionText.text = "Choose SCAM or LEGITIMATE, then SUBMIT FINDINGS.";
        UIKit.SetLabel(scamButton, "×  SCAM");
        UIKit.SetLabel(legitButton, "✓  LEGITIMATE");
        UIKit.SetInteractable(scamButton, true);
        UIKit.SetInteractable(legitButton, true);
        UIKit.SetInteractable(submitButton, false);
        UIKit.SetInteractable(exitButton, false);
        SetTimer(0f, quiz != null ? quiz.Scoring.PointsFor(true, 0f) : 0);
    }

    public void SetTimer(float seconds, int pointsIfCorrect)
    {
        if (timerText != null)
            timerText.text = $"Response time  <b>{UIKit.FormatSeconds(seconds)}</b>   ·   correct now: <color={UIKit.HexAccent}><b>{pointsIfCorrect} pts</b></color>";
    }

    public void ShowFeedback(ScoringSystem.AnswerResult r, int score, int maxScore, int reviewed, int total)
    {
        answering = false;
        ShowView(feedbackView);
        string word = UIKit.VerdictWord(r.message.correctVerdict);
        string hex = r.message.correctVerdict == MessageVerdict.Scam ? UIKit.HexDanger : UIKit.HexSafe;
        verdictText.text = r.isCorrect
            ? $"<color={UIKit.HexSafe}>✓  CORRECT</color>   <size=70%>The campaign is <color={hex}><b>{word}</b></color>.</size>"
            : $"<color={UIKit.HexWarning}>×  INCORRECT</color>   <size=70%>You submitted {UIKit.VerdictWord(r.chosen)}. The campaign is <color={hex}><b>{word}</b></color>.</size>";
        explanationText.text = r.message.explanation;
        scoreText.text = $"<color={UIKit.HexAccent}><b>+{r.points} points</b></color>   ·   answered in {UIKit.FormatSeconds(r.seconds)}   ·   " +
                         $"Total score {score} / {maxScore}   ·   Messages reviewed {reviewed}/{total}";
        UIKit.SetInteractable(finishButton, true);
        UIKit.SetInteractable(exitButton, true);
    }

    public void Hide()
    {
        answering = false;
        shown = null;
        ShowView(lockedView);
        RefreshStatus();
    }

    void ShowView(GameObject view)
    {
        lockedView.SetActive(view == lockedView);
        caseView.SetActive(view == caseView);
        feedbackView.SetActive(view == feedbackView);
        if (exitButton != null) exitButton.gameObject.SetActive(view != lockedView);
    }
}
