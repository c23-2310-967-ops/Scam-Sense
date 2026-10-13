using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Put this on a chair, sofa or stool. Look at it (or stand right next to it facing it) and press E to sit;
/// press E (or Space) to stand. Only one chair responds at a time, so chairs side by side never trigger together.
///
/// Children (auto-found by name, case-insensitive):
///   SeatPoint   - where the player's EYES are when seated. Its blue (Z) arrow is the facing direction.
///   ExitPoint   - a point on the FLOOR where the player stands up. Keep it clear of furniture.
///   ScreenFocus - optional; the camera turns to look at it when seated (e.g. between two monitors).
///
/// Uses the existing controller: PlayerMotor / InputManager / MouseLook are switched off while seated and
/// SeatedHeadLook (on the camera) handles looking around. Standing restores position, camera and controls.
/// </summary>
[DefaultExecutionOrder(-50)]   // after QuizManager / ComputerTerminal (they may claim E first), before doors
public class SittableChair : MonoBehaviour
{
    [Header("Points (auto-found by name if left empty)")]
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform lookTarget;

    [Header("Interaction")]
    [SerializeField] private string sitLabel = "Sit";
    [Tooltip("How far away the chair can be when you look straight at it.")]
    [SerializeField] private float interactDistance = 2.2f;
    [Tooltip("Standing this close and roughly facing the seat also works, without aiming exactly.")]
    [SerializeField] private float nearDistance = 1.2f;
    [SerializeField] private Key useKey = Key.E;
    [SerializeField] private Key altStandKey = Key.Space;

    [Header("Motion")]
    [SerializeField] private float moveTime = 0.45f;
    [Tooltip("Degrees the view tilts down when seated (used when there is no ScreenFocus).")]
    [SerializeField] private float seatedPitch = 8f;
    [Tooltip("Camera field of view while seated. 0 = keep the normal FOV.")]
    [SerializeField] private float seatedFieldOfView = 0f;
    [SerializeField] private float cooldown = 0.35f;

    [Header("Scripts switched off while seated")]
    [SerializeField] private MonoBehaviour[] disableWhileSitting;
    [Tooltip("Scripts on the Player (matched by class name) switched off automatically while seated.")]
    [SerializeField] private string[] autoDisableByName = { "PlayerMotor", "InputManager", "PlayerLook", "MouseLook" };

    // ---------- Shared state: one focused chair per frame, one occupied chair at a time ----------
    static readonly List<SittableChair> all = new List<SittableChair>();
    static SittableChair focused;
    static int focusFrame = -1;
    static Transform player;
    static CharacterController controller;
    static Camera cam;

    /// <summary>The chair the player is sitting in, or null.</summary>
    public static SittableChair Occupied { get; private set; }
    public static bool PlayerSeated => Occupied != null;

    private readonly List<Behaviour> autoFound = new List<Behaviour>();
    private SeatedHeadLook headLook;
    private Vector3 positionBeforeSit;
    private Quaternion rotationBeforeSit;
    private Vector3 camStartLocalPos;
    private Quaternion camStartLocalRot;
    private float camStartFov;
    private float footOffset;            // player root height above its feet
    private bool busy;
    private float readyTime;

    public Transform SeatPoint => seatPoint;

    void Awake()
    {
        if (seatPoint == null) seatPoint = FindChild("SeatPoint");
        if (exitPoint == null) exitPoint = FindChild("ExitPoint");
        if (lookTarget == null) lookTarget = FindChild("ScreenFocus");

        if (seatPoint == null || exitPoint == null)
        {
            Debug.LogWarning($"{name}: SittableChair needs SeatPoint and ExitPoint children - disabled.", this);
            enabled = false;
        }
    }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }

    void OnDisable()
    {
        all.Remove(this);
        InteractionPrompt.Clear(this);
    }

    void OnDestroy()
    {
        if (Occupied == this) Occupied = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all.Clear();
        focused = null;
        focusFrame = -1;
        Occupied = null;
        player = null;
        controller = null;
        cam = null;
    }

    // ---------- Per-frame ----------

    void Update()
    {
        UpdateFocus();
        if (focused != this || busy || Time.time < readyTime || InteractionGate.UIOpen)
        {
            InteractionPrompt.Clear(this);
            return;
        }

        var kb = Keyboard.current;
        if (Occupied == this)
        {
            InteractionPrompt.Set(this, useKey.ToString(), "Stand up", priority: 0);
            bool standPressed = kb != null &&
                ((kb[useKey].wasPressedThisFrame && !InteractionGate.InteractBlocked) ||
                 (altStandKey != Key.None && kb[altStandKey].wasPressedThisFrame));
            if (standPressed)
            {
                InteractionGate.ConsumeInteract();
                StartCoroutine(StandUp());
            }
        }
        else
        {
            InteractionPrompt.Set(this, useKey.ToString(), sitLabel, priority: 2);
            if (kb != null && kb[useKey].wasPressedThisFrame && !InteractionGate.InteractBlocked)
            {
                InteractionGate.ConsumeInteract();
                StartCoroutine(SitDown());
            }
        }
    }

    /// <summary>Once per frame: the chair being looked at (or the nearest one in front) gets focus.</summary>
    static void UpdateFocus()
    {
        if (focusFrame == Time.frameCount) return;
        focusFrame = Time.frameCount;
        focused = null;

        if (Occupied != null) { focused = Occupied; return; }
        if (!FindPlayer()) return;

        Transform c = cam.transform;
        var hits = Physics.RaycastAll(c.position, c.forward, 4f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.transform.root == player.root) continue;   // own collider
            var chair = hit.collider.GetComponentInParent<SittableChair>();
            if (chair != null && chair.enabled && hit.distance <= chair.interactDistance) focused = chair;
            break;   // first thing in the way decides
        }
        if (focused != null) return;

        float best = float.MaxValue;
        Vector3 flatFwd = Vector3.ProjectOnPlane(c.forward, Vector3.up).normalized;
        foreach (var chair in all)
        {
            Vector3 to = chair.seatPoint.position - player.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d > chair.nearDistance || d >= best) continue;
            if (d > 0.3f && Vector3.Dot(flatFwd, to / d) < 0.5f) continue;   // must be roughly in front
            best = d;
            focused = chair;
        }
    }

    static bool FindPlayer()
    {
        if (player != null && cam != null) return true;
        GameObject tagged = null;
        try { tagged = GameObject.FindWithTag("Player"); } catch { /* tag may not exist */ }
        if (tagged == null)
        {
            var cc = FindFirstObjectByType<CharacterController>();
            if (cc != null) tagged = cc.gameObject;
        }
        if (tagged == null) return false;
        player = tagged.transform;
        controller = tagged.GetComponent<CharacterController>();
        cam = tagged.GetComponentInChildren<Camera>();
        return cam != null;
    }

    // ---------- Sit / stand ----------

    /// <summary>Sit in this chair from code (same checks as pressing E). Returns false if not possible now.</summary>
    public bool TrySit()
    {
        if (busy || Occupied != null || InteractionGate.UIOpen || !FindPlayer()) return false;
        StartCoroutine(SitDown());
        return true;
    }

    /// <summary>Stand up from this chair from code. Returns false if not seated here.</summary>
    public bool TryStand()
    {
        if (busy || Occupied != this || InteractionGate.UIOpen) return false;
        StartCoroutine(StandUp());
        return true;
    }

    IEnumerator SitDown()
    {
        busy = true;
        Occupied = this;
        InteractionPrompt.Clear(this);

        positionBeforeSit = player.position;
        rotationBeforeSit = player.rotation;
        camStartLocalPos = cam.transform.localPosition;
        camStartLocalRot = cam.transform.localRotation;
        camStartFov = cam.fieldOfView;
        footOffset = controller != null && controller.enabled ? player.position.y - controller.bounds.min.y : 1f;

        SetControlEnabled(false);

        // Place the root so the CAMERA ends exactly on the seat point, facing the seat direction.
        Quaternion rootRot = Quaternion.Euler(0f, seatPoint.eulerAngles.y, 0f);
        Vector3 eyeOffset = rootRot * Vector3.Scale(camStartLocalPos, player.lossyScale);
        Vector3 rootPos = seatPoint.position - eyeOffset;

        Quaternion camRot = Quaternion.Euler(seatedPitch, 0f, 0f);
        if (lookTarget != null)
        {
            Vector3 dir = Quaternion.Inverse(rootRot) * (lookTarget.position - seatPoint.position);
            camRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        float fov = seatedFieldOfView > 0f ? seatedFieldOfView : camStartFov;

        yield return Move(rootPos, rootRot, camStartLocalPos, camRot, fov);

        headLook = cam.GetComponent<SeatedHeadLook>();
        if (headLook != null)
        {
            headLook.enabled = true;
            headLook.Sit();
        }

        readyTime = Time.time + cooldown;
        busy = false;
    }

    IEnumerator StandUp()
    {
        busy = true;
        InteractionPrompt.Clear(this);
        if (headLook != null) headLook.Stand();

        Vector3 standPos = FindStandPosition();
        Quaternion standRot = Quaternion.Euler(0f, player.eulerAngles.y, 0f);   // keep facing
        yield return Move(standPos, standRot, camStartLocalPos, camStartLocalRot, camStartFov);

        Physics.SyncTransforms();
        SetControlEnabled(true);

        Occupied = null;
        readyTime = Time.time + cooldown;
        busy = false;
    }

    IEnumerator Move(Vector3 rootPos, Quaternion rootRot, Vector3 camPos, Quaternion camRot, float fov)
    {
        Vector3 p0 = player.position;
        Quaternion r0 = player.rotation;
        Vector3 cp0 = cam.transform.localPosition;
        Quaternion cr0 = cam.transform.localRotation;
        float f0 = cam.fieldOfView;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, moveTime))
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            player.SetPositionAndRotation(Vector3.Lerp(p0, rootPos, s), Quaternion.Slerp(r0, rootRot, s));
            cam.transform.localPosition = Vector3.Lerp(cp0, camPos, s);
            cam.transform.localRotation = Quaternion.Slerp(cr0, camRot, s);
            cam.fieldOfView = Mathf.Lerp(f0, fov, s);
            yield return null;
        }
        player.SetPositionAndRotation(rootPos, rootRot);
        cam.transform.localPosition = camPos;
        cam.transform.localRotation = camRot;
        cam.fieldOfView = fov;
    }

    Vector3 FindStandPosition()
    {
        var candidates = new List<Vector3>();
        if (exitPoint != null) candidates.Add(exitPoint.position + Vector3.up * footOffset);
        candidates.Add(positionBeforeSit);

        Vector3 seat = seatPoint.position;
        float floorY = exitPoint != null ? exitPoint.position.y : positionBeforeSit.y - footOffset;
        Vector3 fwd = Vector3.ProjectOnPlane(seatPoint.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        foreach (var dir in new[] { -fwd, right, -right, fwd })
            candidates.Add(new Vector3(seat.x, floorY + footOffset, seat.z) + dir * 0.9f);

        foreach (var c in candidates)
            if (IsSpaceFree(c)) return c;

        Debug.LogWarning($"{name}: no free stand-up spot found; returning to where you sat down from. Move the ExitPoint to clear floor.", this);
        return positionBeforeSit;
    }

    bool IsSpaceFree(Vector3 rootPos)
    {
        if (controller == null) return true;
        float scale = player.lossyScale.y;
        float r = Mathf.Max(0.05f, controller.radius * scale - 0.03f);
        float half = Mathf.Max(controller.height * scale * 0.5f, r);
        Vector3 c = rootPos + player.rotation * Vector3.Scale(controller.center, player.lossyScale);
        Vector3 bottom = c + Vector3.up * (-half + r + 0.1f);   // lifted so the floor isn't counted
        Vector3 top = c + Vector3.up * (half - r);
        return !Physics.CheckCapsule(bottom, top, r, ~0, QueryTriggerInteraction.Ignore);
    }

    void SetControlEnabled(bool on)
    {
        if (!on)
        {
            autoFound.Clear();
            foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.enabled) continue;
                string typeName = mb.GetType().Name;
                foreach (var wanted in autoDisableByName)
                    if (typeName == wanted) { autoFound.Add(mb); break; }
            }
            if (disableWhileSitting != null)
                foreach (var s in disableWhileSitting)
                    if (s != null && s.enabled && !autoFound.Contains(s)) autoFound.Add(s);
            foreach (var b in autoFound) b.enabled = false;
            if (controller != null) controller.enabled = false;   // required before moving the transform
        }
        else
        {
            if (controller != null) controller.enabled = true;
            foreach (var b in autoFound) if (b != null) b.enabled = true;
            autoFound.Clear();
        }
    }

    Transform FindChild(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child != transform && child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Transform s = seatPoint != null ? seatPoint : FindChild("SeatPoint");
        Transform e = exitPoint != null ? exitPoint : FindChild("ExitPoint");
        if (s != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(s.position, 0.08f);
            Gizmos.DrawRay(s.position, s.forward * 0.5f);
        }
        if (e != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(e.position, 0.12f);
        }
    }
}
