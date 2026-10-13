using UnityEngine;

/// <summary>
/// Marks the phone or the computer as a place where messages can be read.
/// Needs a Collider on this object or a child so the player's look ray can hit it.
/// QuizManager shows a prompt when the player looks at it, and uses its position
/// for the "near computer" / "near phone" arrival triggers.
/// </summary>
public class MessageDevice : MonoBehaviour
{
    public MessageChannel channel = MessageChannel.Email;
    [Tooltip("Shown when looking at the device with nothing new, e.g. 'Computer - no new emails'.")]
    public string idleLabel = "Computer  -  no new emails";

    void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning($"{name}: MessageDevice needs a Collider so it can be looked at.", this);
    }
}
