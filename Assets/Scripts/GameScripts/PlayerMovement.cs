using UnityEngine;
using Unity.Netcode;
using System.Collections;
using HockeyGame.Game;

public class PlayerMovement : NetworkBehaviour
{
    public enum Team : byte { Red = 0, Blue = 1 }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 80f; // Reduced for more realistic ice feel
    [SerializeField] private float sprintSpeed = 120f; // Reduced for more realistic ice feel
    [SerializeField] private float rotationSpeed = 200f; // Reduced for more realistic turning
    [SerializeField] private float iceFriction = 0.98f; // Higher value = more slippery (was 0.95f)
    [SerializeField] private float acceleration = 60f; // How fast we reach target speed
    [SerializeField] private float deceleration = 40f; // How fast we slow down when no input

    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenRate = 10f;
    [SerializeField] private float staminaDrainRate = 20f;

    [Header("Camera")]
    [SerializeField] private GameObject playerCameraPrefab;
    [SerializeField] private Transform cameraHolder;
    private CameraFollow cameraFollow;
    private Camera playerCamera;

    [Header("Interaction")]
    [SerializeField] private float pickupRange = 1.5f;
    [SerializeField] private SphereCollider pickupTrigger;
    [SerializeField] private SphereCollider pickupCollider;

    private NetworkVariable<bool> isSkating = new NetworkVariable<bool>();
    private NetworkVariable<bool> isShooting = new NetworkVariable<bool>();
    // FIXED: Re-add the missing network variables that were accidentally removed
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector3> networkVelocity = new NetworkVariable<Vector3>();
    private NetworkVariable<Team> networkTeam = new NetworkVariable<Team>(Team.Red, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float currentStamina;
    private bool canSprint;
    private float currentShootCharge;
    private Rigidbody rb;
    public Animator animator;
    private Vector3 currentVelocity;
    private Vector3 moveDirection;
    private bool currentSprintState;
    private bool canMove = true;
    private bool isMovementEnabled = true;

    // FIXED: Add missing variable declarations
    private bool isMyPlayer = false;
    private ulong localClientId = 0;
    private PlayerTeam teamComponent;
    private PlayerTeamVisuals visuals;
    private bool hasLoggedOwnership = false;

    // Animation hash IDs for performance
    private int isSkatingHash;
    private int isShootingHash;
    private int shootTriggerHash;
    private int speedHash;

    // Movement tracking for ServerRpc optimization
    private float lastSentHorizontal = 0f;
    private float lastSentVertical = 0f;
    private bool lastSentSprint = false;
    private float inputSendRate = 0.05f; // Send input 20 times per second
    private float lastInputSendTime = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        var netObj = GetComponent<NetworkObject>();
        if (netObj == null || !netObj.enabled)
        {
            Debug.LogError("NetworkObject is missing or disabled on player prefab!");
            return;
        }
        
        Debug.Log($"Player spawned with NetworkObject enabled. IsOwner: {IsOwner}, NetworkObjectId: {NetworkObjectId}");

        InitializeComponents();

        if (IsOwner)
        {
            Debug.Log($"Player spawned. IsOwner: {IsOwner}, ClientId: {OwnerClientId}");
            SetupCamera();
            SetupPickupTrigger();
        }

        isSkating.OnValueChanged += OnSkatingChanged;
        isShooting.OnValueChanged += OnShootingChanged;

        // Listen for team changes
        networkTeam.OnValueChanged += (oldTeam, newTeam) => { ApplyTeamColor(newTeam); };

        // Apply color immediately on spawn
        ApplyTeamColor(networkTeam.Value);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // ICE PHYSICS: Enhanced for more realistic ice feel
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.mass = 75f; // Slightly lighter for better ice feel
        rb.linearDamping = 0.1f; // Small amount of damping for realism
        rb.angularDamping = 5f; // Reduced for more sliding during rotation
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Force initial Y position
        Vector3 pos = transform.position;
        pos.y = 0.71f;
        transform.position = pos;

        Debug.Log($"ICE PHYSICS: Enhanced ice feel - Mass: {rb.mass}, LinearDamping: {rb.linearDamping}");
    }

    private void InitializeComponents()
    {
        animator = GetComponent<Animator>();
        teamComponent = GetComponent<PlayerTeam>();
        visuals = GetComponent<PlayerTeamVisuals>();
        
        var puckPickup = GetComponent<PuckPickup>();
        if (puckPickup == null)
        {
            gameObject.AddComponent<PuckPickup>();
        }
        
        currentStamina = maxStamina;
        canSprint = true;

        if (animator != null)
        {
            isSkatingHash = Animator.StringToHash("IsSkating");
            isShootingHash = Animator.StringToHash("IsShooting");
            shootTriggerHash = Animator.StringToHash("Shoot");
            speedHash = Animator.StringToHash("Speed");
        }
    }

    private void SetupCamera()
    {
        if (playerCameraPrefab == null)
        {
            Debug.LogWarning("Player camera prefab not assigned! Creating basic camera...");
            CreateBasicCamera();
            return;
        }

        Vector3 offset = new Vector3(0f, 6f, -10f);
        Vector3 cameraPos = transform.position + offset;
        GameObject cameraObj = Instantiate(playerCameraPrefab, cameraPos, Quaternion.identity);

        cameraFollow = cameraObj.GetComponent<CameraFollow>();
        playerCamera = cameraObj.GetComponent<Camera>();

        if (cameraFollow != null && playerCamera != null)
        {
            cameraFollow.SetTarget(transform);
            Debug.Log($"Camera setup complete for player {OwnerClientId}");
        }
        else
        {
            Debug.LogError("Required camera components missing!");
            Destroy(cameraObj);
            CreateBasicCamera();
        }
    }

    private void CreateBasicCamera()
    {
        GameObject cameraObj = new GameObject("Player Camera");
        playerCamera = cameraObj.AddComponent<Camera>();
        cameraObj.AddComponent<AudioListener>();
        
        cameraFollow = cameraObj.AddComponent<CameraFollow>();
        cameraFollow.SetTarget(transform);
        cameraFollow.SetOffset(new Vector3(0f, 6f, -10f));
        
        Debug.Log($"Basic camera created for player {OwnerClientId}");
    }

    private void SetupPickupTrigger()
    {
        GameObject triggerObj = new GameObject("PickupTrigger");
        triggerObj.transform.parent = transform;
        triggerObj.transform.localPosition = Vector3.zero;
        pickupTrigger = triggerObj.AddComponent<SphereCollider>();
        pickupTrigger.radius = pickupRange;
        pickupTrigger.isTrigger = true;

        var physicsCollider = gameObject.GetComponent<CapsuleCollider>();
        if (physicsCollider == null)
        {
            physicsCollider = gameObject.AddComponent<CapsuleCollider>();
            physicsCollider.height = 2f;
            physicsCollider.radius = 0.5f;
            physicsCollider.isTrigger = false;
        }
    }

    private void Update()
    {
        // Only process input for the owner
        if (!IsOwner && NetworkManager.Singleton != null) return;

        HandleMovementInput();

        // REMOVED: All puck pickup logic - PuckPickup component handles this
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal"); // A/D for rotation ONLY
        float vertical = Input.GetAxis("Vertical");     // W/S for movement ONLY
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        bool quickStop = Input.GetKey(KeyCode.Space);   // NEW: Space for quick stopping

        currentSprintState = sprint;

        if (IsOwner)
        {
            // Store current velocity before any changes
            Vector3 currentVel = rb != null ? rb.linearVelocity : Vector3.zero;
            float currentHorizontalSpeed = new Vector3(currentVel.x, 0f, currentVel.z).magnitude;
            
            // ICE PHYSICS: Smoother rotation with momentum preservation
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                // Rotation happens regardless of movement state - keeps momentum
                float rotationAmount = horizontal * rotationSpeed * Time.deltaTime;
                // Reduce rotation speed when moving fast for more realistic ice turning
                if (currentHorizontalSpeed > 30f)
                {
                    rotationAmount *= Mathf.Lerp(1f, 0.6f, (currentHorizontalSpeed - 30f) / 70f);
                }
                transform.Rotate(0f, rotationAmount, 0f);
            }

            // PRIORITY 1: Quick stop with space (overrides everything)
            if (quickStop && rb != null)
            {
                // ICE PHYSICS: More gradual stopping (still quick but feels more icy)
                Vector3 stoppedVel = currentVel * 0.7f; // Less aggressive than 0.5f
                rb.linearVelocity = new Vector3(stoppedVel.x, currentVel.y, stoppedVel.z);
            }
            // PRIORITY 2: Active movement input (W/S pressed)
            else if (Mathf.Abs(vertical) > 0.1f)
            {
                // ICE PHYSICS: Gradual acceleration instead of instant velocity
                float targetSpeed = sprint ? sprintSpeed : moveSpeed;
                Vector3 targetDirection = transform.forward * vertical;
                Vector3 targetVelocity = targetDirection * targetSpeed;
                
                if (rb != null)
                {
                    Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                    Vector3 velocityDiff = targetVelocity - currentHorizontalVel;
                    
                    // Apply acceleration force for more realistic ice feel
                    float accelForce = acceleration * Time.deltaTime;
                    Vector3 newVelocity;
                    
                    if (velocityDiff.magnitude > accelForce)
                    {
                        // Gradual acceleration
                        newVelocity = currentHorizontalVel + velocityDiff.normalized * accelForce;
                    }
                    else
                    {
                        // Close enough to target
                        newVelocity = targetVelocity;
                    }
                    
                    rb.linearVelocity = new Vector3(newVelocity.x, currentVel.y, newVelocity.z);
                }
            }
            // PRIORITY 3: Momentum maintenance - ICE PHYSICS: Natural sliding
            else if (currentHorizontalSpeed > 0.1f)
            {
                // ICE PHYSICS: Apply gradual deceleration when no input
                if (rb != null)
                {
                    Vector3 horizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                    float decelAmount = deceleration * Time.deltaTime;
                    
                    if (horizontalVel.magnitude > decelAmount)
                    {
                        Vector3 decelVel = horizontalVel - horizontalVel.normalized * decelAmount;
                        rb.linearVelocity = new Vector3(decelVel.x, currentVel.y, decelVel.z);
                    }
                    else
                    {
                        // Almost stopped
                        rb.linearVelocity = new Vector3(0f, currentVel.y, 0f);
                    }
                }
            }
            // PRIORITY 4: Complete stop (no velocity adjustment needed)
            // Let the natural deceleration handle the final stopping

            // FIXED: Update animations based ONLY on active W/S input (not momentum or velocity)
            if (animator != null)
            {
                // CRITICAL: Animation ONLY plays when actively pressing W/S, NOT during momentum maintenance
                bool isActivelyMoving = Mathf.Abs(vertical) > 0.1f && !quickStop;
                animator.SetBool("IsSkating", isActivelyMoving);
                animator.speed = sprint ? 1.5f : 1.0f;
                
                // Debug log to verify animation state
                Debug.Log($"DIAGNOSTIC: Animation - vertical input: {vertical:F2}, isActivelyMoving: {isActivelyMoving}, quickStop: {quickStop}");
            }
        }

        // Network sync: Send all inputs including quick stop
        bool inputChanged = Mathf.Abs(horizontal - lastSentHorizontal) > 0.05f || 
                           Mathf.Abs(vertical - lastSentVertical) > 0.05f || 
                           sprint != lastSentSprint;
        
        bool shouldSend = inputChanged || (Time.time - lastInputSendTime) > inputSendRate;
        
        if (shouldSend && NetworkManager.Singleton != null && IsSpawned)
        {
            MoveServerRpc(horizontal, vertical, sprint, quickStop, transform.position, transform.rotation);
            lastSentHorizontal = horizontal;
            lastSentVertical = vertical;
            lastSentSprint = sprint;
            lastInputSendTime = Time.time;
        }
    }

    [ServerRpc]
    private void MoveServerRpc(float horizontal, float vertical, bool sprint, bool quickStop, Vector3 clientPosition, Quaternion clientRotation)
    {
        if (rb == null) return;

        currentSprintState = sprint;

        if (IsOwner) 
        {
            transform.position = clientPosition;
            transform.rotation = clientRotation;
        }
        else // NON-OWNER (remote clients) - apply server-side movement
        {
            Vector3 currentVel = rb.linearVelocity;
            float currentHorizontalSpeed = new Vector3(currentVel.x, 0f, currentVel.z).magnitude;
            
            // ICE PHYSICS: Apply same rotation logic for remote clients
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                float rotationAmount = horizontal * rotationSpeed * Time.fixedDeltaTime;
                if (currentHorizontalSpeed > 30f)
                {
                    rotationAmount *= Mathf.Lerp(1f, 0.6f, (currentHorizontalSpeed - 30f) / 70f);
                }
                transform.Rotate(0f, rotationAmount, 0f);
            }

            if (quickStop)
            {
                Vector3 stoppedVel = currentVel * 0.7f; // Same as owner
                rb.linearVelocity = new Vector3(stoppedVel.x, currentVel.y, stoppedVel.z);
            }
            else if (Mathf.Abs(vertical) > 0.1f)
            {
                // ICE PHYSICS: Same gradual acceleration for remote clients
                float targetSpeed = sprint ? sprintSpeed : moveSpeed;
                Vector3 targetDirection = transform.forward * vertical;
                Vector3 targetVelocity = targetDirection * targetSpeed;
                
                Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                Vector3 velocityDiff = targetVelocity - currentHorizontalVel;
                
                float accelForce = acceleration * Time.fixedDeltaTime;
                Vector3 newVelocity;
                
                if (velocityDiff.magnitude > accelForce)
                {
                    newVelocity = currentHorizontalVel + velocityDiff.normalized * accelForce;
                }
                else
                {
                    newVelocity = targetVelocity;
                }
                
                rb.linearVelocity = new Vector3(newVelocity.x, currentVel.y, newVelocity.z);
            }
            else if (currentHorizontalSpeed > 0.1f)
            {
                // ICE PHYSICS: Same gradual deceleration for remote clients
                Vector3 horizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                float decelAmount = deceleration * Time.fixedDeltaTime;
                
                if (horizontalVel.magnitude > decelAmount)
                {
                    Vector3 decelVel = horizontalVel - horizontalVel.normalized * decelAmount;
                    rb.linearVelocity = new Vector3(decelVel.x, currentVel.y, decelVel.z);
                }
                else
                {
                    rb.linearVelocity = new Vector3(0f, currentVel.y, 0f);
                }
            }

            // REMOTE CLIENTS ONLY: Lock Y position and velocity
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y - 0.71f) > 0.001f)
            {
                pos.y = 0.71f;
                transform.position = pos;
                rb.position = pos;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            }
        }

        // Update network variables - for host, use client's actual values
        networkPosition.Value = transform.position;
        if (IsOwner)
        {
            // HOST: Use the actual client velocity from client-side physics
            networkVelocity.Value = rb != null ? rb.linearVelocity : Vector3.zero;
        }
        else
        {
            // REMOTE CLIENTS: Use server-calculated velocity
            networkVelocity.Value = rb != null ? rb.linearVelocity : Vector3.zero;
        }
        isSkating.Value = Mathf.Abs(vertical) > 0.1f && !quickStop;

        // Update other clients
        UpdateMovementClientRpc(transform.position, rb != null ? rb.linearVelocity : Vector3.zero, transform.rotation, sprint, quickStop, vertical);
    }

    [ClientRpc]
    private void UpdateMovementClientRpc(Vector3 position, Vector3 velocity, Quaternion rotation, bool sprint, bool quickStop, float verticalInput)
    {
        // Only apply to non-owners (remote players)
        if (!IsOwner && rb != null)
        {
            // Smooth interpolation to server position for remote players
            float lerpSpeed = 25f; // Increased from 20f for even faster sync
            transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * lerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * lerpSpeed);
            
            // Apply velocity for remote players
            rb.linearVelocity = velocity;

            // FIXED: Update animations for remote players based on INPUT, not velocity
            if (animator != null)
            {
                // CRITICAL: Use the actual input state from the remote player, not velocity
                bool isActivelyMoving = Mathf.Abs(verticalInput) > 0.1f && !quickStop;
                animator.SetBool("IsSkating", isActivelyMoving);
                animator.speed = sprint ? 1.5f : 1.0f;
            }
        }
    }

    private void FixedUpdate()
    {
        // CRITICAL: For HOST/OWNER, handle Y-locking on CLIENT-SIDE ONLY
        // This prevents server interference with host physics
        if (IsOwner)
        {
            // HOST: Client-side Y position locking (no server interference)
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y - 0.71f) > 0.01f)
            {
                pos.y = 0.71f;
                transform.position = pos;
                if (rb != null)
                {
                    rb.position = pos;
                    // Only zero Y velocity, preserve X/Z momentum exactly
                    Vector3 vel = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
                }
            }

            // HOST: Client-side velocity limiting
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                float maxSpeed = currentSprintState ? sprintSpeed : moveSpeed;
                float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
                
                // Allow some overshoot for ice physics but cap at reasonable limit
                float maxAllowed = maxSpeed * 1.3f; // Reduced from 2.0f for more control
                
                if (horizontalSpeed > maxAllowed)
                {
                    Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
                    horizontalVel = horizontalVel.normalized * maxAllowed;
                    rb.linearVelocity = new Vector3(horizontalVel.x, vel.y, horizontalVel.z);
                }
            }
        }
        else // NON-OWNER
        {
            // REMOTE CLIENTS: Server handles Y-locking
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y - 0.71f) > 0.01f)
            {
                pos.y = 0.71f;
                transform.position = pos;
                if (rb != null)
                {
                    rb.position = pos;
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                }
            }
        }

        // SERVER: Update network variables (but don't interfere with host physics)
        if (IsServer && rb != null)
        {
            networkPosition.Value = transform.position;
            networkVelocity.Value = rb.linearVelocity;
        }
    }

    private void ApplyIceFriction()
    {
        if (rb == null) return;

        Vector3 currentVel = rb.linearVelocity;
        // ICE PHYSICS: Very minimal friction for true ice feel
        rb.linearVelocity = new Vector3(currentVel.x * iceFriction, currentVel.y, currentVel.z * iceFriction);
        
        // Only stop when velocity is extremely low for realistic ice sliding
        if (rb.linearVelocity.magnitude < 0.5f) // Increased threshold for more sliding
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
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

    // Call this from GameNetworkManager after spawning the player object:
    [ServerRpc]
    public void SetTeamServerRpc(Team team)
    {
        if (IsServer)
            networkTeam.Value = team;
    }

    // Public method to ensure the player camera is set up (called by GameNetworkManager)
    public void EnsurePlayerCamera()
    {
        if (IsOwner && playerCamera == null)
        {
            SetupCamera();
        }
    }

    // This method applies the color to the player object
    private void ApplyTeamColor(Team team)
    {
        Color teamColor = team == Team.Blue ? new Color(0f, 0.5f, 1f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            var mats = renderer.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                mats[i].color = teamColor;
                if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", teamColor);
                if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", teamColor);
            }
            renderer.materials = mats;
        }
        
        // Note: PlayerTeamVisuals component handles its own team colors via NetworkVariable
        // No need to call SetTeamColor as it doesn't exist
    }

    // Add this method to allow PlayerShooting to trigger the shoot animation
    public void TriggerShootAnimation()
    {
        if (animator != null)
        {
            animator.SetBool("IsShooting", true);
            animator.SetTrigger("Shoot");
            
            // Reset the shooting animation after a short delay
            StartCoroutine(ResetShootAnimation());
            
            Debug.Log("PlayerMovement: Shoot animation triggered");
        }
        else
        {
            Debug.LogWarning("PlayerMovement: Animator is null, cannot trigger shoot animation");
        }
    }

    private System.Collections.IEnumerator ResetShootAnimation()
    {
        // Wait for the animation to play
        yield return new WaitForSeconds(0.5f);
        
        if (animator != null)
        {
            animator.SetBool("IsShooting", false);
        }
    }
}


