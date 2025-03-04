using UnityEngine;

namespace MainGame
{
    [RequireComponent(typeof(Collider))]
    public class HockeyStickController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("How high above the ice the stick hovers")]
        public float hoverHeight = 0.1f;
        
        [Tooltip("How fast the stick moves to the cursor position")]
        public float movementSpeed = 10f;
        
        [Tooltip("How far from player the stick can move")]
        public float maxDistanceFromPlayer = 2f;
        
        [Header("Physics Settings")]
        [Tooltip("How much force to apply when hitting the puck")]
        public float hitForce = 5f;
        
        [Tooltip("Layer that contains the ice/playing surface")]
        public LayerMask groundLayer;
        
        [Tooltip("Layer that contains the puck")]
        public LayerMask puckLayer;
        
        // Reference to the player this stick belongs to
        public HockeyPlayer player;
        
        // Reference to the player's transform instead of HockeyPlayer component
        // This helps when player prefab doesn't have HockeyPlayer component properly set
        [Header("Player Reference")]
        public Transform playerTransform;
        
        // Reference to the camera
        private Camera mainCamera;
        
        // The target position to move towards
        private Vector3 targetPosition;
        
        // The original relative position of the stick to the player
        private Vector3 originalLocalPosition;

        [Header("Vertical Control")]
        [Tooltip("Maximum angle the stick can be raised")]
        public float maxVerticalAngle = 45f;
        
        [Tooltip("Minimum angle the stick can be lowered")]
        public float minVerticalAngle = -15f;
        
        private float currentVerticalAngle = 0f;

        [Header("Stick Position")]
        public Vector3 stickOffset = new Vector3(0.4f, 0.6f, 0.3f); // Position relative to player
        public Vector3 stickRotation = new Vector3(0f, 0f, -90f); // Default vertical orientation
        public float baseHeight = 0.6f; // Higher base height for one-handed position
        public float maxHeightAdjustment = 0.3f; // Maximum height the stick can raise
        public float bladeHeight = 0.02f; // Height of blade from ground (nearly touching)

        private Vector3 baseWorldPosition;
        private Quaternion baseWorldRotation;

        [Header("Stick Control")]
        public float minStickLength = 1f;    // Minimum distance from player
        public float maxStickLength = 3f;    // Maximum distance from player
        public float currentStickLength;      // Current extension length
        public float stickExtendSpeed = 2f;   // How fast stick extends/retracts
        public float rotationSpeed = 100f;    // Rotation speed with mouse wheel
        public float maxSideAngle = 60f;     // Maximum angle for side movement
        public float heightMultiplier = 0.2f; // How much height increases with length

        private float currentRotation = 0f;   // Current rotation angle
        private float targetLength;           // Target length for smooth movement

        [Header("Stick-Puck Interaction")]
        public float maxHitForce = 15f;      // Maximum force when stick is moved quickly
        public float minHitForce = 2f;       // Minimum force when stick barely touches puck
        public float velocityMultiplier = 2f; // How much stick velocity affects hit force
        public float dragThreshold = 0.5f;    // Velocity threshold for dragging vs hitting
        public float dragStrength = 5f;       // How strongly the stick "pulls" the puck when moving slowly
        
        private Vector3 previousPosition;    // Position from previous frame to calculate velocity
        private Vector3 currentVelocity;     // Current stick velocity
        private Rigidbody attachedPuck;      // Reference to a puck being dragged by the stick
        private Vector3 lastHitDirection;    // Direction of last hit for visual feedback

        [Header("Mouse Control Settings")]
        public float horizontalSensitivity = 2f;  // Sensitivity for side-to-side movement
        public float verticalSensitivity = 2f;    // Sensitivity for forward-backward movement
        public float maxHorizontalDistance = 1.5f; // Maximum distance stick can move horizontally from center
        public float minForwardDistance = 0.5f;   // Minimum forward distance (when pulled back)
        public float maxForwardDistance = 2.5f;   // Maximum forward distance (when pushed forward)
        public float maxRaiseHeight = 0.8f;       // Maximum height the stick can be raised
        public float stickHeightCurve = 2f;       // How quickly stick raises with extension (higher = more curve)

        private Vector2 stickPosition = Vector2.zero; // Normalized position of stick (-1,1) on both axes
        private float targetHeight = 0f;             // Target height of the stick

        private void Start()
        {
            mainCamera = Camera.main;
            
            // Get player reference if not assigned
            if (playerTransform == null)
            {
                playerTransform = transform.parent;
                Debug.Log("Automatically set player transform to parent: " + 
                    (playerTransform != null ? playerTransform.name : "NULL"));
            }
            
            // Initial setup
            if (player == null && playerTransform != null)
            {
                player = playerTransform.GetComponent<HockeyPlayer>();
                Debug.Log("Trying to find HockeyPlayer component: " + 
                    (player != null ? "Found" : "Not found"));
            }
            
            // Make sure the collider is set to trigger
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false; // We want physical interactions
            }

            currentStickLength = minStickLength;
            targetLength = currentStickLength;
            
            // Debug to verify mouse input is working
            Debug.Log("Hockey stick controller initialized. Move your mouse to control the stick.");

            previousPosition = transform.position;

            // Set proper hockey stick orientation - blade on ice AWAY from player, shaft extending back toward player
            transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            Debug.Log($"Initial stick rotation corrected with blade away from player: {transform.localRotation.eulerAngles}");
        }
        
        private void Update()
        {
            Transform activeTransform = player != null ? player.transform : playerTransform;
            
            if (activeTransform == null || mainCamera == null)
            {
                Debug.LogWarning("Missing references in HockeyStickController!");
                return;
            }

            // Get mouse input
            float mouseX = Input.GetAxis("Mouse X") * horizontalSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * verticalSensitivity;
            
            // Update stick position based on mouse movement (-1 to 1 range)
            stickPosition.x = Mathf.Clamp(stickPosition.x + mouseX * Time.deltaTime, -1f, 1f);
            stickPosition.y = Mathf.Clamp(stickPosition.y + mouseY * Time.deltaTime, -1f, 1f);
            
            // Calculate stick parameters based on position
            // Forward distance: pulled back (Y=-1) = minimum, pushed forward (Y=1) = maximum
            float forwardDistance = Mathf.Lerp(minForwardDistance, maxForwardDistance, (stickPosition.y + 1f) * 0.5f);
            
            // Horizontal offset: -1 = left, 0 = center, 1 = right
            float horizontalOffset = stickPosition.x * maxHorizontalDistance;
            
            // Stick height: increases as we extend further forward, using a curve for better feel
            // But keep it low enough to touch the ground
            targetHeight = Mathf.Pow((stickPosition.y + 1f) * 0.5f, stickHeightCurve) * maxRaiseHeight;
            
            // Calculate base position near player's feet - position where the player holds the end of the stick
            Vector3 basePosition = activeTransform.position + 
                             activeTransform.right * 0.5f +
                             Vector3.up * baseHeight;
            
            // Calculate position for the blade end of stick with blade touching the ground
            Vector3 targetPos = basePosition 
                + activeTransform.forward * (forwardDistance + 1.0f) // Make sure blade is away from player 
                + activeTransform.right * horizontalOffset
                + Vector3.up * bladeHeight; // Keep blade very close to ice
            
            // Move stick toward target position - this is the BLADE position
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * movementSpeed);
            
            // Blade should always point in the player's forward direction
            Vector3 bladeDirection = activeTransform.forward;
            
            // Shaft direction - should point FROM THE BLADE back toward the player's hands
            Vector3 shaftDirection = (basePosition - transform.position).normalized;
            
            // Don't adjust blade direction based on mouse X input - keep it pointing forward
            // bladeDirection remains unchanged
            
            // Calculate rotation to align stick correctly
            // This makes the stick's "up" vector (green line/shaft) point BACK toward the player
            // And the "forward" vector (blue line/blade) point in player's forward direction
            Quaternion targetRotation = Quaternion.LookRotation(bladeDirection, -shaftDirection);
            
            // Apply a fixed correction to account for model orientation
            targetRotation *= Quaternion.Euler(-90f, 0f, 0f);
            
            // Smoothly rotate the stick
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * movementSpeed);
            
            // Calculate stick velocity for puck interactions
            currentVelocity = (transform.position - previousPosition) / Time.deltaTime;
            previousPosition = transform.position;
            
            // Handle attached puck
            if (attachedPuck != null)
            {
                // Calculate target position slightly in front of the stick blade
                Vector3 puckTargetPos = transform.position + transform.forward * 0.3f;
                
                // Direction to move the puck
                Vector3 directionToPuck = puckTargetPos - attachedPuck.position;
                
                // If puck gets too far, detach it
                if (directionToPuck.magnitude > 1.2f)
                {
                    attachedPuck = null;
                }
                else
                {
                    // Apply force to guide the puck to follow the stick
                    float dragMultiplier = 5f - 3f * (directionToPuck.magnitude / 1.2f); // Less force when far away
                    attachedPuck.AddForce(directionToPuck * dragStrength * dragMultiplier, ForceMode.Acceleration);
                    
                    // If stick is moving fast, transfer some of that momentum
                    if (currentVelocity.magnitude > 1.5f)
                    {
                        attachedPuck.AddForce(currentVelocity * 0.3f, ForceMode.Acceleration);
                    }
                }
            }
            
            // Debug visualization - improved for clarity
            Debug.DrawLine(basePosition, transform.position, Color.yellow);         // Line from player to stick
            Debug.DrawRay(transform.position, transform.forward * 0.5f, Color.blue); // Blade direction
            Debug.DrawRay(transform.position, transform.up * 1.2f, Color.green);     // Shaft direction toward player
            Debug.DrawRay(transform.position, transform.right * 0.3f, Color.red);    // Side orientation
        }

        private Vector3 GetMouseWorldPosition()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.up * stickOffset.y);
            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            return transform.position;
        }

        private void UpdateStickTransform()
        {
            if (player != null)
            {
                // Position the stick slightly in front of the player, on the ground
                Vector3 newPosition = player.transform.position + 
                    (player.transform.right * stickOffset.x) +
                    (Vector3.up * 0.1f) + // Blade should be on the ice
                    (player.transform.forward * (stickOffset.z + 1.5f)); // Blade out front

                transform.position = newPosition;
                
                // Calculate rotation for blade to be on ice, shaft pointing back to player
                Vector3 shaftDirection = player.transform.position - newPosition;
                Quaternion lookRotation = Quaternion.LookRotation(player.transform.right, -shaftDirection);
                
                // Apply additional rotation to match model orientation
                transform.rotation = lookRotation * Quaternion.Euler(-90f, 0f, 0f);
            }
            else if (playerTransform != null)
            {
                Vector3 newPosition = playerTransform.position + 
                    (playerTransform.right * stickOffset.x) +
                    (Vector3.up * stickOffset.y) +
                    (playerTransform.forward * stickOffset.z);

                transform.position = newPosition;
                transform.rotation = Quaternion.Euler(0f, playerTransform.eulerAngles.y, -90f);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Check if we hit a puck
            if (((1 << collision.gameObject.layer) & puckLayer) != 0)
            {
                Rigidbody puckRb = collision.rigidbody;
                
                if (puckRb != null)
                {
                    // Calculate velocity magnitude for force scaling
                    float velocityMag = currentVelocity.magnitude;
                    
                    // Store the last hit direction for visualization
                    lastHitDirection = (collision.contacts[0].point - transform.position).normalized;
                    
                    // If the stick is moving slowly, try to "carry" the puck
                    if (velocityMag < dragThreshold)
                    {
                        // Try to attach the puck to the stick for dragging
                        attachedPuck = puckRb;
                    }
                    else
                    {
                        // If moving fast, hit the puck with force
                        // Scale force based on stick movement speed
                        float force = Mathf.Clamp(velocityMag * velocityMultiplier, minHitForce, maxHitForce);
                        
                        // Apply force to the puck in the direction of the hit
                        Vector3 forceVector = lastHitDirection * force;
                        
                        // Add some upward force to make it look more realistic
                        forceVector += Vector3.up * 0.2f;
                        
                        Debug.Log($"Hit puck with force: {force:F1}, velocity: {velocityMag:F1}");
                        puckRb.AddForce(forceVector, ForceMode.Impulse);
                    }
                }
            }
        }
        
        private void OnCollisionStay(Collision collision)
        {
            // Check if we're touching a puck
            if (((1 << collision.gameObject.layer) & puckLayer) != 0)
            {
                Rigidbody puckRb = collision.rigidbody;
                
                // If moving slowly, try to take control of the puck
                if (currentVelocity.magnitude < dragThreshold && puckRb != null)
                {
                    attachedPuck = puckRb;
                }
            }
        }
        
        private void OnCollisionExit(Collision collision)
        {
            // When no longer touching the puck, apply a small force in the direction we were moving
            if (((1 << collision.gameObject.layer) & puckLayer) != 0)
            {
                Rigidbody puckRb = collision.rigidbody;
                
                if (puckRb != null && puckRb == attachedPuck)
                {
                    // Apply a gentle push in the direction the stick is moving
                    puckRb.AddForce(currentVelocity * 0.5f, ForceMode.Impulse);
                    
                    // Clear the attached puck reference
                    attachedPuck = null;
                }
            }
        }
    }
}