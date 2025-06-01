using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

namespace HockeyGame.Game
{

    public class RootGameManager : NetworkBehaviour
    {
        public static RootGameManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameUI gameUI;
        [SerializeField] private QuarterTransitionPanel quarterTransitionPanel;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private GameOverPanel gameOverPanel;
        [SerializeField] private PauseMenuManager pauseMenuManager;
  
        [Header("Score UI")]
        [SerializeField] private UnityEngine.UI.Text scoreDisplayText;
        [SerializeField] private TMPro.TextMeshProUGUI scoreDisplayTMP;

        // Score variables
        private NetworkVariable<int> redScore = new NetworkVariable<int>(0);
        private NetworkVariable<int> blueScore = new NetworkVariable<int>(0);
        
        // Component references
        private ScoreManager scoreManager;
        private QuarterManager quarterManager;

        private void Awake()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu")
            {
                Debug.LogError($"CRITICAL ABORT: RootGameManager in MainMenu - destroying immediately");
                Destroy(gameObject);
                return;
            }
            
            if (!IsGameScene(currentScene))
            {
                Debug.LogError($"RootGameManager: Not in game scene ({currentScene}) - destroying");
                Destroy(gameObject);
                return;
            }

            if (Instance == null)
            {
                Instance = this;
                ValidateReferences();
                Debug.Log("RootGameManager initialized successfully");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void OnGameEnd()
        {
            var rootManager = FindFirstObjectByType<HockeyGame.Game.GameManager>();
            if (rootManager != null && rootManager != this)
            {
                rootManager.OnGameEnd();
            }
            else
            {
                Debug.Log("Game ended!");
                
                // Show game over panel
                var gameOverPanel = FindFirstObjectByType<GameOverPanel>();
                if (gameOverPanel != null)
                {
                    var scoreManager = FindFirstObjectByType<ScoreManager>();
                    if (scoreManager != null)
                    {
                 
                        scoreManager.UpdateScoreDisplay();
                        gameOverPanel.ShowGameOver(scoreManager.GetRedScore(), scoreManager.GetBlueScore());
                    }
                }
                
                // Stop game timer
                var gameTimer = FindFirstObjectByType<GameTimer>();
                if (gameTimer != null)
                {
                    gameTimer.StopTimer();
                }
                
                // Disable player controls
                var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    player.enabled = false;
                }
                
                // Start end game sequence
                StartCoroutine(EndGameSequence());
            }
        }
        
        private System.Collections.IEnumerator EndGameSequence()
        {
            yield return new WaitForSeconds(5f); // Show results for 5 seconds
            
            // Return to main menu
            if (IsServer)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }
        
        private void FindAndSetupComponents()
        {
            // Find UI components if not assigned
            if (gameUI == null)
            {
                gameUI = FindFirstObjectByType<GameUI>();
                if (gameUI == null)
                {
                    Debug.LogWarning("GameUI not found in scene");
                }
            }
            
            if (scoreManager == null)
            {
                scoreManager = FindFirstObjectByType<ScoreManager>();
                if (scoreManager == null)
                {
                    Debug.LogWarning("ScoreManager not found in scene");
                }
            }
            
            if (quarterManager == null)
            {
                quarterManager = FindFirstObjectByType<QuarterManager>();
                if (quarterManager == null)
                {
                    Debug.LogWarning("QuarterManager not found in scene");
                }
            }
        }
        
        private void UpdateGameUI()
        {
            if (gameUI != null)
            {
                gameUI.UpdateScore(redScore.Value, blueScore.Value);
            }
            
            // FIXED: Update score display UI directly
            UpdateScoreDisplayUI(redScore.Value, blueScore.Value);
            
            if (scoreManager != null)
            {
                scoreManager.UpdateScoreDisplay();
            }
        }
        
        // FIXED: Add method to update score display UI
        private void UpdateScoreDisplayUI(int redScore, int blueScore)
        {
            string scoreText = $"Red {redScore} - Blue {blueScore}";
            
            // Update UI Text component if assigned
            if (scoreDisplayText != null)
            {
                scoreDisplayText.text = scoreText;
                Debug.Log($"RootGameManager: Updated UI Text score display: {scoreText}");
            }
            
            // Update TextMeshPro component if assigned
            if (scoreDisplayTMP != null)
            {
                scoreDisplayTMP.text = scoreText;
                Debug.Log($"RootGameManager: Updated TMP score display: {scoreText}");
            }
            
            // FIXED: Also find and update any score UI elements in the scene
            if (scoreDisplayText == null && scoreDisplayTMP == null)
            {
                FindAndUpdateScoreUI(scoreText);
            }
        }
        
        // FIXED: Add method to find and update score UI elements automatically
        private void FindAndUpdateScoreUI(string scoreText)
        {
            // Try to find UI Text components with score-related names
            var allTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
            foreach (var text in allTexts)
            {
                if (text.name.ToLower().Contains("score"))
                {
                    text.text = scoreText;
                    Debug.Log($"RootGameManager: Found and updated score text: {text.name}");
                }
            }
            
            // Try to find TextMeshPro components with score-related names
            var allTMPs = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var tmp in allTMPs)
            {
                if (tmp.name.ToLower().Contains("score"))
                {
                    tmp.text = scoreText;
                    Debug.Log($"RootGameManager: Found and updated TMP score text: {tmp.name}");
                }
            }
        }
        
        private bool IsGameScene(string sceneName)
        {
            return sceneName == "TrainingMode" || 
                   sceneName == "GameScene2v2" || 
                   sceneName == "GameScene4v4";
        }

        private void ValidateReferences()
        {
            if (gameUI == null)
            {
                Debug.LogError("GameUI not found in scene! Creating basic GameUI...");
                CreateBasicGameUI();
            }

            if (quarterTransitionPanel == null)
            {
                Debug.LogWarning("QuarterTransitionPanel was not assigned, attempting to find in scene");
                quarterTransitionPanel = Object.FindFirstObjectByType<QuarterTransitionPanel>();
                
                if (quarterTransitionPanel == null)
                {
                    Debug.LogWarning("QuarterTransitionPanel not found in scene - creating simple one");
                    CreateBasicQuarterTransitionPanel();
                }
            }

            if (gameTimer == null)
            {
                Debug.LogWarning("GameTimer not found in scene");
                gameTimer = Object.FindFirstObjectByType<GameTimer>();
                
                if (gameTimer == null)
                {
                    Debug.LogWarning("GameTimer not found - creating basic one");
                    CreateBasicGameTimer();
                }
            }

            if (gameOverPanel == null)
            {
                Debug.LogWarning("GameOverPanel not found in scene");
                gameOverPanel = Object.FindFirstObjectByType<GameOverPanel>();
                
                if (gameOverPanel == null)
                {
                    Debug.LogWarning("GameOverPanel not found - creating basic one");
                    CreateBasicGameOverPanel();
                }
            }

            if (pauseMenuManager == null)
            {
                Debug.LogWarning("PauseMenuManager not found in scene");
                pauseMenuManager = Object.FindFirstObjectByType<PauseMenuManager>();
                
                if (pauseMenuManager == null)
                {
                    Debug.LogWarning("PauseMenuManager not found - creating basic one");
                    CreateBasicPauseMenuManager();
                }
            }

            Debug.Log("GameManager: All references validated and created if missing");
        }

        private void CreateBasicGameUI()
        {
            GameObject gameUIObj = new GameObject("GameUI");
            Canvas canvas = gameUIObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            
            gameUIObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            gameUIObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            gameUI = gameUIObj.AddComponent<GameUI>();
            
            Debug.Log("Created basic GameUI");
        }

        private void CreateBasicQuarterTransitionPanel()
        {
            GameObject panelObj = new GameObject("QuarterTransitionPanel");
            quarterTransitionPanel = panelObj.AddComponent<QuarterTransitionPanel>();
            panelObj.SetActive(false);
            
            Debug.Log("Created basic QuarterTransitionPanel");
        }

        private void CreateBasicGameTimer()
        {
            GameObject timerObj = new GameObject("GameTimer");
            gameTimer = timerObj.AddComponent<GameTimer>();
            
            Debug.Log("Created basic GameTimer");
        }

        private void CreateBasicGameOverPanel()
        {
            GameObject panelObj = new GameObject("GameOverPanel");
            gameOverPanel = panelObj.AddComponent<GameOverPanel>();
            panelObj.SetActive(false);
            
            Debug.Log("Created basic GameOverPanel");
        }

        private void CreateBasicPauseMenuManager()
        {
            GameObject managerObj = new GameObject("PauseMenuManager");
            pauseMenuManager = managerObj.AddComponent<PauseMenuManager>();
            
            Debug.Log("Created basic PauseMenuManager");
        }

        public override void OnNetworkSpawn()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu")
            {
                Debug.LogError($"ABORT: GameManager.OnNetworkSpawn in MainMenu - skipping");
                return;
            }
            
            if (!IsGameScene(currentScene))
            {
                Debug.LogError($"ABORT: GameManager.OnNetworkSpawn not in game scene ({currentScene}) - skipping");
                return;
            }

            Debug.Log($"GameManager.OnNetworkSpawn in scene: {currentScene}");
            
            // FIXED: Subscribe to score changes for UI updates
            redScore.OnValueChanged += OnRedScoreChanged;
            blueScore.OnValueChanged += OnBlueScoreChanged;
            
            if (IsServer)
            {
                InitializeGameState();
            }
            
            ValidateReferences();
        }

        // FIXED: Add score change handlers for UI updates
        private void OnRedScoreChanged(int oldValue, int newValue)
        {
            Debug.Log($"RootGameManager: Red score changed from {oldValue} to {newValue}");
            UpdateGameUI();
        }

        private void OnBlueScoreChanged(int oldValue, int newValue)
        {
            Debug.Log($"RootGameManager: Blue score changed from {oldValue} to {newValue}");
            UpdateGameUI();
        }

        public override void OnNetworkDespawn()
        {
            // FIXED: Unsubscribe from score changes
            redScore.OnValueChanged -= OnRedScoreChanged;
            blueScore.OnValueChanged -= OnBlueScoreChanged;
            
            base.OnNetworkDespawn();
        }

        private void InitializeGameState()
        {
            Debug.Log("GameManager: Initializing game state");
            
            if (gameTimer != null)
            {
                gameTimer.StartTimer();
            }
        }

        public void PauseGame()
        {
            if (pauseMenuManager != null)
            {
                pauseMenuManager.TogglePause();
            }
        }

        public void ShowGameOver(int red, int blue)
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.ShowGameOver(red, blue);
            }
            else
            {
                Debug.LogError("GameOverPanel not found!");
            }
        }

        public void ShowQuarterTransition(int quarter)
        {
            if (quarterTransitionPanel != null)
            {
                quarterTransitionPanel.ShowQuarterTransition(quarter);
            }
        }

        // Add method to update scores
        public void UpdateScore(int redPoints, int bluePoints)
        {
            if (IsServer)
            {
                redScore.Value = redPoints;
                blueScore.Value = bluePoints;
                UpdateGameUI();
            }
        }
        
        // Add method to add score
        public void AddScore(bool isBlueTeam, int points = 1)
        {
            if (IsServer)
            {
                if (isBlueTeam)
                {
                    blueScore.Value += points;
                }
                else
                {
                    redScore.Value += points;
                }
                UpdateGameUI();
            }
        }
        
        // Public properties for score access
        public int RedScore => redScore.Value;
        public int BlueScore => blueScore.Value;
    }
}