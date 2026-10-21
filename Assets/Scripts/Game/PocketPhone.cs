using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// First-person smartphone held in the player's LEFT hand (a low-poly hand and phone built in code - there is
/// no Animator or clip; the motion is procedural).
///
/// Taking it out (PullOut):
///   1. Reach - the floating left hand drops down to the left trouser pocket (FloatingHand.SetPocketReach).
///   2. Grab  - out of view, that hand is swapped for this rig (the same hand now holding the phone), with a
///              small dip into the pocket.
///   3. Raise - the phone swings up out of the pocket along a curve and turns its screen toward the player,
///              settling with a slight overshoot into the held pose left of the decision card.
/// Putting it away (PutAway) plays the same steps in reverse: the phone goes down into the pocket, then the
/// empty left hand comes back up to its normal pose.
/// The right floating hand stays visible in its normal pose and never reaches out while the phone is used.
///
/// It only animates and displays. MessagePopupUI moves its phone screen onto <see cref="ScreenRoot"/> and keeps
/// the decision card (timer, details, SCAM / LEGITIMATE, feedback) beside it, so the message flow, timer and
/// scoring stay in QuizManager / ScoringSystem. Added to the player camera automatically (see <see cref="For"/>);
/// no Inspector setup is needed. While the phone is out, movement and mouse-look are frozen through InteractionGate.
///
/// Clipping: the phone stays between the camera's near clip plane (0.3 m) and the player's collider radius
/// (0.5 m), so it is never cut by the camera and never pokes into walls or furniture.
/// </summary>
[DisallowMultipleComponent]
public class PocketPhone : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Color bodyColor = new Color32(0x1A, 0x1F, 0x2B, 255);
    [SerializeField] private Color edgeColor = new Color32(0x3A, 0x44, 0x55, 255);
    [Tooltip("Matches FloatingHand's default skin colour.")]
    [SerializeField] private Color skinColor = new Color(0.87f, 0.69f, 0.55f);
    [Tooltip("Hoodie sleeve on the holding arm (matches the KidHoodie character).")]
    [SerializeField] private Color sleeveColor = new Color32(0x2F, 0x80, 0xA8, 255);

    [Header("Hand")]
    [Tooltip("Hold the phone in the left hand (pocket on the left). Off = the original right-hand version.")]
    [SerializeField] private bool leftHanded = true;

    [Header("Held pose")]
    [Tooltip("Metres in front of the camera. Keep it above the near clip plane and below the player's collider radius.")]
    [SerializeField] private float holdDistance = 0.37f;
    [Tooltip("How much of the screen height the phone covers when held up.")]
    [SerializeField, Range(0.4f, 0.9f)] private float screenHeightFill = 0.7f;
    [SerializeField, Range(0.2f, 0.8f)] private float viewportY = 0.5f;
    [Tooltip("Width kept free on the right for the decision card, in 1080p canvas units. The phone is centred in the space left of it.")]
    [SerializeField] private float reservedRightUnits = 680f;

    [Header("Timing")]
    [Tooltip("The empty left hand dropping to the pocket before the phone comes up (0 = skip).")]
    [SerializeField] private float reachTime = 0.22f;
    [SerializeField] private float pullOutTime = 0.6f;
    [SerializeField] private float putAwayTime = 0.45f;
    [Tooltip("The empty left hand coming back up after the phone is pocketed.")]
    [SerializeField] private float returnTime = 0.25f;

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

    private float Side => leftHanded ? -1f : 1f;

    // Right-hand pocket and half-way poses, camera-local (mirrored for the left hand). The held pose comes from the viewport.
    private static readonly Vector3 PocketPos = new Vector3(0.30f, -0.62f, 0.16f);
    private static readonly Vector3 PocketEuler = new Vector3(80f, -40f, 65f);
    private static readonly Vector3 MidPos = new Vector3(0.24f, -0.33f, 0.34f);
    private static readonly Vector3 MidEuler = new Vector3(38f, -22f, 28f);

    Vector3 Mirror(Vector3 p) => new Vector3(p.x * Side, p.y, p.z);
    Quaternion MirrorRot(Vector3 euler) => Quaternion.Euler(euler.x, euler.y * Side, euler.z * Side);

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
    /// Ignored if it is already coming out (it can never be pulled out twice); if it was on its way back,
    /// it turns around from where it is.
    /// </summary>
    public void PullOut(Action onRaised)
    {
        if (state == State.Held) { onRaised?.Invoke(); return; }
        if (state == State.PullingOut) return;
        TakeControl();
        StartAnim(PullOutSequence(onRaised));
    }

    /// <summary>Lowers the phone back into the pocket, then gives control back to the player.</summary>
    public void PutAway(Action onPocketed)
    {
        if (state == State.Pocketed) { onPocketed?.Invoke(); return; }
        if (state == State.PuttingAway) return;
        StartAnim(PutAwaySequence(onPocketed));
    }

    // ---------- Animation ----------

    void StartAnim(IEnumerator routine)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(routine);
    }

    IEnumerator PullOutSequence(Action onRaised)
    {
        state = State.PullingOut;
        // 1. Reach: the empty left hand drops to the pocket (skipped if the phone is already part-way out).
        if (progress <= 0f && !rig.gameObject.activeSelf && hands != null && reachTime > 0f)
        {
            hands.SetHandVisible(Side, true);
            yield return Tween(reachTime, k => hands.SetPocketReach(k));
        }
        // 2. Grab: out of view, the floating hand becomes this rig's hand holding the phone.
        if (hands != null) { hands.SetHandVisible(Side, false); hands.SetPocketReach(0f); }
        rig.gameObject.SetActive(true);
        // 3. Raise
        yield return Move(1f, pullOutTime);
        state = State.Held;
        anim = null;
        onRaised?.Invoke();
    }

    IEnumerator PutAwaySequence(Action onPocketed)
    {
        state = State.PuttingAway;
        yield return Move(0f, putAwayTime);
        rig.gameObject.SetActive(false);
        // The empty left hand comes back up from the pocket to its normal pose.
        if (hands != null)
        {
            hands.SetPocketReach(1f);
            hands.SetHandVisible(Side, true);
            if (returnTime > 0f) yield return Tween(returnTime, k => hands.SetPocketReach(1f - k));
            hands.SetPocketReach(0f);
        }
        state = State.Pocketed;
        anim = null;
        ReleaseControl();
        onPocketed?.Invoke();
    }

    /// <summary>Runs <paramref name="apply"/> from 0 to 1 over <paramref name="duration"/> scaled seconds (the pause menu freezes it).</summary>
    static IEnumerator Tween(float duration, Action<float> apply)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            apply(t / duration);
            yield return null;
        }
        apply(1f);
    }

    /// <summary>Moves the phone along its path to <paramref name="target"/> (1 = held, 0 = pocket).</summary>
    IEnumerator Move(float target, float duration)
    {
        float start = progress;
        float time = Mathf.Max(0.05f, duration * Mathf.Abs(target - start));   // shorter if it turns around mid-way
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            // Coming up: ease out with a slight overshoot so the phone settles in the hand.
            // Going back: ease-in (accelerating down into the pocket).
            k = target > start ? EaseOutBack(k) : k * k;
            progress = Mathf.LerpUnclamped(start, target, k);
            yield return null;
        }
        progress = target;
    }

    static float EaseOutBack(float k)
    {
        const float s = 1.2f;   // small overshoot (about 4%)
        k -= 1f;
        return 1f + k * k * ((s + 1f) * k + s);
    }

    void LateUpdate()
    {
        if (state == State.Pocketed || cam == null || !rig.gameObject.activeSelf) return;

        // The phone's size is set so it fills the same share of the screen at any distance / FOV.
        float h = screenHeightFill * 2f * holdDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        model.localScale = Vector3.one * h;

        // Held pose: centred in the free space left of the decision card, facing the eye.
        float freeRight = reservedRightUnits / (1080f * Mathf.Max(0.5f, cam.aspect));
        float cx = Mathf.Clamp((1f - freeRight) * 0.5f, 0.22f, 0.5f);
        Vector3 holdPos = cam.transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(cx, viewportY, holdDistance)));
        Quaternion holdRot = Quaternion.LookRotation(holdPos.normalized, Vector3.up) * Quaternion.Euler(-3f, 0f, 2.5f * Side);

        // Gentle breathing sway while it is up.
        float held = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, progress));
        float tt = Time.time;
        holdPos += new Vector3(Mathf.Sin(tt * 1.1f) * 0.0015f, Mathf.Sin(tt * 1.7f) * 0.002f, 0f) * held;
        holdRot *= Quaternion.Euler(Mathf.Sin(tt * 1.3f) * 0.4f * held, 0f, Mathf.Sin(tt * 0.9f) * 0.3f * held);

        // Grab: a small dip deeper into the pocket at the very start of the pull (out of view), so the hand
        // closes on the phone before lifting it.
        float pc = Mathf.Clamp01(progress);
        float dip = state == State.PullingOut ? Mathf.Sin(Mathf.Clamp01(pc / 0.18f) * Mathf.PI) * (1f - pc) : 0f;
        Vector3 pocket = Mirror(PocketPos) + new Vector3(0f, -0.05f, -0.02f) * dip;

        // Curved path pocket -> mid -> held (quadratic Bezier), with a matching rotation blend,
        // so the phone swings up and turns toward the player instead of sliding in a straight line.
        float p = progress;   // may overshoot slightly above 1 while settling
        Vector3 a = Vector3.LerpUnclamped(pocket, Mirror(MidPos), p), b = Vector3.LerpUnclamped(Mirror(MidPos), holdPos, p);
        rig.localPosition = Vector3.LerpUnclamped(a, b, p);
        Quaternion pocketRot = MirrorRot(PocketEuler), midRot = MirrorRot(MidEuler);
        rig.localRotation = Quaternion.Slerp(Quaternion.Slerp(pocketRot, midRot, pc), Quaternion.Slerp(midRot, holdRot, pc), pc);
    }

    // ---------- Player control ----------

    void TakeControl()
    {
        if (!holdingGate) { InteractionGate.Acquire(this); holdingGate = true; }   // freezes movement + mouse-look
        if (hands == null) hands = FindFirstObjectByType<FloatingHand>();
        if (hands != null)
        {
            hands.ReachSuppressed = true;   // the right hand stays at rest: E must not send it out
            hands.Retract();
            hands.SetHandVisible(-Side, true);
        }
    }

    void ReleaseControl()
    {
        if (holdingGate) { InteractionGate.Release(this); holdingGate = false; }
        if (hands != null)
        {
            hands.ReachSuppressed = false;
            hands.SetPocketReach(0f);
            hands.SetHandsVisible(true);
        }
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

        // Hand holding it from behind: palm on the back, fingers curling round the far edge, thumb along the
        // near edge, wrist and forearm leaving the bottom of the screen. Authored as a right hand; every hand
        // block is mirrored on X for the left hand (the phone itself is not mirrored).
        var hand = new GameObject(leftHanded ? "LeftHand" : "RightHand").transform;
        hand.SetParent(model, false);
        HandBlock(hand, "Palm", new Vector3(0.06f, -0.2f, depth + 0.035f), new Vector3(0.46f, 0.46f, 0.07f), Vector3.zero, skin);
        float fx = -bw * 0.5f - 0.03f;
        for (int i = 0; i < 4; i++)
        {
            float fy = 0.02f - i * 0.12f;
            HandBlock(hand, "Finger" + i, new Vector3(fx, fy, depth * 0.5f + 0.01f), new Vector3(0.055f, 0.095f, depth + 0.07f), Vector3.zero, skin);
            HandBlock(hand, "Tip" + i, new Vector3(fx + 0.012f, fy, -0.006f), new Vector3(0.035f, 0.085f, 0.014f), Vector3.zero, skin);
        }
        HandBlock(hand, "Thumb", new Vector3(bw * 0.5f + 0.028f, -0.27f, depth * 0.5f), new Vector3(0.06f, 0.28f, depth + 0.03f), new Vector3(0f, 0f, 12f), skin);
        HandBlock(hand, "Wrist", new Vector3(0.14f, -0.58f, depth + 0.07f), new Vector3(0.34f, 0.34f, 0.13f), new Vector3(0f, 0f, -18f), skin);
        // Hoodie sleeve over the forearm, with a darker cuff at the wrist
        var sleeve = MakeMaterial(sleeveColor, 0.12f, 0.35f);
        var cuff = MakeMaterial(sleeveColor * 0.78f, 0.12f, 0.35f);
        HandBlock(hand, "Sleeve", new Vector3(0.28f, -1.0f, depth + 0.12f), new Vector3(0.38f, 0.64f, 0.19f), new Vector3(0f, 0f, -22f), sleeve);
        HandBlock(hand, "Cuff", new Vector3(0.19f, -0.75f, depth + 0.1f), new Vector3(0.36f, 0.08f, 0.17f), new Vector3(0f, 0f, -20f), cuff);
    }

    /// <summary>A hand block authored for the right hand, mirrored on X when the phone is left-handed.</summary>
    void HandBlock(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat) =>
        Block(parent, name, Mirror(pos), scale, new Vector3(euler.x, euler.y * Side, euler.z * Side), mat);

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
