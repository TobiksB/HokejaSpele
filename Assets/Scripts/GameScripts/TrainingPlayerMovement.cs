using UnityEngine;

namespace HockeyGame.Game
{
    public class TrainingPlayerMovement : MonoBehaviour
    {        [Header("Movement Settings")]
        public float moveSpeed = 8f; // Atbilst tiešsaistes režīma ātrumam
        public float rotationSpeed = 200f; // Atbilst tiešsaistes režīma rotācijai
        [SerializeField] private float sprintSpeed = 12f; // Atbilst tiešsaistes režīma sprintam
        [SerializeField] private float iceFriction = 0.95f; // Mazāk slidens treniņam
        [SerializeField] private float acceleration = 8f; // Palielināts ātrākai atsaucībai
        [SerializeField] private float deceleration = 8f; // Palielināts mazākai slīdēšanai        // Pievieno šīs īpašības inicializācijas pozīcijām TrainingModeManager
        public Vector3 initialPosition { get; set; }
        public Quaternion initialRotation { get; set; }

        private Rigidbody rb;
        private Animator animator;
        private bool currentSprintState;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();            // Iestata fiziku hokejam - PRECĪZI ATBILST TIEŠSAISTES REŽĪMAM
            if (rb != null)
            {
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                rb.mass = 75f; // Match online player mass
                rb.linearDamping = 0.1f; // Match online player drag
                rb.angularDamping = 5f; // Match online player angular drag
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                  // Uzspiež pozīciju
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
        {            float horizontal = Input.GetAxis("Horizontal"); // A/D rotācijai
            float vertical = Input.GetAxis("Vertical");     // W/S kustībai
            bool sprint = Input.GetKey(KeyCode.LeftShift);
            bool quickStop = Input.GetKey(KeyCode.Space);   // Atstarpe ātrai apturēšanai

            currentSprintState = sprint;            // Saglabā pašreizējo ātrumu pirms jebkādām izmaiņām
            Vector3 currentVel = rb != null ? rb.linearVelocity : Vector3.zero;
            float currentHorizontalSpeed = new Vector3(currentVel.x, 0f, currentVel.z).magnitude;
            
            // PRECĪZI ATBILST TIEŠSAISTES FIZIKAI: Rotācijas apstrāde
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
            {                // PRIORITĀTE 1: Ātrā apstāšanās - PRECĪZI ATBILST TIEŠSAISTES REŽĪMAM
                if (quickStop)
                {
                    // LEDUS FIZIKA: Pakāpeniskāka apstāšanās (joprojām ātra, bet sajūta vairāk ledaina)
                    Vector3 stoppedVel = currentVel * 0.7f;
                    rb.linearVelocity = new Vector3(stoppedVel.x, currentVel.y, stoppedVel.z);
                }
                // PRIORITĀTE 2: Kustības ievade - PRECĪZI ATBILST TIEŠSAISTES REŽĪMAM
                else if (Mathf.Abs(vertical) > 0.1f)
                {                    // LEDUS FIZIKA: Pakāpeniska paātrināšanās, nevis tūlītējs ātrums
                    float targetSpeed = sprint ? sprintSpeed : moveSpeed;
                    Vector3 targetDirection = transform.forward * vertical;
                    Vector3 targetVelocity = targetDirection * targetSpeed;
                    
                    Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                    Vector3 velocityDiff = targetVelocity - currentHorizontalVel;
                    
                    // Piemēro paātrināšanās spēku - PRECĪZI ATBILST TIEŠSAISTES REŽĪMAM
                    float accelForce = acceleration * Time.deltaTime;
                    Vector3 newVelocity;
                    
                    if (velocityDiff.magnitude > accelForce)
                    {                        // Pakāpeniska paātrināšanās
                        newVelocity = currentHorizontalVel + velocityDiff.normalized * accelForce;
                    }
                    else
                    {
                        // Pietiekami tuvu mērķim
                        newVelocity = targetVelocity;
                    }
                    
                    rb.linearVelocity = new Vector3(newVelocity.x, currentVel.y, newVelocity.z);
                }                // PRIORITĀTE 3: Ledus berze/slīdēšana - PRECĪZI ATBILST TIEŠSAISTES REŽĪMAM
                else if (currentHorizontalSpeed > 0.1f)
                {
                    // LEDUS FIZIKA: Piemēro pakāpenisku palēnināšanos, kad nav ievades
                    Vector3 horizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                    float decelAmount = deceleration * Time.deltaTime;
                    
                    if (horizontalVel.magnitude > decelAmount)
                    {
                        Vector3 decelVel = horizontalVel - horizontalVel.normalized * decelAmount;
                        rb.linearVelocity = new Vector3(decelVel.x, currentVel.y, decelVel.z);
                    }
                    else
                    {                        // Gandrīz apstājies
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
        {            // Saglabā spēlētāju pareizajā Y pozīcijā - PRECĪZI ATBILST TIEŠSAISTES REŽĪMAM
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
            }            // Ierobežo ātrumu labākai kontrolei - PRECĪZI ATBILST TIEŠSAISTES REŽĪMAM
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                float maxSpeed = currentSprintState ? sprintSpeed : moveSpeed;
                float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
                
                // Atļauj nelielu pārsniegšanu ledus fizikai, bet ierobežo līdz saprātīgam limitam
                float maxAllowed = maxSpeed * 1.3f; // Same max allowed as in online mode
                
                if (horizontalSpeed > maxAllowed)
                {
                    Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
                    horizontalVel = horizontalVel.normalized * maxAllowed;
                    rb.linearVelocity = new Vector3(horizontalVel.x, vel.y, horizontalVel.z);
                }
            }
        }        // Metode šaušanas animācijas aktivizēšanai
        public void TriggerShootAnimation()
        {
            if (animator != null)
            {
                animator.SetBool("IsShooting", true);
                animator.SetTrigger("Shoot");
                
                // Atiestata animāciju pēc aizkaves
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
