using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;         // Movement speed
    public float jumpForce = 10f;        // Jump force
    public float rotationSpeed = 700f;   // Rotation speed
    public float uprightForce = 10f;     // Force to keep player upright

    private Rigidbody rb;
    private float horizontalInput;
    private float verticalInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
        MaintainUpright();
    }

    void HandleMovement()
    {
        horizontalInput = Input.GetAxis("Horizontal");  // A/D or Left/Right arrow keys
        verticalInput = Input.GetAxis("Vertical");      // W/S or Up/Down arrow keys

        // Create a movement vector based on user input
        Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput) * moveSpeed;

        // Apply the movement to the Rigidbody
        rb.AddForce(movement, ForceMode.VelocityChange);
    }

    void HandleRotation()
    {
        // Get the current rotation angle around the Y axis
        float turn = horizontalInput * rotationSpeed * Time.deltaTime;

        // Apply rotation around the Y axis
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
    }

    void MaintainUpright()
    {
        // Apply an upward force to maintain the player's upright position
        if (transform.up.y < 0.5f)  // Detect if the player is leaning too much
        {
            rb.AddTorque(Vector3.up * uprightForce, ForceMode.Force);
        }
    }
}
