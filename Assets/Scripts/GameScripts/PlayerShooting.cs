using UnityEngine;
using Unity.Netcode;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] private float shootForce = 40f; // Increased base force
    [SerializeField] private float maxChargeTime = 1.2f; // Shorter max charge for snappier feel
    [SerializeField] private bool enableDebugLogs = true;

    private PuckPickup puckPickup;
    private bool isCharging = false;
    private float chargeTime = 0f;

    private void Awake()
    {
        puckPickup = GetComponent<PuckPickup>();
        if (puckPickup == null)
        {
            puckPickup = gameObject.AddComponent<PuckPickup>();
            Debug.LogWarning("PlayerShooting: Added missing PuckPickup component");
        }
    }

    private void Update()
    {
        // Only process input for the owner
        if (!IsOwner) return;

        HandleShootingInput();
    }

    private void HandleShootingInput()
    {
        // Only allow shooting if we have the puck and not already charging
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
            {
                if (enableDebugLogs)
                {
                    Debug.Log("PlayerShooting: Shot cancelled - not enough charge");
                }
                isCharging = false;
                chargeTime = 0f;
            }
        }

        // Remove quick shoot on right mouse button for true charge-only shooting
    }

    private void StartCharging()
    {
        if (!puckPickup.CanShootPuck()) return;

        isCharging = true;
        chargeTime = 0f;

        if (enableDebugLogs)
        {
            Debug.Log("PlayerShooting: Started charging shot");
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

        if (enableDebugLogs)
        {
            Debug.Log($"PlayerShooting: Shooting with {chargePercentage:P0} charge (force: {finalForce:F1})");
        }

        // Get puck and release it
        Puck puck = puckPickup.GetCurrentPuck();
        if (puck != null)
        {
            puckPickup.ReleasePuckForShooting();

            Vector3 shootDirection = transform.forward;
            Vector3 shootVelocity = shootDirection * finalForce;

            ShootPuckServerRpc(puck.GetComponent<NetworkObject>().NetworkObjectId, shootVelocity);

            if (enableDebugLogs)
            {
                Debug.Log($"PlayerShooting: Shot puck with velocity {shootVelocity}");
            }
        }

        isCharging = false;
        chargeTime = 0f;

        // Reset shooting flag after a delay
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
    }

    // Public method to check if currently charging
    public bool IsCharging()
    {
        return isCharging;
    }

    // Public method to get current charge percentage
    public float GetChargePercentage()
    {
        if (!isCharging) return 0f;
        return chargeTime / maxChargeTime;
    }
}
