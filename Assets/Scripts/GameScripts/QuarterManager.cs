using Unity.Netcode;
using UnityEngine;
using TMPro; // Add TMPro namespace

namespace HockeyGame.Game
{
    public class QuarterManager : NetworkBehaviour
    {
        public static QuarterManager Instance { get; private set; }
        
        [Header("Game Settings")]
        [SerializeField] private float quarterDuration = 20f; // TEST: 30 seconds per quarter
        [SerializeField] private int totalQuarters = 3;
        
        [Header("UI References")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private QuarterDisplayUI quarterDisplayUI;
        [SerializeField] private QuarterTransitionPanel quarterTransitionPanel;
        [SerializeField] private GameOverPanel gameOverPanel; // Add this inspector reference

        private NetworkVariable<int> currentQuarter = new NetworkVariable<int>(1);
        private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(20f); // TEST: 30 seconds initial value
        private bool isGameActive = true;

        private void Awake()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            // CRITICAL: Only allow in game scenes
            if (currentScene == "MainMenu")
            {
                Debug.LogError($"CRITICAL ABORT: QuarterManager in MainMenu - DESTROYING IMMEDIATELY");
                Destroy(gameObject);
                return;
            }
            
            if (!IsGameScene(currentScene))
            {
                Debug.LogError($"QuarterManager: Not in game scene ({currentScene}) - destroying");
                Destroy(gameObject);
                return;
            }

            if (Instance == null)
            {
                Instance = this;
                Debug.Log($"QuarterManager initialized in scene: {currentScene}");
                
                // IMPROVED: Only look for dependencies if we're in an actual game scene with UI
                if (currentScene != "TrainingMode") // Training mode might not have full UI
                {
                    if (quarterTransitionPanel == null)
                    {
                        quarterTransitionPanel = FindFirstObjectByType<QuarterTransitionPanel>();
                        if (quarterTransitionPanel == null)
                        {
                            Debug.LogWarning("QuarterTransitionPanel not found, creating one now.");
                            var go = new GameObject("QuarterTransitionPanel");
                            quarterTransitionPanel = go.AddComponent<QuarterTransitionPanel>();
                        }
                    }
                }
                
                // Find QuarterDisplayUI in scene
                if (quarterDisplayUI == null)
                {
                    quarterDisplayUI = FindFirstObjectByType<QuarterDisplayUI>();
                }

                // --- Show Q1 at start ---
                if (quarterDisplayUI != null)
                {
                    quarterDisplayUI.SetQuarter(1);
                }

                // CRITICAL: Start the timer immediately
                Debug.Log("QuarterManager: Starting game timer automatically");
                isGameActive = true;
                timeRemaining.Value = quarterDuration;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // FIXED: Add missing IsGameScene method
        private bool IsGameScene(string sceneName)
        {
            return sceneName == "TrainingMode" || 
                   sceneName == "GameScene2v2" || 
                   sceneName == "GameScene4v4";
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                timeRemaining.Value = quarterDuration;
                currentQuarter.Value = 1;
                StartQuarter();
            }

            // Subscribe to changes for UI updates
            timeRemaining.OnValueChanged += OnTimeChanged;
            currentQuarter.OnValueChanged += OnQuarterChanged;

            // Ensure UI is correct on spawn
            UpdateQuarterUI();
        }

        public override void OnNetworkDespawn()
        {
            timeRemaining.OnValueChanged -= OnTimeChanged;
            currentQuarter.OnValueChanged -= OnQuarterChanged;
        }

        private void Update()
        {
            // FIXED: Countdown timer - time goes down
            if (IsServer && isGameActive && timeRemaining.Value > 0)
            {
                timeRemaining.Value -= Time.deltaTime;
                
                if (timeRemaining.Value <= 0)
                {
                    timeRemaining.Value = 0;
                    EndQuarter();
                }
            }
        }

        private void StartQuarter()
        {
            if (!IsServer) return;

            Debug.Log($"Starting Quarter {currentQuarter.Value}");
            isGameActive = true;
            timeRemaining.Value = quarterDuration;

            // --- Ensure quarter display is updated at the start of each quarter ---
            UpdateQuarterUI();

            // Update UI
            UpdateTimerUI();

            // --- CRITICAL: Always reset puck at start of quarter, especially Q2 and Q3 ---
            StartCoroutine(BruteForceResetPuck());
        }

        // --- NEW: Completely reliable puck reset with clear debugging ---
        private System.Collections.IEnumerator BruteForceResetPuck() 
        {
            Debug.LogWarning($"QuarterManager: BRUTE FORCE puck reset at Q{currentQuarter.Value} start");
            
            // Wait for physics to stabilize
            yield return new WaitForSeconds(1.0f);
            
            // Find all pucks
            var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (allPucks.Length == 0) {
                Debug.LogError("QuarterManager: No puck found to reset!");
                yield break;
            }
            
            GameObject puck = allPucks[0].gameObject;
            Debug.Log($"QuarterManager: Found puck {puck.name} at position {puck.transform.position}");
            
            // Force release from any player
            var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
            foreach (var player in allPlayers) {
                if (player.HasPuck()) {
                    Debug.Log($"QuarterManager: Forcing player {player.name} to release puck");
                    player.ForceReleasePuckForSteal();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            
            // Clear all PuckFollower
            var puckFollower = puck.GetComponent<PuckFollower>();
            if (puckFollower != null) {
                Debug.Log("QuarterManager: Stopping PuckFollower");
                puckFollower.StopFollowing();
                puckFollower.enabled = false;
            }
            
            // Teleport to center
            Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
            Debug.Log($"QuarterManager: Teleporting puck from {puck.transform.position} to {centerPos}");
            
            puck.transform.position = centerPos;
            puck.transform.rotation = Quaternion.identity;
            
            // Reset rigidbody and physics
            var rb = puck.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = centerPos;
                rb.rotation = Quaternion.identity;
                Debug.Log($"QuarterManager: Reset puck rigidbody to {centerPos}");
            }
            
            // Ensure collider is enabled
            var col = puck.GetComponent<Collider>();
            if (col != null) {
                col.enabled = true;
                Debug.Log("QuarterManager: Enabled puck collider");
            }
            
            // Reset held state
            var puckComponent = puck.GetComponent<Puck>();
            if (puckComponent != null) {
                puckComponent.SetHeld(false);
                Debug.Log("QuarterManager: Set puck held state to false");
            }
            
            // Tell all clients to reset puck position
            if (puckComponent != null && IsServer) {
                var puckNetObj = puck.GetComponent<NetworkObject>();
                if (puckNetObj != null) {
                    SetPuckPositionClientRpc(puckNetObj.NetworkObjectId, centerPos);
                    Debug.Log($"QuarterManager: Sent SetPuckPositionClientRpc to clients for puck {puckNetObj.NetworkObjectId}");
                }
            }
            
            Debug.LogWarning($"QuarterManager: PUCK RESET COMPLETE - Now at {puck.transform.position}");
        }

        [ClientRpc]
        private void SetPuckPositionClientRpc(ulong puckNetworkId, Vector3 position)
        {
            Debug.Log($"QuarterManager: CLIENT received SetPuckPositionClientRpc for puck {puckNetworkId}");
            
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var netObj))
            {
                var puck = netObj.gameObject;
                Debug.Log($"QuarterManager: CLIENT found puck {puck.name}, setting position to {position}");
                
                // Set position
                puck.transform.position = position;
                puck.transform.rotation = Quaternion.identity;
                
                // Reset physics
                var rb = puck.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = position;
                    rb.rotation = Quaternion.identity;
                }
                
                // End any following
                var puckFollower = puck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }
                
                // Reset held state
                var puckComponent = puck.GetComponent<Puck>();
                if (puckComponent != null)
                {
                    puckComponent.SetHeld(false);
                }
                
                Debug.LogWarning($"QuarterManager: CLIENT puck reset complete at {position}");
            }
            else
            {
                Debug.LogError($"QuarterManager: CLIENT could not find puck with NetworkObjectId {puckNetworkId}");
            }
        }
        
        private void EndQuarter()
        {
            if (!IsServer) return;

            isGameActive = false;

            // Reset all players to spawn points at end of quarter
            ResetAllPlayersToSpawnPoints();

            // --- FINAL FIX: Use the same logic as GoalTrigger to reset the puck ---
            ResetPuckToCenterGoalTriggerStyle();

            if (currentQuarter.Value >= totalQuarters)
            {
                ShowGameOverPanelClientRpc();
            }
            else
            {
                StartCoroutine(TransitionToNextQuarter());
            }
        }

        // --- FINAL: Use the exact logic as GoalTrigger for puck reset ---
        private void ResetPuckToCenterGoalTriggerStyle()
        {
            StartCoroutine(ResetPuckCoroutineGoalStyle());
        }

        private System.Collections.IEnumerator ResetPuckCoroutineGoalStyle()
        {
            yield return new WaitForSeconds(1.5f); // Wait for player resets and network sync

            // ENHANCED: Multiple strategies to find the puck
            GameObject puckObject = FindPuckByAllMeans();
            
            if (puckObject == null)
            {
                Debug.LogError("QuarterManager: No puck found after exhaustive search. Cannot reset!");
                yield break;
            }
            
            Debug.Log($"QuarterManager: Found puck: {puckObject.name} at position {puckObject.transform.position}");
            
            var puckComponent = puckObject.GetComponent<Puck>();

            // Ensure puck is completely free before reset
            if (puckComponent != null)
            {
                puckComponent.SetHeld(false);

                // Clear from any player still holding it
                var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
                foreach (var player in allPlayers)
                {
                    if (player.GetCurrentPuck() == puckComponent)
                    {
                        player.ForceReleasePuckForSteal();
                        Debug.Log($"QuarterManager: Cleared remaining reference from {player.name}");
                    }
                }
            }

            // Stop any PuckFollower components
            var puckFollower = puckObject.GetComponent<PuckFollower>();
            if (puckFollower != null)
            {
                puckFollower.StopFollowing();
                puckFollower.enabled = false;
            }

            // Proper puck reset to center (same as after goal)
            Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
            puckObject.transform.SetParent(null);
            puckObject.transform.position = centerPos;
            puckObject.transform.rotation = Quaternion.identity;

            var rb = puckObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = centerPos;
            }

            var col = puckObject.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }

            // Use Puck's ResetToCenter method if available (for network sync)
            if (puckComponent != null && puckComponent.IsServer)
            {
                try
                {
                    var resetMethod = puckComponent.GetType().GetMethod("ResetToCenter");
                    if (resetMethod != null)
                    {
                        resetMethod.Invoke(puckComponent, null);
                        Debug.Log("QuarterManager: Used Puck.ResetToCenter() method for network sync");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"QuarterManager: Failed to use Puck.ResetToCenter(): {e.Message}");
                }
            }

            Debug.Log($"QuarterManager: Puck reset to center after quarter at position {centerPos}");
        }
        
        // New method to find puck using multiple search strategies
        private GameObject FindPuckByAllMeans()
        {
            Debug.Log("QuarterManager: Starting exhaustive puck search using multiple methods...");
            
            // Method 1: FindObjectsByType<Puck>
            var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (allPucks != null && allPucks.Length > 0)
            {
                Debug.Log($"QuarterManager: Found {allPucks.Length} pucks using FindObjectsByType<Puck>");
                return allPucks[0].gameObject;
            }
            
            // Method 2: FindGameObjectsWithTag
            var taggedObjects = GameObject.FindGameObjectsWithTag("Puck");
            if (taggedObjects != null && taggedObjects.Length > 0)
            {
                Debug.Log($"QuarterManager: Found {taggedObjects.Length} objects with tag 'Puck'");
                return taggedObjects[0];
            }
            
            // Method 3: Find objects on puck layer
            var allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.layer == LayerMask.NameToLayer("Puck"))
                {
                    Debug.Log($"QuarterManager: Found object {obj.name} on 'Puck' layer");
                    return obj;
                }
            }
            
            // Method 4: Find by name contains
            foreach (var obj in allObjects)
            {
                if (obj.name.ToLower().Contains("puck"))
                {
                    Debug.Log($"QuarterManager: Found object with 'puck' in name: {obj.name}");
                    return obj;
                }
            }
            
            // Method 5: Last resort - create a new puck
            Debug.LogWarning("QuarterManager: No puck found! Creating a new puck at center position.");
            var newPuck = new GameObject("EmergencyPuck");
            newPuck.transform.position = new Vector3(0f, 0.71f, 0f);
            newPuck.transform.rotation = Quaternion.identity;
            newPuck.tag = "Puck";
            newPuck.layer = LayerMask.NameToLayer("Puck");
            var rb = newPuck.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            newPuck.AddComponent<SphereCollider>().radius = 0.5f;
            
            // Add a simple Puck component
            if (newPuck.GetComponent<Puck>() == null)
            {
                newPuck.AddComponent<Puck>();
            }
            
            return newPuck;
        }

        // --- Show GameOverPanel on ALL clients ---
        [ClientRpc]
        private void ShowGameOverPanelClientRpc()
        {
            Debug.Log("QuarterManager: [ClientRpc] ShowGameOverPanelClientRpc called on client");
            
            // Force immediate execution on main thread
            StartCoroutine(ShowGameOverPanelCoroutine());
        }

        private System.Collections.IEnumerator ShowGameOverPanelCoroutine()
        {
            yield return null; // Wait one frame
            
            Debug.Log("QuarterManager: [Coroutine] Starting GameOver panel search and display");
            
            // Get scores from all possible sources
            int redScore = 0, blueScore = 0;
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                redScore = scoreManager.GetRedScore();
                blueScore = scoreManager.GetBlueScore();
                Debug.Log($"QuarterManager: Got scores from ScoreManager - Red: {redScore}, Blue: {blueScore}");
            }

            // Try multiple ways to find the GameOverPanel
            GameOverPanel panelToUse = gameOverPanel;
            
            if (panelToUse == null)
            {
                panelToUse = FindFirstObjectByType<GameOverPanel>();
                Debug.Log($"QuarterManager: FindFirstObjectByType result: {(panelToUse != null ? panelToUse.name : "null")}");
            }
            
            if (panelToUse == null)
            {
                var panelGO = GameObject.Find("GameOverPanel");
                if (panelGO != null)
                {
                    panelToUse = panelGO.GetComponent<GameOverPanel>();
                    Debug.Log($"QuarterManager: Found by GameObject.Find: {panelGO.name}");
                }
            }
            
            if (panelToUse == null)
            {
                // Search in all canvases
                var allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var canvas in allCanvases)
                {
                    var panel = canvas.GetComponentInChildren<GameOverPanel>(true);
                    if (panel != null)
                    {
                        panelToUse = panel;
                        Debug.Log($"QuarterManager: Found GameOverPanel in canvas: {canvas.name}");
                        break;
                    }
                }
            }

            if (panelToUse != null)
            {
                Debug.Log($"QuarterManager: Found GameOverPanel: {panelToUse.name}, activating...");
                
                // Force activate the panel's game object first
                panelToUse.gameObject.SetActive(true);
                
                // Wait a frame for activation
                yield return null;
                
                // Now show the game over screen
                panelToUse.ShowGameOver(redScore, blueScore);
                
                Debug.Log($"QuarterManager: GameOverPanel activated and ShowGameOver called!");
            }
            else
            {
                Debug.LogError("QuarterManager: Could not find GameOverPanel anywhere! Creating fallback...");
                CreateFallbackGameOverUI(redScore, blueScore);
            }
        }

        // --- Create a simple fallback UI if GameOverPanel is missing ---
        private void CreateFallbackGameOverUI(int redScore, int blueScore)
        {
            var tempPanel = new GameObject("FallbackGameOverPanel");
            var canvas = tempPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            var canvasScaler = tempPanel.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            
            tempPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            var background = tempPanel.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0, 0, 0, 0.8f);
            
            var text = new GameObject("GameOverText");
            text.transform.SetParent(tempPanel.transform);
            var textComponent = text.AddComponent<TMPro.TextMeshProUGUI>();
            
            string winner = "Draw!";
            if (redScore > blueScore) winner = "Red Team Wins!";
            else if (blueScore > redScore) winner = "Blue Team Wins!";
            
            textComponent.text = $"{winner}\nRed: {redScore} - Blue: {blueScore}\n\nGame Over";
            textComponent.fontSize = 48;
            textComponent.color = Color.white;
            textComponent.alignment = TMPro.TextAlignmentOptions.Center;
            
            var rectTransform = text.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            
            Debug.Log("QuarterManager: Created fallback game over UI successfully");
        }

        // --- NEW: Reset all players to their spawn points (same as after a goal) ---
        private void ResetAllPlayersToSpawnPoints()
        {
            var gameNetMgr = FindFirstObjectByType<GameNetworkManager>();
            if (gameNetMgr == null)
            {
                Debug.LogWarning("QuarterManager: GameNetworkManager not found, cannot reset player positions.");
                return;
            }

            var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
            if (players == null || players.Length == 0)
            {
                Debug.LogWarning("QuarterManager: No PlayerMovement objects found to reset positions.");
                return;
            }

            foreach (var player in players)
            {
                if (player == null)
                {
                    Debug.LogWarning("QuarterManager: Found null PlayerMovement in players array.");
                    continue;
                }

                var netObj = player.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogWarning($"QuarterManager: Player {player.name} has no NetworkObject.");
                    continue;
                }

                ulong clientId = netObj.OwnerClientId;
                // Try to get team as string
                string team = "Red";
                try
                {
                    if (player.GetType().GetProperty("Team") != null)
                    {
                        team = player.GetType().GetProperty("Team").GetValue(player)?.ToString() ?? "Red";
                    }
                    else if (player.GetType().GetField("team") != null)
                    {
                        team = player.GetType().GetField("team").GetValue(player)?.ToString() ?? "Red";
                    }
                    else if (player.GetType().GetField("networkTeam", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null)
                    {
                        var networkTeamVar = player.GetType().GetField("networkTeam", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(player);
                        if (networkTeamVar != null)
                        {
                            var valueProp = networkTeamVar.GetType().GetProperty("Value");
                            if (valueProp != null)
                            {
                                var enumValue = valueProp.GetValue(networkTeamVar);
                                team = enumValue?.ToString() ?? "Red";
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"QuarterManager: Error determining team for player {player.name}: {e.Message}");
                }

                Vector3 spawnPos = player.transform.position;
                try
                {
                    var method = gameNetMgr.GetType().GetMethod("GetSpawnPositionFromInspector", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        var result = method.Invoke(gameNetMgr, new object[] { clientId, team });
                        if (result is Vector3 pos)
                        {
                            spawnPos = pos;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"QuarterManager: Error getting spawn position for player {player.name}: {e.Message}");
                }

                Quaternion spawnRot = team == "Blue"
                    ? Quaternion.Euler(0, 90, 0)
                    : Quaternion.Euler(0, -90, 0);

                // Teleport player to spawn
                player.transform.position = spawnPos;
                player.transform.rotation = spawnRot;
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = spawnPos;
                    rb.rotation = spawnRot;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            Debug.Log("QuarterManager: All players reset to their spawn points at end of quarter.");
        }

        private System.Collections.IEnumerator TransitionToNextQuarter()
        {
            // Show transition panel
            if (quarterTransitionPanel != null)
            {
                quarterTransitionPanel.ShowTransition(currentQuarter.Value + 1);
            }

            yield return new WaitForSeconds(3f); // 3 second transition

            if (IsServer)
            {
                // --- CRITICAL: Use a ServerRpc to update quarter and timer for all clients ---
                SetNextQuarterServerRpc();
            }

            // Hide transition panel
            if (quarterTransitionPanel != null)
            {
                quarterTransitionPanel.HideTransition();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetNextQuarterServerRpc()
        {
            currentQuarter.Value++;
            timeRemaining.Value = quarterDuration;
            isGameActive = true;
            UpdateQuarterUI();
            UpdateTimerUI();
        }

        private void EndGame()
        {
            Debug.Log("Game ended - all quarters completed");
            isGameActive = false;

            // --- NEW: Show end game panel and winner ---
            ShowEndGamePanel();

            // --- NEW: Start coroutine to return to main menu and shutdown networking ---
            StartCoroutine(EndGameSequence());
        }

        // --- NEW: Show end game panel and winner ---
        private void ShowEndGamePanel()
        {
            int redScore = 0, blueScore = 0;
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                redScore = scoreManager.GetRedScore();
                blueScore = scoreManager.GetBlueScore();
            }

            string winner = "Draw!";
            if (redScore > blueScore) winner = "Red Team Wins!";
            else if (blueScore > redScore) winner = "Blue Team Wins!";

            // Try to find a GameOverPanel or similar UI
            var gameOverPanel = FindFirstObjectByType<GameOverPanel>();
            if (gameOverPanel != null)
            {
                // Show only winner text and buttons
                gameOverPanel.ShowWinner(winner);
            }
            else
            {
                // Fallback: log to console
                Debug.Log($"GAME OVER! {winner}");
            }
        }

        // --- NEW: Coroutine to return to main menu and shutdown networking ---
        private System.Collections.IEnumerator EndGameSequence()
        {
            // Wait 5 seconds to show the panel
            yield return new WaitForSeconds(5f);

            // Shutdown host/client and return to main menu
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }

            // Wait a moment to ensure shutdown before loading scene
            yield return new WaitForSeconds(0.5f);

            // --- FIX: Always load MainMenu for both host and client ---
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");

            // --- Optional: If you want to be extra sure, force shutdown again after scene load ---
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }
        }

        private void OnTimeChanged(float previousValue, float newValue)
        {
            UpdateTimerUI();
        }

        private void OnQuarterChanged(int previousValue, int newValue)
        {
            // --- CRITICAL: Reset timer and activate game on all clients when quarter changes ---
            timeRemaining.Value = quarterDuration;
            isGameActive = true;
            UpdateQuarterUI();
            UpdateTimerUI();
        }

        private void UpdateTimerUI()
        {
            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(timeRemaining.Value / 60f);
                int seconds = Mathf.FloorToInt(timeRemaining.Value % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
            }
            
            // Also update ScoreManager if it exists
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                // FIXED: Call UpdateScoreDisplay without parameters instead of with int scores
                scoreManager.UpdateScoreDisplay();
            }
        }

        private void UpdateQuarterUI()
        {
            // Update quarter display in UI
            Debug.Log($"Current Quarter: {currentQuarter.Value}/{totalQuarters}");
            if (quarterDisplayUI == null)
            {
                quarterDisplayUI = FindFirstObjectByType<QuarterDisplayUI>();
            }
            if (quarterDisplayUI != null)
            {
                quarterDisplayUI.SetQuarter(currentQuarter.Value);
            }
        }

        // Public methods for external access
        public int GetCurrentQuarter() => currentQuarter.Value;
        public float GetTimeRemaining() => timeRemaining.Value;
        public bool IsGameActive() => isGameActive;
        public float GetQuarterDuration() => quarterDuration;

        [ServerRpc(RequireOwnership = false)]
        public void PauseGameServerRpc()
        {
            isGameActive = false;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ResumeGameServerRpc()
        {
            isGameActive = true;
        }

        // FIXED: Add missing EndCurrentQuarter method
        public void EndCurrentQuarter()
        {
            if (!IsServer) return;
            
            Debug.Log($"Manually ending current quarter {currentQuarter.Value}");
            EndQuarter();
        }

        private void OnQuarterEnd()
        {
            // Handle end of quarter logic
            var scoreManager = FindFirstObjectByType<ScoreManager>();
            if (scoreManager != null)
            {
                // FIXED: Call UpdateScoreDisplay without parameters instead of with int
                scoreManager.UpdateScoreDisplay();
                Debug.Log($"QuarterManager: Quarter {currentQuarter} ended, score display updated");
            }
            
        }
    }
}
