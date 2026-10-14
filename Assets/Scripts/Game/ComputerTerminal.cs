using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A usable computer. A World Space canvas is fitted onto the physical screen face and shows a small
/// desktop with a Mail app (inbox + reading pane). Look at the monitor and press E: the camera eases in
/// to face the screen, the cursor is freed, and you click emails directly on the monitor.
/// Emails, timing and scoring come from QuizManager - this only displays them (IMessagePresenter).
///
/// Setup: put this on an UNSCALED parent object that contains the monitor model and the screen surface
/// (a thin quad/cube covering the display area, with a collider). Assign Screen Surface; View Point is
/// created automatically in front of the screen if left empty.
/// </summary>
[DefaultExecutionOrder(-90)]
public class ComputerTerminal : MonoBehaviour, IMessagePresenter
{
    [Header("Screen")]
    [Tooltip("The monitor model (with Screen Submesh set) or a thin object covering the display area.")]
    [SerializeField] private Transform screenSurface;
    [Tooltip("Index of the monitor mesh's screen sub-mesh (computerScreen.fbx: 1). -1 = use the object's thin box instead.")]
    [SerializeField] private int screenSubmesh = -1;
    [Tooltip("Where the camera moves while using the computer. Auto-created if empty.")]
    [SerializeField] private Transform viewPoint;
    [Tooltip("Optional: which side the screen faces. Defaults to the side facing the View Point.")]
    [SerializeField] private Transform frontHint;
    [SerializeField] private float canvasWidth = 1280f;
    [SerializeField] private float surfaceOffset = 0.004f;
    [Tooltip("How much of the vertical view the screen fills when the View Point is auto-placed.")]
    [SerializeField, Range(0.5f, 0.95f)] private float viewFill = 0.86f;

    [Header("Interaction")]
    [SerializeField] private string displayName = "Use computer";
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Key useKey = Key.E;
    [SerializeField] private float cameraMoveTime = 0.45f;

    [Header("References (auto-found if empty)")]
    [SerializeField] private QuizManager quiz;

    public event Action<MessageVerdict> AnswerChosen;
    public event Action ContinueClicked;

    public bool InUse { get; private set; }

    // Camera / player
    private Camera cam;
    private Transform player;
    private FloatingHand hands;
    private LevelHUD hud;
    private Vector3 camLocalPos;
    private Quaternion camLocalRot;
    private float camFov;
    private Coroutine camRoutine;
    private bool camAtScreen;

    // Screen UI
    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private RectTransform inboxList, pane, emptyPane, decisionBar, feedbackBox, reviewBox;
    private TMP_Text unreadText, clockText, toastText, paneSubject, paneFrom, paneMeta, paneBody, paneLink;
    private TMP_Text timerText, worthText, verdictText, pointsText, answerText, explanationText, scoreText;
    private TMP_Text reviewVerdict, reviewExplanation;
    private Button scamButton, legitButton, backButton, exitButton, detailsToggle;
    private TMP_Text paneDetails;
    private GameObject paneDivider;
    private bool showingDetails;
    private GameObject toast;

    private ScamMessage shown;         // message in the reading pane
    private int clockMinute = -1;
    private bool answering;

    // ---------- Unity ----------

    void Awake()
    {
        if (quiz == null) quiz = FindFirstObjectByType<QuizManager>();
        if (screenSurface == null)
        {
            Debug.LogError($"{name}: ComputerTerminal needs a Screen Surface.", this);
            enabled = false;
            return;
        }
        BuildScreen();
    }

    void OnEnable()
    {
        if (quiz != null) quiz.StateChanged += Refresh;
    }

    void OnDisable()
    {
        if (quiz != null) quiz.StateChanged -= Refresh;
        InteractionPrompt.Clear(this);
        InteractionGate.RemoveEscapeHandler(this);
        InteractionGate.Release(this);
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void Start()
    {
        cam = Camera.main;
        GameObject p = null;
        try { p = GameObject.FindWithTag("Player"); } catch { /* tag may not exist */ }
        player = p != null ? p.transform : (cam != null ? cam.transform.root : null);
        hands = cam != null ? cam.GetComponent<FloatingHand>() : null;
        if (canvas != null) canvas.worldCamera = cam;
        Refresh();
    }

    void Update()
    {
        // The clock shows minutes: format it only when the minute changes (not a new string every frame).
        var now = DateTime.Now;
        int minute = now.Hour * 60 + now.Minute;
        if (minute != clockMinute)
        {
            clockMinute = minute;
            clockText.text = now.ToString("h:mm tt");
        }
        if (InUse || cam == null) return;

        bool focused = !InteractionGate.UIOpen && !PauseMenu.IsPaused && IsLookedAt();
        if (!focused)
        {
            InteractionPrompt.Clear(this);
            return;
        }

        bool newMail = quiz != null && quiz.PendingMessage != null && quiz.PendingMessage.channel == MessageChannel.Email;
        string secondary = SittableChair.PlayerSeated ? "Space: stand up" : null;
        InteractionPrompt.Set(this, InteractionPrompt.KeyName(useKey), newMail ? "Read new email" : displayName, priority: 4, secondary: secondary);

        var kb = Keyboard.current;
        if (kb != null && kb[useKey].wasPressedThisFrame && !InteractionGate.InteractBlocked)
        {
            InteractionGate.ConsumeInteract();
            Enter();
        }
    }

    void LateUpdate()
    {
        // Hold the camera on the screen while in use (nothing else should move it, but be safe).
        if (InUse && camAtScreen && cam != null)
            cam.transform.SetPositionAndRotation(viewPoint.position, viewPoint.rotation);
    }

    bool IsLookedAt()
    {
        // First thing in the way decides (player's own collider skipped); shared, allocation-free ray - see LookRaycast.
        return LookRaycast.TryGetFirstHit(cam, player, interactDistance, out RaycastHit hit) && hit.transform.IsChildOf(transform);
    }

    // ---------- Enter / exit ----------

    public void Enter()
    {
        if (InUse || cam == null) return;
        InUse = true;
        InteractionPrompt.Clear(this);
        InteractionGate.Acquire(this);
        InteractionGate.PushEscapeHandler(this, OnEscape);

        camLocalPos = cam.transform.localPosition;
        camLocalRot = cam.transform.localRotation;
        camFov = cam.fieldOfView;
        if (hands != null) hands.SetHandsVisible(false);
        if (hud == null) hud = FindFirstObjectByType<LevelHUD>();
        if (hud != null) hud.SetVisible(false);   // nothing should cover the monitor

        raycaster.enabled = true;
        Refresh();
        if (camRoutine != null) StopCoroutine(camRoutine);
        camRoutine = StartCoroutine(MoveCamera(true));
    }

    public void Exit()
    {
        if (!InUse) return;
        if (answering) return;                       // finish the open email first (button is disabled too)
        if (quiz != null && quiz.OpenPresenter == (IMessagePresenter)this)
            ContinueClicked?.Invoke();               // closing during feedback counts as "back to inbox"

        raycaster.enabled = false;
        InteractionGate.RemoveEscapeHandler(this);
        if (camRoutine != null) StopCoroutine(camRoutine);
        camRoutine = StartCoroutine(MoveCamera(false));
    }

    bool OnEscape()
    {
        if (!InUse || PauseMenu.IsPaused || answering) return false;   // answering: let the pause menu open instead
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
            Refresh();
        }
        camRoutine = null;
    }

    // ---------- IMessagePresenter (driven by QuizManager) ----------

    public void ShowMessage(ScamMessage msg)
    {
        answering = true;
        shown = msg;
        ShowPane(msg);
        decisionBar.gameObject.SetActive(true);
        feedbackBox.gameObject.SetActive(false);
        reviewBox.gameObject.SetActive(false);
        UIKit.SetInteractable(scamButton, true);
        UIKit.SetInteractable(legitButton, true);
        UIKit.SetInteractable(exitButton, false);
        SetTimer(0f, quiz != null ? quiz.Scoring.PointsFor(true, 0f) : 0);
    }

    public void SetTimer(float seconds, int pointsIfCorrect)
    {
        timerText.text = $"Response time  <b>{UIKit.FormatSeconds(seconds)}</b>";
        worthText.text = $"Correct now: <color={UIKit.HexAccent}><b>{pointsIfCorrect} pts</b></color>";
    }

    public void ShowFeedback(ScoringSystem.AnswerResult r, int score, int maxScore, int reviewed, int total)
    {
        answering = false;
        SetDetailsView(false);   // the feedback needs the room; the toggle lives in the hidden decision bar
        decisionBar.gameObject.SetActive(false);
        feedbackBox.gameObject.SetActive(true);
        UIKit.SetInteractable(exitButton, true);

        string word = UIKit.VerdictWord(r.message.correctVerdict);
        string hex = r.message.correctVerdict == MessageVerdict.Scam ? UIKit.HexDanger : UIKit.HexSafe;
        if (r.isCorrect)
        {
            verdictText.text = $"<color={UIKit.HexSafe}>✓  CORRECT</color>";
            answerText.text = $"This email is <color={hex}><b>{word}</b></color>.";
        }
        else
        {
            verdictText.text = $"<color={UIKit.HexWarning}>×  NOT QUITE</color>";
            answerText.text = $"You chose {UIKit.VerdictWord(r.chosen)}. This email is <color={hex}><b>{word}</b></color>.";
        }
        pointsText.text = $"<color={UIKit.HexAccent}><b>+{r.points} points</b></color>   ·   answered in {UIKit.FormatSeconds(r.seconds)}";
        explanationText.text = r.message.explanation;
        scoreText.text = $"Score {score} / {maxScore}   ·   Messages reviewed {reviewed}/{total}";
        UIKit.SetLabel(backButton, reviewed >= total ? "Finish case" : "Back to inbox");
        UIKit.SetInteractable(backButton, true);
    }

    public void Hide()
    {
        answering = false;
        shown = null;
        ShowEmptyPane();
        Refresh();
    }

    void Choose(MessageVerdict v)
    {
        if (!answering || !scamButton.interactable) return;
        UIKit.SetInteractable(scamButton, false);   // one answer only
        UIKit.SetInteractable(legitButton, false);
        AnswerChosen?.Invoke(v);
    }

    void BackToInbox()
    {
        if (!backButton.interactable) return;
        UIKit.SetInteractable(backButton, false);
        ContinueClicked?.Invoke();
    }

    // ---------- Inbox ----------

    void Refresh()
    {
        if (canvas == null || quiz == null || quiz.Level == null) return;

        var level = quiz.Level;
        ScamMessage open = quiz.OpenMessage;
        ScamMessage pending = quiz.PendingMessage;
        int unread = 0;

        // Newest first: the waiting/open email, then answered ones (latest first), then older inbox mail.
        var list = new List<ScamMessage>();
        if (open != null && open.channel == MessageChannel.Email) list.Add(open);
        if (pending != null && pending.channel == MessageChannel.Email) { list.Add(pending); unread++; }
        for (int i = level.messages.Count - 1; i >= 0; i--)
        {
            var m = level.messages[i];
            if (m.channel == MessageChannel.Email && quiz.Scoring.HasAnswered(m.id) && m != open) list.Add(m);
        }
        // Rows are reused: only a row whose state changed (new -> open -> answered) is rebuilt. Rebuilding the
        // whole inbox on every message event created dozens of UI objects and text buffers at once (a hitch).
        usedRows.Clear();
        int sibling = 0;
        foreach (var m in list) PlaceRow(m, m == pending, m == open, false, sibling++);
        if (level.inboxExtras != null)
            foreach (var m in level.inboxExtras) PlaceRow(m, false, false, true, sibling++);
        staleRows.Clear();
        foreach (var kv in rowCache) if (!usedRows.Contains(kv.Key)) staleRows.Add(kv.Key);
        foreach (var m in staleRows) { DestroyRow(rowCache[m].go); rowCache.Remove(m); }

        unreadText.text = unread > 0 ? $"{unread} new" : "No new mail";
        toast.SetActive(!InUse && unread > 0);
        if (unread > 0) toastText.text = $"New email from {pending.sender}";
    }

    struct RowEntry { public GameObject go; public int state; }
    private readonly Dictionary<ScamMessage, RowEntry> rowCache = new Dictionary<ScamMessage, RowEntry>();
    private readonly HashSet<ScamMessage> usedRows = new HashSet<ScamMessage>();
    private readonly List<ScamMessage> staleRows = new List<ScamMessage>();

    /// <summary>Shows the row for a message at the given position, reusing it if its state hasn't changed.</summary>
    void PlaceRow(ScamMessage m, bool isNew, bool isOpen, bool isOld, int sibling)
    {
        int state = isOpen ? 1 : isNew ? 2 : isOld ? 3 : (FindResult(m).isCorrect ? 4 : 5);
        if (!rowCache.TryGetValue(m, out var entry) || entry.go == null || entry.state != state)
        {
            if (entry.go != null) DestroyRow(entry.go);
            entry = new RowEntry { go = MakeRow(m, isNew, isOpen, isOld), state = state };
            rowCache[m] = entry;
        }
        entry.go.transform.SetSiblingIndex(sibling);
        usedRows.Add(m);
    }

    static void DestroyRow(GameObject go)
    {
        go.SetActive(false);   // leaves the layout immediately; the object itself is destroyed at the end of the frame
        Destroy(go);
    }

    /// <summary>The inbox preview shows about 45 characters before "…"; passing the whole body only made the text bigger to process.</summary>
    static string PreviewText(string body) =>
        string.IsNullOrEmpty(body) || body.Length <= 100 ? body : body.Substring(0, 100);

    GameObject MakeRow(ScamMessage m, bool isNew, bool isOpen, bool isOld)
    {
        var row = UIKit.Rect("Row_" + m.id, inboxList);
        var bg = UIKit.AddImage(row, isOpen ? UIKit.PanelRaised : new Color(1, 1, 1, 0.03f), rounded: true);
        UIKit.Size(row, -1, 108);
        var btn = row.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.6f, 1.6f, 1.8f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => Select(m));

        if (isNew || isOpen)
        {
            var dot = UIKit.Rect("Unread", row);
            UIKit.Place(dot, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(12, 12));
            UIKit.AddImage(dot, UIKit.Accent, UIKit.CircleSprite).raycastTarget = false;
        }

        var sender = UIKit.Text(row, "Sender", m.sender, 24, UIKit.TextPrimary, bold: isNew || isOpen);
        sender.textWrappingMode = TextWrappingModes.NoWrap;
        sender.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Place(sender.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -12), new Vector2(330, 32));

        var subject = UIKit.Text(row, "Subject", m.subject, 21, isNew ? UIKit.TextPrimary : UIKit.TextSecondary, bold: isNew);
        subject.textWrappingMode = TextWrappingModes.NoWrap;
        subject.overflowMode = TextOverflowModes.Ellipsis;
        bool answered = !isNew && !isOpen && !isOld;   // leaves room for the "you were right" line
        UIKit.Place(subject.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -44), new Vector2(answered ? 270 : 440, 28));

        var preview = UIKit.Text(row, "Preview", PreviewText(m.body), 19, UIKit.TextSecondary);
        preview.textWrappingMode = TextWrappingModes.NoWrap;
        preview.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Place(preview.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -74), new Vector2(440, 26));

        // Status tag (always a word, never colour alone)
        string tag; Color tagBg, tagFg = UIKit.Background;
        if (isOpen) { tag = "OPEN"; tagBg = UIKit.Accent; }
        else if (isNew) { tag = "NEW"; tagBg = UIKit.Accent; }
        else if (isOld) { tag = "OLD"; tagBg = UIKit.Border; tagFg = UIKit.TextPrimary; }
        else
        {
            // Tag = the real verdict; the line under it = how the player did (kept separate so
            // "SCAM" can never be misread as "not scam").
            bool scam = m.correctVerdict == MessageVerdict.Scam;
            tag = scam ? "SCAM" : "LEGIT";
            tagBg = scam ? UIKit.DangerButton : UIKit.SafeButton;
            tagFg = Color.white;
            bool right = FindResult(m).isCorrect;
            var you = UIKit.Text(row, "YourAnswer", right ? $"<color={UIKit.HexSafe}>✓ you were right</color>"
                                                          : $"<color={UIKit.HexWarning}>× you missed it</color>",
                                 17, UIKit.TextSecondary, TextAlignmentOptions.TopRight, bold: true);
            you.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(you.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -48), new Vector2(170, 24));
        }
        var badge = UIKit.Badge(row, tag, tagBg, tagFg, 17);
        var brt = (RectTransform)badge.transform.parent;
        UIKit.Place(brt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -14), new Vector2(110, 30));
        brt.GetComponent<HorizontalLayoutGroup>().enabled = false;

        return row.gameObject;
    }

    void Select(ScamMessage m)
    {
        if (answering) return;                                   // the open email must be answered first
        if (quiz != null && quiz.PendingMessage == m)
        {
            quiz.OpenPending(this);                              // QuizManager calls ShowMessage and starts the timer
            return;
        }
        if (quiz != null && quiz.OpenPresenter == (IMessagePresenter)this) return;   // feedback showing

        // Read-only view of an answered or old email
        shown = m;
        ShowPane(m);
        decisionBar.gameObject.SetActive(false);
        feedbackBox.gameObject.SetActive(false);
        reviewBox.gameObject.SetActive(true);

        if (quiz.Level.inboxExtras != null && quiz.Level.inboxExtras.Contains(m))
        {
            reviewVerdict.text = $"<color={UIKit.HexSecondary}>Older email · not part of this case</color>";
            reviewExplanation.text = "";
        }
        else
        {
            var r = FindResult(m);
            string word = UIKit.VerdictWord(m.correctVerdict);
            string hex = m.correctVerdict == MessageVerdict.Scam ? UIKit.HexDanger : UIKit.HexSafe;
            reviewVerdict.text = (r.isCorrect ? $"<color={UIKit.HexSafe}>✓ You answered correctly</color>"
                                              : $"<color={UIKit.HexWarning}>× You answered {UIKit.VerdictWord(r.chosen)}</color>")
                                 + $"  ·  This email is <color={hex}><b>{word}</b></color>";
            reviewExplanation.text = m.explanation;
        }
    }

    ScoringSystem.AnswerResult FindResult(ScamMessage m)
    {
        if (quiz != null)
            foreach (var r in quiz.Scoring.Results)
                if (r.message != null && r.message.id == m.id) return r;
        return default;
    }

    void ShowPane(ScamMessage m)
    {
        emptyPane.gameObject.SetActive(false);
        pane.gameObject.SetActive(true);
        paneSubject.text = string.IsNullOrEmpty(m.subject) ? "(no subject)" : m.subject;
        // <noparse> shows the angle brackets literally (TMP does not decode &lt; / &gt;).
        paneFrom.text = $"From: <b>{m.sender}</b>" +
                        (string.IsNullOrEmpty(m.senderDetail) ? "" : $"  <color={UIKit.HexSecondary}><noparse><{m.senderDetail}></noparse></color>");
        paneMeta.text = "To: me   ·   Today";
        paneBody.text = m.body;
        bool hasLink = !string.IsNullOrEmpty(m.link);
        paneLink.gameObject.SetActive(hasLink);
        paneLink.text = hasLink ? $"Link in email: <u>{m.link}</u>" : "";

        string details = UIKit.MessageDetailsText(m, quiz != null && quiz.Level != null ? quiz.Level.caseNotes : null);
        paneDetails.text = details;
        detailsToggle.gameObject.SetActive(!string.IsNullOrEmpty(details));
        SetDetailsView(false);   // every email opens on the message itself
    }

    void SetDetailsView(bool details)
    {
        showingDetails = details;
        paneDetails.gameObject.SetActive(details);
        paneBody.gameObject.SetActive(!details);
        // The details already list the sender, so the header lines make room for them.
        paneFrom.gameObject.SetActive(!details);
        paneMeta.gameObject.SetActive(!details);
        if (paneDivider != null) paneDivider.SetActive(!details);
        paneLink.gameObject.SetActive(!details && shown != null && !string.IsNullOrEmpty(shown.link));
        UIKit.SetLabel(detailsToggle, details ? "MESSAGE" : "DETAILS");
    }

    void ShowEmptyPane()
    {
        pane.gameObject.SetActive(false);
        emptyPane.gameObject.SetActive(true);
        UIKit.SetInteractable(exitButton, true);
    }

    // ---------- Screen geometry & build ----------

    /// <summary>
    /// Finds the display rectangle in world space: from the monitor's screen sub-mesh (exact, follows any
    /// tilt) or from a thin box's mesh bounds. Returns the corner points and the face normal.
    /// </summary>
    void GetScreenPoints(List<Vector3> points, out Vector3 normal, out float thickness)
    {
        var mf = screenSurface.GetComponent<MeshFilter>();
        var mesh = mf != null ? mf.sharedMesh : null;
        thickness = 0f;

        if (mesh != null && screenSubmesh >= 0 && !mesh.isReadable)
            Debug.LogWarning($"{name}: enable Read/Write on '{mesh.name}' (model import settings) so the screen can be fitted exactly. Using the mesh bounds instead.", this);

        if (mesh != null && mesh.isReadable && screenSubmesh >= 0 && screenSubmesh < mesh.subMeshCount)
        {
            var v = mesh.vertices;
            var tris = mesh.GetTriangles(screenSubmesh);
            foreach (var i in tris) points.Add(screenSurface.TransformPoint(v[i]));
            normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized;
            return;
        }

        Bounds lb = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        Vector3[] axes = { screenSurface.right, screenSurface.up, screenSurface.forward };
        Vector3 ls = screenSurface.lossyScale;
        float[] ext = { Mathf.Abs(lb.size.x * ls.x), Mathf.Abs(lb.size.y * ls.y), Mathf.Abs(lb.size.z * ls.z) };
        int thin = 0;
        for (int i = 1; i < 3; i++) if (ext[i] < ext[thin]) thin = i;
        normal = axes[thin];
        thickness = ext[thin];
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    points.Add(screenSurface.TransformPoint(lb.center + Vector3.Scale(lb.extents, new Vector3(x, y, z))));
    }

    void BuildScreen()
    {
        var pts = new List<Vector3>();
        GetScreenPoints(pts, out Vector3 normal, out float thickness);
        Vector3 avg = Vector3.zero;
        foreach (var p in pts) avg += p;
        avg /= pts.Count;

        // Front = side facing the hint / view point / the nearest chair.
        Vector3 toFront = frontHint != null ? frontHint.position - avg :
                          viewPoint != null ? viewPoint.position - avg : GuessFront(avg);
        if (Vector3.Dot(normal, toFront) < 0f) normal = -normal;

        // In-plane axes: "up" follows the screen's tilt, "right" completes the frame.
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
        Vector3 right = Vector3.Cross(up, normal).normalized;
        float minR = float.MaxValue, maxR = float.MinValue, minU = float.MaxValue, maxU = float.MinValue;
        foreach (var p in pts)
        {
            float r = Vector3.Dot(p - avg, right), u = Vector3.Dot(p - avg, up);
            minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
            minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
        }
        float w = maxR - minR, h = maxU - minU;
        Vector3 centre = avg + right * (minR + maxR) * 0.5f + up * (minU + maxU) * 0.5f;

        // Canvas (scene root: monitors can have zero/odd scale on their thin axis)
        var go = new GameObject(name + " Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = gameObject.layer;
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 2f;
        raycaster = go.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        var rt = (RectTransform)go.transform;
        float canvasHeight = Mathf.Round(canvasWidth * h / Mathf.Max(0.01f, w));
        rt.sizeDelta = new Vector2(canvasWidth, canvasHeight);
        rt.position = centre + normal * (thickness * 0.5f + surfaceOffset);
        rt.rotation = Quaternion.LookRotation(-normal, up);   // canvas front faces the viewer
        rt.localScale = Vector3.one * (w / canvasWidth);

        if (viewPoint == null)
        {
            var vp = new GameObject("ViewPoint").transform;
            vp.SetParent(transform, false);
            float vfov = 60f * Mathf.Deg2Rad;
            float dist = (h * 0.5f) / Mathf.Tan(vfov * viewFill * 0.5f);
            dist = Mathf.Max(dist, (w * 0.5f) / Mathf.Tan(Mathf.Atan(Mathf.Tan(vfov * 0.5f) * 16f / 9f) * viewFill));
            vp.position = rt.position + normal * dist;
            vp.rotation = Quaternion.LookRotation(-normal, up);
            viewPoint = vp;
        }

        BuildDesktop(rt, canvasHeight);
    }

    Vector3 GuessFront(Vector3 centre)
    {
        float best = float.MaxValue;
        Vector3 dir = -transform.forward;
        foreach (var chair in FindObjectsByType<SittableChair>(FindObjectsSortMode.None))
        {
            if (chair.SeatPoint == null) continue;
            float d = Vector3.Distance(chair.SeatPoint.position, centre);
            if (d < best && d < 3f) { best = d; dir = chair.SeatPoint.position - centre; }
        }
        return dir;
    }

    /// <summary>
    /// Lets a long text take the space left in the reading pane and shrink its font to fit,
    /// instead of overlapping the answer buttons below it.
    /// </summary>
    static void FitToSpace(TMP_Text t, float minFont)
    {
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 60f;
        le.preferredHeight = 60f;
        le.flexibleHeight = 1f;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = minFont;
        t.overflowMode = TextOverflowModes.Ellipsis;
    }

    void BuildDesktop(RectTransform root, float height)
    {
        // Wallpaper
        var wall = UIKit.Rect("Desktop", root);
        UIKit.Stretch(wall);
        UIKit.AddImage(wall, UIKit.Background);
        var brand = UIKit.Text(wall, "Brand", "SCAM SENSE  ·  SECURE WORKSTATION", 18, new Color(1, 1, 1, 0.18f), TextAlignmentOptions.BottomLeft, bold: true);
        UIKit.Stretch(brand.rectTransform, 0);
        brand.margin = new Vector4(20, 0, 0, 64);
        brand.characterSpacing = 4f;

        // Taskbar
        var bar = UIKit.Rect("Taskbar", root);
        bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(0.5f, 0);
        bar.sizeDelta = new Vector2(0, 56); bar.anchoredPosition = Vector2.zero;
        UIKit.AddImage(bar, new Color32(0x07, 0x0C, 0x16, 255));
        var mailIcon = UIKit.Text(bar, "MailApp", "MAIL", 22, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, bold: true);
        UIKit.Stretch(mailIcon.rectTransform);
        mailIcon.margin = new Vector4(20, 0, 0, 0);
        clockText = UIKit.Text(bar, "Clock", "", 22, UIKit.TextSecondary, TextAlignmentOptions.Midline);
        UIKit.Stretch(clockText.rectTransform);
        exitButton = UIKit.Button(bar, "Exit computer  [Esc]", Exit, UIKit.Secondary, 20);
        var ert = (RectTransform)exitButton.transform;
        UIKit.Place(ert, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(300, 44));

        // Mail window
        var win = UIKit.Rect("MailWindow", root);
        win.anchorMin = Vector2.zero; win.anchorMax = Vector2.one;
        win.offsetMin = new Vector2(16, 70); win.offsetMax = new Vector2(-16, -16);
        UIKit.PanelImage(win, UIKit.Panel);

        var title = UIKit.Rect("TitleBar", win);
        title.anchorMin = new Vector2(0, 1); title.anchorMax = new Vector2(1, 1); title.pivot = new Vector2(0.5f, 1);
        title.sizeDelta = new Vector2(0, 54); title.anchoredPosition = Vector2.zero;
        var tt = UIKit.Text(title, "Title", "Inbox", 28, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, bold: true);
        UIKit.Stretch(tt.rectTransform); tt.margin = new Vector4(24, 0, 0, 0);
        unreadText = UIKit.Text(title, "Unread", "", 22, UIKit.Accent, TextAlignmentOptions.MidlineLeft, bold: true);
        UIKit.Stretch(unreadText.rectTransform); unreadText.margin = new Vector4(130, 0, 0, 0);

        // Inbox column
        var listBg = UIKit.Rect("InboxColumn", win);
        listBg.anchorMin = new Vector2(0, 0); listBg.anchorMax = new Vector2(0, 1); listBg.pivot = new Vector2(0, 0.5f);
        listBg.offsetMin = new Vector2(12, 12); listBg.offsetMax = new Vector2(12 + 500, -60);
        inboxList = UIKit.Rect("List", listBg);
        UIKit.Stretch(inboxList);
        var lv = UIKit.Vertical(inboxList, 0, 8);
        lv.childControlHeight = true;
        listBg.gameObject.AddComponent<RectMask2D>();

        // Reading pane
        var paneRoot = UIKit.Rect("PaneRoot", win);
        paneRoot.anchorMin = new Vector2(0, 0); paneRoot.anchorMax = new Vector2(1, 1);
        paneRoot.offsetMin = new Vector2(528, 12); paneRoot.offsetMax = new Vector2(-12, -60);
        UIKit.AddImage(paneRoot, UIKit.PanelRaised, rounded: true);
        paneRoot.gameObject.AddComponent<RectMask2D>();

        emptyPane = UIKit.Rect("Empty", paneRoot);
        UIKit.Stretch(emptyPane);
        var et = UIKit.Text(emptyPane, "Hint", "Select an email on the left to read it.", UIKit.SizeBody, UIKit.TextSecondary, TextAlignmentOptions.Center);
        UIKit.Stretch(et.rectTransform, 30);

        pane = UIKit.Rect("Pane", paneRoot);
        UIKit.Stretch(pane);
        var pv = UIKit.Vertical(pane, 26, 10);
        pv.padding.top = 22;

        paneSubject = UIKit.Text(pane, "Subject", "", 32, UIKit.TextPrimary, bold: true);
        paneFrom = UIKit.Text(pane, "From", "", 23, UIKit.TextPrimary);
        paneMeta = UIKit.Text(pane, "Meta", "", 20, UIKit.TextSecondary);
        paneDivider = UIKit.Divider(pane).gameObject;
        paneBody = UIKit.Text(pane, "Body", "", 25, UIKit.TextPrimary);
        paneBody.lineSpacing = 10f;
        FitToSpace(paneBody, 17f);
        paneLink = UIKit.Text(pane, "Link", "", 22, UIKit.Link);
        paneDetails = UIKit.Text(pane, "Details", "", 21, UIKit.TextPrimary);
        paneDetails.lineSpacing = 4f;
        FitToSpace(paneDetails, 15f);
        paneDetails.gameObject.SetActive(false);

        // Decision bar
        decisionBar = UIKit.Rect("Decision", pane);
        UIKit.AddImage(decisionBar, new Color(0, 0, 0, 0.25f), rounded: true);
        UIKit.Vertical(decisionBar, 16, 10);
        var qRow = UIKit.Rect("QuestionRow", decisionBar);
        var qh = UIKit.Horizontal(qRow, 12, TextAnchor.MiddleLeft);
        qh.childForceExpandHeight = false;
        UIKit.Size(qRow, -1, 48);
        var question = UIKit.Text(qRow, "Question", "Is this email a scam or legitimate?", 24, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, bold: true);
        question.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        detailsToggle = UIKit.HudButton(qRow, "DETAILS", () => SetDetailsView(!showingDetails), UIKit.HudVariant.Secondary, 20);
        UIKit.Size(detailsToggle, 200, 46);
        var timerRow = UIKit.Rect("TimerRow", decisionBar);
        UIKit.Horizontal(timerRow, 10).childForceExpandHeight = false;
        timerText = UIKit.Text(timerRow, "Timer", "", 21, UIKit.TextSecondary);
        worthText = UIKit.Text(timerRow, "Worth", "", 21, UIKit.TextSecondary, TextAlignmentOptions.TopRight);
        var btnRow = UIKit.Rect("Buttons", decisionBar);
        UIKit.Horizontal(btnRow, 14);
        UIKit.Size(btnRow, -1, 66);
        scamButton = UIKit.HudButton(btnRow, "×  SCAM", () => Choose(MessageVerdict.Scam), UIKit.HudVariant.Danger, 26);
        legitButton = UIKit.HudButton(btnRow, "✓  LEGITIMATE", () => Choose(MessageVerdict.Legitimate), UIKit.HudVariant.Safe, 26);

        // Feedback
        feedbackBox = UIKit.Rect("Feedback", pane);
        UIKit.AddImage(feedbackBox, new Color(0, 0, 0, 0.25f), rounded: true);
        UIKit.Vertical(feedbackBox, 16, 8);
        verdictText = UIKit.Text(feedbackBox, "Verdict", "", 30, UIKit.TextPrimary, bold: true);
        pointsText = UIKit.Text(feedbackBox, "Points", "", 21, UIKit.TextPrimary);
        answerText = UIKit.Text(feedbackBox, "Answer", "", 21, UIKit.TextPrimary);
        explanationText = UIKit.Text(feedbackBox, "Why", "", 21, UIKit.TextPrimary);
        scoreText = UIKit.Text(feedbackBox, "Score", "", 19, UIKit.TextSecondary);
        backButton = UIKit.HudButton(feedbackBox, "Back to inbox", BackToInbox, UIKit.HudVariant.Primary, 24);
        UIKit.Size(backButton, -1, 58);

        // Review (answered / old email)
        reviewBox = UIKit.Rect("Review", pane);
        UIKit.AddImage(reviewBox, new Color(0, 0, 0, 0.25f), rounded: true);
        UIKit.Vertical(reviewBox, 16, 8);
        reviewVerdict = UIKit.Text(reviewBox, "Verdict", "", 22, UIKit.TextPrimary, bold: true);
        reviewExplanation = UIKit.Text(reviewBox, "Why", "", 21, UIKit.TextSecondary);

        // Boxes keep their natural height; only the body/details text grows into spare space.
        foreach (var box in new[] { decisionBar, feedbackBox, reviewBox })
            box.gameObject.AddComponent<LayoutElement>().flexibleHeight = 0f;

        pane.gameObject.SetActive(false);
        decisionBar.gameObject.SetActive(false);
        feedbackBox.gameObject.SetActive(false);
        reviewBox.gameObject.SetActive(false);

        // "New email" toast visible on the monitor from across the room
        var tr = UIKit.Rect("NewMailToast", root);
        UIKit.Place(tr, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 76), new Vector2(560, 110));
        UIKit.PanelImage(tr, UIKit.AccentDark);
        toastText = UIKit.Text(tr, "Text", "", 28, Color.white, TextAlignmentOptions.Center, bold: true);
        UIKit.Stretch(toastText.rectTransform, 12);
        toast = tr.gameObject;
        toast.SetActive(false);
    }
}
