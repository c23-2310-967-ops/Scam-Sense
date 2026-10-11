using UnityEngine;

public class ChairInteractable : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("Where the player's EYES should be when seated. Blue (Z) arrow = direction the player faces.")]
    public Transform sitPoint;

    [Header("Recommended")]
    [Tooltip("A point on the FLOOR where the player stands after getting up. Keep it clear of walls/furniture.")]
    public Transform standPoint;

    [Header("Prompts")]
    public string sitPrompt = "Press E to Sit";
    public string standPrompt = "Press E to Stand";

    // Set by PlayerSitInteractor so two sit requests can't overlap.
    public bool IsOccupied { get; set; }

    private void Awake()
    {
        if (sitPoint == null)
            Debug.LogError($"[{name}] ChairInteractable: Sit Point is not assigned!", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (sitPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(sitPoint.position, 0.1f);
            Gizmos.DrawRay(sitPoint.position, sitPoint.forward * 0.5f); // facing direction
        }
        if (standPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(standPoint.position, 0.1f);
        }
    }
}