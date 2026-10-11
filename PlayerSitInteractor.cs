using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerSitInteractor : MonoBehaviour
{
    private enum State { Standing, MovingToSeat, Sitting, MovingToStand }

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private TMP_Text promptText;

    [Header("Scripts To Control")]
    [Tooltip("Your movement/jump scripts (e.g. PlayerMotor). Disabled the whole time you are seated.")]
    [SerializeField] private MonoBehaviour[] movementScripts;
    [Tooltip("Your mouse-look script(s). Disabled only during sit/stand transitions; enabled while seated so you can look around.")]
    [SerializeField] private MonoBehaviour[] lookScripts;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 2.5f;
    [SerializeField] private LayerMask interactMask = ~0;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 0.6f;
    [Tooltip("Camera near clip plane while seated (smaller = less clipping through the chair).")]
    [SerializeField] private float seatedNearClip = 0.03f;

    [Header("Optional Animator (full-body model)")]
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private string sittingBoolName = "IsSitting";

    private State state = State.Standing;
    private ChairInteractable currentChair;
    private Vector3 positionBeforeSit;
    private float defaultNearClip;

    private void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null || characterController == null)
        {
            Debug.LogError("PlayerSitInteractor: Player Camera or CharacterController is missing.", this);
            enabled = false;
            return;
        }
        if (promptText == null)
            Debug.LogWarning("PlayerSitInteractor: Prompt Text not assigned. Interaction works but no prompt will show.", this);

        defaultNearClip = playerCamera.nearClipPlane;
        HidePrompt();
    }

    private void Update()
    {
        switch (state)
        {
            case State.Standing: HandleStanding(); break;
            case State.Sitting:  HandleSitting();  break;
            default:             HidePrompt();     break; // ignore input while transitioning
        }
    }

    // ---------- STATE HANDLERS ----------

    private void HandleStanding()
    {
        ChairInteractable chair = FindChairInView();

        if (chair == null || chair.IsOccupied || chair.sitPoint == null)
        {
            HidePrompt();
            return;
        }

        ShowPrompt(chair.sitPrompt);

        if (InteractPressed())
            StartCoroutine(SitRoutine(chair));
    }

    private void HandleSitting()
    {
        ShowPrompt(currentChair != null ? currentChair.standPrompt : "Press E to Stand");

        if (InteractPressed())
            StartCoroutine(StandRoutine());
    }

    // ---------- SIT / STAND ----------

    private IEnumerator SitRoutine(ChairInteractable chair)
    {
        state = State.MovingToSeat;   // locks out further input immediately
        HidePrompt();

        currentChair = chair;
        chair.IsOccupied = true;
        positionBeforeSit = transform.position;

        SetScripts(movementScripts, false);
        SetScripts(lookScripts, false);
        characterController.enabled = false; // required before moving the transform

        if (bodyAnimator != null) bodyAnimator.SetBool(sittingBoolName, true);

        // Face the same way as the sit point (yaw only, so the player never tilts).
        Quaternion targetRot = Quaternion.Euler(0f, chair.sitPoint.eulerAngles.y, 0f);

        // Place the player root so the CAMERA ends up exactly on the sit point.
        Vector3 eyeLocal = transform.InverseTransformPoint(playerCamera.transform.position);
        Vector3 targetPos = chair.sitPoint.position - targetRot * eyeLocal;

        yield return MoveRoot(targetPos, targetRot);

        playerCamera.nearClipPlane = seatedNearClip;
        SetScripts(lookScripts, true); // allow looking around while seated
        state = State.Sitting;
    }

    private IEnumerator StandRoutine()
    {
        state = State.MovingToStand;
        HidePrompt();

        SetScripts(lookScripts, false);
        playerCamera.nearClipPlane = defaultNearClip;

        Vector3 standPos = FindSafeStandPosition(currentChair);
        Quaternion standRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f); // keep current facing

        yield return MoveRoot(standPos, standRot);

        Physics.SyncTransforms();
        characterController.enabled = true;

        if (bodyAnimator != null) bodyAnimator.SetBool(sittingBoolName, false);

        SetScripts(movementScripts, true);
        SetScripts(lookScripts, true);

        if (currentChair != null) currentChair.IsOccupied = false;
        currentChair = null;
        state = State.Standing;
    }

    private IEnumerator MoveRoot(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float t = 0f;
        float duration = Mathf.Max(0.01f, transitionDuration);

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float s = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, targetPos, s),
                Quaternion.Slerp(startRot, targetRot, s));
            yield return null;
        }
        transform.SetPositionAndRotation(targetPos, targetRot);
    }

    // ---------- SAFE STAND-UP ----------

    private Vector3 FindSafeStandPosition(ChairInteractable chair)
    {
        var candidates = new List<Vector3>();

        if (chair.standPoint != null)
            candidates.Add(RootFromFeet(chair.standPoint.position));

        candidates.Add(positionBeforeSit); // where we came from

        Transform s = chair.sitPoint;
        Vector3 fwd = Flat(s.forward);
        Vector3 right = Flat(s.right);
        Vector3 basePos = new Vector3(s.position.x, positionBeforeSit.y, s.position.z);

        candidates.Add(basePos - fwd * 0.9f);   // behind the chair
        candidates.Add(basePos + right * 0.9f); // right side
        candidates.Add(basePos - right * 0.9f); // left side
        candidates.Add(basePos + fwd * 0.9f);   // in front

        foreach (Vector3 c in candidates)
            if (IsSpaceFree(c)) return c;

        Debug.LogWarning("PlayerSitInteractor: No free stand-up spot found; using original position. Move the StandPoint to clearer space.", this);
        return positionBeforeSit;
    }

    // Converts a floor point into the player root position (works whatever your pivot is).
    private Vector3 RootFromFeet(Vector3 feet)
    {
        float bottomLocalY = characterController.center.y - characterController.height * 0.5f;
        return new Vector3(feet.x, feet.y - bottomLocalY + 0.02f, feet.z);
    }

    private bool IsSpaceFree(Vector3 rootPos)
    {
        float r = Mathf.Max(0.05f, characterController.radius - 0.03f);
        float half = Mathf.Max(characterController.height * 0.5f, r);
        Vector3 c = rootPos + characterController.center;

        Vector3 bottom = c + Vector3.up * (-half + r + 0.1f); // lifted so the floor isn't counted
        Vector3 top = c + Vector3.up * (half - r);

        return !Physics.CheckCapsule(bottom, top, r, ~0, QueryTriggerInteraction.Ignore);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // ---------- HELPERS ----------

    private ChairInteractable FindChairInView()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<ChairInteractable>();
        return null;
    }

    private static void SetScripts(MonoBehaviour[] scripts, bool value)
    {
        if (scripts == null) return;
        foreach (MonoBehaviour s in scripts)
            if (s != null) s.enabled = value;
    }

    private void ShowPrompt(string message)
    {
        if (promptText == null) return;
        if (promptText.text != message) promptText.text = message;
        if (!promptText.gameObject.activeSelf) promptText.gameObject.SetActive(true);
    }

    private void HidePrompt()
    {
        if (promptText != null && promptText.gameObject.activeSelf)
            promptText.gameObject.SetActive(false);
    }

    private static bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}