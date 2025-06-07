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
        [SerializeField] private Button exitButton;

        [Header("Settings Panel")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Button settingsBackButton;

        private bool isPaused = false;

        private void Awake()
        {
            if (pausePanel == null)
            {
                CreateBasicPauseMenu();
            }
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        private void Start()
        {
            EnsureTimeScaleIsNormal();

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            // Setup pause panel buttons
            if (resumeButton != null)
                resumeButton.onClick.AddListener(ResumeGame);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(GoToMainMenu);

            if (exitButton != null)
                exitButton.onClick.AddListener(ExitGame);

            // Setup settings panel
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(CloseSettings);

            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.value = GameSettingsManager.Instance.mouseSensitivity;
                mouseSensitivitySlider.onValueChanged.AddListener(GameSettingsManager.Instance.SetMouseSensitivity);
            }
            if (volumeSlider != null)
            {
                volumeSlider.value = GameSettingsManager.Instance.gameVolume;
                volumeSlider.onValueChanged.AddListener(GameSettingsManager.Instance.SetGameVolume);
            }
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = GameSettingsManager.Instance.isFullscreen;
                fullscreenToggle.onValueChanged.AddListener(GameSettingsManager.Instance.SetFullscreen);
            }
            if (resolutionDropdown != null)
            {
                var options = GameSettingsManager.Instance.GetResolutionOptions();
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(new System.Collections.Generic.List<string>(options));
                resolutionDropdown.value = GameSettingsManager.Instance.CurrentResolutionIndex;
                resolutionDropdown.RefreshShownValue();
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }
        }

        private void Update()
        {
            // Open/close pause menu with Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    CloseSettings();
                }
                else
                {
                    TogglePause();
                }
            }
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

        public void ResumeGame()
        {
            if (!isPaused) return;

            isPaused = false;
            Time.timeScale = 1f;

            if (pausePanel != null)
                pausePanel.SetActive(false);

            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            Debug.Log("PauseMenuManager: Game resumed");
        }

        public void PauseGame()
        {
            if (isPaused) return;

            isPaused = true;
            Time.timeScale = 0f;

            if (pausePanel != null)
                pausePanel.SetActive(true);

            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            Debug.Log("PauseMenuManager: Game paused");
        }

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
            // Shut down all networking
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                try
                {
                    if (Unity.Netcode.NetworkManager.Singleton.IsHost || Unity.Netcode.NetworkManager.Singleton.IsServer || Unity.Netcode.NetworkManager.Singleton.IsClient)
                    {
                        Unity.Netcode.NetworkManager.Singleton.Shutdown();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"PauseMenuManager: Error shutting down NetworkManager: {e.Message}");
                }
            }

            // Reset lobby player list and chat
            if (LobbyManager.Instance != null)
            {
                try
                {
                    LobbyManager.Instance.StopAllCoroutines();
                    // Use reflection to clear private dictionaries and chat
                    var lobbyType = typeof(LobbyManager);
                    var playerNamesField = lobbyType.GetField("playerNames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var playerTeamsField = lobbyType.GetField("playerTeams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var playerReadyStatesField = lobbyType.GetField("playerReadyStates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var chatMessagesField = lobbyType.GetField("chatMessages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var currentLobbyField = lobbyType.GetField("currentLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (playerNamesField != null)
                    {
                        var dict = playerNamesField.GetValue(LobbyManager.Instance) as System.Collections.IDictionary;
                        dict?.Clear();
                    }
                    if (playerTeamsField != null)
                    {
                        var dict = playerTeamsField.GetValue(LobbyManager.Instance) as System.Collections.IDictionary;
                        dict?.Clear();
                    }
                    if (playerReadyStatesField != null)
                    {
                        var dict = playerReadyStatesField.GetValue(LobbyManager.Instance) as System.Collections.IDictionary;
                        dict?.Clear();
                    }
                    if (chatMessagesField != null)
                    {
                        var chatList = chatMessagesField.GetValue(LobbyManager.Instance) as System.Collections.IList;
                        chatList?.Clear();
                    }
                    if (currentLobbyField != null)
                    {
                        currentLobbyField.SetValue(LobbyManager.Instance, null);
                    }
                    // ADD: Reset all internal state to allow new host/client sessions
                    LobbyManager.Instance.ResetLobbyState();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"PauseMenuManager: Error resetting lobby player list or chat: {e.Message}");
                }
            }

            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        public void ExitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OpenSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        private void CloseSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
            }
        }

        private void OnResolutionChanged(int index)
        {
            GameSettingsManager.Instance.SetResolution(index, GameSettingsManager.Instance.isFullscreen);
        }

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
