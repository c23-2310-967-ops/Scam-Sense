using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;
    private Vector3 moveDirection;
    private bool isGrounded;

    [Header("Movement")]
    public float speed = 5f;
    public float gravity = -9.8f;
    public float jumpHeight = 1.5f;

    [Header("Stairs")]
    [Tooltip("Tallest step the player can climb. Set slightly above ONE stair's height.")]
    public float stepHeight = 0.5f;
    [Tooltip("Max slope angle the player can walk up.")]
    public float slopeLimit = 45f;
    [Tooltip("Layers that count as stairs/ground. Leave on Everything if unsure.")]
    public LayerMask groundMask = ~0;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Built-in step handling (keeps working as a backup to the step assist below)
        controller.stepOffset = Mathf.Min(stepHeight, controller.height + 2f * controller.radius - 0.01f);
        controller.slopeLimit = slopeLimit;
        controller.minMoveDistance = 0f; // helps the controller catch on stair edges
    }

    void Update()
    {
        // Do nothing while the game is paused
        if (Time.timeScale == 0f) return;

        isGrounded = controller.isGrounded;

        // Keep the player stuck to the ground (helps with stairs and slopes)
        if (isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = -2f;
        }

        // Horizontal velocity from input
        Vector3 horizontal = transform.TransformDirection(Vector3.ClampMagnitude(moveDirection, 1f)) * speed;

        // Lift the player over a stair if one is blocking the way
        if (isGrounded && playerVelocity.y <= 0f)
        {
            TryStepUp(horizontal);
        }

        // Apply gravity
        playerVelocity.y += gravity * Time.deltaTime;

        // ONE Move call per frame: horizontal + vertical together
        Vector3 velocity = horizontal + Vector3.up * playerVelocity.y;
        controller.Move(velocity * Time.deltaTime);
    }

    // Receive the inputs from InputManager.cs
    public void ProcessMove(Vector2 input)
    {
        moveDirection = new Vector3(input.x, 0f, input.y);
    }

    // Call this from InputManager.cs when the jump button is pressed
    public void Jump()
    {
        if (isGrounded && Time.timeScale > 0f)
        {
            // v = sqrt(h * -2 * g)
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    // Step assist: if something low is in front of us and the space above it is free,
    // raise the player to the top of that step.
    private void TryStepUp(Vector3 horizontal)
    {
        Vector3 dir = new Vector3(horizontal.x, 0f, horizontal.z);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Bounds b = controller.bounds;
        float bottom = b.min.y;
        float ahead = controller.radius + 0.15f;

        // Look DOWN at the ground just in front of the player.
        // This works even on open stairs that have no risers (gaps between treads).
        Vector3 probeOrigin = new Vector3(b.center.x, bottom + stepHeight + 0.05f, b.center.z) + dir * ahead;
        if (!Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, stepHeight + 0.1f, groundMask, QueryTriggerInteraction.Ignore))
            return;

        // Only step onto reasonably flat surfaces (a tread), not steep edges
        if (hit.normal.y < 0.7f) return;

        float rise = hit.point.y - bottom;
        if (rise > 0.02f && rise <= stepHeight)
        {
            controller.Move(Vector3.up * (rise + 0.02f));
        }
    }
}