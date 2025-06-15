using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

namespace HockeyGame.Game
{
   
    /// Galvenā spēles pārvaldības klase, kas koordinē spēles gaitu, rezultātus un starpperiodu pārejas.
    /// Izmanto NetworkBehaviour, lai nodrošinātu datu sinhronizāciju starp visiem spēlētājiem tīklā.
   
    public class RootGameManager : NetworkBehaviour
    {
        // Singltona instance, lai viegli piekļūtu no citām klasēm
        public static RootGameManager Instance { get; private set; }

        [Header("UI atsauces")]
        [SerializeField] private GameUI gameUI; // Galvenā UI komponente spēles informācijas attēlošanai
        [SerializeField] private QuarterTransitionPanel quarterTransitionPanel; // Panelis periodu pārejām
        [SerializeField] private GameTimer gameTimer; // Spēles laika taimeris
        [SerializeField] private GameOverPanel gameOverPanel; // Spēles beigu panelis ar rezultātiem
        [SerializeField] private PauseMenuManager pauseMenuManager; // Pauzes izvēlnes pārvaldnieks
        
        // Rezultātu UI atsauces
        [Header("Rezultātu UI")]
        [SerializeField] private UnityEngine.UI.Text scoreDisplayText; // Legacy UI Text komponente rezultātu attēlošanai
        [SerializeField] private TMPro.TextMeshProUGUI scoreDisplayTMP; // TextMeshPro komponente rezultātu attēlošanai

        // Rezultātu mainīgie, kas tiek sinhronizēti pār tīklu
        private NetworkVariable<int> redScore = new NetworkVariable<int>(0); // Sarkanās komandas rezultāts
        private NetworkVariable<int> blueScore = new NetworkVariable<int>(0); // Zilās komandas rezultāts
        
        // Komponenšu atsauces
        private ScoreManager scoreManager; // Rezultātu pārvaldnieks
        private QuarterManager quarterManager; // Periodu pārvaldnieks

        /// Unity Awake funkcija, kas tiek izsaukta objekta inicializācijas laikā.
        /// Pārbauda vai esam pareizajā scēnā un iestatām singltona instanci.
        private void Awake()
        {
            // Pārbaudīt, vai mēs esam pareizajā scēnā
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu")
            {
                Debug.LogError($"KRITISKA KĻŪDA: RootGameManager atrodas galvenajā izvēlnē - tiek iznīcināts nekavējoties");
                Destroy(gameObject);
                return;
            }
            
            if (!IsGameScene(currentScene))
            {
                Debug.LogError($"RootGameManager: Nav spēles scēna ({currentScene}) - tiek iznīcināts");
                Destroy(gameObject);
                return;
            }

            // Iestatīt singltona instanci
            if (Instance == null)
            {
                Instance = this;
                ValidateReferences(); // Pārbaudīt vai visas nepieciešamās komponentes ir pieejamas
                Debug.Log("RootGameManager inicializēts veiksmīgi");
            }
            else
            {
                Destroy(gameObject); // Nodrošinam, ka eksistē tikai viena instance
            }
        }

        /// 
        /// Tiek izsaukta, kad spēle beidzas. Parāda rezultātus un apstādina spēli.
        /// 
        public void OnGameEnd()
        {
            // Mēģinām atrast ar another GameManager instance (ja tāda ir)
            var rootManager = FindFirstObjectByType<HockeyGame.Game.GameManager>();
            if (rootManager != null && rootManager != this)
            {
                rootManager.OnGameEnd();
            }
            else
            {
                Debug.Log("Spēle beigusies!");
                
                // Parādīt spēles beigu paneli
                var gameOverPanel = FindFirstObjectByType<GameOverPanel>();
                if (gameOverPanel != null)
                {
                    var scoreManager = FindFirstObjectByType<ScoreManager>();
                    if (scoreManager != null)
                    {
                        // Atjaunināt rezultātu attēlojumu
                        scoreManager.UpdateScoreDisplay();
                        gameOverPanel.ShowGameOver(scoreManager.GetRedScore(), scoreManager.GetBlueScore());
                    }
                }
                
                // Apturēt spēles taimeri
                var gameTimer = FindFirstObjectByType<GameTimer>();
                if (gameTimer != null)
                {
                    gameTimer.StopTimer();
                }
                
                // Atspējot spēlētāju kontroles
                var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    player.enabled = false;
                }
                
                // Sākt beigu sekvenci
                StartCoroutine(EndGameSequence());
            }
        }
        
        /// 
        /// Korutīna, kas apstrādā spēles beigu sekvenci - rāda rezultātus un atgriežas galvenajā izvēlnē.
        /// 
        private System.Collections.IEnumerator EndGameSequence()
        {
            yield return new WaitForSeconds(5f); // Rādīt rezultātus 5 sekundes
            
            // Atgriezties galvenajā izvēlnē
            if (IsServer)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }
        
        /// 
        /// Meklē un iestatām nepieciešamās komponentes, ja tās vēl nav piešķirtas.
        /// 
        private void FindAndSetupComponents()
        {
            // Atrast UI komponentes, ja tās nav piešķirtas
            if (gameUI == null)
            {
                gameUI = FindFirstObjectByType<GameUI>();
                if (gameUI == null)
                {
                    Debug.LogWarning("GameUI nav atrasts scēnā");
                }
            }
            
            if (scoreManager == null)
            {
                scoreManager = FindFirstObjectByType<ScoreManager>();
                if (scoreManager == null)
                {
                    Debug.LogWarning("ScoreManager nav atrasts scēnā");
                }
            }
            
            if (quarterManager == null)
            {
                quarterManager = FindFirstObjectByType<QuarterManager>();
                if (quarterManager == null)
                {
                    Debug.LogWarning("QuarterManager nav atrasts scēnā");
                }
            }
        }
        
        /// 
        /// Atjaunina spēles UI ar aktuālajiem rezultātiem.
        /// 
        private void UpdateGameUI()
        {
            if (gameUI != null)
            {
                gameUI.UpdateScore(redScore.Value, blueScore.Value);
            }
            
            // Atjaunināt rezultātu attēlojumu UI tieši
            UpdateScoreDisplayUI(redScore.Value, blueScore.Value);
            
            if (scoreManager != null)
            {
                scoreManager.UpdateScoreDisplay();
            }
        }
        
        /// 
        /// Atjaunina rezultātu attēlojumu UI komponentēs.
        /// 
        /// <param name="redScore">Sarkanās komandas punkti</param>
        /// <param name="blueScore">Zilās komandas punkti</param>
        private void UpdateScoreDisplayUI(int redScore, int blueScore)
        {
            string scoreText = $"Sarkanā {redScore} - Zilā {blueScore}";
            
            // Atjaunināt UI Text komponenti, ja tā ir piešķirta
            if (scoreDisplayText != null)
            {
                scoreDisplayText.text = scoreText;
                Debug.Log($"RootGameManager: Atjaunināts UI Text rezultātu attēlojums: {scoreText}");
            }
            
            // Atjaunināt TextMeshPro komponenti, ja tā ir piešķirta
            if (scoreDisplayTMP != null)
            {
                scoreDisplayTMP.text = scoreText;
                Debug.Log($"RootGameManager: Atjaunināts TMP rezultātu attēlojums: {scoreText}");
            }
            
            // Atrast un atjaunināt visus rezultātu UI elementus scēnā, ja neviens nav piešķirts
            if (scoreDisplayText == null && scoreDisplayTMP == null)
            {
                FindAndUpdateScoreUI(scoreText);
            }
        }
        
        /// 
        /// Meklē un atjaunina visus rezultātu UI elementus automātiski.
        /// 
        /// <param name="scoreText">Rezultāta teksts, ko attēlot</param>
        private void FindAndUpdateScoreUI(string scoreText)
        {
            // Mēģināt atrast UI Text komponentes ar rezultātiem saistītiem nosaukumiem
            var allTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
            foreach (var text in allTexts)
            {
                if (text.name.ToLower().Contains("score"))
                {
                    text.text = scoreText;
                    Debug.Log($"RootGameManager: Atrasts un atjaunināts rezultātu teksts: {text.name}");
                }
            }
            
            // Mēģināt atrast TextMeshPro komponentes ar rezultātiem saistītiem nosaukumiem
            var allTMPs = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var tmp in allTMPs)
            {
                if (tmp.name.ToLower().Contains("score"))
                {
                    tmp.text = scoreText;
                    Debug.Log($"RootGameManager: Atrasts un atjaunināts TMP rezultātu teksts: {tmp.name}");
                }
            }
        }
        
        /// 
        /// Pārbauda vai norādītā scēna ir spēles scēna.
        /// 
        /// <param name="sceneName">Scēnas nosaukums</param>
        /// <returns>True, ja tā ir spēles scēna</returns>
        private bool IsGameScene(string sceneName)
        {
            return sceneName == "TrainingMode" || 
                   sceneName == "GameScene2v2" || 
                   sceneName == "GameScene4v4";
        }

        /// 
        /// Pārbauda vai visas nepieciešamās komponentes ir piešķirtas un izveidotas.
        /// Ja komponente nav atrasta, tā tiek izveidota.
        /// 
        private void ValidateReferences()
        {
            // Pārbaudīt un izveidot GameUI
            if (gameUI == null)
            {
                Debug.LogError("GameUI nav atrasts scēnā! Veidojam pamata GameUI...");
                CreateBasicGameUI();
            }

            // Pārbaudīt un izveidot QuarterTransitionPanel
            if (quarterTransitionPanel == null)
            {
                Debug.LogWarning("QuarterTransitionPanel nav piešķirts, mēģinām atrast scēnā");
                quarterTransitionPanel = Object.FindFirstObjectByType<QuarterTransitionPanel>();
                
                if (quarterTransitionPanel == null)
                {
                    Debug.LogWarning("QuarterTransitionPanel nav atrasts scēnā - veidojam vienkāršu");
                    CreateBasicQuarterTransitionPanel();
                }
            }

            // Pārbaudīt un izveidot GameTimer
            if (gameTimer == null)
            {
                Debug.LogWarning("GameTimer nav atrasts scēnā");
                gameTimer = Object.FindFirstObjectByType<GameTimer>();
                
                if (gameTimer == null)
                {
                    Debug.LogWarning("GameTimer nav atrasts - veidojam pamata");
                    CreateBasicGameTimer();
                }
            }

            // Pārbaudīt un izveidot GameOverPanel
            if (gameOverPanel == null)
            {
                Debug.LogWarning("GameOverPanel nav atrasts scēnā");
                gameOverPanel = Object.FindFirstObjectByType<GameOverPanel>();
                
                if (gameOverPanel == null)
                {
                    Debug.LogWarning("GameOverPanel nav atrasts - veidojam pamata");
                    CreateBasicGameOverPanel();
                }
            }

            // Pārbaudīt un izveidot PauseMenuManager
            if (pauseMenuManager == null)
            {
                Debug.LogWarning("PauseMenuManager nav atrasts scēnā");
                pauseMenuManager = Object.FindFirstObjectByType<PauseMenuManager>();
                
                if (pauseMenuManager == null)
                {
                    Debug.LogWarning("PauseMenuManager nav atrasts - veidojam pamata");
                    CreateBasicPauseMenuManager();
                }
            }

            Debug.Log("GameManager: Visas atsauces pārbaudītas un izveidotas, ja trūka");
        }

        /// 
        /// Izveido pamata GameUI komponenti, ja tā nav atrasta.
        /// 
        private void CreateBasicGameUI()
        {
            GameObject gameUIObj = new GameObject("GameUI");
            Canvas canvas = gameUIObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            
            gameUIObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            gameUIObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            gameUI = gameUIObj.AddComponent<GameUI>();
            
            Debug.Log("Izveidots pamata GameUI");
        }

        /// 
        /// Izveido pamata QuarterTransitionPanel, ja tas nav atrasts.
        /// 
        private void CreateBasicQuarterTransitionPanel()
        {
            GameObject panelObj = new GameObject("QuarterTransitionPanel");
            quarterTransitionPanel = panelObj.AddComponent<QuarterTransitionPanel>();
            panelObj.SetActive(false);
            
            Debug.Log("Izveidots pamata QuarterTransitionPanel");
        }

        /// 
        /// Izveido pamata GameTimer, ja tas nav atrasts.
        /// 
        private void CreateBasicGameTimer()
        {
            GameObject timerObj = new GameObject("GameTimer");
            gameTimer = timerObj.AddComponent<GameTimer>();
            
            Debug.Log("Izveidots pamata GameTimer");
        }

        /// 
        /// Izveido pamata GameOverPanel, ja tas nav atrasts.
        /// 
        private void CreateBasicGameOverPanel()
        {
            GameObject panelObj = new GameObject("GameOverPanel");
            gameOverPanel = panelObj.AddComponent<GameOverPanel>();
            panelObj.SetActive(false);
            
            Debug.Log("Izveidots pamata GameOverPanel");
        }

        /// 
        /// Izveido pamata PauseMenuManager, ja tas nav atrasts.
        /// 
        private void CreateBasicPauseMenuManager()
        {
            GameObject managerObj = new GameObject("PauseMenuManager");
            pauseMenuManager = managerObj.AddComponent<PauseMenuManager>();
            
            Debug.Log("Izveidots pamata PauseMenuManager");
        }

        /// 
        /// Tiek izsaukta, kad tīkla objekts tiek izveidots.
        /// Inicializē spēles stāvokli un pierakstās uz rezultātu izmaiņu notikumiem.
        /// 
        public override void OnNetworkSpawn()
        {
            // Pārbaudīt vai esam pareizajā scēnā
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu")
            {
                Debug.LogError($"PĀRTRAUKT: GameManager.OnNetworkSpawn galvenajā izvēlnē - izlaižam");
                return;
            }
            
            if (!IsGameScene(currentScene))
            {
                Debug.LogError($"PĀRTRAUKT: GameManager.OnNetworkSpawn nav spēles scēna ({currentScene}) - izlaižam");
                return;
            }

            Debug.Log($"GameManager.OnNetworkSpawn scēnā: {currentScene}");
            
            // Pierakstīties uz rezultātu izmaiņām UI atjaunināšanai
            redScore.OnValueChanged += OnRedScoreChanged;
            blueScore.OnValueChanged += OnBlueScoreChanged;
            
            // Inicializēt spēles stāvokli, ja esam serveris
            if (IsServer)
            {
                InitializeGameState();
            }
            
            ValidateReferences();
        }

        /// 
        /// Tiek izsaukta, kad sarkanās komandas rezultāts mainās.
        /// 
        /// <param name="oldValue">Vecā vērtība</param>
        /// <param name="newValue">Jaunā vērtība</param>
        private void OnRedScoreChanged(int oldValue, int newValue)
        {
            Debug.Log($"RootGameManager: Sarkanās komandas rezultāts mainījās no {oldValue} uz {newValue}");
            UpdateGameUI();
        }

        /// 
        /// Tiek izsaukta, kad zilās komandas rezultāts mainās.
        /// 
        /// <param name="oldValue">Vecā vērtība</param>
        /// <param name="newValue">Jaunā vērtība</param>
        private void OnBlueScoreChanged(int oldValue, int newValue)
        {
            Debug.Log($"RootGameManager: Zilās komandas rezultāts mainījās no {oldValue} uz {newValue}");
            UpdateGameUI();
        }

        /// 
        /// Tiek izsaukta, kad tīkla objekts tiek iznīcināts.
        /// Atrakstās no rezultātu izmaiņu notikumiem.
        /// 
        public override void OnNetworkDespawn()
        {
            // Atrakstīties no rezultātu izmaiņām
            redScore.OnValueChanged -= OnRedScoreChanged;
            blueScore.OnValueChanged -= OnBlueScoreChanged;
            
            base.OnNetworkDespawn();
        }

        /// 
        /// Inicializē spēles stāvokli servera pusē.
        /// 
        private void InitializeGameState()
        {
            Debug.Log("GameManager: Inicializē spēles stāvokli");
            
            // Sākt spēles taimeri
            if (gameTimer != null)
            {
                gameTimer.StartTimer();
            }
        }

        /// 
        /// Iestata spēles pauzes režīmu.
        /// 
        public void PauseGame()
        {
            if (pauseMenuManager != null)
            {
                pauseMenuManager.TogglePause();
            }
        }

        /// Parāda spēles beigu ekrānu ar rezultātiem.
        /// <param name="red">Sarkanās komandas rezultāts</param>
        /// <param name="blue">Zilās komandas rezultāts</param>
        public void ShowGameOver(int red, int blue)
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.ShowGameOver(red, blue);
            }
            else
            {
                Debug.LogError("GameOverPanel nav atrasts!");
            }
        }

        /// Parāda periodu pārejas ekrānu.
        
        public void ShowQuarterTransition(int quarter)
        {
            if (quarterTransitionPanel != null)
            {
                quarterTransitionPanel.ShowQuarterTransition(quarter);
            }
        }

        /// 
        /// Iestatīt abu komandu rezultātus.
    
        /// <param name="redPoints">Sarkanās komandas punkti</param>
        /// <param name="bluePoints">Zilās komandas punkti</param>
        public void UpdateScore(int redPoints, int bluePoints)
        {
            if (IsServer)
            {
                redScore.Value = redPoints;
                blueScore.Value = bluePoints;
                UpdateGameUI();
            }
        }
        
         
        /// Pievienot punktus komandai.
       
        /// <param name="isBlueTeam">Vai punkti jāpievieno zilajai komandai</param>
        /// <param name="points">Pievienojamo punktu skaits</param>
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
        
        // Publiskas īpašības rezultātu piekļuvei
        /// Sarkanās komandas rezultāts.
         
        public int RedScore => redScore.Value;
        /// Zilās komandas rezultāts.
        public int BlueScore => blueScore.Value;
    }
}