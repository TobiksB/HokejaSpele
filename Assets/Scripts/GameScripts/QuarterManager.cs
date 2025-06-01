using Unity.Netcode;
using UnityEngine;

namespace HockeyGame.Game
{
    public class QuarterManager : NetworkBehaviour
    {
        public static QuarterManager Instance { get; private set; }
        
        [Header("Game Settings")]
        [SerializeField] private float quarterDuration = 300f; // 5 minutes
        [SerializeField] private int totalQuarters = 3;
        
        [Header("UI References")]
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private QuarterTransitionPanel quarterTransitionPanel;
        
        private NetworkVariable<int> currentQuarter = new NetworkVariable<int>(1);
        private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(300f);
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
            
            // Update UI
            UpdateTimerUI();
        }

        private void EndQuarter()
        {
            if (!IsServer) return;
            
            Debug.Log($"Quarter {currentQuarter.Value} ended");
            isGameActive = false;
            
            if (currentQuarter.Value >= totalQuarters)
            {
                EndGame();
            }
            else
            {
                StartCoroutine(TransitionToNextQuarter());
            }
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
            
            // Notify game manager
            var gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                gameManager.OnGameEnd();
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
