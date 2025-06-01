using UnityEngine;
using Unity.Netcode;
using HockeyGame.Game; // FIXED: Add namespace for GoalTrigger

public class Puck : NetworkBehaviour
{
    [Header("Puck Settings")]
    [SerializeField] private float friction = 0.95f;
    [SerializeField] private float minVelocity = 0.1f;
    [SerializeField] private bool enableDebugLogs = true;
    
    private Rigidbody puckRigidbody;
    private PuckPickup currentHolder;
    private bool isHeld = false;
    
    // Network variable to sync held state
    private NetworkVariable<bool> networkIsHeld = new NetworkVariable<bool>(false);
    
    private void Awake()
    {
        puckRigidbody = GetComponent<Rigidbody>();
        if (puckRigidbody == null)
        {
            puckRigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        // Ensure proper puck physics
        puckRigidbody.mass = 0.16f; // Standard hockey puck mass
        puckRigidbody.linearDamping = 0.5f;
        puckRigidbody.angularDamping = 0.5f;
        
        // Ensure proper tag and layer
        if (!CompareTag("Puck"))
        {
            tag = "Puck";
        }
        
        if (gameObject.layer != 7) // Layer 7 for pucks
        {
            gameObject.layer = 7;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: Initialized with mass {puckRigidbody.mass}kg on layer {gameObject.layer}");
        }
    }
    
    private void Update()
    {
        // Sync local state with network state
        bool networkState = networkIsHeld.Value;
        if (isHeld != networkState)
        {
            isHeld = networkState;
            if (enableDebugLogs)
            {
                Debug.Log($"Puck: Synced held state to network: {isHeld}");
            }
        }
    }
    
    private void FixedUpdate()
    {
        // FIXED: Only apply physics when NOT kinematic and NOT held
        if (puckRigidbody == null || puckRigidbody.isKinematic || isHeld)
        {
            return;
        }
        
        // Apply friction to slow down the puck over time
        if (puckRigidbody.linearVelocity.magnitude > minVelocity)
        {
            puckRigidbody.linearVelocity *= friction;
        }
        else
        {
            // Stop very slow movement
            puckRigidbody.linearVelocity = Vector3.zero;
        }
        
        // Apply angular friction
        if (puckRigidbody.angularVelocity.magnitude > 0.1f)
        {
            puckRigidbody.angularVelocity *= friction;
        }
        else
        {
            puckRigidbody.angularVelocity = Vector3.zero;
        }
        
        // Keep puck on ice level
        Vector3 pos = transform.position;
        if (pos.y != 0.71f)
        {
            pos.y = 0.71f;
            transform.position = pos;
        }
    }
    
    public bool IsHeld()
    {
        return isHeld;
    }
    
    public void PickupByPlayer(PuckPickup pickup)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: PickupByPlayer called by {pickup.name}");
        }
        
        currentHolder = pickup;
        isHeld = true;
        
        // Update network state if we're the server
        if (IsServer)
        {
            networkIsHeld.Value = true;
        }
        
        // FIXED: Position puck in front of player immediately
        Vector3 holdWorldPosition = pickup.transform.position + pickup.transform.forward * 1.5f + Vector3.up * 0.5f;
        transform.position = holdWorldPosition;
        transform.rotation = pickup.transform.rotation;
        
        // FIXED: Parent to hold position with world position maintenance
        Transform holdPosition = pickup.GetPuckHoldPosition();
        if (holdPosition != null)
        {
            // Parent with world position stays to maintain current position
            transform.SetParent(holdPosition, true);
            
            if (enableDebugLogs)
            {
                Debug.Log($"Puck: Parented to hold position at world pos: {transform.position}");
            }
        }
        else
        {
            // Fallback: parent directly to player
            transform.SetParent(pickup.transform, true);
            
            if (enableDebugLogs)
            {
                Debug.Log($"Puck: Parented directly to player at world pos: {transform.position}");
            }
        }
        
        // Configure for pickup
        if (puckRigidbody != null)
        {
            puckRigidbody.isKinematic = true;
            puckRigidbody.useGravity = false;
        }
        
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: Successfully picked up at final position {transform.position}");
        }
    }

    public void ReleaseFromPlayer(Vector3 position, Vector3 velocity)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: ReleaseFromPlayer called at {position} with velocity {velocity}");
        }
        
        currentHolder = null;
        isHeld = false;
        
        // Update network state if we're the server
        if (IsServer)
        {
            networkIsHeld.Value = false;
        }
        
        // FIXED: Unparent first, then set position
        transform.SetParent(null);
        
        // FIXED: Ensure proper release position (in front of player, not at (0,0,0))
        if (position == Vector3.zero)
        {
            // Emergency fallback if position is zero
            position = new Vector3(0f, 0.71f, 0f);
            Debug.LogWarning("Puck: Release position was zero, using center as fallback");
        }
        
        transform.position = position;
        transform.rotation = Quaternion.identity;
        
        // Configure for release
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true;
        }
        
        if (puckRigidbody != null)
        {
            puckRigidbody.isKinematic = false;
            puckRigidbody.useGravity = true;
            puckRigidbody.linearVelocity = velocity;
            puckRigidbody.angularVelocity = Vector3.zero;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: Successfully released at position {transform.position} with velocity {velocity}");
        }
    }
    
    // ADDED: Method to force clear held state (for shooting system)
    public void SetHeld(bool held)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: SetHeld called with value: {held}");
        }
        
        isHeld = held;
        
        // Update network state if we're the server
        if (IsServer)
        {
            networkIsHeld.Value = held;
        }
        
        if (!held)
        {
            currentHolder = null;
            
            // Only enable physics if we're not kinematic for other reasons
            if (puckRigidbody != null && puckRigidbody.isKinematic)
            {
                puckRigidbody.isKinematic = false;
                puckRigidbody.useGravity = true;
            }
            
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.enabled)
            {
                collider.enabled = true;
            }
            
            // Only unparent if we're actually parented to a hold position
            if (transform.parent != null && 
                (transform.parent.name.Contains("Hold") || transform.parent.name.Contains("Puck")))
            {
                transform.SetParent(null);
            }
        }
    }
    
    // ADDED: Method to apply shooting force properly
    public void ApplyShootForce(Vector3 force)
    {
        if (puckRigidbody == null || puckRigidbody.isKinematic)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"Puck: Cannot apply shoot force - rigidbody is null or kinematic");
            }
            return;
        }
        
        puckRigidbody.AddForce(force, ForceMode.VelocityChange);
        
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: Applied shoot force {force}, resulting velocity: {puckRigidbody.linearVelocity}");
        }
    }
    
    // ADDED: Reset puck to center (for goals)
    public void ResetToCenter()
    {
        if (enableDebugLogs)
        {
            Debug.Log("Puck: Resetting to center");
        }
        
        // Clear held state
        SetHeld(false);
        
        // Position at center
        Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
        transform.position = centerPos;
        transform.rotation = Quaternion.identity;
        
        // Stop all movement
        if (puckRigidbody != null)
        {
            puckRigidbody.linearVelocity = Vector3.zero;
            puckRigidbody.angularVelocity = Vector3.zero;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Handle collisions with walls, players, etc.
        if (enableDebugLogs && !isHeld)
        {
            Debug.Log($"Puck: Collided with {collision.gameObject.name}");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Handle goal detection
        if (other.CompareTag("Goal"))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Puck: Entered goal trigger {other.name}");
            }
            
            // FIXED: Remove reference to non-existent Goal class
            // Only use GoalTrigger which exists in HockeyGame.Game namespace
            var goalTrigger = other.GetComponent<GoalTrigger>();
            if (goalTrigger != null)
            {
                // GoalTrigger will handle the goal logic automatically
                if (enableDebugLogs)
                {
                    Debug.Log($"Puck: Found GoalTrigger component on {other.name}");
                }
            }
            else
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"Puck: Goal object {other.name} has Goal tag but no GoalTrigger component!");
                }
            }
        }
    }
}
