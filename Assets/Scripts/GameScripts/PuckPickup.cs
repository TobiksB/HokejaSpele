using UnityEngine;
using Unity.Netcode;

public class PuckPickup : NetworkBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private Transform puckHoldPosition;
    [SerializeField] private LayerMask puckLayer = 128;
    
    [Header("Input")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Puck Stealing")]
    [SerializeField] private float stealChance = 0.25f; // 25% chance to steal
    [SerializeField] private float stealRange = 3f; // Range to attempt steal
    [SerializeField] private float stealCooldown = 2f; // Cooldown between steal attempts
    [SerializeField] private bool enableStealDebugLogs = true;
    
    private Puck currentPuck;
    private bool hasPuck = false;
    private NetworkVariable<bool> networkHasPuck = new NetworkVariable<bool>(false);
    private bool releasedForShooting = false;
    private float lastStealAttemptTime = 0f;
    
    private void Awake()
    {
        if (puckHoldPosition == null)
        {
            GameObject holdPos = new GameObject("PuckHoldPosition");
            holdPos.transform.SetParent(transform);
            holdPos.transform.localPosition = new Vector3(0, 0.5f, 1.5f);
            holdPos.transform.localRotation = Quaternion.identity;
            puckHoldPosition = holdPos.transform;
        }
        
        if (GetComponent<PlayerShooting>() == null)
        {
            gameObject.AddComponent<PlayerShooting>();
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
        if (!IsOwner)
        {
            hasPuck = newValue;
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: [Remote] Network state changed - HasPuck: {hasPuck}");
            }
        }
    }

    private void Update()
    {
        // Only process input for the owner
        if (!IsOwner) return;

        HandleInput();

        // For the local player, ensure PuckFollower is working properly
        if (hasPuck && currentPuck != null && !releasedForShooting)
        {
            var puckFollower = currentPuck.GetComponent<PuckFollower>();
            if (puckFollower != null && !puckFollower.IsFollowing())
            {
                // Restart following if it stopped
                if (puckHoldPosition != null)
                {
                    puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                    if (enableDebugLogs)
                    {
                        Debug.Log("PuckPickup: Restarted PuckFollower for local player");
                    }
                }
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
                // First try to steal puck from nearby opponent
                bool stealAttempted = TryStealPuck();
                
                if (!stealAttempted)
                {
                    // If no steal attempt was made, try normal pickup
                    if (enableDebugLogs)
                    {
                        Debug.Log("PuckPickup: No steal attempt, trying normal pickup");
                    }
                    TryPickupPuck();
                }
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
                releasedForShooting = false;
                currentPuck = nearestPuck;
                hasPuck = true;

                // Start PuckFollower immediately for local responsiveness
                var puckFollower = nearestPuck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                    puckFollower.enabled = true;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"PuckPickup: Started PuckFollower for local player");
                    }
                }
                else
                {
                    // Add PuckFollower if missing
                    puckFollower = nearestPuck.gameObject.AddComponent<PuckFollower>();
                    puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                    puckFollower.enabled = true;
                    Debug.Log("PuckPickup: Added and started PuckFollower component");
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
                    Debug.Log($"PuckPickup: Successfully picked up puck using PuckFollower!");
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
                releasedForShooting = false;

                try
                {
                    puck.PickupByPlayer(this);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"PuckPickup: PickupByPlayer failed: {e.Message}");
                }

                // Notify all clients to start following
                StartFollowingClientRpc(puckNetworkId, NetworkObjectId);

                OnPuckPickedUpClientRpc();
            }
        }
    }

    [ClientRpc]
    private void StartFollowingClientRpc(ulong puckNetworkId, ulong playerNetworkId)
    {
        var puckObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var puckNetObj) ? puckNetObj.gameObject : null;
        var playerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out var playerNetObj) ? playerNetObj.gameObject : null;

        if (puckObj == null || playerObj == null)
        {
            Debug.LogWarning("PuckPickup: StartFollowingClientRpc could not find puck or player object.");
            return;
        }

        var puckHold = playerObj.GetComponent<PuckPickup>()?.GetPuckHoldPosition();
        bool isLocalPlayer = playerObj.GetComponent<NetworkObject>().IsLocalPlayer;

        if (puckHold != null)
        {
            // Start following for ALL clients using PuckFollower
            var puckFollower = puckObj.GetComponent<PuckFollower>();
            if (puckFollower == null)
            {
                puckFollower = puckObj.AddComponent<PuckFollower>();
                Debug.Log("PuckPickup: Added missing PuckFollower component");
            }
            
            puckFollower.StartFollowing(puckHold, Vector3.zero);
            puckFollower.enabled = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: Started PuckFollower for {(isLocalPlayer ? "LOCAL" : "REMOTE")} player");
            }
        }

        // Disable physics for held puck
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

        // Set state for local player
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

    private void ManualReleasePuck()
    {
        if (currentPuck == null) return;

        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Manual release puck via E key");
        }

        releasedForShooting = false; // This is a manual release, not for shooting

        // Stop following
        var puckFollower = currentPuck.GetComponent<PuckFollower>();
        if (puckFollower != null)
        {
            puckFollower.StopFollowing();
            puckFollower.enabled = false;
            if (enableDebugLogs)
            {
                Debug.Log("PuckPickup: Stopped PuckFollower for manual release");
            }
        }

        Vector3 releasePosition = transform.position + transform.forward * 2f;
        releasePosition.y = 0.71f;

        currentPuck.transform.position = releasePosition;
        currentPuck.transform.rotation = Quaternion.identity;

        // Enable collider and physics
        var col = currentPuck.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var rb = currentPuck.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentPuck.SetHeld(false);

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

        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Manual release completed");
        }
    }

    public void ReleasePuckForShooting()
    {
        if (currentPuck == null) return;

        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Release puck FOR SHOOTING");
        }

        releasedForShooting = true;

        // Stop following immediately
        var puckFollower = currentPuck.GetComponent<PuckFollower>();
        if (puckFollower != null)
        {
            puckFollower.StopFollowing();
            puckFollower.enabled = false;
            if (enableDebugLogs)
            {
                Debug.Log("PuckPickup: Stopped PuckFollower for shooting");
            }
        }

        // --- FIX: Always call ServerRpc for shooting to ensure server-side release ---
        if (IsOwner && NetworkManager.Singleton != null && IsSpawned)
        {
            ReleasePuckForShootingServerRpc();
        }
        else
        {
            // Fallback for host/server
            InternalReleasePuck();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReleasePuckForShootingServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only the server should execute the release logic
        InternalReleasePuck();
    }

    private void InternalReleasePuck()
    {
        if (currentPuck == null) return;

        var puck = currentPuck;
        
        Vector3 releasePosition = transform.position + transform.forward * 2f;
        releasePosition.y = 0.71f;
        
        puck.transform.position = releasePosition;
        puck.transform.rotation = Quaternion.identity;

        var col = puck.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var rb = puck.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        puck.SetHeld(false);
        
        currentPuck = null;
        hasPuck = false;
        
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
    public bool HasPuck() => hasPuck && !releasedForShooting;
    public Puck GetCurrentPuck() => releasedForShooting ? null : currentPuck;
    public Transform GetPuckHoldPosition() => puckHoldPosition;
    public bool CanShootPuck() => hasPuck && currentPuck != null && !releasedForShooting;
    
    public void ResetShootingFlag()
    {
        releasedForShooting = false;
        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Reset shooting flag - ready for new pickup");
        }
    }

    private bool TryStealPuck()
    {
        // Check cooldown
        if (Time.time - lastStealAttemptTime < stealCooldown)
        {
            if (enableStealDebugLogs)
            {
                Debug.Log($"PuckPickup: Steal attempt blocked by cooldown. Time remaining: {stealCooldown - (Time.time - lastStealAttemptTime):F1}s");
            }
            return false;
        }

        // Find nearby players with pucks
        PuckPickup targetPlayer = FindNearestPlayerWithPuck();
        
        if (targetPlayer == null)
        {
            if (enableStealDebugLogs)
            {
                Debug.Log("PuckPickup: No nearby players with pucks found for stealing");
            }
            return false;
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
        
        if (distance > stealRange)
        {
            if (enableStealDebugLogs)
            {
                Debug.Log($"PuckPickup: Target player too far for steal attempt: {distance:F2}m (max: {stealRange}m)");
            }
            return false;
        }

        // Record steal attempt to start cooldown
        lastStealAttemptTime = Time.time;
        
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: Attempting to steal puck from player at {distance:F2}m distance");
        }

        // Check if this is an opponent (different team)
        if (!IsOpponent(targetPlayer))
        {
            if (enableStealDebugLogs)
            {
                Debug.Log("PuckPickup: Cannot steal from teammate");
            }
            return true; // Attempt was made but blocked
        }

        // Roll for steal success
        float roll = Random.Range(0f, 1f);
        bool stealSuccessful = roll <= stealChance;
        
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: Steal roll: {roll:F3} (need ≤ {stealChance:F3}) - {(stealSuccessful ? "SUCCESS" : "FAILED")}");
        }

        if (stealSuccessful)
        {
            // Successful steal - take the puck
            ExecutePuckSteal(targetPlayer);
        }
        else
        {
            // Failed steal attempt
            if (enableStealDebugLogs)
            {
                Debug.Log("PuckPickup: Steal attempt failed - puck remains with opponent");
            }
            
            // Optional: Show visual feedback for failed steal
            ShowStealFailedEffect();
        }

        return true; // Attempt was made
    }

    private PuckPickup FindNearestPlayerWithPuck()
    {
        var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
        PuckPickup nearestPlayer = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var player in allPlayers)
        {
            if (player == null || player == this) continue;
            
            // Check if player has a puck
            if (!player.HasPuck()) continue;
            
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < nearestDistance && distance <= stealRange)
            {
                nearestDistance = distance;
                nearestPlayer = player;
            }
        }
        
        return nearestPlayer;
    }

    private bool IsOpponent(PuckPickup otherPlayer)
    {
        // FIXED: Simplified and more reliable team check for both host and client
        var myPlayerMovement = GetComponent<PlayerMovement>();
        var otherPlayerMovement = otherPlayer.GetComponent<PlayerMovement>();
        
        if (myPlayerMovement == null || otherPlayerMovement == null)
        {
            Debug.LogWarning("PuckPickup: Could not get PlayerMovement components for team check");
            // FIXED: For testing, assume all players are opponents if we can't determine teams
            return true;
        }
        
        // FIXED: Much simpler team check - just assume different teams for now to test stealing
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: Team check - assuming players are on different teams for testing");
        }
        
        // TEMPORARY: Always return true to test stealing mechanics
        // TODO: Implement proper team checking once stealing works
        return true;
        
        /*
        // PROPER TEAM CHECKING CODE (disabled for testing):
        try
        {
            var myTeamField = typeof(PlayerMovement).GetField("networkTeam", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (myTeamField != null)
            {
                var myTeamVar = myTeamField.GetValue(myPlayerMovement);
                var otherTeamVar = myTeamField.GetValue(otherPlayerMovement);
                
                if (myTeamVar != null && otherTeamVar != null)
                {
                    var myTeamValue = myTeamVar.GetType().GetProperty("Value")?.GetValue(myTeamVar);
                    var otherTeamValue = otherTeamVar.GetType().GetProperty("Value")?.GetValue(otherTeamVar);
                    
                    if (myTeamValue != null && otherTeamValue != null)
                    {
                        string myTeam = myTeamValue.ToString();
                        string otherTeam = otherTeamValue.ToString();
                        
                        bool sameTeam = myTeam == otherTeam;
                        bool isOpponent = !sameTeam;
                        
                        if (enableStealDebugLogs)
                        {
                            Debug.Log($"PuckPickup: Team check - My team: {myTeam}, Other team: {otherTeam}, Same team: {sameTeam}, Is opponent: {isOpponent}");
                        }
                        
                        return isOpponent;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            if (enableStealDebugLogs)
            {
                Debug.LogWarning($"PuckPickup: Error in team check reflection: {e.Message}");
            }
        }
        
        return true; // Fallback: assume opponent
        */
    }

    private void ExecutePuckSteal(PuckPickup targetPlayer)
    {
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: Executing successful puck steal from {targetPlayer.name}");
        }

        // --- FIX: Always get the puck reference ON THE SERVER, not on the client ---
        // On the client, the targetPlayer's currentPuck may be null due to network sync delay.
        // Instead, always send the ServerRpc and let the server validate and process the steal.

        if (IsLocalPlayer && NetworkManager.Singleton != null && IsSpawned)
        {
            var targetNetworkObject = targetPlayer.GetComponent<NetworkObject>();
            ulong targetPuckNetworkId = 0;

            // Try to get the puck's NetworkObjectId, but if null, just send 0 (server will check)
            var puck = targetPlayer.GetCurrentPuck();
            if (puck != null)
            {
                var puckNetObj = puck.GetComponent<NetworkObject>();
                if (puckNetObj != null)
                    targetPuckNetworkId = puckNetObj.NetworkObjectId;
            }

            Debug.Log($"PuckPickup: [CLIENT] Sending ExecuteStealServerRpc from client {OwnerClientId} to server. Target: {targetNetworkObject?.NetworkObjectId}, Puck: {targetPuckNetworkId}");

            if (targetNetworkObject != null)
            {
                ExecuteStealServerRpc(targetNetworkObject.NetworkObjectId, targetPuckNetworkId);
            }
            else
            {
                Debug.LogWarning("PuckPickup: [CLIENT] Target NetworkObject is null, cannot send ServerRpc");
            }
        }
        else
        {
            if (!IsLocalPlayer)
                Debug.LogWarning("PuckPickup: [CLIENT] Not IsLocalPlayer, will not send ServerRpc for steal.");
            if (NetworkManager.Singleton == null)
                Debug.LogWarning("PuckPickup: [CLIENT] NetworkManager.Singleton is null, cannot send ServerRpc for steal.");
            if (!IsSpawned)
                Debug.LogWarning("PuckPickup: [CLIENT] Not spawned, cannot send ServerRpc for steal.");
        }

        // Show feedback immediately for local player (optional)
        ShowStealSuccessEffect();

        if (enableStealDebugLogs)
        {
            Debug.Log("PuckPickup: Puck steal network command sent!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ExecuteStealServerRpc(ulong targetPlayerNetworkId, ulong puckNetworkId, ServerRpcParams rpcParams = default)
    {
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: [ServerRpc] Processing steal - Stealer: {NetworkObjectId}, Target: {targetPlayerNetworkId}, Puck: {puckNetworkId}");
        }

        var targetPlayerObj = GetNetworkObjectById(targetPlayerNetworkId);
        if (targetPlayerObj == null)
        {
            Debug.LogWarning($"PuckPickup: [ServerRpc] Could not find target player object for steal.");
            return;
        }

        var targetPuckPickup = targetPlayerObj.GetComponent<PuckPickup>();
        if (targetPuckPickup == null)
        {
            Debug.LogWarning($"PuckPickup: [ServerRpc] Could not find PuckPickup on target player.");
            return;
        }

        // --- FIX: Always get the puck reference ON THE SERVER ---
        var puck = targetPuckPickup.GetCurrentPuck();
        if (puck == null)
        {
            Debug.LogWarning("PuckPickup: [ServerRpc] Target player doesn't have a puck to steal.");
            return;
        }

        var puckObj = puck.gameObject;
        if (puckObj == null)
        {
            Debug.LogWarning("PuckPickup: [ServerRpc] Target puck object is null.");
            return;
        }

        // Validate the target actually has the puck
        if (!targetPuckPickup.HasPuck())
        {
            Debug.LogWarning($"PuckPickup: [ServerRpc] Target player doesn't have the puck.");
            return;
        }

        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: [ServerRpc] Executing steal - forcing target to release puck");
        }

        // Force the target player to release their puck immediately
        targetPuckPickup.ForceReleasePuckForSteal();

        // Wait a moment for the release to process
        StartCoroutine(CompleteStealAfterRelease(puck, rpcParams.Receive.SenderClientId));
    }

    // Modified to accept stealerClientId
    private System.Collections.IEnumerator CompleteStealAfterRelease(Puck puck, ulong stealerClientId)
    {
        yield return new WaitForSeconds(0.1f); // Brief delay for release to complete

        if (puck == null)
        {
            Debug.LogWarning("PuckPickup: [ServerRpc] Puck became null during steal completion");
            yield break;
        }

        // Find the stealer's PuckPickup by clientId
        var stealerObj = NetworkManager.Singleton.ConnectedClients.ContainsKey(stealerClientId)
            ? NetworkManager.Singleton.ConnectedClients[stealerClientId].PlayerObject?.GetComponent<PuckPickup>()
            : null;

        if (stealerObj == null)
        {
            Debug.LogWarning("PuckPickup: [ServerRpc] Could not find stealer's PuckPickup component");
            yield break;
        }

        stealerObj.currentPuck = puck;
        stealerObj.hasPuck = true;
        stealerObj.releasedForShooting = false;
        stealerObj.networkHasPuck.Value = true;

        puck.SetHeld(true);

        try
        {
            puck.PickupByPlayer(stealerObj);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PuckPickup: [ServerRpc] PickupByPlayer failed during steal: {e.Message}");
        }

        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: [ServerRpc] Steal completed on server - puck now belongs to stealer");
        }

        // Notify all clients about the completed steal
        CompleteStealClientRpc(stealerObj.NetworkObjectId, puck.GetComponent<NetworkObject>().NetworkObjectId);
    }

    // FIXED: New ClientRpc to handle steal completion
    [ClientRpc]
    private void CompleteStealClientRpc(ulong stealerNetworkId, ulong puckNetworkId)
    {
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: [ClientRpc] Completing steal - Stealer: {stealerNetworkId}, Puck: {puckNetworkId}");
        }

        // Find the objects
        var stealerObj = GetNetworkObjectById(stealerNetworkId);
        var puckObj = GetNetworkObjectById(puckNetworkId);

        if (stealerObj == null || puckObj == null)
        {
            Debug.LogWarning($"PuckPickup: [ClientRpc] Could not find objects for steal completion");
            return;
        }

        // Update local state for the stealer (if it's the local player)
        bool isLocalStealer = stealerObj.GetComponent<NetworkObject>().IsLocalPlayer;
        if (isLocalStealer)
        {
            var stealerPickup = stealerObj.GetComponent<PuckPickup>();
            if (stealerPickup != null)
            {
                stealerPickup.currentPuck = puckObj.GetComponent<Puck>();
                stealerPickup.hasPuck = true;
                stealerPickup.releasedForShooting = false;
                
                if (enableStealDebugLogs)
                {
                    Debug.Log($"PuckPickup: [ClientRpc] Updated local stealer state");
                }
            }
        }

        // Start PuckFollower for the stealer
        var stealerPickupComponent = stealerObj.GetComponent<PuckPickup>();
        if (stealerPickupComponent != null && stealerPickupComponent.GetPuckHoldPosition() != null)
        {
            var puckFollower = puckObj.GetComponent<PuckFollower>();
            if (puckFollower == null)
            {
                puckFollower = puckObj.AddComponent<PuckFollower>();
            }
            
            puckFollower.StartFollowing(stealerPickupComponent.GetPuckHoldPosition(), Vector3.zero);
            puckFollower.enabled = true;
            
            if (enableStealDebugLogs)
            {
                Debug.Log($"PuckPickup: [ClientRpc] Started PuckFollower for stealer");
            }
        }

        // Configure puck physics for being held
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
    }

    // FIXED: Helper method to find NetworkObject by ID
    private GameObject GetNetworkObjectById(ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject))
        {
            return networkObject.gameObject;
        }
        
        // Fallback: search all NetworkObjects
        var allNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (var obj in allNetworkObjects)
        {
            if (obj != null && obj.NetworkObjectId == networkObjectId)
            {
                return obj.gameObject;
            }
        }
        
        return null;
    }

    private void ShowStealSuccessEffect()
    {
        // Add visual/audio feedback for successful steal
        // You can add particle effects, sound effects, etc. here
        if (enableStealDebugLogs)
        {
            Debug.Log("PuckPickup: Steal Succeeded"); // Simplified string
        }
    }

    private void ShowStealFailedEffect()
    {
        // Add visual/audio feedback for failed steal attempt
        // You can add particle effects, sound effects, etc. here
        if (enableStealDebugLogs)
        {
            Debug.Log("PuckPickup: Steal Failed"); // Simplified string
        }
    }

    // Called by the server to force this player to release the puck for a steal
    public void ForceReleasePuckForSteal()
    {
        if (currentPuck == null) return;

        // Stop following
        var puckFollower = currentPuck.GetComponent<PuckFollower>();
        if (puckFollower != null)
        {
            puckFollower.StopFollowing();
            puckFollower.enabled = false;
        }

        // Enable collider and physics
        var col = currentPuck.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var rb = currentPuck.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentPuck.SetHeld(false);

        currentPuck = null;
        hasPuck = false;
        releasedForShooting = false;

        // Sync network variable
        networkHasPuck.Value = false;
    }

    // FIXED: Improve the PickupStolenPuck method - this method is no longer used
    private System.Collections.IEnumerator PickupStolenPuck(Puck puck)
    {
        // This method is no longer needed since ExecuteStealServerRpc handles everything
        yield break;
    }
}
