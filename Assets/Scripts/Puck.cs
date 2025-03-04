using UnityEngine;

namespace MainGame
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Puck : MonoBehaviour
    {
        [Header("Puck Settings")]
        public float mass = 0.17f; // NHL puck is about 170 grams
        public float drag = 0.05f; // REDUCED for better sliding
        public float angularDrag = 0.1f; // REDUCED for better sliding
        public float iceSliding = 0.01f; // REDUCED - Very low friction on ice
        public float maxSpeed = 30f; // Maximum speed the puck can travel
        
        [Header("Status")]
        public bool isControlled = false; // Whether a player is carrying the puck
        
        [Header("Reset Settings")]
        public Vector3 defaultPosition = Vector3.zero; // Default position to reset to (usually center ice)
        public float resetHeight = 0.05f;             // Height above ice when resetting
        
        private Rigidbody rb;
        private Collider puckCollider;
        private Transform carriedBy;
        private Vector3 carryOffset = new Vector3(0, 0.05f, 1f); // Position relative to player when carried
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            puckCollider = GetComponent<Collider>();
            
            // Force-configure rigidbody for realistic puck physics
            rb.mass = mass;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.constraints = RigidbodyConstraints.None; // Allow full movement
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Better collision detection
            rb.interpolation = RigidbodyInterpolation.Interpolate; // Smoother visual movement
            rb.useGravity = true; // Make sure gravity is enabled
            
            // Ensure the collider is NOT a trigger
            puckCollider.isTrigger = false;
            
            // Configure collider for better interactions
            if (puckCollider is SphereCollider)
            {
                // Adjust sphere collider if needed
                ((SphereCollider)puckCollider).radius = 0.038f; // NHL puck radius (3.8 cm)
            }
            
            // Force-create and apply physics material
            CreateAndApplyPuckPhysicsMaterial();
            
            Debug.Log("Puck physics configured for better sliding");
        }
        
        private void CreateAndApplyPuckPhysicsMaterial()
        {
            PhysicsMaterial puckMaterial = new PhysicsMaterial("PuckMaterial");
            puckMaterial.dynamicFriction = iceSliding;
            puckMaterial.staticFriction = iceSliding;
            puckMaterial.bounciness = 0.4f;
            puckMaterial.frictionCombine = PhysicsMaterialCombine.Minimum; // ALWAYS use minimum friction
            puckMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            
            puckCollider.material = puckMaterial;
            Debug.Log("Applied low-friction physics material to puck");
        }
        
        private void FixedUpdate()
        {
            // Force the puck to stay on the ice - don't let it bounce up
            if (rb.position.y > 0.1f)
            {
                Vector3 pos = rb.position;
                pos.y = 0.05f; // Keep puck very close to ice
                rb.MovePosition(pos);
                
                // Zero out vertical velocity
                Vector3 vel = rb.linearVelocity;
                vel.y = 0;
                rb.linearVelocity = vel;
            }
            
            // Limit maximum speed if needed
            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
            
            // If being carried by a player, maintain position
            if (isControlled && carriedBy != null)
            {
                Vector3 targetPos = carriedBy.position + carriedBy.forward * carryOffset.z +
                                    Vector3.up * carryOffset.y;
                
                // Move puck to follow player
                rb.MovePosition(Vector3.Lerp(rb.position, targetPos, 0.3f));
                
                // Keep puck velocity aligned with player's movement
                Vector3 playerVelocity = carriedBy.GetComponent<Rigidbody>()?.linearVelocity ?? Vector3.zero;
                rb.linearVelocity = playerVelocity;
            }
            
            // Add a small force in the direction of movement to compensate for excessive friction
            if (rb.linearVelocity.magnitude > 0.5f && rb.linearVelocity.magnitude < 5f)
            {
                rb.AddForce(rb.linearVelocity.normalized * 0.1f, ForceMode.Acceleration);
            }
        }
        
        public void AttachToPlayer(Transform player)
        {
            carriedBy = player;
            isControlled = true;
            
            // Make puck kinematic when controlled by player
            rb.isKinematic = true;
            
            Debug.Log($"Puck attached to {player.name}");
        }
        
        public void Shoot(float power, Vector3 direction)
        {
            isControlled = false;
            carriedBy = null;
            
            // Make puck dynamic again for physics
            rb.isKinematic = false;
            
            // Apply shooting force
            float shootForce = 10f + (power * 20f); // Scale by shot power
            rb.AddForce(direction * shootForce, ForceMode.Impulse);
            
            Debug.Log($"Puck shot with power {power}, force: {shootForce}");
        }
        
        public void Release(Vector3 initialVelocity)
        {
            isControlled = false;
            carriedBy = null;
            
            // Make puck dynamic again for physics
            rb.isKinematic = false;
            
            // Apply initial velocity
            rb.linearVelocity = initialVelocity;
            
            Debug.Log("Puck released");
        }
        
        /// <summary>
        /// Resets the puck to the default position (usually center ice)
        /// Called when a goal is scored or game is restarted
        /// </summary>
        public void ResetPosition()
        {
            // Release from any player control
            isControlled = false;
            carriedBy = null;
            
            // Reset physics state
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // Move back to center ice (or specified default position)
            Vector3 resetPos = defaultPosition;
            resetPos.y = resetHeight; // Make sure it's slightly above the ice
            transform.position = resetPos;
            
            Debug.Log("Puck reset to default position");
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            // Check for collision with hockey stick
            HockeyStickController stick = collision.gameObject.GetComponent<HockeyStickController>();
            if (stick != null)
            {
                // Calculate impact force based on stick velocity
                Vector3 stickVelocity = stick.GetCurrentVelocity();
                float impactForce = stickVelocity.magnitude;
                
                Debug.Log($"Puck hit by stick with velocity: {impactForce}");
                
                // Only apply strong force if stick was moving fast enough
                if (impactForce > 1.0f)
                {
                    // Calculate direction based on stick-to-puck vector
                    Vector3 hitDirection = (transform.position - collision.contacts[0].point).normalized;
                    hitDirection.y = 0; // Keep puck on ice
                    
                    // Apply a strong impulse force
                    float forceMagnitude = Mathf.Clamp(impactForce * 2f, 2f, 15f);
                    rb.AddForce(hitDirection * forceMagnitude, ForceMode.Impulse);
                    
                    Debug.Log($"Applied force: {forceMagnitude} in direction: {hitDirection}");
                }
                else
                {
                    // For gentle touches, just align puck with stick movement
                    rb.linearVelocity = stickVelocity * 0.8f;
                }
            }
            
            // Play sound effects based on collision velocity
            float collisionForce = collision.relativeVelocity.magnitude;
            Debug.Log($"Puck collision with force: {collisionForce}");
        }
    }
}
