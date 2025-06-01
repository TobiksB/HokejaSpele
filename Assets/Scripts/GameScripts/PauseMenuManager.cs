using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HockeyGame.Game
{
    public class PauseMenuManager : MonoBehaviour
    {
        [Header("Pause Menu")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        
        private bool isPaused = false;
        
        private void Awake()
        {
            if (pausePanel == null)
            {
                CreateBasicPauseMenu();
            }
        }
        
        private void Start()
        {
            EnsureTimeScaleIsNormal();
            
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        private void Update()
        {
            // Check for pause button press
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
            
            // Regularly check time scale in editor
            #if UNITY_EDITOR
            if (!isPaused && Time.timeScale != 1f)
            {
                EnsureTimeScaleIsNormal();
            }
            #endif
        }
        
        private void CreateBasicPauseMenu()
        {
            // Create basic pause menu
            pausePanel = new GameObject("PausePanel");
            pausePanel.transform.SetParent(transform);
            
            Canvas canvas = pausePanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            
            Image background = pausePanel.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.7f);
            
            pausePanel.SetActive(false);
            
            Debug.Log("Created basic pause menu");
        }
        
        // FIXED: Add missing ResumeGame method
        public void ResumeGame()
        {
            if (!isPaused) return;

            isPaused = false;
            Time.timeScale = 1f;
            
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
            
            Debug.Log("PauseMenuManager: Game resumed");
        }

        // FIXED: Add missing PauseGame method
        public void PauseGame()
        {
            if (isPaused) return;

            isPaused = true;
            Time.timeScale = 0f;
            
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
            }
            
            Debug.Log("PauseMenuManager: Game paused");
        }

        // FIXED: Add missing TogglePause method for InputManager
        public void TogglePause()
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
            
            Debug.Log($"PauseMenuManager: Toggle pause - isPaused: {isPaused}");
        }
        
        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // FIXED: Add missing EnsureTimeScaleIsNormal method
        private void EnsureTimeScaleIsNormal()
        {
            if (Time.timeScale != 1f)
            {
                Debug.LogWarning($"PauseMenuManager: Time.timeScale was {Time.timeScale}, resetting to 1");
                Time.timeScale = 1f;
            }
        }
    }
}
