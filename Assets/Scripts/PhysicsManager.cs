using UnityEngine;

namespace MainGame
{
    public class PhysicsManager : MonoBehaviour
    {
        [Header("Physics Materials")]
        public PhysicsMaterial iceMaterial;
        public PhysicsMaterial puckMaterial;
        public PhysicsMaterial stickMaterial;
        
        [Header("Physics Settings")]
        [Range(0.001f, 0.1f)] public float iceMinFriction = 0.01f;
        [Range(0.001f, 0.1f)] public float iceMaxFriction = 0.03f;
        [Range(0.001f, 0.1f)] public float stickIceFriction = 0.02f;
        [Range(0.001f, 0.5f)] public float puckFriction = 0.05f;
        
        // Singleton instance
        public static PhysicsManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePhysicsMaterials();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void InitializePhysicsMaterials()
        {
            // Create Ice material if not set
            if (iceMaterial == null)
            {
                iceMaterial = new PhysicsMaterial("Ice");
                iceMaterial.dynamicFriction = iceMinFriction;
                iceMaterial.staticFriction = iceMaxFriction;
                iceMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
                iceMaterial.bounciness = 0.1f;
                iceMaterial.bounceCombine = PhysicsMaterialCombine.Multiply;
            }
            
            // Create Puck material if not set
            if (puckMaterial == null)
            {
                puckMaterial = new PhysicsMaterial("Puck");
                puckMaterial.dynamicFriction = puckFriction;
                puckMaterial.staticFriction = puckFriction * 1.2f;
                puckMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
                puckMaterial.bounciness = 0.4f;
                puckMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            }
            
            // Create Stick material if not set
            if (stickMaterial == null)
            {
                stickMaterial = new PhysicsMaterial("HockeyStick");
                stickMaterial.dynamicFriction = stickIceFriction;
                stickMaterial.staticFriction = stickIceFriction * 1.1f;
                stickMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
                stickMaterial.bounciness = 0.2f;
                stickMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            }
            
            Debug.Log("Physics materials initialized");
        }
        
        // Apply proper materials to objects
        public void ApplyMaterials()
        {
            // Apply ice material to all ice objects
            GameObject[] iceObjects = GameObject.FindGameObjectsWithTag("Untagged"); // You can use a specific tag if available
            foreach (GameObject obj in iceObjects)
            {
                if (obj.layer == LayerMask.NameToLayer("Ice"))
                {
                    Collider col = obj.GetComponent<Collider>();
                    if (col != null)
                    {
                        col.material = iceMaterial;
                        Debug.Log($"Applied ice material to {obj.name}");
                    }
                }
            }
            
            // Apply materials to pucks
            Puck[] pucks = FindObjectsOfType<Puck>();
            foreach (Puck puck in pucks)
            {
                Collider col = puck.GetComponent<Collider>();
                if (col != null)
                {
                    col.material = puckMaterial;
                    Debug.Log($"Applied puck material to {puck.name}");
                    
                    // Also ensure proper Rigidbody settings
                    Rigidbody rb = puck.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rb.interpolation = RigidbodyInterpolation.Interpolate;
                        rb.mass = 0.17f; // Standard hockey puck mass
                        rb.linearDamping = 0.1f; // Reduced damping for better sliding
                        rb.angularDamping = 0.2f;
                        Debug.Log($"Updated {puck.name} physics settings");
                    }
                }
            }
        }
        
        // Apply physics for hockey stick specifically
        public void ApplyStickPhysics(HockeyStickController stick)
        {
            if (stick == null) return;
            
            // Apply stick material
            Collider col = stick.GetComponent<Collider>();
            if (col != null)
            {
                col.material = stickMaterial;
                col.isTrigger = false; // Ensure it's not a trigger
                
                // For mesh colliders, ensure they're convex
                if (col is MeshCollider meshCol)
                {
                    meshCol.convex = true;
                }
            }
            
            // Setup stick rigidbody
            Rigidbody rb = stick.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = stick.gameObject.AddComponent<Rigidbody>();
            }
            
            rb.mass = 0.3f;
            rb.useGravity = false; // Managed by script, not physics
            rb.isKinematic = false; // Let physics act on it
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            
            // Allow rotation around Y axis only
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            
            Debug.Log($"Applied stick physics to {stick.name}");
        }
    }
}
