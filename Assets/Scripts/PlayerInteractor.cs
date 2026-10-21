using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Put this on the Main Camera (child of Player). Look at something and press E:
///   - a door: open or close it;
///   - an InspectableObject (case folder, evidence document, investigation board): read it in DocumentReaderUI,
///     or get told what to do first if it is not available yet;
///   - the phone with no message waiting: a short "no new messages" note. (A waiting text is opened by
///     QuizManager with the left-hand pocket phone, from anywhere, before this script sees the key.)
/// Computers, case-file stations and the final case terminal handle their own E press.
/// Uses the shared look ray (the first collider in front of the camera, so walls block it) and the
/// InteractionGate (E is used once per frame, and nothing opens while another panel is open).
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] float interactDistance = 2.5f;
    [Tooltip("Reach for the phone's 'no new messages' note (matches QuizManager's look distance).")]
    [SerializeField] float phoneDistance = 3f;
    [SerializeField] Key interactKey = Key.E;

    Camera cam;
    LevelHUD hud;
    QuizManager quiz;

    void Update()
    {
        if (!TryLook(out RaycastHit hit))
        {
            InteractionPrompt.Clear(this);
            return;
        }

        bool pressed = Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame &&
                       !InteractionGate.InteractBlocked;   // E already used this frame (message, computer...)
        string key = InteractionPrompt.KeyName(interactKey);

        // Doors
        var door = hit.distance <= interactDistance ? hit.collider.GetComponentInParent<Door>() : null;
        if (door != null)
        {
            InteractionPrompt.Set(this, key, door.IsOpen ? "Close door" : "Open door", priority: 1);
            if (pressed) { InteractionGate.ConsumeInteract(); door.Toggle(); }
            return;
        }

        // Case folders, evidence documents, investigation boards
        var item = hit.collider.GetComponentInParent<InspectableObject>();
        if (item != null && item.isActiveAndEnabled && hit.distance <= item.interactDistance)
        {
            bool available = item.IsAvailable(out _);
            InteractionPrompt.Set(this, key, item.Prompt, priority: 2, secondary: available ? null : item.lockedHint);
            if (pressed)
            {
                InteractionGate.ConsumeInteract();
                item.Interact(Hud);
            }
            return;
        }

        // The phone when nothing is waiting (its prompt comes from QuizManager)
        if (pressed && hit.distance <= phoneDistance)
        {
            var device = hit.collider.GetComponentInParent<MessageDevice>();
            if (device != null && MessageChannels.OnPhone(device.channel) && Quiz != null && Quiz.PendingMessage == null && !Quiz.IsAnswering)
            {
                InteractionGate.ConsumeInteract();
                if (Hud != null) Hud.ShowToast(Quiz.IsComplete ? "Every message in this case has been reviewed." : "No new messages on your phone yet.");
            }
        }
        InteractionPrompt.Clear(this);
    }

    void OnDisable() => InteractionPrompt.Clear(this);

    LevelHUD Hud => hud != null ? hud : (hud = FindFirstObjectByType<LevelHUD>());
    QuizManager Quiz => quiz != null ? quiz : (quiz = FindFirstObjectByType<QuizManager>());

    bool TryLook(out RaycastHit hit)
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }
        // The first thing in the way decides (a door, a document, or a wall blocking the view); the player's own
        // collider is skipped. Shared, allocation-free ray - see LookRaycast. Each target applies its own reach.
        return LookRaycast.TryGetFirstHit(cam, transform.root, LookRaycast.MaxDistance, out hit);
    }
}
