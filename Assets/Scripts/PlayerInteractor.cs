using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Put this on the Main Camera (child of Player).
/// Look at a door and press E to open or close it.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] float interactDistance = 2.5f;
    [SerializeField] Key interactKey = Key.E;

    Door lookedAtDoor;

    void Update()
    {
        lookedAtDoor = FindDoor();

        if (lookedAtDoor == null)
        {
            InteractionPrompt.Clear(this);
            return;
        }

        InteractionPrompt.Set(this, InteractionPrompt.KeyName(interactKey), lookedAtDoor.IsOpen ? "Close door" : "Open door", priority: 1);

        if (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame &&
            !InteractionGate.InteractBlocked)   // E already used this frame (message, computer...)
        {
            InteractionGate.ConsumeInteract();
            lookedAtDoor.Toggle();
        }
    }

    void OnDisable() => InteractionPrompt.Clear(this);

    Camera cam;

    Door FindDoor()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }
        // The first thing in the way decides (a door, or a wall blocking the view); the player's own
        // collider is skipped. Shared, allocation-free ray - see LookRaycast.
        return LookRaycast.TryGetFirstHit(cam, transform.root, interactDistance, out RaycastHit hit)
            ? hit.collider.GetComponentInParent<Door>()
            : null;
    }
}
