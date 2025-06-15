using UnityEngine;
using Unity.Netcode;
using System.Collections;
using HockeyGame.Game;

public class PlayerMovement : NetworkBehaviour
{
    public enum Team : byte { Red = 0, Blue = 1 }

    [Header("Movement Settings")]    [SerializeField] private float moveSpeed = 80f; // Samazināts reālistiskākai ledus sajūtai
    [SerializeField] private float sprintSpeed = 120f; // Samazināts reālistiskākai ledus sajūtai
    [SerializeField] private float rotationSpeed = 200f; // Samazināts reālistiskākai ledus pagriezieniem
    [SerializeField] private float iceFriction = 0.98f; // Augstāka vērtība = slidenāks (bija 0.95f)
    [SerializeField] private float acceleration = 60f; // Cik ātri sasniegam mērķa ātrumu
    [SerializeField] private float deceleration = 40f; // Cik ātri palēninām ātrumu, kad nav ievades

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
    [SerializeField] private SphereCollider pickupCollider;    private NetworkVariable<bool> isSkating = new NetworkVariable<bool>();
    private NetworkVariable<bool> isShooting = new NetworkVariable<bool>();
    // LABOTS: Atkārtoti pievienojam trūkstošos tīkla mainīgos, kas tika nejauši dzēsti
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
    private bool isMovementEnabled = true;    // LABOTS: Pievienojam trūkstošās mainīgo deklarācijas
    private bool isMyPlayer = false;
    private ulong localClientId = 0;
    private PlayerTeam teamComponent;
    private PlayerTeamVisuals visuals;
    private bool hasLoggedOwnership = false;    // Animāciju haši veiktspējai
    private int isSkatingHash;
    private int isShootingHash;
    private int shootTriggerHash;
    private int speedHash;

    // Kustību sekošana ServerRpc optimizācijai
    private float lastSentHorizontal = 0f;
    private float lastSentVertical = 0f;
    private bool lastSentSprint = false;
    private float inputSendRate = 0.05f; // Sūta ievadi 20 reizes sekundē
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
        }        isSkating.OnValueChanged += OnSkatingChanged;
        isShooting.OnValueChanged += OnShootingChanged;

        // Klausās komandas izmaiņas
        networkTeam.OnValueChanged += (oldTeam, newTeam) => { ApplyTeamColor(newTeam); };

        // Uzreiz piemēro krāsu pie parādīšanās
        ApplyTeamColor(networkTeam.Value);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }        // LEDUS FIZIKA: Uzlabota reālistiskākai ledus sajūtai
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.mass = 75f; // Nedaudz vieglāks labākai ledus sajūtai
        rb.linearDamping = 0.1f; // Neliels daudzums slāpēšanas reālismam
        rb.angularDamping = 5f; // Samazināts lielākam slidinājumam rotācijas laikā
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Piespiedu sākotnējā Y pozīcija
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
    }    private void Update()
    {
        // Apstrādā ievadi tikai īpašniekam (tikai tas spēlētājs, kuram pieder šis objekts, var kontrolēt kustību)
        if (!IsOwner && NetworkManager.Singleton != null) return;

        HandleMovementInput();

        // --- PELES SKATS AR JUTĪGUMU ---
        // Ja spēlētājs tur nospiestu Alt, ļauj grozīt kameru ar peli, ņemot vērā peles jutīgumu no iestatījumiem
        if (IsOwner && Input.GetKey(KeyCode.LeftAlt))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseSensitivity = 1.0f;
            // Vienmēr iegūst jaunāko vērtību no GameSettingsManager (lietotāja iestatījumi)
            if (GameSettingsManager.Instance != null)
                mouseSensitivity = GameSettingsManager.Instance.mouseSensitivity;

            // Piemēro peles jutīgumu kameras rotācijai (horizontālā skatīšanās)
            if (playerCamera != null)
            {
                playerCamera.transform.Rotate(Vector3.up, mouseX * mouseSensitivity * 2.0f, Space.World);
            }
            else
            {
                // Ja kamera ir spēlētāja bērns, rotē visu spēlētāju
                transform.Rotate(0f, mouseX * mouseSensitivity * 2.0f, 0f);
            }
        }

        // NOŅEMTS: Visa ripas pacelšanas loģika - PuckPickup komponents tagad apstrādā ripas pacelšanu
    }    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal"); // A/D tikai rotācijai
        float vertical = Input.GetAxis("Vertical");     // W/S tikai kustībai
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        bool quickStop = Input.GetKey(KeyCode.Space);   // JAUNS: Space taustiņš ātrai apturēšanai

        currentSprintState = sprint;        if (IsOwner)
        {
            // Saglabā pašreizējo ātrumu pirms jebkādām izmaiņām
            Vector3 currentVel = rb != null ? rb.linearVelocity : Vector3.zero;
            float currentHorizontalSpeed = new Vector3(currentVel.x, 0f, currentVel.z).magnitude;
            
            // LEDUS FIZIKA: Vienmērīgāka rotācija ar impulsa saglabāšanu
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                // Rotācija notiek neatkarīgi no kustības stāvokļa - saglabā impulsu
                float rotationAmount = horizontal * rotationSpeed * Time.deltaTime;
                // Samazina rotācijas ātrumu, kad kustas ātri, lai būtu reālistiskāki ledus pagriezieni
                if (currentHorizontalSpeed > 30f)
                {
                    rotationAmount *= Mathf.Lerp(1f, 0.6f, (currentHorizontalSpeed - 30f) / 70f);
                }
                transform.Rotate(0f, rotationAmount, 0f);
            }

            // PRIORITĀTE 1: Ātrā apstāšanās ar atstarpi (pārspēj visu)
            if (quickStop && rb != null)
            {
                // LEDUS FIZIKA: Pakāpeniskāka apturēšana (joprojām ātra, bet jūtas vairāk ledaina)
                Vector3 stoppedVel = currentVel * 0.7f; // Mazāk agresīva nekā 0.5f
                rb.linearVelocity = new Vector3(stoppedVel.x, currentVel.y, stoppedVel.z);
            }
            // PRIORITĀTE 2: Aktīvā kustības ievade (W/S nospiests)
            else if (Mathf.Abs(vertical) > 0.1f)
            {
                // LEDUS FIZIKA: Pakāpeniska paātrināšanās pretstatā tūlītējam ātrumam
                float targetSpeed = sprint ? sprintSpeed : moveSpeed;
                Vector3 targetDirection = transform.forward * vertical;
                Vector3 targetVelocity = targetDirection * targetSpeed;
                  if (rb != null)
                {
                    Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                    Vector3 velocityDiff = targetVelocity - currentHorizontalVel;
                    
                    // Piemēro paātrinājuma spēku reālistiskākai ledus sajūtai
                    float accelForce = acceleration * Time.deltaTime;
                    Vector3 newVelocity;
                    
                    if (velocityDiff.magnitude > accelForce)
                    {
                        // Pakāpeniska paātrināšanās
                        newVelocity = currentHorizontalVel + velocityDiff.normalized * accelForce;
                    }
                    else
                    {
                        // Pietiekami tuvu mērķim
                        newVelocity = targetVelocity;
                    }
                    
                    rb.linearVelocity = new Vector3(newVelocity.x, currentVel.y, newVelocity.z);
                }
            }
            // PRIORITĀTE 3: Impulsa uzturēšana - LEDUS FIZIKA: Dabiska slīdēšana
            else if (currentHorizontalSpeed > 0.1f)
            {
                // LEDUS FIZIKA: Piemēro pakāpenisku palēnināšanos, kad nav ievades
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
                        // Gandrīz apstājies
                        rb.linearVelocity = new Vector3(0f, currentVel.y, 0f);
                    }
                }
            }
            // PRIORITĀTE 4: Pilnīga apstāšanās (nav nepieciešama ātruma regulēšana)
            // Ļauj dabiskai palēnināšanai apstrādāt galīgo apstāšanos            // LABOTS: Atjaunina animācijas TIKAI, balstoties uz aktīvu W/S ievadi (nevis impulsu vai ātrumu)
            if (animator != null)
            {
                // SVARĪGI: Animācija tiek atskaņota TIKAI aktīvi spiežot W/S, NEVIS impulsa uzturēšanas laikā
                bool isActivelyMoving = Mathf.Abs(vertical) > 0.1f && !quickStop;
                animator.SetBool("IsSkating", isActivelyMoving);
                animator.speed = sprint ? 1.5f : 1.0f;
                
                // Atkļūdošanas ieraksts animācijas stāvokļa pārbaudei
                Debug.Log($"DIAGNOSTIKA: Animācija - vertikālā ievade: {vertical:F2}, aktīvi kustīgs: {isActivelyMoving}, ātrā apture: {quickStop}");
            }
        }

        // Tīkla sinhronizācija: Nosūta visas ievades, ieskaitot ātro apstāšanos
        bool inputChanged = Mathf.Abs(horizontal - lastSentHorizontal) > 0.05f || 
                           Mathf.Abs(vertical - lastSentVertical) > 0.05f || 
                           sprint != lastSentSprint;
        
        bool shouldSend = inputChanged || (Time.time - lastInputSendTime) > inputSendRate;
        
        // --- TĪKLA KUSTĪBAS SINHRONIZĀCIJA ---
        // Šeit tiek pārbaudīts, vai spēlētāja ievade (kustība, sprint, ātrā apstāšanās) ir mainījusies vai pagājis noteikts laiks kopš pēdējās nosūtīšanas.
        // Ja jā, tad ar MoveServerRpc tiek nosūtīti visi ievades dati uz serveri.
        // ServerRpc metodes Unity Netcode sistēmā ļauj klientam droši paziņot serverim par savu stāvokli vai darbībām.
        // Serveris pēc tam apstrādā šo informāciju, atjaunina fiziku un nosūta rezultātu citiem klientiem (izmantojot ClientRpc).
        // Šī loģika nodrošina, ka visi spēlētāji redz konsekventu un sinhronizētu kustību tīklā, pat ja notiek nelielas aiztures vai atšķirības starp klientiem.
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
        }        else // NE-ĪPAŠNIEKS (attālie klienti) - piemēro servera puses kustību
        {
            Vector3 currentVel = rb.linearVelocity;
            float currentHorizontalSpeed = new Vector3(currentVel.x, 0f, currentVel.z).magnitude;
            
            // LEDUS FIZIKA: Piemēro tādu pašu rotācijas loģiku attālajiem klientiem
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                float rotationAmount = horizontal * rotationSpeed * Time.fixedDeltaTime;
                if (currentHorizontalSpeed > 30f)
                {
                    rotationAmount *= Mathf.Lerp(1f, 0.6f, (currentHorizontalSpeed - 30f) / 70f);
                }
                transform.Rotate(0f, rotationAmount, 0f);
            }            if (quickStop)
            {
                Vector3 stoppedVel = currentVel * 0.7f; // Tāds pats kā īpašniekam
                rb.linearVelocity = new Vector3(stoppedVel.x, currentVel.y, stoppedVel.z);
            }
            else if (Mathf.Abs(vertical) > 0.1f)
            {
                // LEDUS FIZIKA: Tāda pati pakāpeniskā paātrināšanās attālajiem klientiem
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
            }            else if (currentHorizontalSpeed > 0.1f)
            {
                // LEDUS FIZIKA: Tāda pati pakāpeniskā palēnināšanās attālajiem klientiem
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
            }            // TIKAI ATTĀLAJIEM KLIENTIEM: Bloķē Y pozīciju un ātrumu
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
    }    [ClientRpc]
    private void UpdateMovementClientRpc(Vector3 position, Vector3 velocity, Quaternion rotation, bool sprint, bool quickStop, float verticalInput)
    {
        // Piemēro tikai ne-īpašniekiem (attālajiem spēlētājiem)
        if (!IsOwner && rb != null)
        {
            // Vienmērīga interpolācija uz servera pozīciju attālajiem spēlētājiem
            float lerpSpeed = 25f; // Palielināts no 20f ātrākai sinhronizācijai
            transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * lerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * lerpSpeed);
            
            // Piemēro ātrumu attālajiem spēlētājiem
            rb.linearVelocity = velocity;

            // LABOTS: Atjaunina attālo spēlētāju animācijas, balstoties uz IEVADI, nevis ātrumu
            if (animator != null)
            {
                // SVARĪGI: Izmanto faktisko ievades stāvokli no attālā spēlētāja, nevis ātrumu
                bool isActivelyMoving = Mathf.Abs(verticalInput) > 0.1f && !quickStop;
                animator.SetBool("IsSkating", isActivelyMoving);
                animator.speed = sprint ? 1.5f : 1.0f;
            }
        }
    }    private void FixedUpdate()
    {
        // SVARĪGI: HOST/ĪPAŠNIEKAM apstrādā Y-bloķēšanu TIKAI KLIENTA PUSĒ
        // Tas novērš servera iejaukšanos hosta fizikā
        if (IsOwner)
        {
            // HOSTS: Klienta puses Y pozīcijas bloķēšana (bez servera iejaukšanās)
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y - 0.71f) > 0.01f)
            {
                pos.y = 0.71f;
                transform.position = pos;
                if (rb != null)
                {
                    rb.position = pos;
                    // Tikai nulles Y ātrums, saglabā X/Z impulsu precīzi
                    Vector3 vel = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
                }
            }

            // HOSTS: Klienta puses ātruma ierobežošana
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                float maxSpeed = currentSprintState ? sprintSpeed : moveSpeed;
                float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
                
                // Atļauj nelielu pārsniegšanu ledus fizikas dēļ, bet ierobežo līdz saprātīgam limitam
                float maxAllowed = maxSpeed * 1.3f; // Samazināts no 2.0f labākai kontrolei
                
                if (horizontalSpeed > maxAllowed)
                {
                    Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
                    horizontalVel = horizontalVel.normalized * maxAllowed;
                    rb.linearVelocity = new Vector3(horizontalVel.x, vel.y, horizontalVel.z);
                }
            }
        }
        else // NE-ĪPAŠNIEKS
        {
            // ATTĀLIE KLIENTI: Serveris apstrādā Y-bloķēšanu
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

        // SERVERIS: Atjaunina tīkla mainīgos (bet neiejaucas hosta fizikā)
        if (IsServer && rb != null)
        {
            networkPosition.Value = transform.position;
            networkVelocity.Value = rb.linearVelocity;
        }
    }    private void ApplyIceFriction()
    {
        if (rb == null) return;

        Vector3 currentVel = rb.linearVelocity;
        // LEDUS FIZIKA: Ļoti minimāla berze īstai ledus sajūtai
        rb.linearVelocity = new Vector3(currentVel.x * iceFriction, currentVel.y, currentVel.z * iceFriction);
        
        // Apstājas tikai tad, kad ātrums ir ārkārtīgi zems reālistiskai ledus slīdēšanai
        if (rb.linearVelocity.magnitude < 0.5f) // Palielināts slieksnis lielākai slīdēšanai
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
    }    // Izsauc šo no GameNetworkManager pēc spēlētāja objekta parādīšanās:
    [ServerRpc]
    public void SetTeamServerRpc(Team team)
    {
        if (IsServer)
            networkTeam.Value = team;
    }

    // Publiska metode, lai nodrošinātu, ka spēlētāja kamera ir iestatīta (izsauc GameNetworkManager)
    public void EnsurePlayerCamera()
    {
        if (IsOwner && playerCamera == null)
        {
            SetupCamera();
        }
    }

    // Šī metode piemēro krāsu spēlētāja objektam
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
            }            renderer.materials = mats;
        }
        
        // Piezīme: PlayerTeamVisuals komponents apstrādā savas komandas krāsas caur NetworkVariable
        // Nav nepieciešams izsaukt SetTeamColor, jo tas neeksistē
    }

    // Pievieno šo metodi, lai ļautu PlayerShooting aktivizēt šaušanas animāciju
    public void TriggerShootAnimation()
    {
        if (animator != null)
        {
            animator.SetBool("IsShooting", true);
            animator.SetTrigger("Shoot");
            
            // Atiestata šaušanas animāciju pēc īsa laika
            StartCoroutine(ResetShootAnimation());
            
            Debug.Log("PlayerMovement: Šaušanas animācija aktivizēta");
        }
        else
        {
            Debug.LogWarning("PlayerMovement: Animator ir null, nevar aktivizēt šaušanas animāciju");
        }
    }    private System.Collections.IEnumerator ResetShootAnimation()
    {
        // Gaida animācijas atskaņošanu
        yield return new WaitForSeconds(0.5f);
        
        if (animator != null)
        {
            animator.SetBool("IsShooting", false);
        }
    }
}


