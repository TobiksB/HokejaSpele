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
        
        private void Start()
        {
            mainCamera = Camera.main;
            
            // Store the original local position
            if (player != null)
            {
                originalLocalPosition = transform.localPosition;
            }
            else
            {
                Debug.LogWarning("No HockeyPlayer reference set for this stick. The stick may not move correctly.");
            }
            
            // Make sure the collider is set to trigger
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false; // We want physical interactions
            }
        }
        
        private void Update()
        {
            if (player == null || mainCamera == null) return;

            // Get mouse position and movement
            Vector3 mousePos = Input.mousePosition;
            float mouseY = Input.GetAxis("Mouse Y");
            
            // Handle vertical rotation based on mouse Y movement
            currentVerticalAngle -= mouseY * verticalSensitivity;
            currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
            
            mousePos.z = Vector3.Distance(mainCamera.transform.position, player.transform.position);
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
            worldPos.y = player.transform.position.y + hoverHeight;

            // Calculate horizontal direction
            Vector3 direction = (worldPos - player.transform.position).normalized;
            targetPosition = player.transform.position + (direction * maxDistanceFromPlayer);

            // Move to target position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * movementSpeed);

            // Apply both horizontal and vertical rotation
            if (direction != Vector3.zero)
            {
                Quaternion horizontalRotation = Quaternion.LookRotation(direction);
                Quaternion verticalRotation = Quaternion.Euler(currentVerticalAngle, 0, 0);
                Quaternion targetRotation = horizontalRotation * verticalRotation;
                
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * movementSpeed);
            }

            // Debug visualization
            Debug.DrawLine(player.transform.position, targetPosition, Color.red);
            Debug.DrawRay(transform.position, transform.up * 0.5f, Color.green);
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