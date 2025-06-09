using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Unity.Netcode;

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
        private float previousTimeScale;

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
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            if (exitButton != null)
                exitButton.onClick.AddListener(ExitGame);

            // Setup settings panel
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(CloseSettings);

            // --- FORCE POPULATE SETTINGS PANEL UI ON START ---
            // Do NOT set up listeners here, only populate values
            ForcePopulateSettingsPanelUI();
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
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            isPaused = true;

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

        public void ReturnToMainMenu()
        {
            // Find and destroy GameSettingsManager to prevent duplicates when returning to MainMenu
            var gameSettingsManager = FindObjectOfType<GameSettingsManager>();
            if (gameSettingsManager != null)
            {
                Debug.Log("PauseMenuManager: Destroying GameSettingsManager before returning to MainMenu");
                Destroy(gameSettingsManager.gameObject);
            }
            else
            {
                Debug.Log("PauseMenuManager: No GameSettingsManager found to destroy");
            }

            // Shutdown networking if running
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

            // Reset time scale (in case it was paused)
            Time.timeScale = 1f;

            // Load main menu
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        public void ExitGame()
        {
            // Find and destroy GameSettingsManager before exiting
            var gameSettingsManager = FindObjectOfType<GameSettingsManager>();
            if (gameSettingsManager != null)
            {
                Debug.Log("PauseMenuManager: Destroying GameSettingsManager before exiting game");
                Destroy(gameSettingsManager.gameObject);
            }

            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OpenSettings()
        {
            // Always update UI and listeners when opening settings

            // --- SYNC FROM SettingsPanel IF IT EXISTS ---
            var settingsPanelObj = GameObject.FindObjectOfType<SettingsPanel>();
            bool copiedDropdown = false;
            if (settingsPanelObj != null)
            {
                // Copy values from SettingsPanel UI to PauseMenuManager's UI
                if (mouseSensitivitySlider != null && settingsPanelObj.mouseSensitivitySlider != null)
                    mouseSensitivitySlider.value = settingsPanelObj.mouseSensitivitySlider.value;
                if (volumeSlider != null && settingsPanelObj.volumeSlider != null)
                    volumeSlider.value = settingsPanelObj.volumeSlider.value;
                if (fullscreenToggle != null && settingsPanelObj.fullscreenToggle != null)
                    fullscreenToggle.isOn = settingsPanelObj.fullscreenToggle.isOn;
                if (resolutionDropdown != null && settingsPanelObj.resolutionDropdown != null)
                {
                    // --- FIX: Copy options using AddOptions with string list ---
                    resolutionDropdown.ClearOptions();
                    var optionList = new System.Collections.Generic.List<string>();
                    foreach (var opt in settingsPanelObj.resolutionDropdown.options)
                        optionList.Add(opt.text);
                    resolutionDropdown.AddOptions(optionList);
                    resolutionDropdown.value = settingsPanelObj.resolutionDropdown.value;
                    resolutionDropdown.RefreshShownValue();
                    copiedDropdown = true;
                }
            }
            // --- CRITICAL: Always populate dropdown from GameSettingsManager if SettingsPanel is missing or has no options ---
            if (!copiedDropdown && resolutionDropdown != null && GameSettingsManager.Instance != null)
            {
                resolutionDropdown.ClearOptions();
                var options = GameSettingsManager.Instance.GetResolutionOptions();
                if (options != null && options.Length > 0)
                {
                    resolutionDropdown.AddOptions(new System.Collections.Generic.List<string>(options));
                    resolutionDropdown.SetValueWithoutNotify(GameSettingsManager.Instance.CurrentResolutionIndex);
                }
                else
                {
                    // Fallback: add current screen resolution if no options
                    string currentRes = $"{Screen.currentResolution.width} x {Screen.currentResolution.height}";
                    resolutionDropdown.AddOptions(new System.Collections.Generic.List<string> { currentRes });
                    resolutionDropdown.SetValueWithoutNotify(0);
                }
                resolutionDropdown.RefreshShownValue();
            }

            SetupSettingsPanelListenersAndValues();
            // --- CRITICAL: Set up listeners for FULLSCREEN toggle and dropdown here ---
            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.SetIsOnWithoutNotify(GameSettingsManager.Instance != null ? GameSettingsManager.Instance.isFullscreen : Screen.fullScreen);
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            }
            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.RemoveAllListeners();
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            ForcePopulateSettingsPanelUI();

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        // Helper to set up listeners and values for settings panel (call ONLY when opening)
        private void SetupSettingsPanelListenersAndValues()
        {
            if (GameSettingsManager.Instance == null) return;

            // Mouse Sensitivity
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
                mouseSensitivitySlider.SetValueWithoutNotify(GameSettingsManager.Instance.mouseSensitivity);
                mouseSensitivitySlider.onValueChanged.AddListener(GameSettingsManager.Instance.SetMouseSensitivity);
            }
            // Volume
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveAllListeners();
                volumeSlider.SetValueWithoutNotify(GameSettingsManager.Instance.gameVolume);
                volumeSlider.onValueChanged.AddListener(GameSettingsManager.Instance.SetGameVolume);
            }
            // Fullscreen
            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.SetIsOnWithoutNotify(GameSettingsManager.Instance.isFullscreen);
                fullscreenToggle.onValueChanged.AddListener(GameSettingsManager.Instance.SetFullscreen);
            }
            // Resolution Dropdown
            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.RemoveAllListeners();
                var options = GameSettingsManager.Instance.GetResolutionOptions();
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(new System.Collections.Generic.List<string>(options));
                resolutionDropdown.SetValueWithoutNotify(GameSettingsManager.Instance.CurrentResolutionIndex);
                resolutionDropdown.RefreshShownValue();
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
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
            if (GameSettingsManager.Instance != null)
                GameSettingsManager.Instance.SetResolution(index, fullscreenToggle != null ? fullscreenToggle.isOn : false);
        }

        private void OnFullscreenToggled(bool isFullscreen)
        {
            if (GameSettingsManager.Instance != null)
            {
                GameSettingsManager.Instance.isFullscreen = isFullscreen;
                Resolution currentRes = Screen.currentResolution;
                Screen.SetResolution(currentRes.width, currentRes.height, isFullscreen);
                PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
                PlayerPrefs.Save();
            }
            else
            {
                Screen.fullScreen = isFullscreen;
            }
            Debug.Log($"PauseMenuManager: Fullscreen toggled to {isFullscreen} using SetResolution");
        }

        private void EnsureTimeScaleIsNormal()
        {
            if (Time.timeScale != 1f)
            {
                Debug.LogWarning($"PauseMenuManager: Time.timeScale was {Time.timeScale}, resetting to 1");
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// Ensures the settings panel UI is always fully populated, even if the scene was loaded directly.
        /// </summary>
        private void ForcePopulateSettingsPanelUI()
        {
            if (GameSettingsManager.Instance == null) return;

            // Only set values, do not set up listeners here
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.SetValueWithoutNotify(GameSettingsManager.Instance.mouseSensitivity);
            if (volumeSlider != null)
                volumeSlider.SetValueWithoutNotify(GameSettingsManager.Instance.gameVolume);
            if (fullscreenToggle != null)
                fullscreenToggle.SetIsOnWithoutNotify(GameSettingsManager.Instance.isFullscreen);
            if (resolutionDropdown != null)
            {
                var options = GameSettingsManager.Instance.GetResolutionOptions();
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(new System.Collections.Generic.List<string>(options));
                resolutionDropdown.SetValueWithoutNotify(GameSettingsManager.Instance.CurrentResolutionIndex);
                resolutionDropdown.RefreshShownValue();
            }
        }

        private void OnEnable()
        {
            // Ensure settings panel is always populated when this object is enabled (scene load, etc.)
            ForcePopulateSettingsPanelUI();
        }
    }
}
