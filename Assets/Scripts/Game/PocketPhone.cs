using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// First-person smartphone: a low-poly phone held in a low-poly right hand that is pulled out of the
/// pocket, held up in front of the camera while a text message is read, then put back.
///
/// It only animates and displays. MessagePopupUI moves its phone screen onto <see cref="ScreenRoot"/>
/// and keeps the decision card (timer, details, SCAM / LEGITIMATE, feedback) beside it, so the message
/// flow, timer and scoring stay in QuizManager / ScoringSystem.
///
/// Added to the player camera automatically (see <see cref="For"/>); no Inspector setup is needed.
/// While the phone is out, player movement and mouse-look are frozen through InteractionGate and the
/// floating hands are hidden (this rig has its own hand, so nothing overlaps).
///
/// Clipping: the phone stays between the camera's near clip plane (0.3 m) and the player's collider
/// radius (0.5 m), so it is never cut by the camera and never pokes into walls or furniture.
/// </summary>
[DisallowMultipleComponent]
public class PocketPhone : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Color bodyColor = new Color32(0x1A, 0x1F, 0x2B, 255);
    [SerializeField] private Color edgeColor = new Color32(0x3A, 0x44, 0x55, 255);
    [Tooltip("Matches FloatingHand's default skin colour.")]
    [SerializeField] private Color skinColor = new Color(0.87f, 0.69f, 0.55f);

    [Header("Held pose")]
    [Tooltip("Metres in front of the camera. Keep it above the near clip plane and below the player's collider radius.")]
    [SerializeField] private float holdDistance = 0.37f;
    [Tooltip("How much of the screen height the phone covers when held up.")]
    [SerializeField, Range(0.4f, 0.9f)] private float screenHeightFill = 0.7f;
    [SerializeField, Range(0.2f, 0.8f)] private float viewportY = 0.5f;
    [Tooltip("Width kept free on the right for the decision card, in 1080p canvas units. The phone is centred in the space left of it.")]
    [SerializeField] private float reservedRightUnits = 680f;

    [Header("Timing")]
    [SerializeField] private float pullOutTime = 0.6f;
    [SerializeField] private float putAwayTime = 0.45f;

    /// <summary>Canvas units of the phone screen (same size as MessagePopupUI's phone view).</summary>
    public const float ScreenUnitsW = 500f, ScreenUnitsH = 920f;

    /// <summary>World-space UI root on the phone's screen.</summary>
    public RectTransform ScreenRoot { get; private set; }
    /// <summary>True from the moment the phone starts coming out until it is back in the pocket.</summary>
    public bool IsOut => state != State.Pocketed;
    public bool IsHeld => state == State.Held;

    private enum State { Pocketed, PullingOut, Held, PuttingAway }
    private State state = State.Pocketed;

    private Camera cam;
    private Transform rig, model;
    private FloatingHand hands;
    private Coroutine anim;
    private float progress;          // 0 = in pocket, 1 = held up (position along the path)
    private bool holdingGate;

    // Pocket and half-way poses, camera-local. The held pose is computed from the viewport.
    private static readonly Vector3 PocketPos = new Vector3(0.30f, -0.62f, 0.16f);
    private static readonly Quaternion PocketRot = Quaternion.Euler(80f, -40f, 65f);
    private static readonly Vector3 MidPos = new Vector3(0.24f, -0.33f, 0.34f);
    private static readonly Quaternion MidRot = Quaternion.Euler(38f, -22f, 28f);

    /// <summary>The phone on this camera, created on first use.</summary>
    public static PocketPhone For(Camera camera)
    {
        if (camera == null) return null;
        var p = camera.GetComponent<PocketPhone>();
        return p != null ? p : camera.gameObject.AddComponent<PocketPhone>();
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        Build();
        rig.gameObject.SetActive(false);
    }

    void OnDestroy() => ReleaseControl();

    // ---------- API (driven by MessagePopupUI) ----------

    /// <summary>
    /// Takes the phone out of the pocket. <paramref name="onRaised"/> runs once it is held up.
    /// Ignored if it is already out (it can never be pulled out twice); if it was on its way back,
    /// it turns around from where it is.
    /// </summary>
    public void PullOut(Action onRaised)
    {
        if (state == State.Held) { onRaised?.Invoke(); return; }
        if (state == State.PullingOut) return;

        TakeControl();
        rig.gameObject.SetActive(true);
        StartAnim(Animate(1f, pullOutTime, State.PullingOut, State.Held, onRaised));
    }

    /// <summary>Lowers the phone back into the pocket, then gives control back to the player.</summary>
    public void PutAway(Action onPocketed)
    {
        if (state == State.Pocketed) { onPocketed?.Invoke(); return; }
        if (state == State.PuttingAway) return;
        StartAnim(Animate(0f, putAwayTime, State.PuttingAway, State.Pocketed, () =>
        {
            rig.gameObject.SetActive(false);
            ReleaseControl();
            onPocketed?.Invoke();
        }));
    }

    // ---------- Animation ----------

    void StartAnim(IEnumerator routine)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(routine);
    }

    IEnumerator Animate(float target, float duration, State during, State after, Action done)
    {
        state = during;
        float start = progress;
        float time = Mathf.Max(0.05f, duration * Mathf.Abs(target - start));   // shorter if it turns around mid-way
        for (float t = 0f; t < time; t += Time.deltaTime)                     // scaled: the pause menu freezes it
        {
            float k = t / time;
            // Ease in-out when coming up, ease-in (accelerating down) when going back.
            k = target > start ? k * k * (3f - 2f * k) : k * k;
            progress = Mathf.Lerp(start, target, k);
            yield return null;
        }
        progress = target;
        state = after;
        anim = null;
        done?.Invoke();
    }

    void LateUpdate()
    {
        if (state == State.Pocketed || cam == null) return;

        // The phone's size is set so it fills the same share of the screen at any distance / FOV.
        float h = screenHeightFill * 2f * holdDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        model.localScale = Vector3.one * h;

        // Held pose: centred in the free space left of the decision card, facing the eye.
        float freeRight = reservedRightUnits / (1080f * Mathf.Max(0.5f, cam.aspect));
        float cx = Mathf.Clamp((1f - freeRight) * 0.5f, 0.22f, 0.5f);
        Vector3 holdPos = cam.transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(cx, viewportY, holdDistance)));
        Quaternion holdRot = Quaternion.LookRotation(holdPos.normalized, Vector3.up) * Quaternion.Euler(-3f, 0f, 2.5f);

        // Gentle breathing sway while it is up.
        float held = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, progress));
        float tt = Time.time;
        holdPos += new Vector3(Mathf.Sin(tt * 1.1f) * 0.0015f, Mathf.Sin(tt * 1.7f) * 0.002f, 0f) * held;
        holdRot *= Quaternion.Euler(Mathf.Sin(tt * 1.3f) * 0.4f * held, 0f, Mathf.Sin(tt * 0.9f) * 0.3f * held);

        // Curved path pocket -> mid -> held (quadratic Bezier), with a matching rotation blend,
        // so the phone swings up and turns toward the player instead of sliding in a straight line.
        float p = progress;
        Vector3 a = Vector3.Lerp(PocketPos, MidPos, p), b = Vector3.Lerp(MidPos, holdPos, p);
        rig.localPosition = Vector3.Lerp(a, b, p);
        rig.localRotation = Quaternion.Slerp(Quaternion.Slerp(PocketRot, MidRot, p), Quaternion.Slerp(MidRot, holdRot, p), p);
    }

    // ---------- Player control ----------

    void TakeControl()
    {
        if (!holdingGate) { InteractionGate.Acquire(this); holdingGate = true; }   // freezes movement + mouse-look
        if (hands == null) hands = FindFirstObjectByType<FloatingHand>();
        if (hands != null) hands.SetHandsVisible(false);
    }

    void ReleaseControl()
    {
        if (holdingGate) { InteractionGate.Release(this); holdingGate = false; }
        if (hands != null) hands.SetHandsVisible(true);
    }

    // ---------- Build (unit-height phone; scaled in LateUpdate) ----------

    void Build()
    {
        rig = new GameObject("PocketPhone Rig").transform;
        rig.SetParent(cam != null ? cam.transform : transform, false);
        model = new GameObject("Model").transform;
        model.SetParent(rig, false);

        var body = MakeMaterial(bodyColor, 0.75f, 0.15f);
        var edge = MakeMaterial(edgeColor, 0.6f, 0.2f);
        // A little self-light so the hand's sides don't go black in dim rooms (it faces away from most lights).
        var skin = MakeMaterial(skinColor, 0.15f, 0.75f);

        // Phone: screen faces the camera (-Z), height 1, width matches the 500 x 920 screen UI.
        float sw = ScreenUnitsW / ScreenUnitsH;      // screen width
        float bw = sw + 0.04f, bh = 1.04f, depth = 0.055f;
        Block(model, "Body", new Vector3(0f, 0f, depth * 0.5f), new Vector3(bw, bh, depth), Vector3.zero, body);
        Block(model, "Frame", new Vector3(0f, 0f, depth * 0.5f), new Vector3(bw + 0.012f, bh - 0.06f, depth * 0.8f), Vector3.zero, edge);
        Block(model, "VolumeButton", new Vector3(-bw * 0.5f - 0.008f, 0.22f, depth * 0.5f), new Vector3(0.012f, 0.14f, 0.025f), Vector3.zero, edge);
        Block(model, "PowerButton", new Vector3(bw * 0.5f + 0.008f, 0.25f, depth * 0.5f), new Vector3(0.012f, 0.09f, 0.025f), Vector3.zero, edge);

        // Screen canvas, just in front of the body's face
        var cgo = new GameObject("Screen", typeof(RectTransform), typeof(Canvas));
        cgo.transform.SetParent(model, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ScreenRoot = (RectTransform)cgo.transform;
        ScreenRoot.sizeDelta = new Vector2(ScreenUnitsW, ScreenUnitsH);
        ScreenRoot.localPosition = new Vector3(0f, 0f, -0.002f);
        ScreenRoot.localScale = Vector3.one / ScreenUnitsH;

        // Right hand holding it from behind: palm on the back, fingers curling round the left edge,
        // thumb along the right edge, wrist and forearm leaving the bottom of the screen.
        var hand = new GameObject("Hand").transform;
        hand.SetParent(model, false);
        Block(hand, "Palm", new Vector3(0.06f, -0.2f, depth + 0.035f), new Vector3(0.46f, 0.46f, 0.07f), Vector3.zero, skin);
        float fx = -bw * 0.5f - 0.03f;
        for (int i = 0; i < 4; i++)
        {
            float fy = 0.02f - i * 0.12f;
            Block(hand, "Finger" + i, new Vector3(fx, fy, depth * 0.5f + 0.01f), new Vector3(0.055f, 0.095f, depth + 0.07f), Vector3.zero, skin);
            Block(hand, "Tip" + i, new Vector3(fx + 0.012f, fy, -0.006f), new Vector3(0.035f, 0.085f, 0.014f), Vector3.zero, skin);
        }
        Block(hand, "Thumb", new Vector3(bw * 0.5f + 0.028f, -0.27f, depth * 0.5f), new Vector3(0.06f, 0.28f, depth + 0.03f), new Vector3(0f, 0f, 12f), skin);
        Block(hand, "Wrist", new Vector3(0.14f, -0.58f, depth + 0.07f), new Vector3(0.34f, 0.34f, 0.13f), new Vector3(0f, 0f, -18f), skin);
        Block(hand, "Forearm", new Vector3(0.26f, -0.98f, depth + 0.12f), new Vector3(0.32f, 0.6f, 0.15f), new Vector3(0f, 0f, -22f), skin);
    }

    static void Block(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());     // visual only: never pushes the player or props
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static Material MakeMaterial(Color c, float smoothness, float selfLight)
    {
        Shader s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(s);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (selfLight > 0f && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * selfLight);
        }
        return m;
    }
}
