using Unity.Netcode;
using UnityEngine;

namespace HockeyGame.Game
{
    public class QuarterManager : NetworkBehaviour
    {
        public static QuarterManager Instance { get; private set; }
        
        [Header("Game Settings")]
        [SerializeField] private float quarterDuration = 100f; // 5 minutes
        [SerializeField] private int totalQuarters = 3;
        
        [Header("UI References")]
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private QuarterTransitionPanel quarterTransitionPanel;
        
        private NetworkVariable<int> currentQuarter = new NetworkVariable<int>(1);
        private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(300f);
        private bool isGameActive = true;

        private QuarterDisplayUI quarterDisplayUI; // Add this field

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
                    if (gameTimer == null)
                    {
                        gameTimer = FindFirstObjectByType<GameTimer>();
                        if (gameTimer == null)
                        {
                            Debug.LogWarning("GameTimer not found, creating one now.");
                            var go = new GameObject("GameTimer");
                            gameTimer = go.AddComponent<GameTimer>();
                        }
                    }

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
            if (quarterDisplayUI != null)
            {
                quarterDisplayUI.SetQuarter(currentQuarter.Value);
            }

            // Update UI
            UpdateTimerUI();
        }

        private void EndQuarter()
        {
            if (!IsServer) return;

            Debug.Log($"Quarter {currentQuarter.Value} ended");
            isGameActive = false;

            // --- NEW: Reset all players to spawn points at end of quarter ---
            ResetAllPlayersToSpawnPoints();

            if (currentQuarter.Value >= totalQuarters)
            {
                EndGame();
            }
            else
            {
                StartCoroutine(TransitionToNextQuarter());
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
            foreach (var player in players)
            {
                ulong clientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? 0;
                // Try to get team as string
                string team = "Red";
                if (player.GetType().GetProperty("Team") != null)
                {
                    team = player.GetType().GetProperty("Team").GetValue(player).ToString();
                }
                else if (player.GetType().GetField("team") != null)
                {
                    team = player.GetType().GetField("team").GetValue(player).ToString();
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
                            team = enumValue.ToString();
                        }
                    }
                }

                // Get spawn position from GameNetworkManager
                Vector3 spawnPos = gameNetMgr.GetType().GetMethod("GetSpawnPositionFromInspector", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(gameNetMgr, new object[] { clientId, team }) as Vector3? ?? player.transform.position;

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
            
            // Start next quarter
            if (IsServer)
            {
                currentQuarter.Value++;
                StartQuarter();
            }
            
            // Hide transition panel
            if (quarterTransitionPanel != null)
            {
                quarterTransitionPanel.HideTransition();
            }
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
            UpdateQuarterUI();
        }

        private void UpdateTimerUI()
        {
            if (gameTimer != null)
            {
                gameTimer.UpdateTimer(timeRemaining.Value);
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
