using UnityEngine;
using Unity.Netcode;
using System.Collections;
using HockeyGame.Game;

public class PlayerMovement : NetworkBehaviour
{
    public enum Team : byte { Red = 0, Blue = 1 }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float rotationSpeed = 0.2f; // Reduced from 1f to 0.5f for less sensitive turning
    [SerializeField] private float iceFriction = 0.95f;

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

        // Configure Rigidbody for hockey movement
        rb.useGravity = false;
        // Freeze ALL rotations to prevent falling over
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.mass = 80f; // Hockey player mass
        rb.linearDamping = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Force initial Y position
        Vector3 pos = transform.position;
        pos.y = 0.71f;
        transform.position = pos;

        Debug.Log($"PlayerMovement Rigidbody configured - Mass: {rb.mass}, Damping: {rb.linearDamping}");
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
        float horizontal = Input.GetAxis("Horizontal"); // A/D for rotation
        float vertical = Input.GetAxis("Vertical");     // W/S for movement
        bool sprint = Input.GetKey(KeyCode.LeftShift);

        // Store for physics update
        moveDirection = transform.forward * vertical;
        currentSprintState = sprint;

        // Apply rotation with A/D keys
        if (Mathf.Abs(horizontal) > 0.01f)
        {
            transform.Rotate(0f, horizontal * rotationSpeed * Time.deltaTime, 0f);
        }

        // Send movement to server if changed
        bool inputChanged = horizontal != lastSentHorizontal || vertical != lastSentVertical || sprint != lastSentSprint;
        
        if (inputChanged)
        {
            if (NetworkManager.Singleton != null && IsSpawned)
            {
                MoveServerRpc(horizontal, vertical, sprint);
            }
            
            lastSentHorizontal = horizontal;
            lastSentVertical = vertical;
            lastSentSprint = sprint;
        }

        // Update animations locally for responsiveness
        if (animator != null)
        {
            bool isMoving = Mathf.Abs(vertical) > 0.1f;
            animator.SetBool("IsSkating", isMoving);
            animator.speed = sprint ? 1.2f : 1.0f;
        }
    }

    [ServerRpc]
    private void MoveServerRpc(float horizontal, float vertical, bool sprint)
    {
        if (rb == null) return;

        // Apply rotation on server
        if (Mathf.Abs(horizontal) > 0.01f)
        {
            transform.Rotate(0f, horizontal * rotationSpeed * Time.fixedDeltaTime, 0f);
        }

        // Apply movement on server
        Vector3 inputVector = transform.forward * vertical;
        float currentSpeed = sprint ? sprintSpeed : moveSpeed;
        
        moveDirection = inputVector;
        currentSprintState = sprint;

        if (inputVector.magnitude > 0.1f)
        {
            Vector3 targetVelocity = inputVector * currentSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            ApplyIceFriction();
        }

        // Lock Y position
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.y - 0.71f) > 0.001f)
        {
            pos.y = 0.71f;
            transform.position = pos;
            rb.position = pos;
        }

        // Update network variables
        networkPosition.Value = transform.position;
        networkVelocity.Value = rb.linearVelocity;
        isSkating.Value = inputVector.magnitude > 0.1f;

        // Update all clients
        UpdateMovementClientRpc(transform.position, rb.linearVelocity, transform.rotation, sprint);
    }

    [ClientRpc]
    private void UpdateMovementClientRpc(Vector3 position, Vector3 velocity, Quaternion rotation, bool sprint)
    {
        // Only apply to non-owners (remote players)
        if (!IsOwner)
        {
            // Smooth interpolation to server position
            transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * 10f);
            
            if (rb != null)
            {
                rb.linearVelocity = velocity;
            }

            // Update animations
            if (animator != null)
            {
                bool isMoving = velocity.magnitude > 0.1f;
                animator.SetBool("IsSkating", isMoving);
                animator.speed = sprint ? 1.2f : 1.0f;
            }
        }
    }

    private void FixedUpdate()
    {
        // SERVER: Update network variables
        if (IsServer)
        {
            networkPosition.Value = transform.position;
            networkVelocity.Value = rb.linearVelocity;
        }
        
        // ALL: Ensure Y position stays locked
        if (IsOwner || IsServer)
        {
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
    }

    private void ApplyIceFriction()
    {
        if (rb == null) return;

        Vector3 currentVel = rb.linearVelocity;
        rb.linearVelocity = new Vector3(currentVel.x * iceFriction, currentVel.y, currentVel.z * iceFriction);
        
        // Stop completely when velocity is very low
        if (rb.linearVelocity.magnitude < 0.1f)
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
    }

    // Add this method to allow PlayerShooting to trigger the shoot animation
    public void TriggerShootAnimation()
    {
        if (animator != null)
        {
            animator.SetBool("IsShooting", true);
            animator.SetTrigger("Shoot");
            // Optionally, you can start a coroutine to reset the animation if needed
        }
    }
}


