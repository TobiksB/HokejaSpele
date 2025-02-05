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
    
    private Rigidbody rb;
    private CapsuleCollider puckCollider;
    private bool isControlled;
    private Transform controllingPlayer;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        puckCollider = GetComponent<CapsuleCollider>();
        
        // Configure Rigidbody
        rb.mass = 0.17f; // NHL puck mass in kg
        rb.linearDamping = 0.3f;
        rb.angularDamping = 0.5f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Configure Collider
        puckCollider.direction = 1; // Y-axis
        puckCollider.height = puckHeight;
        puckCollider.radius = puckRadius;
        puckCollider.isTrigger = false;
        
        // Create and assign PhysicMaterial
        CreatePuckPhysicMaterial();
    }

    private void CreatePuckPhysicMaterial()
    {
        PhysicsMaterial puckMaterial = new PhysicsMaterial("PuckMaterial");
        puckMaterial.dynamicFriction = 0.1f;
        puckMaterial.staticFriction = 0.1f;
        puckMaterial.bounciness = 0.5f;
        puckMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        puckMaterial.bounceCombine = PhysicsMaterialCombine.Average;
        
        puckCollider.material = puckMaterial;
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
    }

    public void Shoot(float powerPercentage, Vector3 direction)
    {
        isControlled = false;
        controllingPlayer = null;
        rb.isKinematic = false;
        
        // Apply force slightly upward to prevent ground sticking
        Vector3 shootDirection = direction + Vector3.up * 0.1f;
        float force = maxShootForce * powerPercentage;
        rb.AddForce(shootDirection.normalized * force, ForceMode.Impulse);
    }

    public void AttachToPlayer(Transform player)
    {
        isControlled = true;
        controllingPlayer = player;
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
