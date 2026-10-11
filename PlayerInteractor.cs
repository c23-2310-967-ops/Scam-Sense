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

        if (lookedAtDoor != null && Keyboard.current != null &&
            Keyboard.current[interactKey].wasPressedThisFrame)
        {
            lookedAtDoor.Toggle();
        }
    }

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

    // Simple on-screen prompt. Delete this method if you'd rather use your own UI.
    void OnGUI()
    {
        if (lookedAtDoor == null) return;

        string text = (lookedAtDoor.IsOpen ? "Close" : "Open") + " door  [" + interactKey + "]";
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
        GUI.Label(new Rect(Screen.width / 2f - 200f, Screen.height / 2f + 40f, 400f, 40f), text, style);
    }
}