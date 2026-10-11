using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Put this on the chair (chairDesk). Walk close, press E to sit, press E again to stand.
/// No trigger collider or tag needed: it finds the player and checks distance.
/// Needs two children named SeatPoint and ExitPoint (capitalization doesn't matter).
/// While seated, the camera turns to look at Look Target (put it between the monitors).
/// </summary>
public class SittableChair : MonoBehaviour
{
    [Header("Points (auto-found by name if left empty)")]
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform exitPoint;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("How close (in metres, horizontally) the player must be to sit.")]
    [SerializeField] private float interactDistance = 2.5f;

    [Header("Behaviour")]
    [SerializeField] private float moveTime = 0.4f;
    [Tooltip("Player movement AND mouse-look scripts to switch off while seated.")]
    [SerializeField] private MonoBehaviour[] disableWhileSitting;
    [Tooltip("Scripts on the Player (matched by class name) that are switched off automatically while seated. No dragging needed.")]
    [SerializeField] private string[] autoDisableByName = { "PlayerMotor", "InputManager", "PlayerLook", "MouseLook" };
    private readonly System.Collections.Generic.List<MonoBehaviour> autoFound = new System.Collections.Generic.List<MonoBehaviour>();

    [Header("Screen focus")]
    [Tooltip("Empty object placed between the two monitors. Camera looks at it while seated.")]
    [SerializeField] private Transform lookTarget;
    [SerializeField] private float seatedFov = 45f;

    private Transform player;
    private CharacterController characterController;
    private Camera cam;
    private SeatedHeadLook headLook;   // optional: lets the player move their head while seated
    private Vector3 camStartLocalPos;
    private Quaternion camStartLocalRot;
    private float camStartFov;

    private bool isSitting;
    private bool busy;
    private bool playerNear;

    private void Awake()
    {
        if (seatPoint == null) seatPoint = FindChild("SeatPoint");
        if (exitPoint == null) exitPoint = FindChild("ExitPoint");
        if (lookTarget == null) lookTarget = FindChild("ScreenFocus");

        if (seatPoint == null || exitPoint == null)
            Debug.LogWarning($"{name}: SeatPoint or ExitPoint child not found.", this);
    }

    private void Start()
    {
        FindPlayer();
        Debug.Log($"{name}: SittableChair ready. Player found: {(player != null ? player.name : "NO")}", this);
    }

    private void FindPlayer()
    {
        GameObject tagged = null;
        try { tagged = GameObject.FindWithTag("Player"); } catch { /* tag may not exist */ }

        if (tagged != null)
            player = tagged.transform;
        else
        {
            characterController = FindFirstObjectByType<CharacterController>();
            if (characterController != null) player = characterController.transform;
            else if (Camera.main != null) player = Camera.main.transform.root;
        }

        if (player != null && characterController == null)
            characterController = player.GetComponent<CharacterController>();
    }

    private Transform FindChild(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child != transform &&
                child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        return null;
    }

    private bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(interactKey);
#endif
    }

    private void Update()
    {
        if (player == null) { FindPlayer(); if (player == null) return; }

        if (!isSitting && seatPoint != null)
        {
            Vector3 a = player.position; a.y = 0f;
            Vector3 b = seatPoint.position; b.y = 0f;
            playerNear = Vector3.Distance(a, b) <= interactDistance;
        }

        if (busy || seatPoint == null || exitPoint == null) return;
        if (!isSitting && !playerNear) return;

        if (InteractPressed())
        {
            Debug.Log(isSitting ? "Standing up" : "Sitting down", this);
            StartCoroutine(isSitting ? StandUp() : SitDown());
        }
    }

    private IEnumerator SitDown()
    {
        busy = true;

        // Remember camera state so we can restore it when standing up
        cam = Camera.main;
        if (cam != null)
        {
            camStartLocalPos = cam.transform.localPosition;
            camStartLocalRot = cam.transform.localRotation;
            camStartFov = cam.fieldOfView;
        }

        SetControlEnabled(false);
        yield return MovePlayerTo(seatPoint);
        yield return FocusCamera();

        // Hand the camera over to head movement (mouse look around the desk)
        headLook = cam != null ? cam.GetComponent<SeatedHeadLook>() : null;
        if (headLook != null) headLook.Sit();

        isSitting = true;
        busy = false;
    }

    private IEnumerator StandUp()
    {
        busy = true;

        // Stop head movement first, then restore the camera
        if (headLook != null) headLook.Stand();

        // Restore camera before handing control back
        if (cam != null)
        {
            cam.transform.localPosition = camStartLocalPos;
            cam.transform.localRotation = camStartLocalRot;
            cam.fieldOfView = camStartFov;
        }

        yield return MovePlayerTo(exitPoint);
        SetControlEnabled(true);

        isSitting = false;
        busy = false;
    }

    private IEnumerator FocusCamera()
    {
        if (cam == null || lookTarget == null) yield break;

        Quaternion startRot = cam.transform.rotation;
        Quaternion endRot = Quaternion.LookRotation(lookTarget.position - cam.transform.position);
        float startFov = cam.fieldOfView;

        for (float t = 0f; t < 1f; t += Time.deltaTime / moveTime)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            cam.transform.rotation = Quaternion.Slerp(startRot, endRot, s);
            cam.fieldOfView = Mathf.Lerp(startFov, seatedFov, s);
            yield return null;
        }

        cam.transform.rotation = endRot;
        cam.fieldOfView = seatedFov;
    }

    private IEnumerator MovePlayerTo(Transform target)
    {
        Vector3 startPos = player.position;
        Quaternion startRot = player.rotation;
        Quaternion endRot = Quaternion.Euler(0f, target.eulerAngles.y, 0f);

        for (float t = 0f; t < 1f; t += Time.deltaTime / moveTime)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            player.SetPositionAndRotation(
                Vector3.Lerp(startPos, target.position, s),
                Quaternion.Slerp(startRot, endRot, s));
            yield return null;
        }
        player.SetPositionAndRotation(target.position, endRot);
    }

    private void CollectAutoScripts()
    {
        autoFound.Clear();
        if (player == null) return;

        foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb == this) continue;
            string typeName = mb.GetType().Name;
            foreach (var wanted in autoDisableByName)
            {
                if (typeName.Equals(wanted, System.StringComparison.OrdinalIgnoreCase))
                {
                    autoFound.Add(mb);
                    break;
                }
            }
        }
    }

    private void SetControlEnabled(bool enabled)
    {
        if (!enabled) CollectAutoScripts();

        // Standing up: turn the controller on first, then the scripts.
        if (enabled && characterController != null) characterController.enabled = true;

        foreach (var script in disableWhileSitting)
            if (script != null) script.enabled = enabled;
        foreach (var script in autoFound)
            if (script != null) script.enabled = enabled;

        // Sitting down: scripts are already off, so now the controller can go off safely.
        if (!enabled && characterController != null) characterController.enabled = false;
    }

    private void OnGUI()
    {
        if (busy || (!isSitting && !playerNear)) return;

        string text = isSitting ? "Press E to stand" : "Press E to sit";
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22
        };
        GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 40), text, style);
    }
}