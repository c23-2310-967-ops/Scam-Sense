using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person mouse look. Put this on the Camera (a child of Player).
/// Mouse left/right turns the Player body, mouse up/down tilts the camera.
/// Press Escape to free the cursor, click to lock it again.
/// </summary>
public class MouseLook : MonoBehaviour
{
    [Tooltip("The Player object (the one with the Character Controller). Auto-detected if empty.")]
    [SerializeField] Transform playerBody;

    [SerializeField] float sensitivity = 0.1f;
    [SerializeField] float minPitch = -80f;
    [SerializeField] float maxPitch = 80f;
    [SerializeField] bool lockCursorOnStart = true;

    float pitch;

    void Start()
    {
        if (playerBody == null && transform.parent != null)
            playerBody = transform.parent;

        if (lockCursorOnStart) SetCursorLocked(true);
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // Escape frees the cursor, left click locks it again
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);
        else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = Mouse.current.delta.ReadValue() * sensitivity;

        // Left/right: rotate the whole player
        if (playerBody != null)
            playerBody.Rotate(Vector3.up * delta.x);

        // Up/down: tilt only the camera
        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}