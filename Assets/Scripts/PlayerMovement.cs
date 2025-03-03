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
    private static readonly int ShootTrigger = Animator.StringToHash("ShootTrigger");
    
    [Header("Shooting")]
    [SerializeField] private float shootCooldown = 1f;
    [SerializeField] private float maxShootCharge = 1f;
    [SerializeField] private ShootingIndicator shootingIndicator;
    private float shootTimer;
    private float currentShootPower;
    private bool isCharging;
    private bool isShooting;
    private Puck controlledPuck;

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
                stickController.player = GetComponent<MainGame.HockeyPlayer>();
            }
            else
            {
                Debug.LogError("Hockey stick controller not found!");
            }
        }
    }

    void Update()
    {
        HandleRotation();
        HandleBoost();
        HandleShooting();
        
        // Handle puck pickup
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryPickupPuck();
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

    void HandleShooting()
    {
        if (shootTimer > 0)
        {
            shootTimer -= Time.deltaTime;
        }

        if (controlledPuck != null)
        {
            if (Input.GetMouseButtonDown(0) && !isCharging && shootTimer <= 0)
            {
                // Start charging shot
                isCharging = true;
                currentShootPower = 0;
                if (shootingIndicator != null)
                {
                    shootingIndicator.gameObject.SetActive(true);
                }
            }
            else if (Input.GetMouseButton(0) && isCharging)
            {
                // Charge shot
                currentShootPower = Mathf.Min(currentShootPower + Time.deltaTime, maxShootCharge);
                if (shootingIndicator != null)
                {
                    shootingIndicator.UpdatePower(currentShootPower / maxShootCharge);
                }
            }
            else if (Input.GetMouseButtonUp(0) && isCharging)
            {
                // Release shot
                ShootPuck();
            }
        }
    }

    private void ShootPuck()
    {
        if (controlledPuck != null)
        {
            isShooting = true;
            isCharging = false;
            shootTimer = shootCooldown;

            if (animator != null)
            {
                animator.SetTrigger(ShootTrigger);
                animator.SetBool(IsShooting, true);
            }

            controlledPuck.Shoot(currentShootPower / maxShootCharge, transform.forward);
            controlledPuck = null;

            if (shootingIndicator != null)
            {
                shootingIndicator.gameObject.SetActive(false);
            }

            Invoke(nameof(ResetShootingState), shootCooldown);
        }
    }

    private void TryPickupPuck()
    {
        // Increase the detection radius and offset
        float pickupRadius = 2f;
        float forwardOffset = 2f;
        Vector3 checkPosition = transform.position + transform.forward * forwardOffset;
        
        Debug.DrawLine(transform.position, checkPosition, Color.yellow, 1f); // Visualize pickup range
        Collider[] colliders = Physics.OverlapSphere(checkPosition, pickupRadius);
        
        Debug.Log($"Checking for puck. Found {colliders.Length} colliders.");
        
        foreach (Collider col in colliders)
        {
            Debug.Log($"Found collider: {col.gameObject.name}");
            Puck puck = col.GetComponent<Puck>();
            if (puck != null && !puck.isControlled)
            {
                Debug.Log($"Found pickup-able puck: {puck.gameObject.name}");
                controlledPuck = puck;
                puck.AttachToPlayer(transform);
                break;
            }
        }
    }

    private void ResetShootingState()
    {
        isShooting = false;
        if (animator != null)
        {
            animator.SetBool(IsShooting, false);
        }
    }
}
