using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class Puck : MonoBehaviour
{
    [Header("Physics Settings")]
    [SerializeField] private float maxShootForce = 2000f;
    [SerializeField] private float stickOffset = 1.5f;
    [SerializeField] private float puckHeight = 0.25f; // Height of the puck
    [SerializeField] private float puckRadius = 0.375f; // Standard hockey puck radius
    [SerializeField] private float bounceForce = 0.8f; // Reduced bounce force
    [SerializeField] private float maxVelocity = 20f;
    [SerializeField] private Vector3 startPosition;
    private float lastBounceTime;
    private const float MIN_BOUNCE_INTERVAL = 0.1f;

    private Rigidbody rb;
    private CapsuleCollider puckCollider;
    public bool isControlled { get; private set; }
    private Transform controllingPlayer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        puckCollider = GetComponent<CapsuleCollider>();

        // Configure Rigidbody with mesh-friendly physics settings
        rb.mass = 0.17f;
        rb.linearDamping = 1f; // Increased drag for better control
        rb.angularDamping = 1f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.sleepThreshold = 0.0f; // Prevent the puck from sleeping

        // Configure Collider for mesh collision
        puckCollider.direction = 1;
        puckCollider.height = puckHeight;
        puckCollider.radius = puckRadius;
        puckCollider.isTrigger = false;
        puckCollider.material = CreatePuckPhysicMaterial();

        startPosition = transform.position;
    }

    private PhysicsMaterial CreatePuckPhysicMaterial()
    {
        PhysicsMaterial puckMaterial = new PhysicsMaterial("PuckMaterial");
        puckMaterial.dynamicFriction = 0.05f; // Reduced friction
        puckMaterial.staticFriction = 0.05f;
        puckMaterial.bounciness = 0.5f;
        puckMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        puckMaterial.bounceCombine = PhysicsMaterialCombine.Average;
        return puckMaterial;
    }

    void FixedUpdate()
    {
        if (isControlled && controllingPlayer != null)
        {
            // Keep puck at a fixed height when controlled
            Vector3 targetPos = controllingPlayer.position + controllingPlayer.forward * stickOffset;
            targetPos.y = puckHeight / 2f; // Keep puck at proper height
            transform.position = targetPos;
            transform.rotation = Quaternion.Euler(0, controllingPlayer.eulerAngles.y, 0);
        }
        else if (!isControlled)
        {
            // Ensure puck stays at minimum height
            if (transform.position.y < puckHeight / 2f)
            {
                Vector3 pos = transform.position;
                pos.y = puckHeight / 2f;
                transform.position = pos;
            }
        }

        // Clamp velocity to prevent excessive speed
        if (rb.linearVelocity.magnitude > maxVelocity)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxVelocity;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!rb.isKinematic)
        {
            // Get the collision normal and current velocity
            ContactPoint contact = collision.GetContact(0);
            Vector3 normal = contact.normal;
            Vector3 currentVelocity = rb.linearVelocity;

            // Calculate reflection with mesh-aware bounce
            Vector3 reflection = Vector3.Reflect(currentVelocity, normal.normalized);
            
            // Apply velocity with dampening
            float dampening = Mathf.Clamp01(bounceForce);
            rb.linearVelocity = reflection * dampening;

            // Add slight upward force to prevent sticking
            rb.AddForce(Vector3.up * 0.1f, ForceMode.Impulse);

            // Prevent excessive bouncing
            if (currentVelocity.magnitude < 1f)
            {
                rb.linearVelocity = Vector3.zero;
            }

            lastBounceTime = Time.time;
            
            // Debug visualization
            Debug.DrawRay(contact.point, normal, Color.red, 1f);
            Debug.DrawRay(contact.point, reflection, Color.green, 1f);
        }
        else if (collision.gameObject.CompareTag("Net"))
        {
            // Stop the puck when it hits the net
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void Shoot(float powerPercentage, Vector3 direction)
    {
        isControlled = false;
        controllingPlayer = null;
        rb.isKinematic = false;
        transform.parent = null;

        // Apply force slightly upward to prevent ground sticking
        Vector3 shootDirection = direction + Vector3.up * 0.1f;
        float force = maxShootForce * powerPercentage;
        rb.AddForce(shootDirection.normalized * force, ForceMode.Impulse);
    }

    public void AttachToPlayer(Transform player)
    {
        Debug.Log($"Attaching puck to player: {player.name}");
        isControlled = true;
        controllingPlayer = player;
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        // Position the puck relative to the player
        transform.position = player.position + player.forward * stickOffset;
        transform.position = new Vector3(transform.position.x, puckHeight / 2f, transform.position.z);
    }

    public void ResetPosition()
    {
        transform.position = startPosition;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
