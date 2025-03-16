using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class Puck : NetworkBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float forwardOffset = 1f;
    [SerializeField] private float moveSpeed = 15f; // Renamed from followLerpSpeed

    private NetworkVariable<bool> isPickedUp = new NetworkVariable<bool>();
    private NetworkVariable<ulong> holderClientId = new NetworkVariable<ulong>();
    private Transform holder;
    private Rigidbody rb;
    private NetworkTransform networkTransform;
    private Vector3 initialPosition;

    private void Awake()
    {
        initialPosition = transform.position;
        
        networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null)
        {
            networkTransform.InLocalSpace = false;
            networkTransform.Interpolate = true;
        }

        // Force enable renderer
        if (TryGetComponent<MeshRenderer>(out var renderer))
        {
            renderer.enabled = true;
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.mass = 0.5f; // Lighter mass for better physics
        rb.linearDamping = 0.2f; // Low drag for ice-like movement
        rb.angularDamping = 0.5f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        holderClientId.OnValueChanged += OnHolderChanged;

        // Store initial spawn position
        if (spawnPoint != null)
        {
            initialPosition = spawnPoint.position;
            // Set initial position before network spawn
            transform.position = initialPosition;
            rb.position = initialPosition;
        }

        // Make sure the object is visible
        if (TryGetComponent<Renderer>(out var renderer))
        {
            renderer.enabled = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Initialize Rigidbody if not already done
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.mass = 1f;
        }

        // Ensure visibility
        gameObject.SetActive(true);
        if (TryGetComponent<MeshRenderer>(out var renderer))
        {
            renderer.enabled = true;
        }

        if (IsServer)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnEnable()
    {
        // Ensure visibility whenever the object is enabled
        if (TryGetComponent<Renderer>(out var renderer))
        {
            renderer.enabled = true;
        }
    }

    private void OnHolderChanged(ulong previousValue, ulong newValue)
    {
        if (IsServer)
        {
            if (newValue == ulong.MaxValue)
            {
                holder = null;
                UpdateHolderClientRpc(0);
            }
            else
            {
                var networkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(newValue);
                if (networkObject != null)
                {
                    holder = networkObject.transform;
                    UpdateHolderClientRpc(networkObject.NetworkObjectId);
                }
            }
        }
    }

    [ClientRpc]
    private void UpdateHolderClientRpc(ulong networkObjectId)
    {
        if (!IsServer)
        {
            if (networkObjectId == 0)
            {
                holder = null;
            }
            else
            {
                var networkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[networkObjectId];
                holder = networkObject?.transform;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        if (isPickedUp.Value && holder != null)
        {
            Vector3 targetPos = holder.position + holder.forward * forwardOffset;
            targetPos.y = spawnPoint != null ? spawnPoint.position.y : initialPosition.y;
            
            // Use moveSpeed for smoother following
            Vector3 newPos = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime);
            transform.position = newPos;
            rb.MovePosition(newPos);
            
            // Match holder rotation
            Quaternion targetRot = Quaternion.LookRotation(holder.forward);
            transform.rotation = targetRot;
            rb.MoveRotation(targetRot);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickUpServerRpc(ulong playerId)
    {
        Debug.Log($"PickUpServerRpc called by player {playerId}");
        
        // Clear velocities before making kinematic
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Set pickup state
        isPickedUp.Value = true;
        holderClientId.Value = playerId;
        rb.isKinematic = true;

        // Update position if holder exists
        if (holder != null)
        {
            Vector3 targetPos = holder.position + holder.forward * forwardOffset;
            targetPos.y = spawnPoint != null ? spawnPoint.position.y : initialPosition.y;
            transform.position = targetPos;
            rb.position = targetPos;
        }
    }

    public void PickUp(Transform newHolder)
    {
        Debug.Log($"PickUp called by {newHolder.name}");
        if (!NetworkObject.IsSpawned) 
        {
            Debug.LogError("Cannot pick up - NetworkObject not spawned");
            return;
        }

        NetworkObject holderNetObj = newHolder.GetComponent<NetworkObject>();
        if (holderNetObj == null)
        {
            Debug.LogError("Cannot pick up - No NetworkObject on holder");
            return;
        }

        PickUpServerRpc(holderNetObj.OwnerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ShootServerRpc(Vector3 direction, float force, bool isHighShot)
    {
        rb.isKinematic = false;
        isPickedUp.Value = false;
        holderClientId.Value = ulong.MaxValue;

        Vector3 shotDirection = direction;
        if (isHighShot)
        {
            // Small lift with forward momentum
            Vector3 liftVector = Vector3.up * 0.1f + direction * 0.9f;
            shotDirection = liftVector.normalized;
            rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
            rb.useGravity = true; // Enable gravity for natural arc
        }

        // Apply physics force
        rb.AddForce(shotDirection * force, ForceMode.Impulse);
        
        if (isHighShot)
        {
            StartCoroutine(ReturnToGround());
        }
    }

    private IEnumerator ReturnToGround()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (IsServer)
        {
            float groundY = spawnPoint != null ? spawnPoint.position.y : initialPosition.y;
            
            // Wait until puck is falling
            while (rb.linearVelocity.y > 0)
            {
                yield return null;
            }

            // When close to ground level, force position and disable gravity
            while (Mathf.Abs(transform.position.y - groundY) > 0.01f)
            {
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(pos.y, groundY, Time.deltaTime * 10f);
                transform.position = pos;
                yield return null;
            }

            rb.useGravity = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            rb.constraints |= RigidbodyConstraints.FreezePositionY;
        }
    }

    public void Shoot(Vector3 direction, float force, bool isHighShot = false)
    {
        if (!NetworkObject.IsSpawned) return;
        ShootServerRpc(direction, force, isHighShot);
    }

    public void ResetPosition()
    {
        if (!IsServer) return;
        
        Vector3 resetPos = spawnPoint != null ? spawnPoint.position : initialPosition;
        transform.position = resetPos;
        rb.position = resetPos;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        if (isPickedUp.Value)
        {
            isPickedUp.Value = false;
            holderClientId.Value = ulong.MaxValue;
            rb.isKinematic = false;
        }
    }
}
