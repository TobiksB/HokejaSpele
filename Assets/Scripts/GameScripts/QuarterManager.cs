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
        }

        private void EndQuarter()
        {
            if (!IsServer) return;

            isGameActive = false;

            // Reset all players to spawn points at end of quarter
            ResetAllPlayersToSpawnPoints();

            // --- Reset puck to center after each quarter ---
            ResetPuckToCenter();

            if (currentQuarter.Value >= totalQuarters)
            {
                // Show GameOverPanel and do NOT transfer players or change scene
                ShowGameOverPanel();
            }
            else
            {
                StartCoroutine(TransitionToNextQuarter());
            }
        }

        // --- Show GameOverPanel with winner and score, do not transfer players ---
        private void ShowGameOverPanel()
        {
            int redScore = 0, blueScore = 0;
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                redScore = scoreManager.GetRedScore();
                blueScore = scoreManager.GetBlueScore();
            }

            // Try inspector reference first, then fallback to FindObjectOfType
            GameOverPanel panelToUse = gameOverPanel;
            if (panelToUse == null)
            {
                panelToUse = FindFirstObjectByType<GameOverPanel>();
            }

            if (panelToUse != null)
            {
                panelToUse.ShowGameOver(redScore, blueScore);
                Debug.Log($"QuarterManager: Game over panel shown with scores Red: {redScore}, Blue: {blueScore}");
            }
            else
            {
                Debug.LogWarning("QuarterManager: GameOverPanel not found in scene!");
                
                // Fallback: Create a temporary game over panel
                var tempPanel = new GameObject("TempGameOverPanel");
                var canvas = tempPanel.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                
                var background = tempPanel.AddComponent<UnityEngine.UI.Image>();
                background.color = new Color(0, 0, 0, 0.8f);
                
                var text = new GameObject("GameOverText");
                text.transform.SetParent(tempPanel.transform);
                var textComponent = text.AddComponent<TMPro.TextMeshProUGUI>();
                textComponent.text = $"Game Over!\nRed: {redScore} - Blue: {blueScore}";
                textComponent.fontSize = 36;
                textComponent.color = Color.white;
                textComponent.alignment = TMPro.TextAlignmentOptions.Center;
                
                var rectTransform = text.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
                
                Debug.Log("QuarterManager: Created temporary game over panel");
            }
        }

        // --- Reset puck to center after each quarter ---
        private void ResetPuckToCenter()
        {
            var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (allPucks.Length == 0)
            {
                Debug.LogWarning("QuarterManager: No puck found to reset after quarter.");
                return;
            }

            foreach (var puck in allPucks)
            {
                if (puck == null) continue;

                // Clear any references to this puck from players
                var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
                foreach (var player in allPlayers)
                {
                    if (player.GetCurrentPuck() == puck)
                    {
                        player.ForceReleasePuckForSteal();
                    }
                }

                // Stop PuckFollower
                var puckFollower = puck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }

                // Reset physics
                var rb = puck.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                var col = puck.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                puck.SetHeld(false);

                // Move to center spawn position
                Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
                puck.transform.position = centerPos;
                puck.transform.rotation = Quaternion.identity;

                if (rb != null)
                {
                    rb.position = centerPos;
                    rb.rotation = Quaternion.identity;
                }

                // Use Puck's ResetToCenter method if available for network sync
                var resetMethod = puck.GetType().GetMethod("ResetToCenter");
                if (resetMethod != null)
                {
                    resetMethod.Invoke(puck, null);
                }

                Debug.Log("QuarterManager: Puck reset to center after quarter end.");
            }
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
