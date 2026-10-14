using UnityEngine;

/// <summary>
/// The player's visible body (the kid in a hoodie) for the first-person controller.
/// Put the KidHoodie prefab as a child of the Player (the object with the CharacterController).
///   - Walking: legs and arms swing with the player's real speed (works with PlayerMotor unchanged).
///   - Sitting: switches to a seated pose while a SittableChair is occupied (thighs forward, shins down),
///     with the hips placed just below the seated camera.
///   - First-person view: the hoodie, head and arms are "shadows only" (they cast the full shadow but never
///     block or clip the camera); the jeans and sneakers are visible when looking down. The first-person
///     hands (FloatingHand / PocketPhone) wear matching hoodie sleeves.
/// Purely visual: no colliders, no effect on movement, scoring or interactions.
/// </summary>
public class PlayerBody : MonoBehaviour
{
    [Header("Rig (assigned by Tools > Scam Sense > Add Kid Character To Player)")]
    [SerializeField] private Transform visual;
    [SerializeField] private Transform hips;
    [SerializeField] private Transform legL, legR, kneeL, kneeR;
    [SerializeField] private Transform shoulderL, shoulderR, elbowL, elbowR;
    [Tooltip("Defaults to the camera inside the Player.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Walking")]
    [SerializeField] private float walkSpeedReference = 5f;   // PlayerMotor.speed
    [SerializeField] private float stepsPerSecond = 1.9f;
    [SerializeField] private float legSwing = 26f;
    [SerializeField] private float kneeBend = 30f;
    [SerializeField] private float armSwing = 22f;
    [SerializeField] private float bob = 0.025f;
    [SerializeField] private float blendSpeed = 6f;

    [Header("Sitting")]
    [Tooltip("How far below the seated camera the hips sit.")]
    [SerializeField] private float seatedHipsBelowEye = 0.78f;

    Transform playerRoot;
    Vector3 lastPos, visualRest;
    float hipsRestY, moveAmount, phase, seatBlend;

    void Start()
    {
        playerRoot = transform.parent != null ? transform.parent : transform;
        if (cameraTransform == null)
        {
            var cam = playerRoot.GetComponentInChildren<Camera>();
            cameraTransform = cam != null ? cam.transform : (Camera.main != null ? Camera.main.transform : null);
        }
        lastPos = playerRoot.position;
        visualRest = visual.localPosition;
        hipsRestY = hips.localPosition.y;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // How fast is the player walking? (horizontal movement since last frame)
        Vector3 delta = playerRoot.position - lastPos;
        lastPos = playerRoot.position;
        delta.y = 0f;
        bool seated = SittableChair.PlayerSeated;
        float target = seated ? 0f : Mathf.Clamp01(delta.magnitude / dt / Mathf.Max(0.1f, walkSpeedReference));
        if (delta.magnitude > 1f) target = 0f;   // teleports (sitting down/standing up) aren't steps
        moveAmount = Mathf.MoveTowards(moveAmount, target, blendSpeed * dt);
        seatBlend = Mathf.MoveTowards(seatBlend, seated ? 1f : 0f, 3f * dt);
        phase += dt * stepsPerSecond * Mathf.PI * 2f * moveAmount;

        // Walking pose
        float s = Mathf.Sin(phase);
        float walkL = -s * legSwing * moveAmount;                     // negative = forward
        float walkR = s * legSwing * moveAmount;
        float kneeLw = Mathf.Max(0f, Mathf.Sin(phase + 1.2f)) * kneeBend * moveAmount;
        float kneeRw = Mathf.Max(0f, Mathf.Sin(phase + 1.2f + Mathf.PI)) * kneeBend * moveAmount;
        float armL = s * armSwing * moveAmount;                       // arms swing opposite the legs
        float armR = -s * armSwing * moveAmount;

        // Blend with the seated pose
        legL.localRotation = Quaternion.Euler(Mathf.Lerp(walkL, -88f, seatBlend), 0f, 0f);
        legR.localRotation = Quaternion.Euler(Mathf.Lerp(walkR, -88f, seatBlend), 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(Mathf.Lerp(kneeLw, 88f, seatBlend), 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(Mathf.Lerp(kneeRw, 88f, seatBlend), 0f, 0f);
        shoulderL.localRotation = Quaternion.Euler(Mathf.Lerp(armL, -38f, seatBlend), 0f, Mathf.Lerp(4f, 8f, seatBlend));
        shoulderR.localRotation = Quaternion.Euler(Mathf.Lerp(armR, -38f, seatBlend), 0f, Mathf.Lerp(-4f, -8f, seatBlend));
        elbowL.localRotation = Quaternion.Euler(Mathf.Lerp(-10f * moveAmount - 6f, -40f, seatBlend), 0f, 0f);
        elbowR.localRotation = Quaternion.Euler(Mathf.Lerp(-10f * moveAmount - 6f, -40f, seatBlend), 0f, 0f);

        // Step bob while walking; when seated, drop the body so the hips are just under the eye.
        Vector3 pos = visualRest;
        pos.y += Mathf.Abs(Mathf.Sin(phase)) * bob * moveAmount;
        if (seatBlend > 0f && cameraTransform != null)
        {
            float hipsWorldTarget = cameraTransform.position.y - seatedHipsBelowEye;
            float hipsWorldRest = transform.TransformPoint(visualRest + Vector3.up * hipsRestY).y;
            pos.y += (hipsWorldTarget - hipsWorldRest) * seatBlend;
        }
        visual.localPosition = pos;
    }
}
