using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A reference computer with read-only case files (trusted contacts, orders, account activity, policy...).
/// Look at the monitor and press E: the camera eases in to face the screen, the cursor is freed, and you
/// click the tabs on the monitor. Esc or the Exit button leaves. It never scores or reveals an answer -
/// the player compares messages against this evidence and decides on the email computer or the phone.
///
/// The screen UI is a World Space canvas built in the scene (Tools > Scam Sense > Build Level 3 Room);
/// its tab buttons call ShowPage(index) and the exit button calls Exit() as persistent onClick events.
/// Pages come from the level's LevelDefinition.caseFiles (via GameManager); the Fallback Pages list is
/// used only when the level has none.
/// </summary>
[DefaultExecutionOrder(-90)]
public class CaseFilesTerminal : MonoBehaviour
{
    [Header("Screen (scene objects)")]
    [SerializeField] private Canvas screenCanvas;
    [Tooltip("Where the camera moves while using this computer.")]
    [SerializeField] private Transform viewPoint;
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private TMP_Text pageTitle;
    [SerializeField] private TMP_Text pageBody;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Shown on the screen while a message is waiting to be read.")]
    [SerializeField] private GameObject newMessageBanner;
    [SerializeField] private Button exitButton;

    [Header("Content")]
    [Tooltip("Used only if the level's LevelDefinition has no Case Files.")]
    [SerializeField] private List<CaseFile> fallbackPages = new List<CaseFile>();

    [Header("Interaction")]
    [SerializeField] private string displayName = "Open case files";
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Key useKey = Key.E;
    [SerializeField] private float cameraMoveTime = 0.45f;
    [SerializeField] private float screenFov = 60f;

    [Header("References (auto-found if empty)")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private QuizManager quiz;

    public bool InUse { get; private set; }

    private Camera cam;
    private Transform player;
    private FloatingHand hands;
    private LevelHUD hud;
    private GraphicRaycaster raycaster;
    private Vector3 camLocalPos;
    private Quaternion camLocalRot;
    private float camFov;
    private Coroutine camRoutine;
    private bool camAtScreen;
    private List<CaseFile> pages;
    private int currentPage;

    void Awake()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (quiz == null) quiz = FindFirstObjectByType<QuizManager>();
        if (screenCanvas == null || viewPoint == null || pageBody == null)
        {
            Debug.LogError($"{name}: CaseFilesTerminal needs Screen Canvas, View Point and Page Body (rebuild with Tools > Scam Sense > Build Level 3 Room).", this);
            enabled = false;
            return;
        }
        raycaster = screenCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;   // clickable only while in use
    }

    void OnEnable()
    {
        if (quiz != null) quiz.StateChanged += RefreshStatus;
    }

    void OnDisable()
    {
        if (quiz != null) quiz.StateChanged -= RefreshStatus;
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

        var level = gameManager != null ? gameManager.Level : null;
        pages = level != null && level.caseFiles != null && level.caseFiles.Count > 0 ? level.caseFiles : fallbackPages;

        // Label the scene's tab buttons from the data; hide extra ones.
        for (int i = 0; i < (tabButtons != null ? tabButtons.Length : 0); i++)
        {
            if (tabButtons[i] == null) continue;
            bool used = pages != null && i < pages.Count;
            tabButtons[i].gameObject.SetActive(used);
            if (used) UIKit.SetLabel(tabButtons[i], pages[i].title.ToUpperInvariant());
        }
        ShowPage(0);
        RefreshStatus();
    }

    void Update()
    {
        if (InUse || cam == null) return;

        bool focused = !InteractionGate.UIOpen && !PauseMenu.IsPaused && IsLookedAt();
        if (!focused)
        {
            InteractionPrompt.Clear(this);
            return;
        }

        InteractionPrompt.Set(this, useKey.ToString(), displayName, priority: 4);
        var kb = Keyboard.current;
        if (kb != null && kb[useKey].wasPressedThisFrame && !InteractionGate.InteractBlocked)
        {
            InteractionGate.ConsumeInteract();
            Enter();
        }
    }

    void LateUpdate()
    {
        if (InUse && camAtScreen && cam != null)
            cam.transform.SetPositionAndRotation(viewPoint.position, viewPoint.rotation);
    }

    bool IsLookedAt()
    {
        Transform c = cam.transform;
        var hits = Physics.RaycastAll(c.position, c.forward, interactDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (player != null && hit.transform.root == player.root) continue;
            return hit.transform.IsChildOf(transform);   // first thing in the way decides
        }
        return false;
    }

    // ---------- Pages (called by the tab buttons' onClick) ----------

    public void ShowPage(int index)
    {
        if (pages == null || pages.Count == 0)
        {
            if (pageTitle != null) pageTitle.text = "No case files";
            pageBody.text = "";
            return;
        }
        currentPage = Mathf.Clamp(index, 0, pages.Count - 1);
        if (pageTitle != null) pageTitle.text = pages[currentPage].title;
        pageBody.text = pages[currentPage].body;

        // Selected tab: brighter label and a "›" marker (never colour alone).
        for (int i = 0; i < (tabButtons != null ? tabButtons.Length : 0); i++)
        {
            if (tabButtons[i] == null || i >= pages.Count) continue;
            bool on = i == currentPage;
            UIKit.SetLabel(tabButtons[i], (on ? "›  " : "") + pages[i].title.ToUpperInvariant());
            var label = tabButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = on ? UIKit.Accent : UIKit.TextPrimary;
        }
    }

    void RefreshStatus()
    {
        bool waiting = quiz != null && quiz.PendingMessage != null;
        if (newMessageBanner != null) newMessageBanner.SetActive(waiting);
        if (statusText != null && quiz != null && quiz.Level != null)
            statusText.text = $"Messages reviewed {quiz.Scoring.Results.Count}/{quiz.Level.MessageCount}";
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
        if (hud != null) hud.SetVisible(false);

        if (raycaster != null) raycaster.enabled = true;
        RefreshStatus();
        if (camRoutine != null) StopCoroutine(camRoutine);
        camRoutine = StartCoroutine(MoveCamera(true));
    }

    public void Exit()
    {
        if (!InUse || !camAtScreen) return;   // ignore clicks while the camera is still moving in
        if (raycaster != null) raycaster.enabled = false;
        InteractionGate.RemoveEscapeHandler(this);
        if (camRoutine != null) StopCoroutine(camRoutine);
        camRoutine = StartCoroutine(MoveCamera(false));
    }

    bool OnEscape()
    {
        if (!InUse || PauseMenu.IsPaused) return false;
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
        float targetFov = toScreen ? screenFov : camFov;

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
        }
        camRoutine = null;
    }
}
