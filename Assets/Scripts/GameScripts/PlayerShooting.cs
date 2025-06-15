using UnityEngine;
using Unity.Netcode;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] private float shootForce = 40f; // Palielināts pamata spēks
    [SerializeField] private float maxChargeTime = 1.2f; // Īsāks maksimālais uzlādes laiks ātrākai atsaucībai
    [SerializeField] private float movementVelocityMultiplier = 0.8f; // Cik daudz spēlētāja kustība ietekmē ripas ātrumu
    [SerializeField] private bool enableDebugLogs = true;

    private PuckPickup puckPickup;
    private PlayerMovement playerMovement; // Reference lai iegūtu pašreizējo ātrumu
    private Rigidbody playerRb; // Reference uz spēlētāja rigidbody komponenti
    private bool isCharging = false;
    private float chargeTime = 0f;

    private void Awake()
    {
        puckPickup = GetComponent<PuckPickup>();
        if (puckPickup == null)
        {
            puckPickup = gameObject.AddComponent<PuckPickup>();            Debug.LogWarning("PlayerShooting: Pievienots trūkstošais PuckPickup komponents");
        }

        // Iegūstam atsauces uz spēlētāja kustības komponentiem
        playerMovement = GetComponent<PlayerMovement>();
        playerRb = GetComponent<Rigidbody>();
        
        if (playerMovement == null)
        {
            Debug.LogWarning("PlayerShooting: Nav atrasts PlayerMovement komponents!");
        }
        if (playerRb == null)
        {
            Debug.LogWarning("PlayerShooting: Nav atrasts Rigidbody komponents!");
        }
    }    private void Update()
    {
        // Apstrādājam ievadi tikai īpašniekam
        if (!IsOwner) return;

        HandleShootingInput();
    }

    private void HandleShootingInput()
    {
        // Ļaujam šaut tikai, ja mums ir ripa un ja jau nenotiek uzlādēšanās
        if (!puckPickup.CanShootPuck())
        {
            if (isCharging)
            {
                isCharging = false;
                chargeTime = 0f;
                if (enableDebugLogs)
                {
                    Debug.Log("PlayerShooting: Cancelled charge - no puck to shoot");
                }
            }
            return;
        }

        // Start charging on mouse down (only if not already charging)
        if (Input.GetMouseButtonDown(0) && !isCharging)
        {
            StartCharging();
        }

        // Continue charging while held
        if (Input.GetMouseButton(0) && isCharging)
        {
            ContinueCharging();
        }

        // Only allow shooting if charged at least 20% (prevents instant tap shots)
        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            if (chargeTime >= maxChargeTime * 0.2f)
            {
                Shoot();
            }
            else
            {                if (enableDebugLogs)
                {
                    Debug.Log("PlayerShooting: Šāviens atcelts - nepietiekama uzlāde");
                }
                isCharging = false;
                chargeTime = 0f;
            }
        }

        // Noņemts ātrais šāviens ar labo peles pogu, lai būtu tikai uzlādes šaušana
    }

    private void StartCharging()
    {
        if (!puckPickup.CanShootPuck()) return;

        isCharging = true;
        chargeTime = 0f;        if (enableDebugLogs)
        {
            Debug.Log("PlayerShooting: Sākta šāviena uzlāde");
        }
    }

    private void ContinueCharging()
    {
        chargeTime += Time.deltaTime;
        chargeTime = Mathf.Clamp(chargeTime, 0f, maxChargeTime);

        // Optional: Add visual feedback for charging here
    }

    private void Shoot()
    {
        if (!puckPickup.CanShootPuck())
        {
            isCharging = false;
            chargeTime = 0f;
            return;
        }

        // Calculate shoot force based on charge time (min 30%, max 100%)
        float chargePercentage = Mathf.Clamp01(chargeTime / maxChargeTime);
        float finalForce = shootForce * Mathf.Lerp(0.3f, 1f, chargePercentage);

        // Get player's current velocity for realistic hockey physics
        Vector3 playerVelocity = Vector3.zero;
        if (playerRb != null)
        {
            // Only use horizontal velocity (X and Z), ignore Y
            playerVelocity = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
        }

        // Calculate base shoot direction and velocity
        Vector3 shootDirection = transform.forward;
        Vector3 baseShootVelocity = shootDirection * finalForce;
        
        // Add player movement velocity to the shot for realistic physics
        Vector3 finalShootVelocity = baseShootVelocity + (playerVelocity * movementVelocityMultiplier);

        if (enableDebugLogs)
        {
            Debug.Log($"PlayerShooting: Šaušana ar {chargePercentage:P0} uzlādi");
            Debug.Log($"  Pamata šāviena spēks: {finalForce:F1}");
            Debug.Log($"  Spēlētāja ātrums: {playerVelocity} (lielums: {playerVelocity.magnitude:F1})");
            Debug.Log($"  Ripas beigu ātrums: {finalShootVelocity} (lielums: {finalShootVelocity.magnitude:F1})");
        }

        // Iegūstam ripu un atlaižam to
        Puck puck = puckPickup.GetCurrentPuck();
        if (puck != null)
        {
            puckPickup.ReleasePuckForShooting();

            ShootPuckServerRpc(puck.GetComponent<NetworkObject>().NetworkObjectId, finalShootVelocity);            if (enableDebugLogs)
            {
                Debug.Log($"PlayerShooting: Iešauta ripa ar beigu ātrumu {finalShootVelocity}");
            }
        }

        // --- AKTIVIZĒ ŠAUŠANAS ANIMĀCIJU PĒC ŠĀVIENA ---
        TriggerShootAnimation();

        isCharging = false;
        chargeTime = 0f;        // Atiestatām šaušanas karogu pēc aiztures
        Invoke(nameof(ResetShootingFlag), 0.5f);
    }

    [ServerRpc]
    private void ShootPuckServerRpc(ulong puckNetworkId, Vector3 shootVelocity)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"PlayerShooting: [ServerRpc] Shooting puck {puckNetworkId} with velocity {shootVelocity}");
        }

        // Find the puck
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var puckNetObj))
        {
            var puck = puckNetObj.GetComponent<Puck>();
            var puckRb = puckNetObj.GetComponent<Rigidbody>();

            if (puck != null && puckRb != null)
            {
                // Ensure puck is released and physics enabled
                puck.SetHeld(false);

                var col = puckNetObj.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                puckRb.isKinematic = false;
                puckRb.useGravity = true;
                puckRb.linearVelocity = shootVelocity;
                puckRb.angularVelocity = Vector3.zero;

                // Stop any PuckFollower
                var puckFollower = puckNetObj.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }

                if (enableDebugLogs)
                {
                    Debug.Log($"PlayerShooting: [ServerRpc] Applied velocity {shootVelocity} to puck");
                }

                // --- TRIGGER SHOOT ANIMATION AFTER THE SHOT ON SERVER ---
                TriggerShootAnimation();

                // Notify all clients
                ApplyPuckVelocityClientRpc(puckNetworkId, shootVelocity);
            }
        }
    }

    [ClientRpc]
    private void ApplyPuckVelocityClientRpc(ulong puckNetworkId, Vector3 shootVelocity)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"PlayerShooting: [ClientRpc] Applying velocity to puck {puckNetworkId}");
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var puckNetObj))
        {
            var puckRb = puckNetObj.GetComponent<Rigidbody>();
            var puck = puckNetObj.GetComponent<Puck>();

            if (puckRb != null && puck != null)
            {
                puck.SetHeld(false);

                var col = puckNetObj.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                puckRb.isKinematic = false;
                puckRb.useGravity = true;
                puckRb.linearVelocity = shootVelocity;
                puckRb.angularVelocity = Vector3.zero;

                // Stop any PuckFollower
                var puckFollower = puckNetObj.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }
            }
        }
    }

    private void ResetShootingFlag()
    {
        if (puckPickup != null)
        {
            puckPickup.ResetShootingFlag();
        }
    }    // Publiska metode, lai pārbaudītu, vai pašlaik notiek uzlāde
    public bool IsCharging()
    {
        return isCharging;
    }

    // Publiska metode, lai iegūtu pašreizējo uzlādes procentu
    public float GetChargePercentage()
    {
        if (!isCharging) return 0f;
        return chargeTime / maxChargeTime;
    }    // JAUNS: Publiska metode, lai iegūtu kopējo šāviena jaudu, ieskaitot kustību
    public float GetTotalShotPower()
    {
        if (!isCharging) return 0f;
        
        float chargePercentage = Mathf.Clamp01(chargeTime / maxChargeTime);
        float baseForce = shootForce * Mathf.Lerp(0.3f, 1f, chargePercentage);
        
        // Calculate movement bonus
        Vector3 playerVelocity = Vector3.zero;
        if (playerRb != null)
        {
            playerVelocity = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
        }
        
        Vector3 baseShootVelocity = transform.forward * baseForce;
        Vector3 finalVelocity = baseShootVelocity + (playerVelocity * movementVelocityMultiplier);
        
        return finalVelocity.magnitude;
    }    // JAUNS: Publiska metode, lai iegūtu kustības ātruma bonusu
    public float GetMovementSpeedBonus()
    {
        if (playerRb == null) return 0f;
        
        Vector3 playerVelocity = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
        Vector3 movementContribution = playerVelocity * movementVelocityMultiplier;
        
        // Projicē kustību uz priekšu, lai iegūtu ātruma bonusu virzienā uz priekšu
        float forwardBonus = Vector3.Dot(movementContribution, transform.forward);
        
        return forwardBonus;
    }    // Pievieno šo metodi, lai labotu trūkstošās atsauces kļūdas un aktivizētu animāciju
    private void TriggerShootAnimation()
    {
        if (playerMovement != null)
        {
            playerMovement.TriggerShootAnimation();
            if (enableDebugLogs)
            {
                Debug.Log("PlayerShooting: Aktivizēta šaušanas animācija PlayerMovement komponentē");
            }
        }
        else
        {
            Debug.LogWarning("PlayerShooting: PlayerMovement reference is null, cannot trigger animation");
        }
    }
}
