using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;

public class MenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gamemodePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject createOrJoinPanel;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button[] backButtons; // One for each sub-panel
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private Button backToMainMenuButton;

    [Header("Settings UI")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Button applySettingsButton;
    [SerializeField] private Button defaultSettingsButton;
    [SerializeField] private Button settingsBackButton;

    [Header("Gamemode Buttons")]
    [SerializeField] private Button trainingButton;
    [SerializeField] private Button mode2v2Button;
    [SerializeField] private Button mode4v4Button;
    [SerializeField] private Button startGameButton;

    [Header("Lobby UI")]
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Button copyLobbyCodeButton;
    [SerializeField] private Button startMatchButton;
    [SerializeField] private TMP_Text playerCountText;

    [Header("Join Lobby UI")]
    [SerializeField] private TMP_InputField joinLobbyCodeInput;
    [SerializeField] private Button joinLobbyConfirmButton;

    private GameMode selectedGameMode = GameMode.None;

    private enum GameMode
    {
        None,
        Training,
        Mode2v2,
        Mode4v4
    }

    private GameObject currentActivePanel;

    private void Start()
    {
        // Set up button listeners
        if (playButton) playButton.onClick.AddListener(OpenGamemodePanel);
        if (settingsButton) settingsButton.onClick.AddListener(OpenSettingsPanel);
        if (exitButton) exitButton.onClick.AddListener(ExitGame);

        // Set up back buttons
        foreach (Button backButton in backButtons)
        {
            if (backButton) backButton.onClick.AddListener(ReturnToMainMenu);
        }

        // Show main menu at start
        ShowPanel(mainMenuPanel);

        // Initialize settings UI
        if (applySettingsButton) applySettingsButton.onClick.AddListener(() => {
            ApplySettings();
            ShowNotification("Settings Applied!");
        });
        
        if (defaultSettingsButton) defaultSettingsButton.onClick.AddListener(() => {
            ResetToDefaultSettings();
            ShowNotification("Settings Reset to Default!");
        });

        if (settingsBackButton) settingsBackButton.onClick.AddListener(() => {
            if (HasUnsavedChanges())
            {
                ShowConfirmationDialog("You have unsaved changes. Discard changes?", () => {
                    RevertSettings();
                    ReturnToMainMenu();
                });
            }
            else
            {
                ReturnToMainMenu();
            }
        });

        // Setup gamemode buttons
        if (trainingButton) trainingButton.onClick.AddListener(() => SelectGameMode(GameMode.Training));
        if (mode2v2Button) mode2v2Button.onClick.AddListener(() => SelectGameMode(GameMode.Mode2v2));
        if (mode4v4Button) mode4v4Button.onClick.AddListener(() => SelectGameMode(GameMode.Mode4v4));
        if (startGameButton) startGameButton.onClick.AddListener(StartSelectedMode);
        
        // Setup lobby buttons
        if (copyLobbyCodeButton) copyLobbyCodeButton.onClick.AddListener(CopyLobbyCode);
        if (startMatchButton) startMatchButton.onClick.AddListener(StartMatch);

        // Setup create/join lobby buttons
        if (createLobbyButton) createLobbyButton.onClick.AddListener(OpenCreateLobby);
        if (joinLobbyButton) joinLobbyButton.onClick.AddListener(OpenJoinLobby);
        if (backToMainMenuButton) backToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (joinLobbyConfirmButton) joinLobbyConfirmButton.onClick.AddListener(JoinLobby);

        // Disable start button initially
        if (startGameButton) startGameButton.interactable = false;

        InitializeSettingsUI();
    }

    private void ShowPanel(GameObject panel)
    {
        // Disable current panel if exists
        if (currentActivePanel != null)
            currentActivePanel.SetActive(false);

        // Enable new panel
        if (panel != null)
        {
            panel.SetActive(true);
            currentActivePanel = panel;
        }
    }

    public void OpenGamemodePanel()
    {
        ShowPanel(gamemodePanel);
    }

    public void OpenSettingsPanel()
    {
        ShowPanel(settingsPanel);
    }

    public void ReturnToMainMenu()
    {
        ShowPanel(mainMenuPanel);
    }

    public void ExitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void InitializeSettingsUI()
    {
        if (mouseSensitivitySlider)
        {
            mouseSensitivitySlider.value = SettingsManager.Instance.MouseSensitivity;
        }
        if (volumeSlider)
        {
            volumeSlider.value = SettingsManager.Instance.Volume;
        }
        if (fullscreenToggle)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
        }
        if (resolutionDropdown)
        {
            SetupResolutionDropdown();
        }
    }

    private void SetupResolutionDropdown()
    {
        Resolution[] resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width}x{resolutions[i].height}";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void ApplySettings()
    {
        if (mouseSensitivitySlider)
        {
            SettingsManager.Instance.MouseSensitivity = mouseSensitivitySlider.value;
        }
        if (volumeSlider)
        {
            SettingsManager.Instance.Volume = volumeSlider.value;
        }
        if (fullscreenToggle)
        {
            Screen.fullScreen = fullscreenToggle.isOn;
        }
        if (resolutionDropdown)
        {
            Resolution resolution = Screen.resolutions[resolutionDropdown.value];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        }

        SettingsManager.Instance.SaveSettings();
    }

    private void ResetToDefaultSettings()
    {
        SettingsManager.Instance.ResetToDefaults();
        InitializeSettingsUI(); // Refresh UI with default values
    }

    private void RevertSettings()
    {
        InitializeSettingsUI(); // Reload current saved settings
    }

    private bool HasUnsavedChanges()
    {
        return mouseSensitivitySlider.value != SettingsManager.Instance.MouseSensitivity ||
               volumeSlider.value != SettingsManager.Instance.Volume ||
               fullscreenToggle.isOn != Screen.fullScreen;
    }

    private void ShowNotification(string message)
    {
        Debug.Log(message); // Replace with your UI notification system
    }

    private void ShowConfirmationDialog(string message, System.Action onConfirm)
    {
        // For now, just confirm automatically
        onConfirm?.Invoke();
        
        // TODO: Replace with your actual dialog UI system
        Debug.Log($"Confirmation Dialog: {message}");
    }

    private void SelectGameMode(GameMode mode)
    {
        selectedGameMode = mode;

        if (mode == GameMode.Training)
        {
            StartTrainingMode();
        }
        else
        {
            ShowPanel(createOrJoinPanel); // Show the "Create or Join" panel for multiplayer modes
        }
    }

    private void StartSelectedMode()
    {
        switch (selectedGameMode)
        {
            case GameMode.Training:
                StartTrainingMode();
                break;
            case GameMode.Mode2v2:
            case GameMode.Mode4v4:
                ShowPanel(createOrJoinPanel);
                break;
            default:
                Debug.LogWarning("No game mode selected!");
                break;
        }
    }

    private void OpenCreateLobby()
    {
        CreateLobby();
    }

    private async void CreateLobby()
    {
        int maxPlayers = selectedGameMode == GameMode.Mode4v4 ? 8 : 4;
        string lobbyCode = await LobbyManager.Instance.CreateLobby(maxPlayers);

        if (!string.IsNullOrEmpty(lobbyCode))
        {
            ShowLobbyPanel(lobbyCode);
        }
        else
        {
            ShowNotification("Failed to create lobby!");
        }
    }

    private void OpenJoinLobby()
    {
        ShowPanel(createOrJoinPanel);
    }

    private async void JoinLobby()
    {
        if (LobbyManager.Instance == null || string.IsNullOrWhiteSpace(joinLobbyCodeInput.text)) return;

        string lobbyCode = joinLobbyCodeInput.text.Trim();
        await LobbyManager.Instance.JoinLobby(lobbyCode);

        // Assuming the lobby join was successful
        ShowLobbyPanel(lobbyCode);
    }

    private void ShowLobbyPanel(string lobbyCode)
    {
        ShowPanel(lobbyPanel);
        LobbyPanelManager lobbyPanelManager = lobbyPanel.GetComponent<LobbyPanelManager>();
        if (lobbyPanelManager != null)
        {
            lobbyPanelManager.SetLobbyCode(lobbyCode);
        }
    }

    private void CopyLobbyCode()
    {
        if (lobbyCodeText != null)
        {
            string code = lobbyCodeText.text.Replace("Lobby Code: ", "");
            GUIUtility.systemCopyBuffer = code;
            ShowNotification("Lobby code copied to clipboard!");
        }
    }

    private void StartMatch()
    {
        LobbyManager.Instance?.StartMatch();
    }

    private void StartTrainingMode()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("TrainingScene");
    }

    public void On2v2ButtonPressed()
    {
        SelectGameMode(GameMode.Mode2v2);
    }

    public void On4v4ButtonPressed()
    {
        SelectGameMode(GameMode.Mode4v4);
    }
}
