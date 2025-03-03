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
        
        [Tooltip("How sensitive the vertical movement is")]
        public float verticalSensitivity = 2f;
        
        private float currentVerticalAngle = 0f;

        [Header("Stick Position")]
        public Vector3 stickOffset = new Vector3(0.5f, 0.1f, 0.3f); // Reduced Y value
        public Vector3 stickRotation = new Vector3(45f, 0f, -90f); // Default hockey stick angles
        public float baseHeight = 0.1f; // Base height off the ground
        public float maxHeightAdjustment = 0.3f; // Maximum height the stick can raise

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

        private void Start()
        {
            mainCamera = Camera.main;
            
            // Initial setup
            if (player != null)
            {
                // Set initial position and rotation
                UpdateStickTransform();
            }
            
            // Make sure the collider is set to trigger
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false; // We want physical interactions
            }

            currentStickLength = minStickLength;
            targetLength = currentStickLength;
        }
        
        private void Update()
        {
            if (player == null || mainCamera == null) return;

            // Handle mouse wheel rotation
            float mouseWheel = Input.GetAxis("Mouse ScrollWheel");
            currentRotation += mouseWheel * rotationSpeed;

            // Handle stick extension with vertical mouse movement
            float mouseY = Input.GetAxis("Mouse Y");
            targetLength = Mathf.Clamp(targetLength - mouseY * stickExtendSpeed, minStickLength, maxStickLength);
            currentStickLength = Mathf.Lerp(currentStickLength, targetLength, Time.deltaTime * movementSpeed);

            // Calculate base position (right side of player, but lower)
            Vector3 basePosition = player.transform.position + 
                player.transform.right * 0.5f + 
                Vector3.up * baseHeight; // Start much lower

            // Calculate horizontal angle based on mouse X position
            float mouseX = Input.mousePosition.x / Screen.width * 2 - 1; // -1 to 1
            float sideAngle = mouseX * maxSideAngle;

            // Calculate stick position
            Vector3 stickDirection = Quaternion.Euler(0, player.transform.eulerAngles.y + sideAngle, 0) * Vector3.forward;
            
            // Calculate height based on length with reduced multiplier
            float heightAdjustment = (currentStickLength - minStickLength) * heightMultiplier;
            heightAdjustment = Mathf.Clamp(heightAdjustment, 0, maxHeightAdjustment);
            
            // Set final position
            targetPosition = basePosition + stickDirection * currentStickLength;
            targetPosition.y = player.transform.position.y + baseHeight + heightAdjustment;

            // Update stick position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * movementSpeed);

            // Apply rotation from mouse wheel and keep stick orientation
            Quaternion targetRotation = Quaternion.Euler(
                currentRotation,                      // X rotation from mouse wheel
                player.transform.eulerAngles.y + sideAngle, // Y rotation follows movement
                -90                                   // Z rotation keeps stick horizontal
            );

            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * movementSpeed);

            // Debug visualization
            Debug.DrawLine(basePosition, targetPosition, Color.red);
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
            Vector3 newPosition = player.transform.position + 
                (player.transform.right * stickOffset.x) +
                (Vector3.up * stickOffset.y) +
                (player.transform.forward * stickOffset.z);

            transform.position = newPosition;
            transform.rotation = Quaternion.Euler(stickRotation);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Check if we hit a puck
            if (((1 << collision.gameObject.layer) & puckLayer) != 0)
            {
                Rigidbody puckRb = collision.rigidbody;
                
                if (puckRb != null)
                {
                    // Calculate direction from stick to puck
                    Vector3 direction = (collision.transform.position - transform.position).normalized;
                    
                    // Apply force to the puck
                    Vector3 force = direction * hitForce;
                    
                    // Add some upward force to make it look more realistic
                    force += Vector3.up * 0.5f;
                    
                    puckRb.AddForce(force, ForceMode.Impulse);
                    
                    // Optionally play sound effect here
                    // AudioSource.PlayClipAtPoint(hitSound, collision.contacts[0].point);
                }
            }
        }
    }
}