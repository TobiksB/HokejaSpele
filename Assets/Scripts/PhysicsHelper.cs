using UnityEngine;

namespace MainGame
{
    public class PhysicsHelper : MonoBehaviour
    {
        public static void EnsureRigidbodyExists(GameObject gameObject, bool configureForHockeyStick = false)
        {
            if (gameObject == null) return;
            
            Rigidbody rb = gameObject.GetComponent<Rigidbody>();
            
            // If rigidbody doesn't exist, add one
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                Debug.Log($"Added Rigidbody to {gameObject.name}");
                
                // Configure it if requested
                if (configureForHockeyStick)
                {
                    rb.useGravity = false;
                    rb.mass = 0.3f;
                    rb.linearDamping = 0.2f;
                    rb.angularDamping = 0.1f;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                    
                    // Apply physics material
                    Collider[] colliders = gameObject.GetComponents<Collider>();
                    if (colliders.Length > 0)
                    {
                        PhysicsMaterial stickPhysicsMat = new PhysicsMaterial("SlipperyStick");
                        stickPhysicsMat.dynamicFriction = 0.02f;
                        stickPhysicsMat.staticFriction = 0.02f;
                        stickPhysicsMat.frictionCombine = PhysicsMaterialCombine.Minimum;
                        
                        foreach (Collider col in colliders)
                        {
                            col.material = stickPhysicsMat;
                            col.isTrigger = false;
                            
                            // Make sure mesh colliders are convex
                            if (col is MeshCollider meshCol)
                            {
                                meshCol.convex = true;
                            }
                        }
                    }
                    
                    Debug.Log($"Configured rigidbody on {gameObject.name} for hockey stick physics");
                }
            }
        }
    }
}
