using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Floating first-person hands that follow the camera.
/// - Idle: gentle float.
/// - Walking: hands swing back and forth in opposite directions, bob with each step, and roll slightly.
/// - Looking around: hands lag behind and sway.
/// Add this to the Main Camera inside your Player. It detects walking automatically from how the
/// player moves, so it works with your PlayerMotor without any changes to it.
/// The model can be a Project asset or an object already in the scene. The left hand is a mirrored copy.
public class FloatingHand : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Transform cameraTransform;        // defaults to Camera.main
    [SerializeField] private Transform customHandModel;        // optional: your own right-hand model
    [SerializeField] private bool buildPlaceholder = true;
    [SerializeField] private bool showBothHands = true;
    [SerializeField] private Color skinColor = new Color(0.87f, 0.69f, 0.55f);
    [Tooltip("Hoodie sleeve on the placeholder hands (matches the KidHoodie character). Alpha 0 = no sleeve.")]
    [SerializeField] private Color sleeveColor = new Color32(0x2F, 0x80, 0xA8, 255);

    [Header("Position (right hand, relative to camera)")]
    [Tooltip("The left hand uses the mirrored X value.")]
    [SerializeField] private Vector3 restOffset = new Vector3(0.32f, -0.28f, 0.6f);
    [SerializeField] private Vector3 restRotation = new Vector3(-10f, -15f, 0f);

    [Header("Idle Float")]
    [SerializeField] private float bobAmplitude = 0.015f;
    [SerializeField] private float bobSpeed = 1.6f;

    [Header("Walking Motion")]
    [SerializeField] private float walkSpeedReference = 5f;    // your PlayerMotor speed
    [SerializeField] private float walkCycleSpeed = 9f;        // higher = faster steps
    [SerializeField] private float walkForwardSwing = 0.03f;   // small forward/back movement of each hand
    [SerializeField] private float walkArcWidth = 0.05f;       // sideways arc: hands swing inward on the forward swing
    [SerializeField] private float walkHandLift = 0.035f;      // each hand rises as it swings forward
    [SerializeField] private float walkStepBob = 0.02f;        // both hands dip a little with every step
    [SerializeField] private Vector3 walkPullIn = new Vector3(-0.03f, -0.05f, -0.12f); // hands relax toward the body while walking
    [SerializeField] private float walkPitch = 14f;            // degrees the hand tilts up/down as it swings
    [SerializeField] private float walkYaw = 8f;               // degrees the hand turns inward/outward
    [SerializeField] private float walkRoll = 5f;              // degrees the hand rolls
    [SerializeField] private float moveBlendSpeed = 8f;        // how quickly it blends idle <-> walking

    [Header("Look Sway")]
    [SerializeField] private float swayAmount = 0.004f;
    [SerializeField] private float swayTilt = 1.5f;
    [SerializeField] private float followSmoothing = 8f;

    [Header("Reach (right hand reaches out when you interact)")]
    [SerializeField] private Key reachKey = Key.E;             // match your Player Interactor key
    [SerializeField] private float reachDistance = 2.5f;       // match your Interact Distance
    [SerializeField] private LayerMask reachLayers = ~0;       // which layers the hand can reach for
    [SerializeField] private float reachHoldTime = 0.4f;       // seconds the hand stays out after pressing
    [SerializeField] private float reachSmoothing = 14f;
    [SerializeField] private float maxReachExtension = 0.22f;  // most the hand moves away from its rest spot
    [SerializeField, Range(0f, 1f)] private float reachAimStrength = 0.5f; // how much the hand turns toward the object
    [SerializeField] private float stopShortDistance = 0.12f;  // used only when the object is very close
    [SerializeField] private bool reachEvenIfNothingHit = false; // reach into the air if nothing is in range

    private class Hand
    {
        public Transform rig;
        public float side;               // +1 right, -1 left
        public Vector3 smoothPos;
        public Quaternion smoothRot;
    }

    private readonly List<Hand> hands = new List<Hand>();
    private Quaternion lastCamRot = Quaternion.identity;
    private Transform reachTarget;       // set by ReachTo(), stays until Retract()
    private Vector3 reachPoint;          // set by the interact key
    private float reachTimer;

    private Transform playerRoot;
    private Vector3 lastPlayerPos;
    private float moveAmount;            // 0 = standing, 1 = walking at reference speed
    private float walkPhase;

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (cameraTransform == null) { Debug.LogError("FloatingHand: no camera found."); enabled = false; return; }

        lastCamRot = cameraTransform.rotation;

        var cc = cameraTransform.GetComponentInParent<CharacterController>();
        playerRoot = cc != null ? cc.transform : cameraTransform.root;
        lastPlayerPos = playerRoot.position;

        // Remember how the custom model is set up before we start moving it around.
        bool modelIsAsset = customHandModel != null && !customHandModel.gameObject.scene.IsValid();
        Vector3 modelScale = customHandModel != null ? customHandModel.localScale : Vector3.one;

        hands.Add(CreateHand(+1f, modelIsAsset, modelScale, reuseSceneObject: true));
        if (showBothHands)
            hands.Add(CreateHand(-1f, modelIsAsset, modelScale, reuseSceneObject: false));
    }

    private Hand CreateHand(float side, bool modelIsAsset, Vector3 modelScale, bool reuseSceneObject)
    {
        var h = new Hand { side = side };

        h.rig = new GameObject(side > 0 ? "HandRig_Right" : "HandRig_Left").transform;
        h.rig.SetParent(cameraTransform, false);

        Vector3 pos = RestPosition(side);
        Quaternion rot = RestRotation(side);
        h.rig.localPosition = pos;
        h.rig.localRotation = rot;
        h.smoothPos = pos;
        h.smoothRot = rot;

        if (customHandModel != null)
        {
            Transform model;
            if (!modelIsAsset && reuseSceneObject) model = customHandModel;      // use the scene object itself
            else model = Instantiate(customHandModel);                            // asset, or the second hand

            model.SetParent(h.rig, false);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            // Mirror the model for the left hand
            model.localScale = new Vector3(modelScale.x * side, modelScale.y, modelScale.z);

            // The hands are visual only, so strip colliders: they can't push the player or props.
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            AddSleeveToModel(model);
        }
        else if (buildPlaceholder)
        {
            var root = BuildPlaceholderHand(h.rig);
            root.localScale = new Vector3(side, 1f, 1f);
        }

        return h;
    }

    private Vector3 RestPosition(float side) => new Vector3(restOffset.x * side, restOffset.y, restOffset.z);
    private Quaternion RestRotation(float side) => Quaternion.Euler(restRotation.x, restRotation.y * side, restRotation.z * side);

    private void LateUpdate()
    {
        if (hands.Count == 0) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateReachInput(dt);

        // ---- How fast is the player walking? (horizontal speed from position change) ----
        Vector3 delta = playerRoot.position - lastPlayerPos;
        lastPlayerPos = playerRoot.position;
        delta.y = 0f;
        float speed = delta.magnitude / dt;
        float targetMove = Mathf.Clamp01(speed / Mathf.Max(0.01f, walkSpeedReference));
        moveAmount = Mathf.MoveTowards(moveAmount, targetMove, moveBlendSpeed * dt);
        walkPhase += dt * walkCycleSpeed * moveAmount;       // only advances while moving

        // ---- Look sway from camera rotation change ----
        Vector3 e = (Quaternion.Inverse(lastCamRot) * cameraTransform.rotation).eulerAngles;
        float dx = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -8f, 8f);
        float dy = Mathf.Clamp(Mathf.DeltaAngle(0f, e.y), -8f, 8f);
        lastCamRot = cameraTransform.rotation;
        Vector3 sway = new Vector3(-dy * swayAmount * 10f, dx * swayAmount * 10f, 0f);

        float follow = 1f - Mathf.Exp(-followSmoothing * dt);
        float reach = 1f - Mathf.Exp(-reachSmoothing * dt);

        foreach (var h in hands)
        {
            bool reaching = IsReaching && h.side > 0f;

            if (reaching)
            {
                Vector3 worldPoint = reachTarget != null ? reachTarget.position : reachPoint;
                Vector3 local = cameraTransform.InverseTransformPoint(worldPoint);
                if (local.z < 0.1f) local.z = 0.1f;   // never reach behind the camera
                // Only a small, controlled stretch toward the object (not all the way to it)
                Vector3 restLocal = RestPosition(h.side);
                Vector3 desired = local - local.normalized * stopShortDistance;
                Vector3 targetPos = restLocal + Vector3.ClampMagnitude(desired - restLocal, maxReachExtension);
                Quaternion lookRot = Quaternion.LookRotation(local.normalized, Vector3.up);
                Quaternion targetRot = Quaternion.Slerp(RestRotation(h.side), lookRot, reachAimStrength);
                h.smoothPos = Vector3.Lerp(h.smoothPos, targetPos, reach);
                h.smoothRot = Quaternion.Slerp(h.smoothRot, targetRot, reach);
                h.rig.localPosition = h.smoothPos;
                h.rig.localRotation = h.smoothRot;
                continue;
            }

            // Smoothed base position (rest + look sway), so the hand lags behind camera turns
            Vector3 basePos = RestPosition(h.side) + sway;
            Quaternion baseRot = RestRotation(h.side) *
                                 Quaternion.Euler(0f, 0f, -sway.x * swayTilt * 100f);
            h.smoothPos = Vector3.Lerp(h.smoothPos, basePos, follow);
            h.smoothRot = Quaternion.Slerp(h.smoothRot, baseRot, follow);

            // Idle float (fades out while walking)
            float idlePhase = Time.time * bobSpeed + (h.side < 0f ? 0.8f : 0f);
            Vector3 idle = new Vector3(
                Mathf.Sin(idlePhase * 0.5f) * bobAmplitude * 0.5f,
                Mathf.Sin(idlePhase) * bobAmplitude,
                0f) * (1f - moveAmount);

            // Walking: hands swing in opposite directions like arms, bob with each step
            float swing = Mathf.Sin(walkPhase + (h.side < 0f ? Mathf.PI : 0f));
            float lift = Mathf.Max(0f, swing);   // hand rises only on its forward swing
            Vector3 pullIn = new Vector3(walkPullIn.x * h.side, walkPullIn.y, walkPullIn.z);
            Vector3 walk = pullIn * moveAmount + new Vector3(
                -swing * walkArcWidth * h.side * moveAmount,
                (lift * walkHandLift + Mathf.Sin(walkPhase * 2f) * walkStepBob) * moveAmount,
                swing * walkForwardSwing * moveAmount);
            Quaternion walkRot = Quaternion.Euler(
                -swing * walkPitch * moveAmount,
                -swing * walkYaw * moveAmount * h.side,
                swing * walkRoll * moveAmount * h.side);

            h.rig.localPosition = h.smoothPos + idle + walk;
            h.rig.localRotation = h.smoothRot * walkRot;

            // Left hand reaching down to (or coming back from) the pocket for the phone.
            if (h.side < 0f && pocketReach > 0f)
            {
                float k = pocketReach * pocketReach * (3f - 2f * pocketReach);
                h.rig.localPosition = Vector3.Lerp(h.rig.localPosition, PocketHandPos, k);
                h.rig.localRotation = Quaternion.Slerp(h.rig.localRotation, PocketHandRot, k);
            }
        }
    }

    private bool IsReaching => !ReachSuppressed && (reachTarget != null || reachTimer > 0f);

    // ---- Phone: the left hand goes down to the pocket (PocketPhone drives this) ----
    private float pocketReach;           // 0 = normal pose, 1 = hand down at the left trouser pocket
    // Camera-local pose of the left hand at the pocket opening (below the view, beside the hip).
    private static readonly Vector3 PocketHandPos = new Vector3(-0.30f, -0.66f, 0.16f);
    private static readonly Quaternion PocketHandRot = Quaternion.Euler(70f, 25f, -40f);

    /// <summary>While true the right hand never reaches out for the E key (the phone is being used).</summary>
    public bool ReachSuppressed { get; set; }

    /// <summary>
    /// Moves the LEFT hand toward the trouser pocket: 0 = its normal pose, 1 = at the pocket (out of view).
    /// Used by PocketPhone so the empty hand visibly reaches down before the phone comes up, and comes back
    /// up after the phone is put away.
    /// </summary>
    public void SetPocketReach(float amount) => pocketReach = Mathf.Clamp01(amount);

    /// <summary>Shows or hides one hand (side +1 = right, -1 = left).</summary>
    public void SetHandVisible(float side, bool visible)
    {
        foreach (var h in hands)
            if (h.rig != null && Mathf.Sign(h.side) == Mathf.Sign(side)) h.rig.gameObject.SetActive(visible);
    }

    // Pressing the reach key shoots a ray from the camera; if it hits something in range,
    // the right hand reaches out to that point and pulls back after a moment.
    private void UpdateReachInput(float dt)
    {
        if (ReachSuppressed) { reachTimer = 0f; return; }   // phone in use: E must not send the right hand out
        var kb = Keyboard.current;
        if (kb != null && reachKey != Key.None)
        {
            var key = kb[reachKey];
            if (key.wasPressedThisFrame || (key.isPressed && reachTimer > 0f))
            {
                if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit,
                        reachDistance, reachLayers, QueryTriggerInteraction.Ignore))
                {
                    reachPoint = hit.point;
                    reachTimer = reachHoldTime;
                }
                else if (reachEvenIfNothingHit && key.wasPressedThisFrame)
                {
                    reachPoint = cameraTransform.position + cameraTransform.forward * (reachDistance * 0.8f);
                    reachTimer = reachHoldTime;
                }
            }
        }

        if (reachTimer > 0f) reachTimer -= dt;
    }

    // Optional: call these from your own interaction script instead of (or as well as) the key.
    //   hand.ReachTo(door.transform);  ... later ...  hand.Retract();
    // Only the right hand reaches.
    /// <summary>Hide/show the hands (e.g. while the camera is up close to a computer screen).</summary>
    public void SetHandsVisible(bool visible)
    {
        foreach (var h in hands)
            if (h.rig != null) h.rig.gameObject.SetActive(visible);
    }

    public void ReachTo(Transform target) => reachTarget = target;
    public void ReachToPoint(Vector3 worldPoint) { reachPoint = worldPoint; reachTimer = reachHoldTime; }
    public void Retract() { reachTarget = null; reachTimer = 0f; }

    // ---------- Hoodie sleeve on a custom hand model ----------

    /// <summary>
    /// Colours the model's "Cuff" part like the hoodie cuff and adds a sleeve running back from it along the
    /// arm (direction taken from Palm -> Cuff, so it fits the model and the mirrored left hand).
    /// Does nothing if the model has no "Cuff" part or sleeveColor alpha is 0.
    /// </summary>
    private void AddSleeveToModel(Transform model)
    {
        if (sleeveColor.a <= 0f) return;
        Transform cuff = null, palm = null;
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Cuff") cuff = t;
            else if (t.name == "Palm") palm = t;
        }
        var cuffR = cuff != null ? cuff.GetComponent<Renderer>() : null;
        if (cuffR == null) return;

        cuffR.material.color = sleeveColor * 0.78f;   // darker ribbed cuff (instance material: the asset is untouched)
        Bounds cb = cuffR.bounds;
        Vector3 from = palm != null && palm.GetComponent<Renderer>() != null ? palm.GetComponent<Renderer>().bounds.center : model.position;
        Vector3 dir = (cb.center - from).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = -model.forward;

        float thick = Mathf.Max(cb.size.x, Mathf.Max(cb.size.y, cb.size.z)) * 1.1f;
        const float length = 0.22f;
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sleeve.name = "Sleeve";
        Destroy(sleeve.GetComponent<Collider>());
        sleeve.transform.SetPositionAndRotation(cb.center + dir * (length * 0.5f), Quaternion.LookRotation(dir, model.up));
        sleeve.transform.localScale = new Vector3(thick, thick, length);
        sleeve.transform.SetParent(model, true);   // keeps the world size, follows the hand
        var r = sleeve.GetComponent<Renderer>();
        r.sharedMaterial = MakeMaterial(sleeveColor);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------- Placeholder hand ----------

    private Transform BuildPlaceholderHand(Transform parent)
    {
        Material mat = MakeMaterial(skinColor);
        var root = new GameObject("PlaceholderHand").transform;
        root.SetParent(parent, false);

        // Palm (hand points along +Z)
        Block(root, "Palm", new Vector3(0f, 0f, 0f), new Vector3(0.09f, 0.025f, 0.10f), Vector3.zero, mat);

        // Four fingers
        float[] xs = { -0.03f, -0.01f, 0.01f, 0.03f };
        float[] lens = { 0.060f, 0.075f, 0.070f, 0.055f };
        for (int i = 0; i < 4; i++)
        {
            Block(root, "Finger" + i, new Vector3(xs[i], 0f, 0.05f + lens[i] * 0.5f),
                new Vector3(0.016f, 0.018f, lens[i]), Vector3.zero, mat);
        }

        // Thumb
        Block(root, "Thumb", new Vector3(-0.058f, 0f, 0.015f),
            new Vector3(0.018f, 0.02f, 0.06f), new Vector3(0f, -35f, 0f), mat);

        // Wrist
        Block(root, "Wrist", new Vector3(0f, 0f, -0.075f), new Vector3(0.07f, 0.022f, 0.06f), Vector3.zero, mat);

        // Hoodie sleeve + darker cuff (the kid character's sleeves)
        if (sleeveColor.a > 0f)
        {
            Block(root, "Sleeve", new Vector3(0f, 0f, -0.175f), new Vector3(0.095f, 0.05f, 0.15f), Vector3.zero, MakeMaterial(sleeveColor));
            Block(root, "Cuff", new Vector3(0f, 0f, -0.1f), new Vector3(0.088f, 0.044f, 0.025f), Vector3.zero, MakeMaterial(sleeveColor * 0.78f));
        }
        return root;
    }

    private static void Block(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());      // no physics, so it never pushes the player or props
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Material MakeMaterial(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Sprites/Default");
        var m = new Material(s);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        return m;
    }
}