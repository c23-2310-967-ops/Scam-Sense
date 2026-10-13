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

        InteractionPrompt.Set(this, interactKey.ToString(), lookedAtDoor.IsOpen ? "Close door" : "Open door", priority: 1);

        if (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame &&
            !InteractionGate.InteractBlocked)   // E already used this frame (message, computer...)
        {
            InteractionGate.ConsumeInteract();
            lookedAtDoor.Toggle();
        }
    }

    void OnDisable() => InteractionPrompt.Clear(this);

    Door FindDoor()
    {
        var ray = new Ray(transform.position, transform.forward);
        var hits = Physics.RaycastAll(ray, interactDistance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Skip the player's own collider (the Character Controller)
            if (hit.transform.root == transform.root) continue;

            // The first thing in the way decides: a door, or a wall blocking the view
            return hit.collider.GetComponentInParent<Door>();
        }
        return null;
    }
}
