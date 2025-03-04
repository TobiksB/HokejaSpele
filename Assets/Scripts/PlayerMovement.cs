using UnityEngine;
using MainGame;  // Add this line at the top

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float dragForce = 1f;
    
    [Header("Boost Settings")]
    [SerializeField] private float boostMultiplier = 1.5f;
    [SerializeField] private float boostCooldown = 2f;
    [SerializeField] private float boostDuration = 1f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    private static readonly int IsSkating = Animator.StringToHash("IsSkating");
    private static readonly int IsShooting = Animator.StringToHash("IsShooting");
    private static readonly int IsIdle = Animator.StringToHash("IsIdle");
    
    [Header("Collision Settings")]
    [SerializeField] private float collisionOffset = 0.5f;
    [SerializeField] private LayerMask wallLayer; // Assign this in inspector
    private CapsuleCollider playerCollider;
    private readonly float skinWidth = 0.1f;
    private readonly float groundOffset = 0.1f;

    private Vector3 currentVelocity;
    private float boostTimer;
    private float boostCooldownTimer;
    private bool isBoosting;

    [Header("Hockey Stick")]
    [SerializeField] private HockeyStickController stickController;

    [Header("Camera Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float cameraFollowSpeed = 5f;

    private bool isShooting;

    void Awake()
    {
        Debug.Log("PlayerMovement: Initializing player");
    }

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        playerCollider = GetComponent<CapsuleCollider>();
        if (playerCollider == null)
        {
            Debug.LogError("Player must have a CapsuleCollider!");
            enabled = false;
        }

        // Set up wall layer mask
        wallLayer = LayerMask.GetMask("Wall");
        Debug.Log($"Wall layer mask: {wallLayer}"); // Debug to verify layer is set
        
        // Ensure player is slightly above ground
        Vector3 pos = transform.position;
        pos.y += groundOffset;
        transform.position = pos;

        if (stickController == null)
        {
            stickController = GetComponentInChildren<HockeyStickController>();
            if (stickController != null)
            {
                // Set player transform reference directly
                stickController.playerTransform = this.transform;
                
                // Also try to set player component if available
                MainGame.HockeyPlayer hockeyPlayer = GetComponent<MainGame.HockeyPlayer>();
                if (hockeyPlayer != null)
                {
                    stickController.player = hockeyPlayer;
                }
                else
                {
                    Debug.LogWarning("Hockey player component not found!");
                }
            }
            else
            {
                Debug.LogError("Hockey stick controller not found! Make sure it's a child of the player.");
            }
        }

        // Use the existing camera in the player prefab
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
        if (playerCamera == null)
        {
            Debug.LogError("Player camera not found!");
        }

        // Try finding the hockey stick if not assigned
        if (stickController == null)
        {
            // First look for it as a direct child
            stickController = GetComponentInChildren<HockeyStickController>();
            
            // If not found, search through all children recursively
            if (stickController == null)
            {
                HockeyStickController[] sticks = GetComponentsInChildren<HockeyStickController>();
                if (sticks.Length > 0)
                {
                    stickController = sticks[0];
                }
            }
            
            // If we found the stick controller, set up references
            if (stickController != null)
            {
                stickController.playerTransform = this.transform;
                
                // Also try to set player component if available
                MainGame.HockeyPlayer hockeyPlayer = GetComponent<MainGame.HockeyPlayer>();
                if (hockeyPlayer != null)
                {
                    stickController.player = hockeyPlayer;
                }
                else
                {
                    Debug.LogWarning("Hockey player component not found! Adding one...");
                    hockeyPlayer = gameObject.AddComponent<MainGame.HockeyPlayer>();
                    stickController.player = hockeyPlayer;
                }
            }
            else
            {
                Debug.LogError("Hockey stick controller not found! Make sure it's a child of the player.");
            }
        }
    }

    void Update()
    {
        HandleBoost();
        
        // Cursor locking for debugging
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleRotation()
    {
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up, mouseX * rotationSpeed);
    }

    void HandleMovement()
    {
        float moveForward = Input.GetAxisRaw("Vertical");
        float moveSideways = Input.GetAxisRaw("Horizontal");
        
        Vector3 moveDirection = (transform.forward * moveForward) + (transform.right * moveSideways);
        moveDirection.Normalize();

        bool isMoving = Mathf.Abs(moveForward) > 0.1f || Mathf.Abs(moveSideways) > 0.1f;
        
        // Update animations
        if (animator != null && !isShooting)
        {
            animator.SetBool(IsSkating, isMoving);
            animator.SetBool(IsIdle, !isMoving);
        }

        // Apply movement
        if (isMoving)
        {
            float currentSpeed = isBoosting ? maxSpeed * boostMultiplier : maxSpeed;
            currentVelocity += moveDirection * acceleration * Time.fixedDeltaTime;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, currentSpeed);

            // Try to move
            Vector3 targetPosition = transform.position + currentVelocity * Time.fixedDeltaTime;
            
            // Check if we can move there
            if (!Physics.CapsuleCast(
                GetTopCapsulePoint(),
                GetBottomCapsulePoint(),
                playerCollider.radius,
                currentVelocity.normalized,
                out RaycastHit hit,
                currentVelocity.magnitude * Time.fixedDeltaTime + collisionOffset,
                wallLayer))
            {
                transform.position = targetPosition;
            }
            else
            {
                // Try sliding along the wall
                Vector3 normal = hit.normal;
                Vector3 deflected = Vector3.ProjectOnPlane(currentVelocity, normal);
                if (deflected.magnitude > 0.1f)
                {
                    transform.position += deflected.normalized * currentSpeed * Time.fixedDeltaTime;
                }
            }
        }

        // Apply drag
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, dragForce * Time.fixedDeltaTime);
    }

    private Vector3 GetTopCapsulePoint()
    {
        return transform.position + Vector3.up * (playerCollider.height - playerCollider.radius);
    }

    private Vector3 GetBottomCapsulePoint()
    {
        return transform.position + Vector3.up * playerCollider.radius;
    }

    void HandleBoost()
    {
        // Update boost cooldown
        if (boostCooldownTimer > 0)
        {
            boostCooldownTimer -= Time.deltaTime;
        }

        // Handle boost activation
        if (Input.GetKey(KeyCode.LeftShift) && boostCooldownTimer <= 0 && !isBoosting)
        {
            isBoosting = true;
            boostTimer = boostDuration;
        }

        // Handle boost duration
        if (isBoosting)
        {
            boostTimer -= Time.deltaTime;
            if (boostTimer <= 0)
            {
                isBoosting = false;
                boostCooldownTimer = boostCooldown;
            }
        }
    }
}
