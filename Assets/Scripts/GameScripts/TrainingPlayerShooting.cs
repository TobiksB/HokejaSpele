using UnityEngine;
using System.Collections;

namespace HockeyGame.Game
{
    // Klase, kas pārvalda spēlētāja ripas šaušanas mehāniku treniņa režīmā
    public class TrainingPlayerShooting : MonoBehaviour
    {
        [SerializeField] private float shootForce = 30f; // Vidējs spēks treniņam
        [SerializeField] private float maxChargeTime = 1.2f; // Maksimālais uzlādes laiks
        [SerializeField] private float movementVelocityMultiplier = 0.8f; // Cik daudz spēlētāja kustības ātrums ietekmē šāvienu
        [SerializeField] private float resetDelay = 3f; // Laiks pirms automātiskās atiestatīšanas
        [SerializeField] private bool enableAutoReset = false; // Pēc noklusējuma atspējots, lai novērstu pastāvīgu respawnošanu
        
        private TrainingPuckPickup puckPickup; // Atsauce uz ripas pacelšanas komponenti
        private TrainingPlayerMovement playerMovement; // Atsauce uz spēlētāja kustības komponenti
        private Rigidbody playerRb; // Spēlētāja fiziskais ķermenis
        private bool isCharging = false; // Vai šaušanas spēks tiek uzlādēts
        private float chargeTime = 0f; // Cik ilgi uzlādēts šaušanas spēks
        private Vector3 initialPlayerPosition; // Sākotnējā spēlētāja pozīcija
        private Quaternion initialPlayerRotation; // Sākotnējā spēlētāja rotācija
        
        private void Awake()
        {
            // Iegūst vai pievieno nepieciešamos komponentus
            puckPickup = GetComponent<TrainingPuckPickup>();
            if (puckPickup == null)
            {
                puckPickup = gameObject.AddComponent<TrainingPuckPickup>();
            }
            
            playerMovement = GetComponent<TrainingPlayerMovement>();
            playerRb = GetComponent<Rigidbody>();
            
            // Saglabā sākotnējo pozīciju un rotāciju atiestatīšanai
            initialPlayerPosition = transform.position;
            initialPlayerRotation = transform.rotation;
        }
        
        private void Update()
        {
            HandleShootingInput(); // Apstrādā spēlētāja ievadi šaušanai
        }
        
        private void HandleShootingInput()
        {
            // Atļauj šaut tikai tad, ja spēlētājam ir ripa
            if (!puckPickup.CanShootPuck())
            {
                if (isCharging)
                {
                    isCharging = false;
                    chargeTime = 0f;
                }
                return;
            }
            
            // Sāk uzlādi, kad nospiež peles pogu
            if (Input.GetMouseButtonDown(0) && !isCharging)
            {
                StartCharging();
            }
            
            // Turpina uzlādēt, kamēr peles poga tiek turēta
            if (Input.GetMouseButton(0) && isCharging)
            {
                ContinueCharging();
            }
            
            // Atļauj šaut tikai, ja ir uzlādēts vismaz 20%
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
        
        // Sāk uzlādes procesu
        private void StartCharging()
        {
            if (!puckPickup.CanShootPuck()) return;
            
            isCharging = true;
            chargeTime = 0f;
        }
        
        // Palielina uzlādes laiku
        private void ContinueCharging()
        {
            chargeTime += Time.deltaTime;
            chargeTime = Mathf.Clamp(chargeTime, 0f, maxChargeTime);
        }
        
        // Veic ripas šaušanu
        private void Shoot()
        {
            if (!puckPickup.CanShootPuck())
            {
                isCharging = false;
                chargeTime = 0f;
                return;
            }
            
            // Aprēķina šaušanas spēku, balstoties uz uzlādes laiku
            float chargePercentage = Mathf.Clamp01(chargeTime / maxChargeTime);
            float finalForce = shootForce * Mathf.Lerp(0.3f, 1f, chargePercentage);
            
            // Iegūst spēlētāja pašreizējo ātrumu
            Vector3 playerVelocity = Vector3.zero;
            if (playerRb != null)
            {
                playerVelocity = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
            }
            
            // Aprēķina šaušanas virzienu un ātrumu
            Vector3 shootDirection = transform.forward;
            Vector3 baseShootVelocity = shootDirection * finalForce;
            Vector3 finalShootVelocity = baseShootVelocity + (playerVelocity * movementVelocityMultiplier);
            
            // Iegūst ripu un atlaiž to
            Puck puck = puckPickup.GetCurrentPuck();
            if (puck != null)
            {
                puckPickup.ReleasePuckForShooting();
                
                // Piemēro ātrumu ripai
                Rigidbody puckRb = puck.GetComponent<Rigidbody>();
                if (puckRb != null)
                {
                    puckRb.linearVelocity = finalShootVelocity;
                }
            }
            
            // Aktivizē animāciju
            if (playerMovement != null)
            {
                playerMovement.TriggerShootAnimation();
            }
            
            isCharging = false;
            chargeTime = 0f;
            
            // Automātiski atiestata spēlētāju un ripu pēc aizkaves - TIKAI ja tas ir īpaši iespējots
            if (enableAutoReset)
            {
                StartCoroutine(ResetAfterDelay(resetDelay));
            }
        }
        
        // Koroutīna, kas atiestata spēlētāja un ripas pozīciju pēc norādītā laika
        private IEnumerator ResetAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Atiestata spēlētāja pozīciju un rotāciju
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
            
            // Atiestata ripu
            GameObject puckObj = null;
            var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (allPucks.Length > 0)
            {
                puckObj = allPucks[0].gameObject;
            }
            
            if (puckObj != null)
            {
                // Aprēķina pozīciju spēlētāja priekšā
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
                
                // Atiestata ripas pozīciju
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
                
                // Automātiski pacel ripu
                StartCoroutine(AutoPickupPuckAfterReset(0.5f));
            }
        }
        
        // Koroutīna, kas automātiski pacel ripu pēc atiestatīšanas
        private IEnumerator AutoPickupPuckAfterReset(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (puckPickup != null)
            {
                puckPickup.TryPickupPuck();
            }
        }
        
        // Pārbauda, vai šaušanas spēks tiek uzlādēts
        public bool IsCharging()
        {
            return isCharging;
        }
        
        // Atgriež uzlādes procentuālo vērtību
        public float GetChargePercentage()
        {
            if (!isCharging) return 0f;
            return chargeTime / maxChargeTime;
        }
    }
}
