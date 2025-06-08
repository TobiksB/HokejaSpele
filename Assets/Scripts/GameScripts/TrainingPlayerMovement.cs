using UnityEngine;

namespace HockeyGame.Game
{
    public class TrainingPlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 8f; // Match online mode speed
        public float rotationSpeed = 200f; // Match online mode rotation
        [SerializeField] private float sprintSpeed = 12f; // Match online mode sprint
        [SerializeField] private float iceFriction = 0.95f; // Less slippery for training
        [SerializeField] private float acceleration = 8f; // Increased for quicker response
        [SerializeField] private float deceleration = 8f; // Increased for less sliding

        // Add these properties for initialization positions in TrainingModeManager
        public Vector3 initialPosition { get; set; }
        public Quaternion initialRotation { get; set; }

        private Rigidbody rb;
        private Animator animator;
        private bool currentSprintState;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();

            // Set up physics for ice hockey - MATCH ONLINE MODE EXACTLY
            if (rb != null)
            {
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                rb.mass = 75f; // Match online player mass
                rb.linearDamping = 0.1f; // Match online player drag
                rb.angularDamping = 5f; // Match online player angular drag
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                
                // Force position
                Vector3 pos = transform.position;
                pos.y = 0.71f;
                transform.position = pos;
            }
        }

        private void Update()
        {
            HandleMovementInput();
        }

        private void HandleMovementInput()
        {
            float horizontal = Input.GetAxis("Horizontal"); // A/D for rotation
            float vertical = Input.GetAxis("Vertical");     // W/S for movement
            bool sprint = Input.GetKey(KeyCode.LeftShift);
            bool quickStop = Input.GetKey(KeyCode.Space);   // Space for quick stopping

            currentSprintState = sprint;

            // Store current velocity before any changes
            Vector3 currentVel = rb != null ? rb.linearVelocity : Vector3.zero;
            float currentHorizontalSpeed = new Vector3(currentVel.x, 0f, currentVel.z).magnitude;
            
            // EXACTLY MATCH ONLINE PHYSICS: Rotation handling
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                float rotationAmount = horizontal * rotationSpeed * Time.deltaTime;
                // Reduce rotation speed when moving fast for more realistic ice turning
                if (currentHorizontalSpeed > 30f)
                {
                    rotationAmount *= Mathf.Lerp(1f, 0.6f, (currentHorizontalSpeed - 30f) / 70f);
                }
                transform.Rotate(0f, rotationAmount, 0f);
            }

            if (rb != null)
            {
                // PRIORITY 1: Quick stop - EXACTLY MATCH ONLINE MODE
                if (quickStop)
                {
                    // ICE PHYSICS: More gradual stopping (still quick but feels more icy)
                    Vector3 stoppedVel = currentVel * 0.7f;
                    rb.linearVelocity = new Vector3(stoppedVel.x, currentVel.y, stoppedVel.z);
                }
                // PRIORITY 2: Movement input - EXACTLY MATCH ONLINE MODE
                else if (Mathf.Abs(vertical) > 0.1f)
                {
                    // ICE PHYSICS: Gradual acceleration instead of instant velocity
                    float targetSpeed = sprint ? sprintSpeed : moveSpeed;
                    Vector3 targetDirection = transform.forward * vertical;
                    Vector3 targetVelocity = targetDirection * targetSpeed;
                    
                    Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                    Vector3 velocityDiff = targetVelocity - currentHorizontalVel;
                    
                    // Apply acceleration force - EXACTLY MATCH ONLINE MODE
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
                // PRIORITY 3: Ice friction/sliding - EXACTLY MATCH ONLINE MODE
                else if (currentHorizontalSpeed > 0.1f)
                {
                    // ICE PHYSICS: Apply gradual deceleration when no input
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

            // Update animations - EXACTLY MATCH ONLINE MODE ANIMATION STATES
            if (animator != null)
            {
                bool isActivelyMoving = Mathf.Abs(vertical) > 0.1f && !quickStop;
                animator.SetBool("IsSkating", isActivelyMoving);
                animator.speed = sprint ? 1.5f : 1.0f;
            }
        }

        private void FixedUpdate()
        {
            // Keep player at correct Y position - EXACTLY MATCH ONLINE MODE
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y - 0.71f) > 0.01f)
            {
                pos.y = 0.71f;
                transform.position = pos;
                if (rb != null)
                {
                    rb.position = pos;
                    Vector3 vel = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
                }
            }

            // Cap velocity for better control - EXACTLY MATCH ONLINE MODE
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                float maxSpeed = currentSprintState ? sprintSpeed : moveSpeed;
                float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
                
                // Allow some overshoot for ice physics but cap at reasonable limit
                float maxAllowed = maxSpeed * 1.3f; // Same max allowed as in online mode
                
                if (horizontalSpeed > maxAllowed)
                {
                    Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
                    horizontalVel = horizontalVel.normalized * maxAllowed;
                    rb.linearVelocity = new Vector3(horizontalVel.x, vel.y, horizontalVel.z);
                }
            }
        }

        // Method to trigger shooting animation
        public void TriggerShootAnimation()
        {
            if (animator != null)
            {
                animator.SetBool("IsShooting", true);
                animator.SetTrigger("Shoot");
                
                // Reset the animation after a delay
                StartCoroutine(ResetShootAnimation());
            }
        }

        private System.Collections.IEnumerator ResetShootAnimation()
        {
            yield return new WaitForSeconds(0.5f);
            
            if (animator != null)
            {
                animator.SetBool("IsShooting", false);
            }
        }
    }
}
