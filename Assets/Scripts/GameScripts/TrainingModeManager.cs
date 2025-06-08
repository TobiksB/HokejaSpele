using UnityEngine;
using System.Collections; // Add this for WaitForSeconds

namespace HockeyGame.Game
{
    public class TrainingModeManager : MonoBehaviour
    {
        [Header("Training Mode Settings")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject puckPrefab;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform puckSpawnPoint;
        
        [Header("UI")]
        [SerializeField] private GameObject pauseMenuPrefab;
        
        [Header("Camera Settings")]
        [SerializeField] private GameObject cameraPrefab; // Use the same camera prefab as in game scenes
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 6f, -10f); // Default offset
        [SerializeField] private float cameraSmoothing = 0.1f; // Default smoothing value

        private GameObject currentPlayer;
        private GameObject currentPuck;
        private PauseMenuManager pauseMenu;
        
        private void Awake()
        {
            // Create pause menu if not assigned
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
            
            // Ensure Time.timeScale is normal
            Time.timeScale = 1f;
        }
        
        private void Start()
        {
            SpawnPlayer();
            SetupPlayerCamera();
            SpawnPuck();
            SetupGoalTriggers();
            
            Debug.Log("TrainingModeManager: Training mode initialized");
        }
        
        private void SpawnPlayer()
        {
            // Default spawn position if not set
            Vector3 spawnPos = playerSpawnPoint != null 
                ? playerSpawnPoint.position 
                : new Vector3(0f, 0.71f, -5f);
                
            Quaternion spawnRot = playerSpawnPoint != null 
                ? playerSpawnPoint.rotation 
                : Quaternion.identity;
            
            if (playerPrefab != null)
            {
                currentPlayer = Instantiate(playerPrefab, spawnPos, spawnRot);
                
                // Disable any NetworkObject components
                var networkComponents = currentPlayer.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>();
                foreach (var comp in networkComponents)
                {
                    Debug.Log($"TrainingModeManager: Disabling network component {comp.GetType().Name}");
                    Destroy(comp);
                }

                var networkObjects = currentPlayer.GetComponentsInChildren<Unity.Netcode.NetworkObject>();
                foreach (var netObj in networkObjects)
                {
                    Debug.Log($"TrainingModeManager: Removing NetworkObject component");
                    Destroy(netObj);
                }
                
                // Remove existing scripts that require networking
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
                
                // Add training-specific components with auto-reset disabled
                var trainingMovement = currentPlayer.AddComponent<TrainingPlayerMovement>();
                currentPlayer.AddComponent<TrainingPuckPickup>();
                var shooting = currentPlayer.AddComponent<TrainingPlayerShooting>();
                
                // Disable auto-reset to prevent constant respawning
                if (shooting != null)
                {
                    var field = shooting.GetType().GetField("enableAutoReset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (field != null)
                        field.SetValue(shooting, false);
                }
                
                // Store initial position for manual resets
                Vector3 initialPos = spawnPos;
                initialPos.y = 0.71f;
                
                // Set the properties after they are added to TrainingPlayerMovement
                trainingMovement.initialPosition = initialPos;
                trainingMovement.initialRotation = spawnRot;
                
                // Make sure physics still works
                var rb = currentPlayer.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                }
                
                Debug.Log($"TrainingModeManager: Player spawned at {spawnPos} with training mode components");
            }
            else
            {
                Debug.LogError("TrainingModeManager: Player prefab not assigned!");
            }
        }
        
        private void SpawnPuck()
        {
            // Default spawn position if not set
            Vector3 spawnPos = puckSpawnPoint != null 
                ? puckSpawnPoint.position 
                : new Vector3(0f, 0.71f, 0f);
            
            if (puckPrefab != null)
            {
                currentPuck = Instantiate(puckPrefab, spawnPos, Quaternion.identity);
                
                // Add Puck component if it doesn't exist
                if (currentPuck.GetComponent<Puck>() == null)
                    currentPuck.AddComponent<Puck>();
                
                // Ensure it has a rigidbody
                var rb = currentPuck.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = currentPuck.AddComponent<Rigidbody>();
                    rb.mass = 0.5f;
                    rb.linearDamping = 0.5f;
                    rb.useGravity = true;
                }
                
                // Ensure it has a collider
                if (currentPuck.GetComponent<Collider>() == null)
                {
                    var col = currentPuck.AddComponent<SphereCollider>();
                    col.radius = 0.5f;
                    col.material = CreateIcyPhysicMaterial();
                }
                
                Debug.Log($"TrainingModeManager: Puck spawned at {spawnPos}");
            }
            else
            {
                Debug.LogError("TrainingModeManager: Puck prefab not assigned!");
            }
        }
        
        private PhysicsMaterial CreateIcyPhysicMaterial()
        {
            PhysicsMaterial material = new PhysicsMaterial("IcyPuck");
            material.dynamicFriction = 0.1f;
            material.staticFriction = 0.1f;
            material.bounciness = 0.3f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounceCombine = PhysicsMaterialCombine.Average;
            return material;
        }
        
        private void SetupGoalTriggers()
        {
            // Find existing goal triggers in the scene
            var goalTriggers = FindObjectsByType<TrainingModeGoalTrigger>(FindObjectsSortMode.None);
            
            if (goalTriggers.Length == 0)
            {
                Debug.LogWarning("TrainingModeManager: No TrainingModeGoalTrigger found in scene. Goals won't work!");
            }
            
            // Make sure all goal triggers have OnGoalScored events wired up
            foreach (var trigger in goalTriggers)
            {
                trigger.OnGoalScored += ResetPuckAfterGoal;
            }
            
            Debug.Log($"TrainingModeManager: Set up {goalTriggers.Length} goal triggers");
        }
        
        // Called when a goal is scored
        private void ResetPuckAfterGoal(string teamName)
        {
            if (currentPuck == null)
            {
                Debug.LogWarning("TrainingModeManager: Cannot reset null puck!");
                return;
            }
            
            StartCoroutine(DelayedPuckReset());
        }
        
        private IEnumerator DelayedPuckReset()
        {
            // Wait a moment for goal effects
            yield return new WaitForSeconds(1.5f);
            
            // Get puck component
            var puckComponent = currentPuck.GetComponent<Puck>();
            
            // Stop following if it's being followed
            var puckFollower = currentPuck.GetComponent<PuckFollower>();
            if (puckFollower != null)
            {
                puckFollower.StopFollowing();
                puckFollower.enabled = false;
            }
            
            // Reset puck state
            if (puckComponent != null)
            {
                puckComponent.SetHeld(false);
            }
            
            // Reset puck physics
            var rb = currentPuck.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            // Reset position
            Vector3 resetPos = puckSpawnPoint != null 
                ? puckSpawnPoint.position 
                : new Vector3(0f, 0.71f, 0f);
                
            currentPuck.transform.position = resetPos;
            currentPuck.transform.rotation = Quaternion.identity;
            
            Debug.Log($"TrainingModeManager: Puck reset to {resetPos}");
        }
        
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

        private void SetupPlayerCamera()
        {
            if (currentPlayer == null)
            {
                Debug.LogError("TrainingModeManager: Cannot set up camera - player is null!");
                return;
            }
            
            // Try to use provided camera prefab (same as game scenes)
            if (cameraPrefab != null)
            {
                GameObject cameraObj = Instantiate(cameraPrefab, currentPlayer.transform.position + cameraOffset, Quaternion.identity);
                
                // Try to get and setup CameraFollow component
                CameraFollow cameraFollow = cameraObj.GetComponent<CameraFollow>();
                if (cameraFollow != null)
                {
                    cameraFollow.SetTarget(currentPlayer.transform);
                    cameraFollow.SetOffset(cameraOffset);
                    Debug.Log("TrainingModeManager: Set up game camera from prefab");
                }
                else
                {
                    Debug.LogWarning("TrainingModeManager: Camera prefab doesn't have CameraFollow component");
                    SetupFallbackCamera();
                }
            }
            // Fallback to creating our own camera
            else
            {
                SetupFallbackCamera();
            }
        }
        
        private void SetupFallbackCamera()
        {
            GameObject cameraObj = new GameObject("Training Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            
            // Add audio listener
            cameraObj.AddComponent<AudioListener>();
            
            // Add CameraFollow script
            CameraFollow cameraFollow = cameraObj.AddComponent<CameraFollow>();
            cameraFollow.SetTarget(currentPlayer.transform);
            cameraFollow.SetOffset(cameraOffset);
            
            Debug.Log("TrainingModeManager: Created fallback camera with CameraFollow");
        }
        
        // Add a public method for manual reset (can be called from UI buttons)
        public void ResetPlayerAndPuck()
        {
            if (currentPlayer != null && currentPlayer.GetComponent<TrainingPlayerMovement>() != null)
            {
                var movement = currentPlayer.GetComponent<TrainingPlayerMovement>();
                
                // Reset player position
                if (movement.initialPosition != Vector3.zero) // Only if we have a valid initial position
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
            
            // Also reset the puck
            ResetTraining();
        }
    }
}
