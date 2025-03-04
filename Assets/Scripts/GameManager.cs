using UnityEngine;

namespace MainGame
{
    public class GameManager : MonoBehaviour
    {
        [Header("Physics Setup")]
        public bool enableCustomPhysics = true;
        public float timeScale = 1.0f;
        public float fixedTimeStep = 0.01f; // Smaller time step for better physics
        
        [Header("Default Objects")]
        public GameObject playerPrefab;
        public GameObject puckPrefab;
        
        private void Awake()
        {
            // Configure physics for hockey
            if (enableCustomPhysics)
            {
                // Set up better physics time step
                Time.fixedDeltaTime = fixedTimeStep;
                Physics.defaultContactOffset = 0.001f; // Smaller offset for more accurate collisions
                Physics.defaultSolverIterations = 12;  // More solver iterations for stable physics
                Physics.defaultSolverVelocityIterations = 8;
                
                // Ensure we have a PhysicsManager
                if (FindObjectOfType<PhysicsManager>() == null)
                {
                    GameObject physicsManagerObj = new GameObject("PhysicsManager");
                    PhysicsManager physicsManager = physicsManagerObj.AddComponent<PhysicsManager>();
                    Debug.Log("Created PhysicsManager");
                }
            }
            
            Debug.Log("GameManager initialized");
        }
        
        private void Start()
        {
            // Apply physics materials
            PhysicsManager physicsManager = FindObjectOfType<PhysicsManager>();
            if (physicsManager != null)
            {
                physicsManager.ApplyMaterials();
                
                // Apply stick physics to all hockey sticks
                HockeyStickController[] sticks = FindObjectsOfType<HockeyStickController>();
                foreach (HockeyStickController stick in sticks)
                {
                    physicsManager.ApplyStickPhysics(stick);
                }
            }
            
            // Make sure the default Physics settings allow the layers to interact
            SetupPhysicsLayers();
        }
        
        private void SetupPhysicsLayers()
        {
            // Make sure the ice layer exists
            int iceLayer = LayerMask.NameToLayer("Ice");
            int stickLayer = LayerMask.NameToLayer("Player"); // Assuming stick is on player layer
            int puckLayer = LayerMask.NameToLayer("Puck");
            
            // Debug layer setup
            Debug.Log($"Ice layer: {iceLayer}, Stick layer: {stickLayer}, Puck layer: {puckLayer}");
            
            // If any critical layer is missing, warn the user
            if (iceLayer < 0 || puckLayer < 0)
            {
                Debug.LogWarning("Critical layers missing! Please set up your layers according to the documentation.");
            }
        }
        
        // Helper method to find or create a layer
        private int EnsureLayerExists(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"Layer '{layerName}' does not exist! Physics interactions may not work correctly.");
            }
            return layer;
        }
    }
}
