using UnityEngine;

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

    private Vector3 currentVelocity;
    private float boostTimer;
    private float boostCooldownTimer;
    private bool isBoosting;

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    void Update()
    {
        HandleRotation();
        HandleBoost();
        HandleShooting();
        
        // Handle puck pickup
        if (Input.GetKeyDown(KeyCode.E) && !controlledPuck)
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
        
        // Update animation states
        if (animator != null)
        {
            // Only update skating and idle if not shooting
            if (!isShooting)
            {
                animator.SetBool(IsSkating, isMoving);
                animator.SetBool(IsIdle, !isMoving);
            }
        }

        // Apply movement only if actually moving
        if (isMoving)
        {
            float currentSpeed = isBoosting ? maxSpeed * boostMultiplier : maxSpeed;
            currentVelocity += moveDirection * acceleration * Time.fixedDeltaTime;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, currentSpeed);
        }

        // Apply drag
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, dragForce * Time.fixedDeltaTime);

        // Apply movement
        transform.position += currentVelocity * Time.fixedDeltaTime;
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
        Collider[] colliders = Physics.OverlapSphere(transform.position + transform.forward * 1.5f, 1f);
        foreach (Collider col in colliders)
        {
            Puck puck = col.GetComponent<Puck>();
            if (puck != null)
            {
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
