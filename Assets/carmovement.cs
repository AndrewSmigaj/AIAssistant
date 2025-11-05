using UnityEngine;
using UnityEngine.InputSystem;

public class carmovement : MonoBehaviour
{
    [Header("Control Settings")]
    [SerializeField] private float moveSpeed = 10f;         // Forward/backward movement speed
    [SerializeField] private float turnSpeed = 100f;        // Rotation speed (degrees per second)
    [SerializeField] private float acceleration = 5f;       // How quickly the car accelerates
    [SerializeField] private float deceleration = 5f;       // How quickly the car decelerates

    [Header("Circle Movement Settings (Auto Mode)")]
    [SerializeField] private bool autoCircleMode = false;   // Toggle automatic circle driving
    [SerializeField] private float circleRadius = 10f;      // Radius of the circle in auto mode

    // Input values
    private Vector2 moveInput;
    private float currentSpeed = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (autoCircleMode)
        {
            // Automatic circle movement
            transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
            float calculatedTurnSpeed = (moveSpeed / circleRadius) * Mathf.Rad2Deg;
            transform.Rotate(Vector3.up, calculatedTurnSpeed * Time.deltaTime);
        }
        else
        {
            // Manual keyboard control
            HandleMovement();
        }
    }

    private void HandleMovement()
    {
        // Calculate target speed based on input
        float targetSpeed = moveInput.y * moveSpeed;

        // Smoothly interpolate to target speed
        if (Mathf.Abs(targetSpeed) > 0.01f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
        }

        // Move forward/backward
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);

        // Rotate left/right (only when moving)
        if (Mathf.Abs(currentSpeed) > 0.1f)
        {
            float turn = moveInput.x * turnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, turn);
        }
    }

    // Called by Player Input component when Move action is triggered
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    // Alternative method for direct input (works with both old and new input systems)
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
}
