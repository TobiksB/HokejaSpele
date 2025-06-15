using UnityEngine;
using System.Collections; // Pievienojam šo, lai izmantotu WaitForSeconds

namespace HockeyGame.Game
{
    // Klase atbild par treniņa režīma pārvaldību hokeja spēlē
    // Tā uztur spēlētāju, ripu un kameras iestatīšanu ārpus tīkla spēles režīma
    public class TrainingModeManager : MonoBehaviour
    {
        [Header("Treniņa režīma iestatījumi")]
        [SerializeField] private GameObject playerPrefab; // Spēlētāja prefabs, ko instantizēt treniņa ainā
        [SerializeField] private GameObject puckPrefab; // Ripas prefabs, ko instantizēt treniņa ainā
        [SerializeField] private Transform playerSpawnPoint; // Vieta, kur spēlētājs parādīsies
        [SerializeField] private Transform puckSpawnPoint; // Vieta, kur ripa parādīsies
        
        [Header("Lietotāja saskarne")]
        [SerializeField] private GameObject pauseMenuPrefab; // Pauzes izvēlnes prefabs
        
        [Header("Kameras iestatījumi")]
        [SerializeField] private GameObject cameraPrefab; // Izmanto to pašu kameras prefabu, kas spēles ainās
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 6f, -10f); // Noklusējuma nobīde
        [SerializeField] private float cameraSmoothing = 0.1f; // Noklusējuma izlīdzināšanas vērtība

        private GameObject currentPlayer; // Atsauce uz pašreizējo spēlētāju
        private GameObject currentPuck; // Atsauce uz pašreizējo ripu
        private PauseMenuManager pauseMenu; // Atsauce uz pauzes izvēlnes pārvaldnieku
        
        private void Awake()
        {
            // Izveido pauzes izvēlni, ja tā nav piešķirta
            if (pauseMenuPrefab == null)
            {
                var pauseMenuGO = new GameObject("PauseMenu");
                pauseMenu = pauseMenuGO.AddComponent<PauseMenuManager>();
            }
            else
            {
                var pauseMenuGO = Instantiate(pauseMenuPrefab);
                pauseMenuGO.name = "PauseMenu";
                pauseMenu = pauseMenuGO.GetComponent<PauseMenuManager>();
                if (pauseMenu == null)
                {
                    pauseMenu = pauseMenuGO.AddComponent<PauseMenuManager>();
                }
            }
            
            // Nodrošina, ka Time.timeScale ir normāls
            Time.timeScale = 1f;
        }
        
        private void Start()
        {
            SpawnPlayer(); // Izvedo spēlētāju
            SetupPlayerCamera(); // Iestata kameru, kas seko spēlētājam
            SpawnPuck(); // Izveido ripu
            SetupGoalTriggers(); // Iestata vārtu trigeru notikumus
            
            Debug.Log("TrainingModeManager: Treniņa režīms inicializēts");
        }
        
        private void SpawnPlayer()
        {
            // Noklusējuma parādīšanās pozīcija, ja nav iestatīta
            Vector3 spawnPos = playerSpawnPoint != null 
                ? playerSpawnPoint.position 
                : new Vector3(0f, 0.71f, -5f);
                
            Quaternion spawnRot = playerSpawnPoint != null 
                ? playerSpawnPoint.rotation 
                : Quaternion.identity;
            
            if (playerPrefab != null)
            {
                currentPlayer = Instantiate(playerPrefab, spawnPos, spawnRot);
                
                // Atspējo visas NetworkObject komponentes
                var networkComponents = currentPlayer.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>();
                foreach (var comp in networkComponents)
                {
                    Debug.Log($"TrainingModeManager: Atspējo tīkla komponenti {comp.GetType().Name}");
                    Destroy(comp);
                }

                var networkObjects = currentPlayer.GetComponentsInChildren<Unity.Netcode.NetworkObject>();
                foreach (var netObj in networkObjects)
                {
                    Debug.Log($"TrainingModeManager: Noņem NetworkObject komponenti");
                    Destroy(netObj);
                }
                
                // Noņem esošos skriptus, kuriem nepieciešama tīklošana
                var existingPlayerMovement = currentPlayer.GetComponent<PlayerMovement>();
                if (existingPlayerMovement != null)
                {
                    Destroy(existingPlayerMovement);
                }
                
                var existingPuckPickup = currentPlayer.GetComponent<PuckPickup>();
                if (existingPuckPickup != null)
                {
                    Destroy(existingPuckPickup);
                }
                
                var existingPlayerShooting = currentPlayer.GetComponent<PlayerShooting>();
                if (existingPlayerShooting != null)
                {
                    Destroy(existingPlayerShooting);
                }
                
                // Pievieno treniņam specifiskus komponentus ar atspējotu automātisku atiestatīšanu
                var trainingMovement = currentPlayer.AddComponent<TrainingPlayerMovement>();
                currentPlayer.AddComponent<TrainingPuckPickup>();
                var shooting = currentPlayer.AddComponent<TrainingPlayerShooting>();
                
                // Atspējo automātisko atiestatīšanu, lai novērstu pastāvīgu respawnošanu
                if (shooting != null)
                {
                    var field = shooting.GetType().GetField("enableAutoReset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (field != null)
                        field.SetValue(shooting, false);
                }
                
                // Saglabā sākotnējo pozīciju manuālām atiestatīšanām
                Vector3 initialPos = spawnPos;
                initialPos.y = 0.71f;
                
                // Iestata īpašības pēc tam, kad tās ir pievienotas TrainingPlayerMovement
                trainingMovement.initialPosition = initialPos;
                trainingMovement.initialRotation = spawnRot;
                
                // Pārliecinās, ka fizika joprojām darbojas
                var rb = currentPlayer.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                }
                
                Debug.Log($"TrainingModeManager: Spēlētājs izvietots pozīcijā {spawnPos} ar treniņa režīma komponentēm");
            }
            else
            {
                Debug.LogError("TrainingModeManager: Nav piešķirts spēlētāja prefabs!");
            }
        }
        
        private void SpawnPuck()
        {
            // Noklusējuma parādīšanās pozīcija, ja nav iestatīta
            Vector3 spawnPos = puckSpawnPoint != null 
                ? puckSpawnPoint.position 
                : new Vector3(0f, 0.71f, 0f);
            
            if (puckPrefab != null)
            {
                currentPuck = Instantiate(puckPrefab, spawnPos, Quaternion.identity);
                
                // Pievieno Puck komponenti, ja tā nav
                if (currentPuck.GetComponent<Puck>() == null)
                    currentPuck.AddComponent<Puck>();
                
                // Nodrošina, ka tai ir rigidbody
                var rb = currentPuck.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = currentPuck.AddComponent<Rigidbody>();
                    rb.mass = 0.5f;
                    rb.linearDamping = 0.5f;
                    rb.useGravity = true;
                }
                
                // Nodrošina, ka tai ir sadursmes detektors
                if (currentPuck.GetComponent<Collider>() == null)
                {
                    var col = currentPuck.AddComponent<SphereCollider>();
                    col.radius = 0.5f;
                    col.material = CreateIcyPhysicMaterial();
                }
                
                Debug.Log($"TrainingModeManager: Ripa izvietota pozīcijā {spawnPos}");
            }
            else
            {
                Debug.LogError("TrainingModeManager: Nav piešķirts ripas prefabs!");
            }
        }
        
        // Izveido ledainu fizikas materiālu ripai
        private PhysicsMaterial CreateIcyPhysicMaterial()
        {
            PhysicsMaterial material = new PhysicsMaterial("IcyPuck");
            material.dynamicFriction = 0.1f; // Zema berze kustībā
            material.staticFriction = 0.1f; // Zema berze miera stāvoklī
            material.bounciness = 0.3f; // Vidēja atlēkšana
            material.frictionCombine = PhysicsMaterialCombine.Minimum; // Izmanto mazāko berzi no abiem objektiem
            material.bounceCombine = PhysicsMaterialCombine.Average; // Izmanto vidējo atlēkšanu no abiem objektiem
            return material;
        }
        
        // Iestata vārtu trigeru notikumus
        private void SetupGoalTriggers()
        {
            // Atrod esošos vārtu trigerus ainā
            var goalTriggers = FindObjectsByType<TrainingModeGoalTrigger>(FindObjectsSortMode.None);
            
            if (goalTriggers.Length == 0)
            {
                Debug.LogWarning("TrainingModeManager: Aināī nav atrasts neviens TrainingModeGoalTrigger. Vārti nedarbosies!");
            }
            
            // Pārliecinās, ka visiem vārtu trigeriem ir pieslēgti OnGoalScored notikumi
            foreach (var trigger in goalTriggers)
            {
                trigger.OnGoalScored += ResetPuckAfterGoal;
            }
            
            Debug.Log($"TrainingModeManager: Iestatīti {goalTriggers.Length} vārtu trigeri");
        }
        
        // Tiek izsaukts, kad tiek gūti vārti
        private void ResetPuckAfterGoal(string teamName)
        {
            if (currentPuck == null)
            {
                Debug.LogWarning("TrainingModeManager: Nevar atiestatīt neeksistējošu ripu!");
                return;
            }
            
            StartCoroutine(DelayedPuckReset());
        }
        
        // Atiestatīt ripu pēc nelielas aizkaves
        private IEnumerator DelayedPuckReset()
        {
            // Pagaida brīdi vārtu efektiem
            yield return new WaitForSeconds(1.5f);
            
            // Iegūst ripas komponenti
            var puckComponent = currentPuck.GetComponent<Puck>();
            
            // Aptur sekošanu, ja tā notiek
            var puckFollower = currentPuck.GetComponent<PuckFollower>();
            if (puckFollower != null)
            {
                puckFollower.StopFollowing();
                puckFollower.enabled = false;
            }
            
            // Atiestata ripas stāvokli
            if (puckComponent != null)
            {
                puckComponent.SetHeld(false);
            }
            
            // Atiestata ripas fiziku
            var rb = currentPuck.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            // Atiestata pozīciju
            Vector3 resetPos = puckSpawnPoint != null 
                ? puckSpawnPoint.position 
                : new Vector3(0f, 0.71f, 0f);
                
            currentPuck.transform.position = resetPos;
            currentPuck.transform.rotation = Quaternion.identity;
            
            Debug.Log($"TrainingModeManager: Ripa atiestatīta uz {resetPos}");
        }
        
        // Publiska metode treniņa atiestatīšanai
        public void ResetTraining()
        {
            if (currentPuck != null)
            {
                StartCoroutine(DelayedPuckReset());
            }
            else
            {
                SpawnPuck();
            }
        }

        // Iestata kameru, kas sekos spēlētājam
        private void SetupPlayerCamera()
        {
            if (currentPlayer == null)
            {
                Debug.LogError("TrainingModeManager: Nevar iestatīt kameru - spēlētājs ir null!");
                return;
            }
            
            // Mēģina izmantot piešķirto kameras prefabu (tas pats, kas spēles ainās)
            if (cameraPrefab != null)
            {
                GameObject cameraObj = Instantiate(cameraPrefab, currentPlayer.transform.position + cameraOffset, Quaternion.identity);
                
                // Mēģina iegūt un iestatīt CameraFollow komponenti
                CameraFollow cameraFollow = cameraObj.GetComponent<CameraFollow>();
                if (cameraFollow != null)
                {
                    cameraFollow.SetTarget(currentPlayer.transform);
                    cameraFollow.SetOffset(cameraOffset);
                    Debug.Log("TrainingModeManager: Iestatīta spēles kamera no prefaba");
                }
                else
                {
                    Debug.LogWarning("TrainingModeManager: Kameras prefabam nav CameraFollow komponentes");
                    SetupFallbackCamera();
                }
            }
            // Rezerves variants - izveido savu kameru
            else
            {
                SetupFallbackCamera();
            }
        }
        
        // Izveido vienkāršu rezerves kameru, ja prefabs nav pieejams
        private void SetupFallbackCamera()
        {
            GameObject cameraObj = new GameObject("Training Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            
            // Pievieno audio klausītāju
            cameraObj.AddComponent<AudioListener>();
            
            // Pievieno CameraFollow skriptu
            CameraFollow cameraFollow = cameraObj.AddComponent<CameraFollow>();
            cameraFollow.SetTarget(currentPlayer.transform);
            cameraFollow.SetOffset(cameraOffset);
            
            Debug.Log("TrainingModeManager: Izveidota rezerves kamera ar CameraFollow");
        }
        
        // Publiska metode manuālai atiestatīšanai (var izsaukt no UI pogām)
        public void ResetPlayerAndPuck()
        {
            if (currentPlayer != null && currentPlayer.GetComponent<TrainingPlayerMovement>() != null)
            {
                var movement = currentPlayer.GetComponent<TrainingPlayerMovement>();
                
                // Atiestata spēlētāja pozīciju
                if (movement.initialPosition != Vector3.zero) // Tikai ja mums ir derīga sākotnējā pozīcija
                {
                    currentPlayer.transform.position = movement.initialPosition;
                    currentPlayer.transform.rotation = movement.initialRotation;
                    
                    var rb = currentPlayer.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.position = movement.initialPosition;
                        rb.rotation = movement.initialRotation;
                    }
                }
            }
            
            // Arī atiestata ripu
            ResetTraining();
        }
    }
}
