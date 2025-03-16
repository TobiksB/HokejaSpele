using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using System.Collections;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private Transform cameraHolder; // Reference to camera holder
    [SerializeField] private float shootForce = 20f;
    [SerializeField] private float highShootForce = 25f;  // Add this field
    [SerializeField] private float highShotAngle = 30f;   // Add this field
    [SerializeField] private float skidFactor = 3f; // How much to increase drag when skidding
    [SerializeField] private float shootAnimationDuration = 0.5f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDepletionRate = 25f;
    [SerializeField] private float staminaRegenRate = 15f;
    [SerializeField] private float maxVelocity = 8f;  // Add this field
    [SerializeField] private float pickupRadius = 1.5f;  // Add this field
    [SerializeField] private float shootChargeRate = 1.5f; // Add this field
    [SerializeField] private float maxShootCharge = 2f; // Add this field

    private Rigidbody rb;
    private float moveInput;
    private float rotationInput;
    private Puck currentPuck;
    private Animator animator;
    private float shootAnimationTimer = 0f;
    private float currentStamina;
    private bool canSprint = true;
    private Camera playerCamera;
    private NetworkAnimator networkAnimator;
    private NetworkVariable<bool> isSkating = new NetworkVariable<bool>();
    private NetworkVariable<bool> isShooting = new NetworkVariable<bool>();
    private float currentShootCharge = 1f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Configure rigidbody for ice-like movement
        rb.linearDamping = 0.2f; // Adjusted for smoother movement
        rb.angularDamping = 0.5f;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Enable interpolation for smoother movement

        networkAnimator = GetComponent<NetworkAnimator>();
        animator = GetComponent<Animator>();
        currentStamina = maxStamina;
    }

    void Update()
    {
        if (!IsOwner) return;

        // Get input
        moveInput = Input.GetAxis("Vertical"); // Use GetAxis for smoother input
        rotationInput = Input.GetAxis("Horizontal"); // Use GetAxis for smoother input

        // Update animations with speed
        bool isMoving = moveInput != 0 || rotationInput != 0;
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && canSprint && currentStamina > 0;
        
        if (isMoving != isSkating.Value)
        {
            UpdateSkatingServerRpc(isMoving);
        }

        // Update animation speed
        if (animator != null)
        {
            float animSpeed = isSprinting ? 1.5f : 1f;
            animator.speed = isMoving ? animSpeed : 1f;
        }

        // Handle shooting with charge mechanic
        if (currentPuck != null)
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                currentShootCharge = Mathf.Min(currentShootCharge + shootChargeRate * Time.deltaTime, maxShootCharge);
            }
            else if (Input.GetMouseButtonUp(0)) // Normal shot
            {
                UpdateShootingServerRpc(true);
                shootAnimationTimer = shootAnimationDuration;
                currentPuck.Shoot(transform.forward, shootForce * currentShootCharge, false);
                currentPuck = null;
                currentShootCharge = 1f;
                Debug.Log("Normal shot");
            }
            else if (Input.GetMouseButtonUp(1)) // High shot
            {
                UpdateShootingServerRpc(true);
                shootAnimationTimer = shootAnimationDuration;
                Vector3 highShotDirection = transform.forward;
                currentPuck.Shoot(highShotDirection, highShootForce * currentShootCharge, true);
                currentPuck = null;
                currentShootCharge = 1f;
                Debug.Log("High shot");
            }
        }

        // Reset shooting animation
        if (shootAnimationTimer > 0)
        {
            shootAnimationTimer -= Time.deltaTime;
            if (shootAnimationTimer <= 0)
            {
                UpdateShootingServerRpc(false);
            }
        }

        // Handle skidding with spacebar
        if (Input.GetKey(KeyCode.Space))
        {
            rb.linearDamping = skidFactor;
        }
        else
        {
            rb.linearDamping = 0.2f; // Return to normal drag
        }

        // Handle stamina and sprinting
        if (Input.GetKey(KeyCode.LeftShift) && canSprint && currentStamina > 0)
        {
            currentStamina -= staminaDepletionRate * Time.deltaTime;
            if (currentStamina <= 0)
            {
                canSprint = false;
                currentStamina = 0;
            }
        }
        else if (!Input.GetKey(KeyCode.LeftShift))
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            if (currentStamina >= maxStamina)
            {
                currentStamina = maxStamina;
                canSprint = true;
            }
        }

        // Update UI
        StaminaBar.Instance?.UpdateStamina(currentStamina / maxStamina);
    }

    void FixedUpdate()
    {
        if (!IsOwner || cameraHolder == null) return;

        // Get camera's forward and right vectors, but ignore Y component
        Vector3 cameraForward = cameraHolder.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        float currentMoveSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) && canSprint && currentStamina > 0)
        {
            currentMoveSpeed *= sprintMultiplier;
        }

        // Calculate desired velocity
        Vector3 targetVelocity = cameraForward * moveInput * currentMoveSpeed;
        
        // Smoothly interpolate current velocity to target velocity
        Vector3 velocityChange = (targetVelocity - rb.linearVelocity);
        velocityChange.y = 0; // Keep vertical velocity unchanged
        
        // Apply force with dampening
        rb.AddForce(velocityChange, ForceMode.VelocityChange);

        // Clamp velocity to prevent excessive speed
        Vector3 currentVelocity = rb.linearVelocity;
        if (currentVelocity.magnitude > maxVelocity)
        {
            currentVelocity = currentVelocity.normalized * maxVelocity;
            rb.linearVelocity = currentVelocity;
        }

        // Rotate player
        transform.Rotate(Vector3.up * rotationInput * rotationSpeed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return; // Only owner can pick up
        if (currentPuck != null) return;
        
        if (other.TryGetComponent<Puck>(out Puck puck))
        {
            Vector3 toPuck = puck.transform.position - transform.position;
            float angle = Vector3.Angle(transform.forward, toPuck);
            
            if (toPuck.magnitude <= pickupRadius && angle <= 90f)
            {
                currentPuck = puck;
                puck.PickUp(transform);
                Debug.Log($"Picked up puck: {puck.name}");
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsOwner) return;
        if (currentPuck != null) return;
        
        Puck puck = other.GetComponent<Puck>();
        if (puck != null)
        {
            Vector3 toPuck = puck.transform.position - transform.position;
            float angle = Vector3.Angle(transform.forward, toPuck);
            
            if (toPuck.magnitude <= pickupRadius && angle <= 90f)
            {
                Debug.Log($"Attempting to pick up puck: {puck.name}");
                currentPuck = puck;
                puck.PickUp(transform);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            Vector3 spawnPosition = NetworkSpawnManager.Instance.GetNextSpawnPoint();
            transform.position = spawnPosition;
            
            // Create dedicated camera for this player
            GameObject cameraObj = new GameObject($"PlayerCamera_{OwnerClientId}");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.enabled = true;
            AudioListener audioListener = cameraObj.AddComponent<AudioListener>();
            audioListener.enabled = true;
            
            CameraFollow follow = cameraObj.AddComponent<CameraFollow>();
            follow.enabled = true;
            follow.SetTarget(transform);
            
            cameraHolder = cameraObj.transform;
            
            // Set player color
            if (TryGetComponent<MeshRenderer>(out var renderer))
            {
                renderer.material.color = IsHost ? Color.blue : Color.green;
            }

            networkAnimator = GetComponent<NetworkAnimator>();
        }
        else
        {
            if (TryGetComponent<MeshRenderer>(out var renderer))
            {
                renderer.material.color = Color.red;
            }
        }
        
        isSkating.OnValueChanged += OnSkatingChanged;
        isShooting.OnValueChanged += OnShootingChanged;
    }

    private void OnSkatingChanged(bool previousValue, bool newValue)
    {
        if (animator != null)
        {
            animator.SetBool("IsSkating", newValue);
        }
    }

    private void OnShootingChanged(bool previousValue, bool newValue)
    {
        if (animator != null)
        {
            animator.SetBool("IsShooting", newValue);
        }
    }

    [ServerRpc]
    private void UpdateSkatingServerRpc(bool skating)
    {
        isSkating.Value = skating;
    }

    [ServerRpc]
    private void UpdateShootingServerRpc(bool shooting)
    {
        isShooting.Value = shooting;
    }
}
