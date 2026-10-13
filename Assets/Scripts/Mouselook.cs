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
    float sensitivityMultiplier = 1f;

    void Start()
    {
        if (playerBody == null && transform.parent != null)
            playerBody = transform.parent;

        if (lockCursorOnStart && !InteractionGate.UIOpen) SetCursorLocked(true);
    }

    // Re-read the Settings sensitivity whenever look control comes back (e.g. after the pause menu).
    void OnEnable() => sensitivityMultiplier = GameAudioSettings.MouseSensitivity;

    void Update()
    {
        if (Mouse.current == null) return;

        // Without a PauseMenu in the scene: Escape frees the cursor, left click locks it again.
        // With one, Escape opens the menu and the menu owns the cursor.
        if (PauseMenu.Instance == null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);
        else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current.leftButton.wasPressedThisFrame &&
                 !InteractionGate.UIOpen)
            SetCursorLocked(true);

        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = Mouse.current.delta.ReadValue() * sensitivity * sensitivityMultiplier;

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