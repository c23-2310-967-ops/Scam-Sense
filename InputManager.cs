using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;

    private PlayerMotor motor;

    void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        motor = GetComponent<PlayerMotor>();

        // Call Jump() on the motor whenever the Jump action is performed
        onFoot.Jump.performed += ctx => motor.Jump();
    }

    // Update (not FixedUpdate) because PlayerMotor moves in Update
    void Update()
    {
        // tell playermotor to move using the value from our movement action.
        motor.ProcessMove(onFoot.Movement.ReadValue<Vector2>());
    }

    private void OnEnable()
    {
        onFoot.Enable();
    }

    private void OnDisable()
    {
        onFoot.Disable();
    }
}