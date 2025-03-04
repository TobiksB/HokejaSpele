using System.Collections.Generic;  // Add this for List<>
using System.Linq;
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
        public float baseHeight = 0.8f; // Lower base height to keep stick closer to ice
        public float maxHeightAdjustment = 0.3f; // Maximum height the stick can raise
        public float bladeHeight = 0.02f; // Very small gap from ground
        public float horizontalRotationFactor = 45f; // How much the stick rotates horizontally

        [Header("Physics & Ground Interaction")]
        public bool enforceGroundCollision = true; // Prevent passing through ground
        public LayerMask groundLayerMask; // Set this in the inspector to include ground/ice layers
        public float groundCheckDistance = 0.1f; // Distance to check for ground
        public float puckForceMultiplier = 2.5f; // Increased force for moving the puck
        public float minBladeHeight = 0.01f; // REDUCED - keep blade very close to ground
        public float groundRaycastOffset = 0.5f; // Distance along blade to perform additional ground checks
        public bool debugGroundDetection = true; // Show debug rays for ground detection
        public float groundDetectionHeight = 1.0f; // Lower for more accurate detection
        public float groundCheckRadius = 0.25f;  // Smaller radius for more precise detection
        public bool useConvexCollider = true;    // Use convex collider for better physics

        // New settings for improved ground handling
        [Header("Ground Handling Options")]
        public bool ignoreDefaultLayer = true; // Fixes erratic behavior by ignoring Default layer
        public float absoluteMinHeight = 0.02f; // Absolute minimum height from ground (Y=0)
        public float fixedGroundHeight = 0.0f; // Fixed ground height reference
        public bool useFixedGroundHeight = true; // Use fixed reference height instead of raycasts
        public float safeGroundOffset = 0.01f; // REDUCED - minimal safe distance from ground

        // Additional fields to help stabilize stick movement
        private Vector3 smoothedPosition;
        private Vector3 lastValidPosition;
        private float positionSmoothingFactor = 5f;
        private int framesSinceGroundDetected = 0;
        private const int MAX_FRAMES_WITHOUT_GROUND = 5;

        [Header("Stick Movement Controls")]
        public float stickExtendFactor = 1.2f; // How much the stick extends with forward movement
        public float stickRaiseFactor = 0.1f;  // REDUCED - less height increase when raising stick
        public float minExtendDistance = 0.5f; // Minimum extension distance
        public float maxExtendDistance = 2.0f; // Maximum extension distance

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

        [Header("Stability Settings")]
        public float stabilityMultiplier = 2.0f;     // Higher values = more stability but less responsiveness
        public float velocityDampening = 0.8f;      // Reduce rapid velocity changes
        public int stabilizationIterations = 3;     // Number of smoothing iterations per frame
        public float maxAllowedVelocity = 20.0f;    // Maximum velocity magnitude to prevent erratic movement
        public int stuckFrameThreshold = 5;         // Frames before detecting stick is stuck/glitching
    
        // Tracking variables for anti-erratic behavior
        private int framesInSamePosition = 0;
        private int framesWithHighVelocity = 0;
        private Vector3[] previousPositions = new Vector3[5];
        private bool needsReset = false;

        // Add new variables for absolute ground control
        [Header("Extreme Ground Control")]
        public bool forceGroundAlignment = true;     // Force stick to ground level
        public float absoluteGroundLevel = 0.01f;    // Absolute height from assumed 0 ground
        public float rinkHeight = 0.0f;              // The actual Y height of the rink surface
        public bool useRaycastsForHeight = false;    // If false, use absolute position only

        // Simplified ground detection settings
        [Header("Simplified Ground Detection")]
        public bool useSimpleGroundDetection = true;   // Use simpler, more reliable ground detection
        public float iceLevel = 0.0f;                  // Y-coordinate of the ice surface
        public float stickGroundOffset = 0.02f;        // Keep stick slightly above ice
        public bool debugIceDetection = true;          // Show debug info for ice detection

        private Rigidbody rb; // Reference to the stick's rigidbody

        // Add this new method to let the puck access the stick's velocity
        public Vector3 GetCurrentVelocity()
        {
            return currentVelocity;
        }

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

            // Set the ground layer mask if not already set
            if (groundLayerMask == 0)
            {
                groundLayerMask = LayerMask.GetMask("Ground", "Ice", "Default");
                Debug.Log($"Using default ground layers: {groundLayerMask}");
            }

            // Ensure the ground layers include Default, since most ground objects are on this layer
            if (groundLayerMask.value == 0)
            {
                groundLayerMask = LayerMask.GetMask("Ground", "Ice", "Default", "Terrain", "Floor");
                Debug.Log($"Using default ground layers: {groundLayerMask}");
            }
            
            // Debug to ensure ground detection works
            Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 10f, groundLayerMask);
            if (hit.collider != null)
            {
                Debug.Log($"Ground detection working. Hit: {hit.collider.name} at distance {hit.distance}");
            }
            else
            {
                Debug.LogWarning("No ground detected! Check layer masks and colliders.");
            }

            // Make sure we include ALL possible ground layers
            groundLayerMask = ~(LayerMask.GetMask("Player", "Puck", "Trigger"));
            Debug.Log($"Using ground layer mask: {groundLayerMask.value} (everything except Player, Puck, Trigger)");
            
            // Test ground detection right away
            DetectGround(transform.position);

            // Make sure we have the right collider settings
            Collider stickCollider = GetComponent<Collider>();
            if (stickCollider != null)
            {
                if (stickCollider is MeshCollider meshCol)
                {
                    meshCol.convex = useConvexCollider;
                    Debug.Log("Set MeshCollider to convex: " + useConvexCollider);
                }
                stickCollider.material = CreateStickPhysicsMaterial();
            }
            else
            {
                Debug.LogWarning("No collider found on hockey stick!");
            }

            // Initialize smoothing variables
            smoothedPosition = transform.position;
            lastValidPosition = transform.position;
            
            // Ensure the ground layer includes the rink
            groundLayerMask = LayerMask.GetMask("Ground", "Ice", "Default", "Rink");
            Debug.Log($"Using ground layer mask: {groundLayerMask.value}");

            // Set initial references for anti-erratic detection
            for (int i = 0; i < previousPositions.Length; i++) {
                previousPositions[i] = transform.position;
            }
            
            // Make sure all colliders involved use continuous collision detection
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                Debug.Log("Set Rigidbody collision mode to continuous dynamic");
            }
            
            // Set the layer mask to include the "Rink" layer specifically
            groundLayerMask |= LayerMask.GetMask("Rink", "Arena");
            Debug.Log($"Updated ground layer mask to include Rink: {groundLayerMask}");

            // Properly configure ground layer mask to avoid Default layer if it causes problems
            if (ignoreDefaultLayer) {
                // Just use our known-good layers
                groundLayerMask = LayerMask.GetMask("Ground", "Ice", "Rink", "Arena");
                Debug.Log($"Using SPECIFIC ground layers: {groundLayerMask.value} (avoiding Default layer)");
            } else {
                // Use the broader approach including Default
                groundLayerMask = ~(LayerMask.GetMask("Player", "Puck", "Trigger"));
                Debug.Log($"Using ALL ground layers: {groundLayerMask.value} (including Default)");
            }

            // Set the layer mask to SPECIFICALLY include the "Rink" layer
            groundLayerMask |= LayerMask.GetMask("Rink", "Arena", "Ice");
            Debug.Log($"Final ground layers: {groundLayerMask.value}");
            
            // Get the ground height if needed
            if (useFixedGroundHeight) {
                // Force ground detection on startup with multiple attempts
                bool groundFound = false;
                
                // Try specific positions for more reliable detection
                Vector3[] testPoints = new Vector3[] {
                    new Vector3(0, 10, 0),   // Center
                    new Vector3(10, 10, 10), // Corner 
                    new Vector3(-10, 10, -10), // Opposite corner
                    transform.position + Vector3.up * 10 // Current position
                };
                
                foreach (Vector3 testPoint in testPoints) {
                    if (Physics.Raycast(testPoint, Vector3.down, out RaycastHit groundHit, 20f, 
                                        LayerMask.GetMask("Rink", "Arena", "Ice", "Ground"))) {
                        fixedGroundHeight = groundHit.point.y;
                        Debug.Log($"Detected fixed ground height: {fixedGroundHeight} from {groundHit.collider.name}");
                        groundFound = true;
                        break;
                    }
                }
                
                if (!groundFound) {
                    fixedGroundHeight = 0.0f; 
                    Debug.LogWarning("Could not detect ground height! Using default of 0.0");
                }
            }

            // FORCE the stick to absolute ground level right away
            ForceToGroundLevel();
            
            // Find the ground/rink once at startup
            DetectRinkHeight();

            // Set up ground detection with priority on Ice layer
            groundLayerMask = LayerMask.GetMask("Ice");  // Start with just Ice layer
            
            // Add other ground layers as fallbacks
            groundLayerMask |= LayerMask.GetMask("Ground", "Rink", "Arena");
            
            // Only include Default if specifically requested
            if (!ignoreDefaultLayer) {
                groundLayerMask |= LayerMask.GetMask("Default");
            }
            
            Debug.Log($"Ground detection layers: {groundLayerMask.value}");
            
            // Try to detect ice level at startup
            DetectIceLevel();

            // Add a Rigidbody component if it doesn't exist
            rb = GetComponent<Rigidbody>();
            if (rb == null) {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            
            // Configure Rigidbody for better physics interactions
            rb.useGravity = false; // Don't use gravity directly
            rb.mass = 0.3f;        // Lightweight hockey stick
            rb.linearDamping = 0.2f;      // Low drag for better sliding
            rb.angularDamping = 0.1f;     // Low angular drag
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Allow rotation around Y axis only
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            
            // Create slippery physics material
            PhysicsMaterial stickPhysicsMat = new PhysicsMaterial("SlipperyStick");
            stickPhysicsMat.dynamicFriction = 0.02f;  // VERY low friction
            stickPhysicsMat.staticFriction = 0.02f;   // VERY low static friction
            stickPhysicsMat.frictionCombine = PhysicsMaterialCombine.Minimum;
            stickPhysicsMat.bounciness = 0.1f;
            stickPhysicsMat.bounceCombine = PhysicsMaterialCombine.Average;
            
            // Apply material to all colliders
            Collider[] stickColliders = GetComponents<Collider>();
            foreach (Collider c in stickColliders)
            {
                c.material = stickPhysicsMat;
                c.isTrigger = false; // Make sure it's not a trigger
                
                // If it's a mesh collider, make sure it's convex
                if (c is MeshCollider meshCol)
                {
                    meshCol.convex = true;
                }
            }

            // Ensure rb is properly initialized
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                Debug.Log("Added Rigidbody component to Hockey Stick");
            }
            
            // Configure Rigidbody immediately after ensuring it exists
            rb.useGravity = false; // Don't use gravity directly
            rb.mass = 0.3f;        // Lightweight hockey stick
            rb.linearDamping = 0.2f;      // Low drag for better sliding
            rb.angularDamping = 0.1f;     // Low angular drag
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Allow rotation around Y axis only
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private PhysicsMaterial CreateStickPhysicsMaterial()
        {
            PhysicsMaterial stickMaterial = new PhysicsMaterial("SlipperyHockeyStick");
            stickMaterial.dynamicFriction = 0.01f;      // VERY low friction for realistic sliding on ice
            stickMaterial.staticFriction = 0.01f;       // VERY low static friction to prevent sticking
            stickMaterial.frictionCombine = PhysicsMaterialCombine.Minimum; // Always use minimum friction
            stickMaterial.bounciness = 0.2f;            // Slight bounce for puck interaction
            stickMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            return stickMaterial;
        }
        
        private bool DetectGround(Vector3 position)
        {
            // Use spherecast for more reliable ground detection
            if (Physics.SphereCast(position + Vector3.up * groundDetectionHeight, groundCheckRadius, 
                                  Vector3.down, out RaycastHit hit, 
                                  groundDetectionHeight + 1f, groundLayerMask))
            {
                Debug.Log($"Ground detected at Y={hit.point.y}, object={hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                return true;
            }
            
            Debug.LogWarning($"No ground detected beneath {position}! Check ground layers and colliders.");
            return false;
        }
        
        private void Update()
        {
            // Early out if we need to reset due to erratic behavior
            if (needsReset) {
                ResetStickPosition();
                needsReset = false;
                return;
            }

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
            
            // IMPROVED EXTENSION AND RAISING:
            // - Pull back (Y=-1): stick closer to player and lower to ground
            // - Push forward (Y=1): stick extends away and raises up
            
            // Calculate extension distance - increases as you push forward (Y=1)
            float extensionRatio = (stickPosition.y + 1f) * 0.5f; // 0 to 1 range
            float extensionDistance = Mathf.Lerp(minExtendDistance, maxExtendDistance, extensionRatio);
            
            // Calculate raise height - increases with extension
            float raiseHeight = extensionRatio * stickRaiseFactor;
            
            // Calculate horizontal sweep distance
            float horizontalOffset = stickPosition.x * maxHorizontalDistance;
            
            // Calculate player's hand position
            Vector3 playerHandPosition = activeTransform.position + 
                activeTransform.right * 0.3f + 
                Vector3.up * baseHeight;
            
            // Calculate stick blade target position
            Vector3 bladePosition = playerHandPosition + 
                activeTransform.forward * extensionDistance * stickExtendFactor +
                activeTransform.right * horizontalOffset;
                
            // Important: Don't set Y position yet, we'll do ground detection first
            
            // SIMPLIFY ground detection: use fewer raycasts and add stability
            float groundHeightAtBlade = DetectGroundHeight(bladePosition);

            // Set Y position based on ground height and raise factor
            bladePosition.y = groundHeightAtBlade + (extensionRatio * stickRaiseFactor * 0.3f);

            // Apply smoothing to prevent jerky movement
            smoothedPosition = Vector3.Lerp(smoothedPosition, bladePosition, 
                Time.deltaTime * movementSpeed * positionSmoothingFactor);

            // Validate the position before applying it
            if (ValidatePosition(smoothedPosition))
            {
                transform.position = smoothedPosition;
                lastValidPosition = smoothedPosition;
            }
            else
            {
                // If invalid, use last valid position with some correction toward target
                smoothedPosition = Vector3.Lerp(lastValidPosition, bladePosition, 0.2f);
                transform.position = smoothedPosition;
            }

            // Force minimum height above ground
            EnsureMinimumHeight();

            // SIMPLIFIED rotation code for more stability
            SetStickRotation(activeTransform, extensionRatio);
            
            // Calculate stick velocity for puck interactions - use more accurate method
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
            Debug.DrawLine(playerHandPosition, transform.position, Color.yellow); // Line from player's hand to blade
            Debug.DrawRay(transform.position, transform.forward * 0.4f, Color.blue); // Blade direction
            Debug.DrawRay(transform.position, transform.up * 1.2f, Color.green);    // Shaft direction
            Debug.DrawRay(transform.position, transform.right * 0.3f, Color.red);   // Blade width direction
            
            // Add ground visualization to see where the stick should stop
            if (debugGroundDetection)
            {
                // Draw a horizontal plane at the minimum blade height
                Vector3 center = transform.position;
                center.y = minBladeHeight;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.3f;
                    Debug.DrawLine(center, center + dir, Color.cyan, Time.deltaTime);
                }
            }

            // Before applying new position, verify it's not causing erratic behavior
            if (IsPositionErratic(bladePosition)) {
                Debug.LogWarning("Detected potential erratic position, stabilizing stick");
                bladePosition = GetStabilizedPosition();
            }
            
            // Apply multi-step smoothing for more stability
            Vector3 finalPosition = bladePosition;
            for (int i = 0; i < stabilizationIterations; i++) {
                finalPosition = Vector3.Lerp(smoothedPosition, finalPosition, 1.0f / (stabilityMultiplier + i));
            }

            // Validate the final position before applying
            if (ValidatePosition(finalPosition)) {
                transform.position = finalPosition;
                
                // Update position history
                System.Array.Copy(previousPositions, 0, previousPositions, 1, previousPositions.Length - 1);
                previousPositions[0] = finalPosition;
                
                // Reset stuck counter if position changed significantly
                if (Vector3.Distance(finalPosition, previousPositions[previousPositions.Length-1]) > 0.01f) {
                    framesInSamePosition = 0;
                } else {
                    framesInSamePosition++;
                }
            } else {
                // If position invalid, use a safer position
                transform.position = GetSafePosition();
                Debug.LogWarning("Invalid position detected, using safe position");
            }
            
            // Force minimum height above ground with more aggressive ground detection
            EnsureMinimumHeightFromGround();
            
            // Clamp velocity to prevent erratic movement
            if (currentVelocity.magnitude > maxAllowedVelocity) {
                currentVelocity = currentVelocity.normalized * maxAllowedVelocity;
                framesWithHighVelocity++;
                
                if (framesWithHighVelocity > 3) {
                    Debug.LogWarning($"High velocity detected for multiple frames: {currentVelocity.magnitude}");
                    currentVelocity *= 0.5f;  // Strongly dampen if persists
                }
            } else {
                framesWithHighVelocity = 0;
            }

            // IMPORTANT: Extra safety to prevent ground clipping
            // This overrides everything else if needed
            if (transform.position.y < fixedGroundHeight + minBladeHeight) {
                Vector3 safePos = transform.position;
                safePos.y = fixedGroundHeight + minBladeHeight;
                transform.position = safePos;
                smoothedPosition.y = safePos.y;
            }

            // ADDED: Force position very close to ground after all other calculations
            EnsureStickStaysGrounded();

            // Calculate desired position without directly modifying the transform
            targetPosition = CalculateBladeTargetPosition(activeTransform);
            
            // We already have groundHeightAtBlade, don't recalculate it - use the existing value
            // or calculate a new one with a different name if really needed
            
            // Just set a target Y position slightly above ground
            targetPosition.y = groundHeightAtBlade + minBladeHeight;
            
            // Calculate direction to move
            Vector3 moveDirection = (targetPosition - transform.position);
            moveDirection.y = 0; // Do not consider vertical component for movement
            
            // Add null check before using rb
            // Check if rb is not null before using it
            if (rb != null)
            {
                // Apply forces rather than direct position changes
                if (moveDirection.magnitude > 0.01f)
                {
                    float moveForce = moveDirection.magnitude * movementSpeed;
                    rb.AddForce(moveDirection.normalized * moveForce, ForceMode.Force);
                    
                    // Limit velocity for stability
                    if (rb.linearVelocity.magnitude > maxAllowedVelocity)
                    {
                        rb.linearVelocity = rb.linearVelocity.normalized * maxAllowedVelocity;
                    }
                }
                else
                {
                    // Apply some damping when not actively moving
                    rb.linearVelocity *= 0.9f;
                }
                
                // Calculate stick velocity for puck interactions
                currentVelocity = rb.linearVelocity;
            }
            else
            {
                Debug.LogError("Rigidbody component is missing on Hockey Stick!");
            }
            
            // Set rotation more directly - we can use physics for position but rotation is better controlled directly
            SetStickRotation(activeTransform, extensionRatio);
            
            // Calculate stick velocity for puck interactions
            currentVelocity = rb.linearVelocity;
        }

        private Vector3 CalculateBladeTargetPosition(Transform activeTransform)
        {
            // Calculate extension ratio from stick position
            float extensionRatio = (stickPosition.y + 1f) * 0.5f; // 0 to 1 range
            float extensionDistance = Mathf.Lerp(minExtendDistance, maxExtendDistance, extensionRatio);
            float horizontalOffset = stickPosition.x * maxHorizontalDistance;
            
            // Calculate player's hand position
            Vector3 playerHandPosition = activeTransform.position + 
                activeTransform.right * 0.3f + 
                Vector3.up * baseHeight;
            
            // Calculate blade position without Y adjustment
            return playerHandPosition + 
                activeTransform.forward * extensionDistance * stickExtendFactor +
                activeTransform.right * horizontalOffset;
        }

        private float DetectGroundHeight(Vector3 position)
        {
            if (useSimpleGroundDetection) {
                // Simply return the ice level with offset
                return iceLevel + stickGroundOffset;
            }
            
            // Try primary detection using specific layers only
            if (Physics.Raycast(
                new Vector3(position.x, position.y + groundDetectionHeight, position.z), 
                Vector3.down, out RaycastHit hit, 
                groundDetectionHeight * 2f, 
                LayerMask.GetMask("Rink", "Ice", "Ground", "Arena")))
            {
                // Success with specific layers - keep very close to ground
                framesSinceGroundDetected = 0;
                if (debugGroundDetection) {
                    Debug.DrawLine(
                        new Vector3(position.x, position.y + groundDetectionHeight, position.z),
                        hit.point, Color.green, Time.deltaTime);
                }
                return hit.point.y + minBladeHeight;
            }
            
            // If specific layers failed but we can use broader detection, try that
            if (!ignoreDefaultLayer && Physics.Raycast(
                new Vector3(position.x, position.y + groundDetectionHeight, position.z),
                Vector3.down, out RaycastHit hitAny, 
                groundDetectionHeight * 2f, groundLayerMask))
            {
                framesSinceGroundDetected = 0;
                return hitAny.point.y + minBladeHeight + safeGroundOffset;
            }
            
            // Count frames without ground detection
            framesSinceGroundDetected++;
            
            // Final fallback: use absolute minimum height
            return Mathf.Max(0.01f, fixedGroundHeight + minBladeHeight);
        }

        private bool ValidatePosition(Vector3 position)
        {
            // Check if position is NaN or infinity
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                return false;
            }
            
            // Check if position is too far from player
            Transform activeTransform = player != null ? player.transform : playerTransform;
            if (Vector3.Distance(position, activeTransform.position) > maxDistanceFromPlayer * 2f)
            {
                return false;
            }
            
            return true;
        }

        private void EnsureMinimumHeight()
        {
            // Double-check we're above the ground with a direct raycast
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, 
                               Vector3.down, out RaycastHit hit, 
                               0.15f, groundLayerMask))
            {
                if (transform.position.y < hit.point.y + minBladeHeight)
                {
                    Vector3 correctedPos = transform.position;
                    correctedPos.y = hit.point.y + minBladeHeight;
                    transform.position = correctedPos;
                    smoothedPosition = correctedPos;
                }
            }
        }

        private void SetStickRotation(Transform activeTransform, float extensionRatio)
        {
            try {
                // Simplified rotation with more clamping and stability
                Vector3 playerHandPosition = activeTransform.position + 
                    activeTransform.right * 0.3f + 
                    Vector3.up * baseHeight;
                
                // Calculate stick-to-hand direction with stability check
                Vector3 bladeToHand = (playerHandPosition - transform.position);
                float distanceToHand = bladeToHand.magnitude;
                
                // If too far, use a default direction
                if (distanceToHand < 0.1f || distanceToHand > 5f) {
                    bladeToHand = -activeTransform.forward + Vector3.up;
                }
                bladeToHand.Normalize();
                
                // Create rotation with fixed blade direction and calculated shaft direction
                Quaternion targetRotation = Quaternion.LookRotation(
                    activeTransform.forward,  // Always align blade with player forward
                    bladeToHand               // Point shaft toward hand
                );
                
                // Apply rotation with strong smoothing to reduce jitter
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    targetRotation, 
                    Time.deltaTime * movementSpeed * 0.8f);
            }
            catch (System.Exception ex) {
                Debug.LogError($"Error setting stick rotation: {ex.Message}");
            }
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
            // Special handling for rink/ground collisions to prevent passing through
            if (((1 << collision.gameObject.layer) & groundLayerMask) != 0)
            {
                // Immediately move above the contact point
                ContactPoint contact = collision.contacts[0];
                Vector3 correctedPos = transform.position;
                correctedPos.y = contact.point.y + minBladeHeight + 0.05f; // Extra buffer
                
                transform.position = correctedPos;
                smoothedPosition = correctedPos;
                lastValidPosition = correctedPos;
                
                // Zero out any downward velocity
                currentVelocity.y = Mathf.Max(0, currentVelocity.y);
                
                Debug.Log($"Corrected stick position after ground collision: {correctedPos.y}");
                return;
            }

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
                    
                    // Calculate a more accurate hit force based on collision normal and stick velocity
                    Vector3 hitDirection = Vector3.Reflect(currentVelocity.normalized, collision.contacts[0].normal);
                    
                    // Print debug info to help diagnose issues
                    Debug.Log($"Puck hit! Velocity: {velocityMag}, Direction: {hitDirection}, " +
                              $"Contact point: {collision.contacts[0].point}");
                    
                    if (velocityMag < dragThreshold)
                    {
                        // Try to attach the puck to the stick for dragging
                        attachedPuck = puckRb;
                        puckRb.linearVelocity = currentVelocity * 0.8f; // Transfer some velocity immediately
                        Debug.Log("Attaching puck to stick for dragging");
                    }
                    else
                    {
                        // If moving fast, hit the puck with force
                        float force = Mathf.Clamp(velocityMag * velocityMultiplier * puckForceMultiplier, 
                                                minHitForce, maxHitForce);
                        
                        // Apply force to the puck in the direction of the hit
                        Vector3 forceVector = hitDirection * force;
                        
                        // Apply the force to move the puck
                        Debug.Log($"Hitting puck with force: {force:F1}, direction: {forceVector}");
                        puckRb.AddForce(forceVector, ForceMode.Impulse);
                        
                        // Also directly set some velocity to ensure movement
                        if (puckRb.linearVelocity.magnitude < 1f)
                        {
                            puckRb.linearVelocity = hitDirection * force * 0.1f;
                        }
                    }
                }
            }
            else if (((1 << collision.gameObject.layer) & groundLayerMask) != 0)
            {
                // Handle ground collision - bounce stick slightly
                Vector3 reflectDir = Vector3.Reflect(currentVelocity.normalized, collision.contacts[0].normal);
                currentVelocity = reflectDir * currentVelocity.magnitude * 0.3f; // Dampen the bounce

                // Force blade position above the collision point when hitting ground
                Vector3 correctedPos = transform.position;
                correctedPos.y = collision.contacts[0].point.y + minBladeHeight;
                transform.position = correctedPos;
                
                // Stop any downward momentum
                if (currentVelocity.y < 0)
                {
                    currentVelocity.y = 0;
                }
                
                Debug.Log($"Ground collision correction at {correctedPos.y:F3}");
                return;
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

        // Enhanced puck handling for dragging
        private void FixedUpdate()
        {
            if (attachedPuck != null)
            {
                // Calculate target position slightly in front of the stick blade
                Vector3 puckTargetPos = transform.position + transform.forward * 0.3f;
                puckTargetPos.y = attachedPuck.position.y; // Maintain puck's height
                
                // Direction to move the puck
                Vector3 directionToPuck = puckTargetPos - attachedPuck.position;
                
                // If puck gets too far, detach it
                if (directionToPuck.magnitude > 1.2f)
                {
                    attachedPuck = null;
                    Debug.Log("Puck detached - too far from stick");
                }
                else
                {
                    // Direct velocity control for more responsive puck movement
                    Vector3 newVelocity = directionToPuck * dragStrength * 2f;
                    
                    // Blend with current velocity for smoother movement
                    attachedPuck.linearVelocity = Vector3.Lerp(attachedPuck.linearVelocity, newVelocity, 0.3f);
                    
                    // Apply additional force in the stick movement direction
                    attachedPuck.AddForce(currentVelocity * 0.8f, ForceMode.Acceleration);
                    
                    // Debug puck dragging
                    Debug.DrawLine(transform.position, attachedPuck.position, Color.magenta);
                }
            }
        }

        private void LateUpdate()
        {
            // Extra safety to prevent ground penetration
            if (enforceGroundCollision && Physics.Raycast(transform.position + Vector3.up * 0.2f, 
                                                         Vector3.down, out RaycastHit hit, 
                                                         0.3f, groundLayerMask))
            {
                if (transform.position.y < hit.point.y + minBladeHeight)
                {
                    Vector3 correctedPos = transform.position;
                    correctedPos.y = hit.point.y + minBladeHeight;
                    transform.position = correctedPos;
                }
            }

            // Extremely aggressive position correction to ensure stick stays on ground
            if (forceGroundAlignment)
            {
                Vector3 forcePos = transform.position;
                forcePos.y = rinkHeight + absoluteGroundLevel;
                transform.position = forcePos;
                smoothedPosition.y = forcePos.y;
            }

            // Simple enforcement - keep stick above ice level
            Vector3 icePos = transform.position;
            if (icePos.y < iceLevel + stickGroundOffset) {
                icePos.y = iceLevel + stickGroundOffset;
                transform.position = icePos;
            }

            // Ensure minimum height using raycasts
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out hit, 0.4f, groundLayerMask)) {
                float minHeightFromGround = hit.point.y + minBladeHeight;
                if (transform.position.y < minHeightFromGround) {
                    // Apply position correction
                    Vector3 correctedPos = transform.position;
                    correctedPos.y = minHeightFromGround;
                    transform.position = correctedPos;
                    
                    // Also adjust rigidbody velocity
                    Rigidbody rb = GetComponent<Rigidbody>();
                    if (rb != null && rb.linearVelocity.y < 0) {
                        Vector3 newVelocity = rb.linearVelocity;
                        newVelocity.y = 0;
                        rb.linearVelocity = newVelocity;
                    }
                }
            }

            // Only enforce a minimum height but don't force an exact height
            // This allows stick to slide across surfaces
            if (rb != null)
            {
                // Only enforce a minimum height but don't force an exact height
                // This allows stick to slide across surfaces
                if (transform.position.y < fixedGroundHeight + minBladeHeight)
                {
                    Vector3 pos = transform.position;
                    pos.y = fixedGroundHeight + minBladeHeight;
                    transform.position = pos;
                    
                    // Zero out downward velocity
                    if (rb.linearVelocity.y < 0)
                    {
                        Vector3 vel = rb.linearVelocity;
                        vel.y = 0;
                        rb.linearVelocity = vel;
                    }
                }
            }
            else
            {
                // If rb is null, still enforce minimum height without velocity adjustment
                if (transform.position.y < fixedGroundHeight + minBladeHeight)
                {
                    Vector3 pos = transform.position;
                    pos.y = fixedGroundHeight + minBladeHeight;
                    transform.position = pos;
                }
                
                // Try to recreate the rigidbody if it's missing
                rb = GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = gameObject.AddComponent<Rigidbody>();
                    Debug.LogWarning("Rigidbody was missing - recreated it in LateUpdate");
                    
                    // Configure the newly added rigidbody
                    rb.useGravity = false;
                    rb.mass = 0.3f;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
            }
            
            // IMPORTANT: Make sure we never go too far above the ice either
            if (transform.position.y > fixedGroundHeight + minBladeHeight + 0.1f)
            {
                Vector3 pos = transform.position;
                pos.y = fixedGroundHeight + minBladeHeight + 0.1f;
                transform.position = pos;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize the ground detection rays and collider shape
            if (Application.isPlaying && debugGroundDetection)
            {
                // Show the stick's collider
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    Gizmos.color = Color.green;
                    if (col is BoxCollider boxCol)
                    {
                        Gizmos.matrix = transform.localToWorldMatrix;
                        Gizmos.DrawWireCube(boxCol.center, boxCol.size);
                    }
                    else if (col is SphereCollider sphereCol)
                    {
                        Gizmos.DrawWireSphere(
                            transform.TransformPoint(sphereCol.center), 
                            sphereCol.radius * Mathf.Max(transform.lossyScale.x, 
                                                         Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)));
                    }
                    else if (col is CapsuleCollider capsuleCol)
                    {
                        // Capsule visualization would be complex, just show a simple representation
                        Gizmos.DrawWireSphere(transform.TransformPoint(capsuleCol.center), capsuleCol.radius);
                    }
                }
            }
        }

        // New helper methods to handle erratic behavior
    
        private bool IsPositionErratic(Vector3 position) {
            // Check for NaN or infinity
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z)) {
                return true;
            }
            
            // Check for extremely large jumps in position
            if (Vector3.Distance(position, transform.position) > 5f) {
                return true;
            }
            
            // Check if stuck in the same spot for too long
            if (framesInSamePosition > stuckFrameThreshold) {
                return true;
            }
            
            return false;
        }
        
        private Vector3 GetStabilizedPosition() {
            // Average recent positions for stability
            Vector3 avgPos = Vector3.zero;
            for (int i = 0; i < previousPositions.Length; i++) {
                avgPos += previousPositions[i];
            }
            return avgPos / previousPositions.Length;
        }
        
        private Vector3 GetSafePosition() {
            Transform activeTransform = player != null ? player.transform : playerTransform;
            if (activeTransform == null) return transform.position;
            
            // Return a safe position near the player
            return activeTransform.position + 
                activeTransform.forward * 1.0f +
                activeTransform.right * 0.5f + 
                Vector3.up * 0.2f;
        }
        
        private void ResetStickPosition() {
            Transform activeTransform = player != null ? player.transform : playerTransform;
            if (activeTransform == null) return;
            
            // Position stick in a safe default position
            Vector3 resetPos = activeTransform.position + 
                activeTransform.forward * 1.0f +
                activeTransform.right * 0.5f + 
                Vector3.up * 0.2f;
                
            transform.position = resetPos;
            smoothedPosition = resetPos;
            lastValidPosition = resetPos;
            currentVelocity = Vector3.zero;
            
            // Reset tracking arrays
            for (int i = 0; i < previousPositions.Length; i++) {
                previousPositions[i] = resetPos;
            }
            
            framesInSamePosition = 0;
            framesWithHighVelocity = 0;
            
            Debug.Log("Stick position has been reset");
        }
        
        private void EnsureMinimumHeightFromGround() {
            if (forceGroundAlignment)
            {
                Vector3 pos = transform.position;
                pos.y = rinkHeight + absoluteGroundLevel;
                transform.position = pos;
                smoothedPosition.y = pos.y;
            }
        }

        // NEW METHOD: Force stick to stay very close to ground
        private void EnsureStickStaysGrounded()
        {
            if (forceGroundAlignment)
            {
                Vector3 pos = transform.position;
                pos.y = rinkHeight + absoluteGroundLevel;
                transform.position = pos;
                smoothedPosition.y = pos.y;
            }
        }

        // New method to detect the rink height at startup
        private void DetectRinkHeight()
        {
            // Try to detect the rink height from multiple positions
            Vector3[] testPoints = new Vector3[] {
                new Vector3(0, 5, 0),
                new Vector3(10, 5, 10),
                new Vector3(-10, 5, -10),
                transform.position + Vector3.up * 5
            };

            foreach (Vector3 point in testPoints)
            {
                if (Physics.Raycast(point, Vector3.down, out RaycastHit hit, 10f, 
                                   LayerMask.GetMask("Rink", "Ice", "Ground", "Arena")))
                {
                    rinkHeight = hit.point.y;
                    Debug.Log($"Found rink at Y={rinkHeight}, object={hit.collider.name}");
                    // Store this as our fixed ground height reference
                    fixedGroundHeight = rinkHeight;
                    return;
                }
            }

            // If no rink found, assume it's at Y=0
            Debug.LogWarning("Could not detect rink height! Assuming Y=0");
            rinkHeight = 0.0f;
            fixedGroundHeight = 0.0f;
        }

        // New method to directly force the stick to the ground level
        private void ForceToGroundLevel()
        {
            Vector3 pos = transform.position;
            pos.y = rinkHeight + absoluteGroundLevel;
            transform.position = pos;
            
            // Also update all the tracking variables
            smoothedPosition = pos;
            lastValidPosition = pos;
            previousPosition = pos;
            
            // Initialize position history
            for (int i = 0; i < previousPositions.Length; i++)
            {
                previousPositions[i] = pos;
            }
            
            Debug.Log($"Forced stick to ground level: Y={pos.y}");
        }

        // Fix the DetectIceLevel method to not rely on undefined tags
        private void DetectIceLevel()
        {
            // Don't use tags since "Ice" tag doesn't exist
            int iceLayer = LayerMask.NameToLayer("Ice");
            if (iceLayer < 0) 
            {
                Debug.LogWarning("Ice layer not found! Make sure to create this layer in Unity.");
                iceLevel = 0.0f;
                return;
            }
            
            // Try to find any objects with ice layer directly
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            List<GameObject> iceObjects = new List<GameObject>();
            
            foreach(GameObject obj in allObjects) 
            {
                if (obj.layer == iceLayer) 
                {
                    iceObjects.Add(obj);
                }
            }
            
            if (iceObjects.Count > 0)  // Fixed: use .Count property without parentheses
            {
                Debug.Log($"Found {iceObjects.Count} ice objects without using tags");
                
                // Check the Y position of each ice object
                foreach (GameObject ice in iceObjects) 
                {
                    Debug.Log($"Ice object: {ice.name}, Y position: {ice.transform.position.y}");
                    
                    // Detect the top surface of the ice using its collider
                    Collider iceCollider = ice.GetComponent<Collider>();
                    if (iceCollider != null) 
                    {
                        // Get the top of the collider
                        if (iceCollider is BoxCollider boxCol) 
                        {
                            // For box collider, calculate top Y
                            iceLevel = ice.transform.position.y + (boxCol.center.y + boxCol.size.y/2) * ice.transform.lossyScale.y;
                            Debug.Log($"Detected ice level at Y={iceLevel} from box collider");
                            return;
                        } 
                        else 
                        {
                            // For other colliders, use object position as reference
                            iceLevel = ice.transform.position.y;
                            Debug.Log($"Using ice object position as ice level: Y={iceLevel}");
                            return;
                        }
                    }
                }
            }
            
            // Try finding a ground object if no ice is found
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(0, 10, 0), Vector3.down, out hit, 20f, LayerMask.GetMask("Ground", "Default"))) 
            {
                iceLevel = hit.point.y;
                Debug.Log($"No ice objects found, using ground at Y={iceLevel}");
            } 
            else 
            {
                Debug.LogWarning("No ice or ground found! Using default level of Y=0.0");
                iceLevel = 0.0f;
            }
        }

        // Add this method to reinitialize physics components if they're missing
        private void OnEnable()
        {
            // Ensure rb is available when the object is enabled
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = gameObject.AddComponent<Rigidbody>();
                    Debug.LogWarning("Rigidbody was missing - added it in OnEnable");
                    
                    // Configure the newly added rigidbody
                    rb.useGravity = false;
                    rb.mass = 0.3f;
                    rb.linearDamping = 0.2f;
                    rb.angularDamping = 0.1f;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                }
            }
        }
    }
}