using UnityEngine;
using System.Collections;

namespace HockeyGame.Game
{
    public class TrainingPlayerShooting : MonoBehaviour
    {
        [SerializeField] private float shootForce = 30f; // Moderate force for training
        [SerializeField] private float maxChargeTime = 1.2f;
        [SerializeField] private float movementVelocityMultiplier = 0.8f;
        [SerializeField] private float resetDelay = 3f; // Time before automatic reset
        [SerializeField] private bool enableAutoReset = false; // Disabled by default to prevent constant respawns
        
        private TrainingPuckPickup puckPickup;
        private TrainingPlayerMovement playerMovement;
        private Rigidbody playerRb;
        private bool isCharging = false;
        private float chargeTime = 0f;
        private Vector3 initialPlayerPosition;
        private Quaternion initialPlayerRotation;
        
        private void Awake()
        {
            // Get or add required components
            puckPickup = GetComponent<TrainingPuckPickup>();
            if (puckPickup == null)
            {
                puckPickup = gameObject.AddComponent<TrainingPuckPickup>();
            }
            
            playerMovement = GetComponent<TrainingPlayerMovement>();
            playerRb = GetComponent<Rigidbody>();
            
            // Store initial position and rotation for reset
            initialPlayerPosition = transform.position;
            initialPlayerRotation = transform.rotation;
        }
        
        private void Update()
        {
            HandleShootingInput();
        }
        
        private void HandleShootingInput()
        {
            // Only allow shooting if we have the puck
            if (!puckPickup.CanShootPuck())
            {
                if (isCharging)
                {
                    isCharging = false;
                    chargeTime = 0f;
                }
                return;
            }
            
            // Start charging on mouse down
            if (Input.GetMouseButtonDown(0) && !isCharging)
            {
                StartCharging();
            }
            
            // Continue charging while held
            if (Input.GetMouseButton(0) && isCharging)
            {
                ContinueCharging();
            }
            
            // Only allow shooting if charged at least 20%
            if (Input.GetMouseButtonUp(0) && isCharging)
            {
                if (chargeTime >= maxChargeTime * 0.2f)
                {
                    Shoot();
                }
                else
                {
                    isCharging = false;
                    chargeTime = 0f;
                }
            }
        }
        
        private void StartCharging()
        {
            if (!puckPickup.CanShootPuck()) return;
            
            isCharging = true;
            chargeTime = 0f;
        }
        
        private void ContinueCharging()
        {
            chargeTime += Time.deltaTime;
            chargeTime = Mathf.Clamp(chargeTime, 0f, maxChargeTime);
        }
        
        private void Shoot()
        {
            if (!puckPickup.CanShootPuck())
            {
                isCharging = false;
                chargeTime = 0f;
                return;
            }
            
            // Calculate shoot force based on charge time
            float chargePercentage = Mathf.Clamp01(chargeTime / maxChargeTime);
            float finalForce = shootForce * Mathf.Lerp(0.3f, 1f, chargePercentage);
            
            // Get player's current velocity
            Vector3 playerVelocity = Vector3.zero;
            if (playerRb != null)
            {
                playerVelocity = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
            }
            
            // Calculate shoot direction and velocity
            Vector3 shootDirection = transform.forward;
            Vector3 baseShootVelocity = shootDirection * finalForce;
            Vector3 finalShootVelocity = baseShootVelocity + (playerVelocity * movementVelocityMultiplier);
            
            // Get puck and release it
            Puck puck = puckPickup.GetCurrentPuck();
            if (puck != null)
            {
                puckPickup.ReleasePuckForShooting();
                
                // Apply velocity to puck
                Rigidbody puckRb = puck.GetComponent<Rigidbody>();
                if (puckRb != null)
                {
                    puckRb.linearVelocity = finalShootVelocity;
                }
            }
            
            // Trigger animation
            if (playerMovement != null)
            {
                playerMovement.TriggerShootAnimation();
            }
            
            isCharging = false;
            chargeTime = 0f;
            
            // Automatically reset player and puck after delay - ONLY if explicitly enabled
            if (enableAutoReset)
            {
                StartCoroutine(ResetAfterDelay(resetDelay));
            }
        }
        
        private IEnumerator ResetAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Reset player position and rotation
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }
            
            transform.position = initialPlayerPosition;
            transform.rotation = initialPlayerRotation;
            
            if (playerRb != null)
            {
                playerRb.position = initialPlayerPosition;
                playerRb.rotation = initialPlayerRotation;
            }
            
            // Reset puck
            GameObject puckObj = null;
            var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (allPucks.Length > 0)
            {
                puckObj = allPucks[0].gameObject;
            }
            
            if (puckObj != null)
            {
                // Calculate position in front of player
                Vector3 puckPos = initialPlayerPosition + transform.forward * 1.5f;
                puckPos.y = 0.71f;
                
                var puckRb = puckObj.GetComponent<Rigidbody>();
                if (puckRb != null)
                {
                    puckRb.linearVelocity = Vector3.zero;
                    puckRb.angularVelocity = Vector3.zero;
                    puckRb.isKinematic = false;
                    puckRb.useGravity = true;
                }
                
                // Reset puck position
                puckObj.transform.position = puckPos;
                
                if (puckRb != null)
                {
                    puckRb.position = puckPos;
                }
                
                var puckComponent = puckObj.GetComponent<Puck>();
                if (puckComponent != null)
                {
                    puckComponent.SetHeld(false);
                }
                
                // Auto-pickup the puck
                StartCoroutine(AutoPickupPuckAfterReset(0.5f));
            }
        }
        
        private IEnumerator AutoPickupPuckAfterReset(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (puckPickup != null)
            {
                puckPickup.TryPickupPuck();
            }
        }
        
        public bool IsCharging()
        {
            return isCharging;
        }
        
        public float GetChargePercentage()
        {
            if (!isCharging) return 0f;
            return chargeTime / maxChargeTime;
        }
    }
}
