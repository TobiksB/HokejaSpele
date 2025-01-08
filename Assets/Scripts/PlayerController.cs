using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float acceleration = 8f;
    public float deceleration = 6f;
    public Rigidbody rb;
    private Vector3 velocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 targetVelocity = new Vector3(horizontal, 0, vertical).normalized * moveSpeed;

        // Smoothly accelerate/decelerate
        velocity = Vector3.Lerp(velocity, targetVelocity, acceleration * Time.deltaTime);

        // Add a friction-like deceleration when no input is detected
        if (targetVelocity.magnitude < 0.1f)
            velocity = Vector3.Lerp(velocity, Vector3.zero, deceleration * Time.deltaTime);

        // Rotate player to face movement direction
        if (velocity.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }
    }

    void FixedUpdate()
    {
        // Apply velocity to the rigidbody
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }
}
