using UnityEngine;
using UnityEngine.InputSystem;

/// Head movement while seated at the desk. Works together with SittableChair.
/// - Mouse turns your head, limited to the desk area (you can't spin around in the chair).
/// - Your head shifts a little as you turn, like a real head moving on a neck.
/// - Looking toward a monitor gently pulls the view onto it, zooms in slightly and leans you
///   forward so you can read the screen clearly. Look away and it eases back.
/// Put this on the Main Camera (inside Player). SittableChair calls Sit() and Stand() for you.
/// You can also tick "Seated" in the Inspector to test it without the chair.
[RequireComponent(typeof(Camera))]
public class SeatedHeadLook : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Drag both monitors here. The view is gently drawn toward whichever one you look at.")]
    [SerializeField] private Transform[] monitors;
    [SerializeField] private bool seated;                       // tick in Play mode to test

    [Header("Look")]
    [SerializeField] private float sensitivity = 0.08f;
    [SerializeField] private float maxYaw = 70f;                // how far left/right you can turn your head
    [SerializeField] private float minPitch = -30f;             // look up limit (negative = up)
    [SerializeField] private float maxPitch = 40f;              // look down limit
    [SerializeField] private float settleTime = 0.5f;           // time to ease into head control after sitting

    [Header("Head Movement")]
    [SerializeField] private float headSideShift = 0.06f;       // head moves sideways as you turn
    [SerializeField] private float headDownLean = 0.04f;        // head moves forward as you look down

    [Header("Monitor Focus")]
    [SerializeField] private float focusRadius = 16f;           // degrees around a monitor where focus kicks in
    [SerializeField] private float focusPull = 4f;              // how strongly the view is drawn onto the monitor
    [SerializeField] private float focusZoom = 8f;              // degrees of extra zoom when focused (narrower FOV)
    [SerializeField] private float focusLean = 0.08f;           // how far you lean forward toward the screen
    [SerializeField] private float focusSmoothing = 6f;

    private Camera cam;
    private Transform seatFrame;       // the Player (parent of the camera)
    private Vector3 basePos;
    private float baseFov;
    private float yaw, pitch;
    private float blend;               // 0..1 easing into head control
    private float focus;               // 0..1, smoothed
    private bool wasSeated;

    public bool IsSeated => seated;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        seatFrame = transform.parent != null ? transform.parent : transform;
    }

    // SittableChair calls these. Sit() is called once the camera has finished turning to the screens.
    public void Sit() => seated = true;

    // Stand() resets immediately and leaves the camera alone: SittableChair restores it.
    public void Stand()
    {
        seated = false;
        wasSeated = false;
        blend = 0f;
        focus = 0f;
    }

    private void LateUpdate()
    {
        if (seated && !wasSeated) Enter();
        wasSeated = seated;
        if (!seated) return;

        float dt = Time.deltaTime;
        blend = Mathf.MoveTowards(blend, 1f, dt / Mathf.Max(0.01f, settleTime));

        // Mouse turns the head
        Vector2 mouse = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        yaw += mouse.x * sensitivity;
        pitch -= mouse.y * sensitivity;

        // Monitor focus: draw the view onto the monitor you're looking toward
        float targetFocus = 0f;
        if (monitors != null && monitors.Length > 0)
        {
            float bestDist = float.MaxValue, bestYaw = yaw, bestPitch = pitch;
            foreach (var m in monitors)
            {
                if (m == null) continue;
                Vector3 local = seatFrame.InverseTransformDirection((m.position - transform.position).normalized);
                float ty = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float tp = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
                float dist = Mathf.Sqrt((ty - yaw) * (ty - yaw) + (tp - pitch) * (tp - pitch));
                if (dist < bestDist) { bestDist = dist; bestYaw = ty; bestPitch = tp; }
            }

            if (bestDist < focusRadius)
            {
                targetFocus = 1f - bestDist / focusRadius;
                // Pull less while the mouse is moving fast so you can still look away freely
                float pull = focusPull * targetFocus * dt * (1f - Mathf.Clamp01(mouse.magnitude / 25f));
                yaw = Mathf.Lerp(yaw, bestYaw, pull);
                pitch = Mathf.Lerp(pitch, bestPitch, pull);
            }
        }

        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        focus = Mathf.Lerp(focus, targetFocus, 1f - Mathf.Exp(-focusSmoothing * dt));

        float s = Mathf.SmoothStep(0f, 1f, blend);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 headOffset = new Vector3(
            (yaw / Mathf.Max(1f, maxYaw)) * headSideShift,
            0f,
            Mathf.Max(0f, pitch) / Mathf.Max(1f, maxPitch) * headDownLean + focus * focusLean);
        transform.localPosition = basePos + headOffset * s;

        float fovTarget = baseFov - focusZoom * focus * s;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovTarget, 1f - Mathf.Exp(-focusSmoothing * dt));
    }

    private void Enter()
    {
        // Start from however the camera is currently framed (SittableChair has just aimed it at the screens)
        basePos = transform.localPosition;
        baseFov = cam.fieldOfView;
        blend = 0f;
        focus = 0f;

        Vector3 e = transform.localEulerAngles;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), minPitch, maxPitch);
        yaw = Mathf.Clamp(Mathf.DeltaAngle(0f, e.y), -maxYaw, maxYaw);
    }
}