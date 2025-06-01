using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] private float maxShootCharge = 3f;
    [SerializeField] private float minShootForce = 5f;
    [SerializeField] private float maxShootForce = 25f;
    [SerializeField] private bool enableDebugLogs = true;
    
    private PuckPickup puckPickup;
    private PlayerMovement playerMovement;
    private float currentShootCharge = 0f;
    
    private void Awake()
    {
        puckPickup = GetComponent<PuckPickup>();
        playerMovement = GetComponent<PlayerMovement>();
        
        if (puckPickup == null)
        {
            Debug.LogError("PlayerShooting: PuckPickup component not found!");
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleShootInput();
    }

    private void HandleShootInput()
    {
        // Check if we can shoot using the new CanShootPuck method
        bool canShoot = puckPickup != null && puckPickup.CanShootPuck();
        
        if (enableDebugLogs && Input.GetMouseButtonDown(0))
        {
            Debug.Log($"PlayerShooting: Left click - CanShoot: {canShoot}, HasPuck: {puckPickup?.HasPuck()}, CurrentPuck: {puckPickup?.GetCurrentPuck()?.name}");
        }

        if (Input.GetMouseButton(0) && canShoot)
        {
            currentShootCharge += Time.deltaTime;
            currentShootCharge = Mathf.Clamp(currentShootCharge, 0f, maxShootCharge);

            // Update UI or visual feedback for charge
            float chargePercent = currentShootCharge / maxShootCharge;
            if (enableDebugLogs && Time.frameCount % 30 == 0) // Log every 30 frames while charging
            {
                Debug.Log($"PlayerShooting: Charging shot - {chargePercent:P0}");
            }
        }

        if (Input.GetMouseButtonUp(0) && canShoot && currentShootCharge > 0f)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"PlayerShooting: Releasing shot with charge {currentShootCharge:F2}");
            }
            ShootPuck();
            currentShootCharge = 0f;
        }

        // Reset charge if we don't have puck anymore
        if (!canShoot && currentShootCharge > 0f)
        {
            currentShootCharge = 0f;
            if (enableDebugLogs)
            {
                Debug.Log("PlayerShooting: Reset charge - no longer have puck");
            }
        }
    }

    private void ShootPuck()
    {
        // Double-check we can still shoot
        if (puckPickup == null || !puckPickup.CanShootPuck())
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("PlayerShooting: Cannot shoot - puck not available");
            }
            return;
        }

        Puck puck = puckPickup.GetCurrentPuck();
        if (puck == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("PlayerShooting: Cannot shoot - no puck reference");
            }
            return;
        }

        // Calculate shooting direction and force
        Vector3 shootDirection = CalculateShootDirection();
        float shootForce = CalculateShootForce();

        if (enableDebugLogs)
        {
            Debug.Log($"PlayerShooting: Shooting puck - Direction: {shootDirection}, Force: {shootForce}");
        }

        // Release puck for shooting first
        puckPickup.ReleasePuckForShooting();

        // Apply shooting force after a short delay
        StartCoroutine(ApplyShootForceAfterRelease(puck, shootDirection, shootForce));

        // Trigger animation
        if (playerMovement != null)
        {
            playerMovement.TriggerShootAnimation();
        }
    }

    private Vector3 CalculateShootDirection()
    {
        // Simple forward direction for now
        return transform.forward;
    }

    private float CalculateShootForce()
    {
        // Calculate force based on charge time
        float chargePercent = currentShootCharge / maxShootCharge;
        return Mathf.Lerp(minShootForce, maxShootForce, chargePercent);
    }

    private IEnumerator ApplyShootForceAfterRelease(Puck puck, Vector3 direction, float force)
    {
        if (puck == null) yield break;
        
        // Wait for release to complete
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        
        // Ensure puck is properly released
        try
        {
            puck.SetHeld(false);
        }
        catch (System.Exception e)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"PlayerShooting: SetHeld failed: {e.Message}");
            }
            
            // Manual release fallback
            puck.transform.SetParent(null);
            var col = puck.GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }
        
        // Apply shooting force
        Vector3 shootVelocity = direction.normalized * force;
        
        var puckRb = puck.GetComponent<Rigidbody>();
        if (puckRb != null)
        {
            puckRb.isKinematic = false;
            puckRb.useGravity = true;
            puckRb.linearVelocity = shootVelocity;
            puckRb.angularVelocity = Vector3.up * (force * 0.1f);
            
            if (enableDebugLogs)
            {
                Debug.Log($"PlayerShooting: Applied velocity {shootVelocity} to puck");
            }
        }
        
        // Send to server for network sync
        if (NetworkManager.Singleton != null && IsSpawned)
        {
            var puckNetObj = puck.GetComponent<NetworkObject>();
            if (puckNetObj != null)
            {
                ShootPuckServerRpc(puckNetObj.NetworkObjectId, direction, force);
            }
        }
        
        // Reset shooting flag after shot completes
        yield return new WaitForSeconds(1f); // Wait a bit for shot to complete
        if (puckPickup != null)
        {
            puckPickup.ResetShootingFlag();
        }
    }
    
    [ServerRpc]
    private void ShootPuckServerRpc(ulong puckNetworkId, Vector3 direction, float force)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"PlayerShooting: [ServerRpc] Processing shot for puck {puckNetworkId} with force {force:F2}");
        }
        
        // Find the puck by network ID
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var networkObject))
        {
            var puck = networkObject.GetComponent<Puck>();
            if (puck != null)
            {
                // Use try-catch to handle SetHeld safely on server
                try
                {
                    puck.SetHeld(false);
                }
                catch (System.Exception e)
                {
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning($"PlayerShooting: [ServerRpc] SetHeld failed, using manual release: {e.Message}");
                    }
                    
                    // Manual release fallback
                    puck.transform.SetParent(null);
                    var col = puck.GetComponent<Collider>();
                    if (col != null) col.enabled = true;
                }
                
                // Apply shooting force on server
                var puckRb = puck.GetComponent<Rigidbody>();
                if (puckRb != null)
                {
                    // Ensure puck physics are enabled before applying forces
                    puckRb.isKinematic = false;
                    puckRb.useGravity = true;
                    
                    // Apply the shooting force
                    Vector3 shootVelocity = direction.normalized * force;
                    puckRb.linearVelocity = shootVelocity;
                    puckRb.angularVelocity = Vector3.up * (force * 0.1f);
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"PlayerShooting: [ServerRpc] Applied velocity {shootVelocity} to puck on server");
                    }
                }
                
                // Notify all clients about the shot
                OnPuckShotClientRpc(puckNetworkId, direction, force);
            }
        }
        else
        {
            Debug.LogWarning($"PlayerShooting: [ServerRpc] Could not find puck with NetworkObject ID {puckNetworkId}");
        }
    }

    [ClientRpc]
    private void OnPuckShotClientRpc(ulong puckNetworkId, Vector3 direction, float force)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"PlayerShooting: [ClientRpc] Puck shot with force {force:F2}");
        }
        
        // Find the puck and ensure it has proper velocity on all clients
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var networkObject))
        {
            var puck = networkObject.GetComponent<Puck>();
            if (puck != null)
            {
                // Use try-catch to handle SetHeld safely on clients
                try
                {
                    puck.SetHeld(false);
                }
                catch (System.Exception e)
                {
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning($"PlayerShooting: [ClientRpc] SetHeld failed, using manual release: {e.Message}");
                    }
                    
                    // Manual release fallback
                    puck.transform.SetParent(null);
                    var col = puck.GetComponent<Collider>();
                    if (col != null) col.enabled = true;
                }
                
                var puckRb = puck.GetComponent<Rigidbody>();
                if (puckRb != null && !IsServer) // Only apply on clients, server already handled it
                {
                    puckRb.isKinematic = false;
                    puckRb.useGravity = true;
                    Vector3 shootVelocity = direction.normalized * force;
                    puckRb.linearVelocity = shootVelocity;
                    puckRb.angularVelocity = Vector3.up * (force * 0.1f);
                }
            }
        }
    }
}
