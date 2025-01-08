using UnityEngine;

public class PuckController : MonoBehaviour
{
    public float friction = 0.98f; // Simulate ice surface
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Reduce speed over time to simulate ice friction
        rb.linearVelocity *= friction;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Add logic for collision with players or goals
        if (collision.gameObject.CompareTag("Player"))
        {
            // Example: Transfer force from player to puck
            Vector3 force = collision.relativeVelocity * 1.5f;
            rb.AddForce(force, ForceMode.Impulse);
        }
    }
}
