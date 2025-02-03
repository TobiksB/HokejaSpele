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
    private static readonly int SkatingSpeed = Animator.StringToHash("SkatingSpeed");
    private static readonly int IsSkidding = Animator.StringToHash("IsSkidding");
    private static readonly int IsShooting = Animator.StringToHash("IsShooting");
    private static readonly int IsIdle = Animator.StringToHash("IsIdle");
    
    [Header("Shooting")]
    [SerializeField] private float shootCooldown = 1f;
    private float shootTimer;
    private bool isShooting;

    private Vector3 currentVelocity;
    private float boostTimer;
    private float boostCooldownTimer;
    private bool isSkidding;
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
        float moveForward = Input.GetAxis("Vertical");
        float moveSideways = Input.GetAxis("Horizontal");
        
        Vector3 moveDirection = (transform.forward * moveForward) + (transform.right * moveSideways);
        moveDirection.Normalize(); // Normalize to prevent faster diagonal movement

        // Animation parameters
        bool isMoving = moveForward != 0 || moveSideways != 0;
        float speedNormalized = currentVelocity.magnitude / maxSpeed;
        
        if (animator != null)
        {
            animator.SetBool(IsSkating, isMoving && !isShooting);
            animator.SetFloat(SkatingSpeed, speedNormalized);
            animator.SetBool(IsSkidding, isSkidding);
            animator.SetBool(IsIdle, !isMoving && !isShooting);
        }

        // Apply acceleration
        if (moveForward != 0 || moveSideways != 0)
        {
            float currentSpeed = isBoosting ? maxSpeed * boostMultiplier : maxSpeed;
            currentVelocity += moveDirection * acceleration * Time.fixedDeltaTime;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, currentSpeed);
        }

        // Apply drag
        float dragMultiplier = isSkidding ? dragForce * 2.5f : dragForce;
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, dragMultiplier * Time.fixedDeltaTime);

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

    void HandleSkidding()
    {
        isSkidding = Input.GetKey(KeyCode.Space);
    }

    void HandleShooting()
    {
        if (shootTimer > 0)
        {
            shootTimer -= Time.deltaTime;
        }

        if (Input.GetMouseButtonDown(0) && shootTimer <= 0)
        {
            isShooting = true;
            shootTimer = shootCooldown;
            
            if (animator != null)
            {
                animator.SetBool(IsShooting, true);
                animator.SetBool(IsSkating, false);
                animator.SetBool(IsIdle, false);
            }

            // Reset shooting state after animation
            Invoke(nameof(ResetShootingState), 0.5f);
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
