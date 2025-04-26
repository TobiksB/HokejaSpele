using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using HockeyGame.Game;

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
    [SerializeField] private Button[] backButtons;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private Button trainingButton;
    [SerializeField] private Button mode2v2Button;
    [SerializeField] private Button mode4v4Button;

    [Header("Join Lobby UI")]
    [SerializeField] private TMP_InputField joinLobbyCodeInput;
    [SerializeField] private Button joinLobbyConfirmButton;

    private GameMode selectedGameMode = GameMode.None;

    private GameObject currentActivePanel;

    private void Start()
    {
        // Set up button listeners
        if (playButton) playButton.onClick.AddListener(OpenGamemodePanel);
        if (settingsButton) settingsButton.onClick.AddListener(OpenSettingsPanel);
        if (exitButton) exitButton.onClick.AddListener(ExitGame);

        if (trainingButton) trainingButton.onClick.AddListener(() => SelectGameMode(GameMode.Training));
        if (mode2v2Button) mode2v2Button.onClick.AddListener(() => SelectGameMode(GameMode.Mode2v2));
        if (mode4v4Button) mode4v4Button.onClick.AddListener(() => SelectGameMode(GameMode.Mode4v4));

        foreach (Button backButton in backButtons)
        {
            if (backButton) backButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (createLobbyButton) createLobbyButton.onClick.AddListener(OpenCreateLobby);
        if (joinLobbyButton) joinLobbyButton.onClick.AddListener(OpenJoinLobby);
        if (backToMainMenuButton) backToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (joinLobbyConfirmButton) joinLobbyConfirmButton.onClick.AddListener(JoinLobby);

        ShowPanel(mainMenuPanel);
    }

    private void ShowPanel(GameObject panel)
    {
        if (currentActivePanel != null)
        {
            currentActivePanel.SetActive(false);
        }

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

    private void SelectGameMode(GameMode mode)
    {
        selectedGameMode = mode;
        Debug.Log($"Selected game mode: {mode}");

        if (mode == GameMode.Training)
        {
            StartTrainingMode();
        }
        else
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.SetGameMode(mode);
            }
            ShowPanel(createOrJoinPanel);
        }
    }

    private void StartTrainingMode()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("TrainingScene");
    }

    private async void OpenCreateLobby()
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("LobbyManager.Instance is null. Ensure the LobbyManager script is in the scene.");
            return;
        }

        if (selectedGameMode == GameMode.None)
        {
            Debug.LogError("selectedGameMode is not set. Ensure a game mode is selected before creating a lobby.");
            return;
        }

        try
        {
            int maxPlayers = selectedGameMode == GameMode.Mode4v4 ? 8 : 4;
            string lobbyCode = await LobbyManager.Instance.CreateLobby(maxPlayers);

            if (!string.IsNullOrEmpty(lobbyCode))
            {
                ShowLobbyPanel(lobbyCode);
            }
            else
            {
                Debug.LogError("Failed to create lobby.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"An error occurred while creating the lobby: {e.Message}");
        }
    }

    private void OpenJoinLobby()
    {
        ShowPanel(createOrJoinPanel);
    }

    private async void JoinLobby()
    {
        if (LobbyManager.Instance == null || string.IsNullOrWhiteSpace(joinLobbyCodeInput.text))
        {
            Debug.LogError("LobbyManager.Instance is null or joinLobbyCodeInput is empty.");
            return;
        }

        string lobbyCode = joinLobbyCodeInput.text.Trim();
        await LobbyManager.Instance.JoinLobby(lobbyCode);

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
}