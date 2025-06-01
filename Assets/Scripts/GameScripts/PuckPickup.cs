using UnityEngine;
using Unity.Netcode;

public class PuckPickup : NetworkBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private Transform puckHoldPosition;
    [SerializeField] private LayerMask puckLayer = 128; // Layer 7 = 2^7 = 128
    
    [Header("Input")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private Puck currentPuck;
    private bool hasPuck = false;
    
    // Network variable to sync puck state
    private NetworkVariable<bool> networkHasPuck = new NetworkVariable<bool>(false);
    
    // FIXED: Add shooting release flag to prevent conflicts
    private bool releasedForShooting = false;
    
    private void Awake()
    {
        // Create puck hold position if not assigned
        if (puckHoldPosition == null)
        {
            GameObject holdPos = new GameObject("PuckHoldPosition");
            holdPos.transform.SetParent(transform);
            holdPos.transform.localPosition = new Vector3(0, 0.5f, 1.5f);
            holdPos.transform.localRotation = Quaternion.identity;
            puckHoldPosition = holdPos.transform;
            
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: Created puck hold position");
            }
        }
        
        // Ensure PlayerShooting component exists
        if (GetComponent<PlayerShooting>() == null)
        {
            var playerShooting = gameObject.AddComponent<PlayerShooting>();
            if (enableDebugLogs)
            {
                Debug.Log("PuckPickup: Added PlayerShooting component automatically");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        networkHasPuck.OnValueChanged += OnNetworkHasPuckChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkHasPuck.OnValueChanged -= OnNetworkHasPuckChanged;
        base.OnNetworkDespawn();
    }

    private void OnNetworkHasPuckChanged(bool oldValue, bool newValue)
    {
        // FIXED: Only update hasPuck for non-owners (remote clients)
        if (!IsOwner)
        {
            hasPuck = newValue;
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: [Remote] Network state changed - HasPuck: {hasPuck}");
            }
        }
        // For owner, let local logic handle hasPuck
    }

    private void Update()
    {
        // Only process input for the owner
        if (!IsOwner) return;

        HandleInput();

        // --- FIX: For the local player, always force the puck to follow the hold position if we have it ---
        if (hasPuck && currentPuck != null && !releasedForShooting)
        {
            if (puckHoldPosition != null)
            {
                // Always set the puck's parent and position every frame for the local player
                if (currentPuck.transform.parent != puckHoldPosition)
                {
                    currentPuck.transform.SetParent(puckHoldPosition, true);
                }
                currentPuck.transform.position = puckHoldPosition.position;
                currentPuck.transform.rotation = puckHoldPosition.rotation;
            }
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(pickupKey))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: E key pressed. HasPuck: {hasPuck}, ReleasedForShooting: {releasedForShooting}");
            }

            if (hasPuck && currentPuck != null && !releasedForShooting)
            {
                // Drop/release puck manually
                if (enableDebugLogs)
                {
                    Debug.Log("PuckPickup: Manually dropping puck via E key");
                }
                ManualReleasePuck();
            }
            else if (!hasPuck)
            {
                // Try to pick up puck
                if (enableDebugLogs)
                {
                    Debug.Log("PuckPickup: Trying to pick up puck");
                }
                TryPickupPuck();
            }
        }
    }

    private void TryPickupPuck()
    {
        // Find nearest puck
        Puck nearestPuck = FindNearestPuck();

        if (nearestPuck != null)
        {
            float distance = Vector3.Distance(transform.position, nearestPuck.transform.position);
            
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: Found puck at distance {distance:F2}m (max: {pickupRange}m)");
            }

            if (distance <= pickupRange)
            {
                // FIXED: Reset shooting release flag
                releasedForShooting = false;
                
                // Immediate local pickup for responsiveness
                currentPuck = nearestPuck;
                hasPuck = true;

                // Disable any PuckFollower
                var puckFollower = nearestPuck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    // Disable follower if using parenting, or enable and set target if using follower
                    if (puckHoldPosition != null)
                    {
                        puckFollower.StopFollowing();
                        puckFollower.enabled = false;
                    }
                    else
                    {
                        puckFollower.StartFollowing(transform, new Vector3(0, 0.5f, 1.5f));
                        puckFollower.enabled = true;
                    }
                }

                // Setup hold position
                if (puckHoldPosition != null)
                {
                    puckHoldPosition.localPosition = new Vector3(0, 0.5f, 1.5f);
                    puckHoldPosition.localRotation = Quaternion.identity;
                    
                    nearestPuck.transform.position = puckHoldPosition.position;
                    nearestPuck.transform.rotation = puckHoldPosition.rotation;
                    nearestPuck.transform.SetParent(puckHoldPosition, false);
                    nearestPuck.transform.localPosition = Vector3.zero;
                    nearestPuck.transform.localRotation = Quaternion.identity;
                }

                // Configure physics for pickup
                var col = nearestPuck.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                var rb = nearestPuck.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // Set puck as held
                nearestPuck.SetHeld(true);

                // Send to server for network sync
                if (NetworkManager.Singleton != null && IsSpawned)
                {
                    var puckNetObj = nearestPuck.GetComponent<NetworkObject>();
                    if (puckNetObj != null)
                    {
                        PickupPuckServerRpc(puckNetObj.NetworkObjectId);
                    }
                }

                if (enableDebugLogs)
                {
                    Debug.Log($"PuckPickup: Successfully picked up puck!");
                }
            }
        }
    }

    private Puck FindNearestPuck()
    {
        var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
        Puck nearestPuck = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var puck in allPucks)
        {
            if (puck == null) continue;
            
            // FIXED: Skip pucks that are held or released for shooting
            bool isHeld = false;
            try
            {
                isHeld = puck.IsHeld();
            }
            catch
            {
                isHeld = false;
            }
            
            if (isHeld) continue;

            float distance = Vector3.Distance(transform.position, puck.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPuck = puck;
            }
        }
        
        return nearestPuck;
    }

    [ServerRpc]
    private void PickupPuckServerRpc(ulong puckNetworkId)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"PuckPickup: [ServerRpc] Processing pickup for puck {puckNetworkId}");
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var networkObject))
        {
            var puck = networkObject.GetComponent<Puck>();
            if (puck != null)
            {
                currentPuck = puck;
                networkHasPuck.Value = true;
                releasedForShooting = false; // Reset on server too

                try
                {
                    puck.PickupByPlayer(this);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"PuckPickup: PickupByPlayer failed: {e.Message}");
                }

                // FIXED: Notify all clients to visually attach the puck to this player
                AttachPuckClientRpc(puckNetworkId, NetworkObjectId);

                OnPuckPickedUpClientRpc();
            }
        }
    }

    // FIXED: Attach puck visually to the correct player on all clients
    [ClientRpc]
    private void AttachPuckClientRpc(ulong puckNetworkId, ulong playerNetworkId)
    {
        // Find the puck and player objects
        var puckObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var puckNetObj) ? puckNetObj.gameObject : null;
        var playerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out var playerNetObj) ? playerNetObj.gameObject : null;

        if (puckObj == null || playerObj == null)
        {
            Debug.LogWarning("PuckPickup: AttachPuckClientRpc could not find puck or player object.");
            return;
        }

        var puckHold = playerObj.GetComponent<PuckPickup>()?.GetPuckHoldPosition();
        bool isLocalPlayer = playerObj.GetComponent<NetworkObject>().IsLocalPlayer;

        // --- FIX: For the local player, set the puck's world position to the hold position before parenting ---
        if (puckHold != null)
        {
            if (isLocalPlayer)
            {
                // Set world position to hold position before parenting
                puckObj.transform.position = puckHold.position;
                puckObj.transform.rotation = puckHold.rotation;
                puckObj.transform.SetParent(puckHold, true);
            }
            else
            {
                puckObj.transform.position = puckHold.position;
                puckObj.transform.rotation = puckHold.rotation;
                puckObj.transform.SetParent(puckHold, false);
                puckObj.transform.localPosition = Vector3.zero;
                puckObj.transform.localRotation = Quaternion.identity;
            }
        }
        else
        {
            Vector3 fallbackPos = playerObj.transform.position + playerObj.transform.forward * 1.5f + Vector3.up * 0.5f;
            if (isLocalPlayer)
            {
                puckObj.transform.position = fallbackPos;
                puckObj.transform.rotation = playerObj.transform.rotation;
                puckObj.transform.SetParent(playerObj.transform, true);
            }
            else
            {
                puckObj.transform.position = fallbackPos;
                puckObj.transform.rotation = playerObj.transform.rotation;
                puckObj.transform.SetParent(playerObj.transform, false);
                puckObj.transform.localPosition = new Vector3(0, 0.5f, 1.5f);
                puckObj.transform.localRotation = Quaternion.identity;
            }
        }

        // Ensure collider and physics are disabled for held puck
        var col = puckObj.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var rb = puckObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // For the local player, set currentPuck and hasPuck so Update() will move the puck every frame
        if (isLocalPlayer)
        {
            var pickup = playerObj.GetComponent<PuckPickup>();
            if (pickup != null)
            {
                pickup.currentPuck = puckObj.GetComponent<Puck>();
                pickup.hasPuck = true;
                pickup.releasedForShooting = false;
            }
        }
    }

    // FIXED: Manual release puck (for E key)
    private void ManualReleasePuck()
    {
        if (currentPuck == null) return;

        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Manual release puck");
        }

        releasedForShooting = false; // This is a manual release, not for shooting

        // FIX: Use a proper release position in front of the player, not Vector3.zero
        Vector3 releasePosition = transform.position + transform.forward * 2f;
        releasePosition.y = 0.71f;

        // Unparent and position
        currentPuck.transform.SetParent(null);
        currentPuck.transform.position = releasePosition;
        currentPuck.transform.rotation = Quaternion.identity;

        // Re-enable PuckFollower if it exists
        var puckFollower = currentPuck.GetComponent<PuckFollower>();
        if (puckFollower != null)
        {
            puckFollower.StopFollowing();
            puckFollower.enabled = false;
        }

        // Enable collider and physics
        var col = currentPuck.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        var rb = currentPuck.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Set puck as not held
        currentPuck.SetHeld(false);

        // Clear local state
        currentPuck = null;
        hasPuck = false;

        // Send to server for network sync
        if (IsServer)
        {
            networkHasPuck.Value = false;
        }
        else if (NetworkManager.Singleton != null && IsSpawned)
        {
            ReleasePuckServerRpc();
        }
    }

    // FIXED: Release puck for shooting (called by PlayerShooting)
    public void ReleasePuckForShooting()
    {
        if (currentPuck == null) return;
        
        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Release puck FOR SHOOTING");
        }
        
        releasedForShooting = true; // Mark as released for shooting
        InternalReleasePuck();
    }

    // FIXED: Internal release method used by both manual and shooting releases
    private void InternalReleasePuck()
    {
        if (currentPuck == null) return;

        var puck = currentPuck;
        
        // Calculate release position
        Vector3 releasePosition = transform.position + transform.forward * 2f;
        releasePosition.y = 0.71f;
        
        // Unparent and position
        puck.transform.SetParent(null);
        puck.transform.position = releasePosition;
        puck.transform.rotation = Quaternion.identity;

        // Re-enable PuckFollower if it exists
        var puckFollower = puck.GetComponent<PuckFollower>();
        if (puckFollower != null)
        {
            puckFollower.enabled = true;
        }

        // Enable collider and physics
        var col = puck.GetComponent<Collider>();
        if (col != null) 
        {
            col.enabled = true;
        }

        var rb = puck.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Set puck as not held
        puck.SetHeld(false);
        
        // Clear local state
        currentPuck = null;
        hasPuck = false;
        
        // Send to server for network sync
        if (IsServer)
        {
            networkHasPuck.Value = false;
        }
        else if (NetworkManager.Singleton != null && IsSpawned)
        {
            ReleasePuckServerRpc();
        }
    }

    [ServerRpc]
    private void ReleasePuckServerRpc()
    {
        if (currentPuck != null)
        {
            Vector3 releaseVelocity = transform.forward * 5f;
            
            try
            {
                currentPuck.ReleaseFromPlayer(puckHoldPosition.position, releaseVelocity);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"PuckPickup: ReleaseFromPlayer failed: {e.Message}");
            }
            
            currentPuck = null;
            networkHasPuck.Value = false;
            OnPuckReleasedClientRpc();
        }
    }

    [ClientRpc]
    private void OnPuckPickedUpClientRpc()
    {
        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Puck picked up (ClientRpc)");
        }
    }

    [ClientRpc]
    private void OnPuckReleasedClientRpc()
    {
        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Puck released (ClientRpc)");
        }
    }

    // Public methods for other scripts
    public bool HasPuck()
    {
        return hasPuck && !releasedForShooting;
    }

    public Puck GetCurrentPuck()
    {
        return releasedForShooting ? null : currentPuck;
    }

    public Transform GetPuckHoldPosition()
    {
        return puckHoldPosition;
    }

    // FIXED: Method for PlayerShooting to check if it can shoot
    public bool CanShootPuck()
    {
        return hasPuck && currentPuck != null && !releasedForShooting;
    }

    // FIXED: Method to reset shooting flag after shot completes
    public void ResetShootingFlag()
    {
        releasedForShooting = false;
        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Reset shooting flag - ready for new pickup");
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
        
        // Draw puck hold position
        if (puckHoldPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(puckHoldPosition.position, 0.2f);
        }
    }
}
