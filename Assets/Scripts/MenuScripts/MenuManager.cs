using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System.Collections;
using HockeyGame.Game;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

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

    [Header("Debug")]
    [SerializeField] private Button debugConsoleButton;
    [SerializeField] private DebugConsole debugConsole;
    [SerializeField] private BuildLogger buildLogger;

    private GameMode selectedGameMode = GameMode.None;

    private GameObject currentActivePanel;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // REMOVE THIS LINE
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // CRITICAL: Verify we're in MainMenu scene and destroy any cameras
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            Debug.LogWarning($"MenuManager: Not in MainMenu scene ({currentScene}), destroying");
            Destroy(gameObject);
            return;
        }
        
        // CRITICAL: Disable any game-related cameras that shouldn't be in MainMenu
        Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            if (cam.name.Contains("Player") || cam.name.Contains("Game") || cam.name.Contains("Local"))
            {
                Debug.LogError($"DESTROYING game camera {cam.name} found in MainMenu scene!");
                Destroy(cam.gameObject);
            }
        }
        
        // Initialize file logger first for debugging
        FileLogger.LogToFile("=== HOCKEY GAME STARTED IN MAINMENU ===");
        FileLogger.LogToFile("MenuManager: Initializing game");
        
        // Set up initial panels and ensure they exist in scene
        foreach (var panel in new[] { mainMenuPanel, gamemodePanel, settingsPanel, lobbyPanel, createOrJoinPanel })
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        SetupButtonListeners();

        // Add debug console setup
        if (debugConsoleButton) debugConsoleButton.onClick.AddListener(ToggleDebugConsole);
        
        // Show debug button in builds, hide in editor
        #if !UNITY_EDITOR
        if (debugConsoleButton) debugConsoleButton.gameObject.SetActive(true);
        #else
        if (debugConsoleButton) debugConsoleButton.gameObject.SetActive(false);
        #endif

        // Initialize logger in builds
        #if !UNITY_EDITOR
        if (buildLogger == null)
        {
            var loggerGO = new GameObject("BuildLogger");
            buildLogger = loggerGO.AddComponent<BuildLogger>();
            DontDestroyOnLoad(loggerGO);
        }
        #endif

        ShowPanel(mainMenuPanel);
        
        FileLogger.LogToFile("MenuManager: Initialization complete");
    }

    private void SetupButtonListeners()
    {
        // Remove all listeners before adding to prevent stacking/clicking
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OpenGamemodePanel);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettingsPanel);
        }
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(ExitGame);
        }

        if (trainingButton != null)
        {
            trainingButton.onClick.RemoveAllListeners();
            trainingButton.onClick.AddListener(() => SelectGameMode(GameMode.Training));
        }
        if (mode2v2Button != null)
        {
            mode2v2Button.onClick.RemoveAllListeners();
            mode2v2Button.onClick.AddListener(() => SelectGameMode(GameMode.Mode2v2));
        }
        if (mode4v4Button != null)
        {
            mode4v4Button.onClick.RemoveAllListeners();
            mode4v4Button.onClick.AddListener(() => SelectGameMode(GameMode.Mode4v4));
        }

        foreach (Button backButton in backButtons)
        {
            if (backButton) backButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (createLobbyButton != null)
        {
            createLobbyButton.onClick.RemoveAllListeners();
            createLobbyButton.onClick.AddListener(OpenCreateLobby);
        }
        if (joinLobbyButton != null)
        {
            joinLobbyButton.onClick.RemoveAllListeners();
            joinLobbyButton.onClick.AddListener(OpenJoinLobby);
        }
        if (backToMainMenuButton != null)
        {
            backToMainMenuButton.onClick.RemoveAllListeners();
            backToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
        if (joinLobbyConfirmButton != null)
        {
            joinLobbyConfirmButton.onClick.RemoveAllListeners();
            joinLobbyConfirmButton.onClick.AddListener(JoinLobby);
        }
    }

    private void ShowPanel(GameObject panel)
    {
        if (panel == null) return;

        if (currentActivePanel != null && currentActivePanel != panel)
        {
            currentActivePanel.SetActive(false);
        }

        panel.SetActive(true);
        currentActivePanel = panel;
        Debug.Log($"Showing panel: {panel.name}, Active: {panel.activeInHierarchy}");

        // If this is the lobby panel, ensure it's properly initialized
        if (panel == lobbyPanel)
        {
            // Use only the UI.LobbyPanelManager
            var lobbyPanelManagerUI = panel.GetComponent<HockeyGame.UI.LobbyPanelManager>();
            
            if (lobbyPanelManagerUI != null)
            {
                Debug.Log("Found UI.LobbyPanelManager, initializing...");
                lobbyPanelManagerUI.ForceInitialize();
            }
            else
            {
                Debug.LogError("No UI.LobbyPanelManager component found on lobby panel!");
                Debug.LogError("Please add the HockeyGame.UI.LobbyPanelManager component to your lobby panel GameObject");
            }
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
        Debug.Log("Loading TrainingMode scene directly");
        UnityEngine.SceneManagement.SceneManager.LoadScene("TrainingMode");
    }

    private async void OpenCreateLobby()
    {
        try
        {
            FileLogger.LogToFile("=== CREATING 2v2 LOBBY ===");
            Debug.Log("Starting 2v2 lobby creation process...");
            
            // First wait for services initialization
            if (!Unity.Services.Core.UnityServices.State.Equals(Unity.Services.Core.ServicesInitializationState.Initialized))
            {
                FileLogger.LogToFile("Initializing Unity Services...");
                Debug.Log("Initializing Unity Services...");
                await Unity.Services.Core.UnityServices.InitializeAsync();
            }

            // Wait for authentication
            if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            {
                FileLogger.LogToFile("Signing in anonymously...");
                Debug.Log("Signing in anonymously...");
                await Unity.Services.Authentication.AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // CRITICAL: Show panel FIRST and ensure it's fully initialized
            FileLogger.LogToFile("Showing lobby panel...");
            ShowPanel(lobbyPanel);
            await Task.Delay(200); // Longer wait for panel activation

            // Use only the UI.LobbyPanelManager
            var lobbyPanelManagerUI = lobbyPanel.GetComponent<HockeyGame.UI.LobbyPanelManager>();

            if (lobbyPanelManagerUI != null)
            {
                Debug.Log("Using UI.LobbyPanelManager for 2v2 lobby");
                lobbyPanelManagerUI.ForceInitialize();
                await Task.Delay(200); // Wait for full initialization

                // CRITICAL: Now create the lobby AFTER UI is ready
                Debug.Log("UI ready, now creating lobby...");
                
                string lobbyCode = null;
                int retryCount = 0;
                while (string.IsNullOrEmpty(lobbyCode) && retryCount < 3)
                {
                    int maxPlayers = 4; // Always 4 for 2v2
                    Debug.Log($"Creating 2v2 lobby attempt {retryCount + 1} for {maxPlayers} players...");
                    lobbyCode = await LobbyManager.Instance.CreateLobby(maxPlayers);
                    retryCount++;
                    if (string.IsNullOrEmpty(lobbyCode))
                    {
                        await Task.Delay(500);
                    }
                }

                if (!string.IsNullOrEmpty(lobbyCode))
                {
                    lobbyPanelManagerUI.SetLobbyCode(lobbyCode);
                    Debug.Log($"Successfully created 2v2 lobby with code: {lobbyCode}");
                    
                    // FINAL: Force one more UI update after everything is set up
                    await Task.Delay(100);
                    if (LobbyManager.Instance != null)
                    {
                        LobbyManager.Instance.RefreshPlayerList();
                    }
                }
                else
                {
                    throw new System.Exception("Failed to create 2v2 lobby after multiple attempts");
                }
            }
            else
            {
                throw new System.Exception("UI.LobbyPanelManager component not found! Please add it to your lobby panel.");
            }
        }
        catch (System.Exception e)
        {
            FileLogger.LogError($"Error in OpenCreateLobby: {e.Message}");
            FileLogger.LogError($"Stack trace: {e.StackTrace}");
            Debug.LogError($"Error in OpenCreateLobby for 2v2: {e.Message}");
            ShowPanel(mainMenuPanel);
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
            Debug.LogError("CLIENT: LobbyManager.Instance is null or joinLobbyCodeInput is empty.");
            return;
        }

        string lobbyCode = joinLobbyCodeInput.text.Trim();
        
        try
        {
            Debug.Log($"CLIENT: ============ STARTING LOBBY JOIN PROCESS ============");
            
            // DISABLE the join button to prevent multiple clicks
            if (joinLobbyConfirmButton) joinLobbyConfirmButton.interactable = false;
            
            // CRITICAL: First verify lobby panel exists
            if (lobbyPanel == null)
            {
                Debug.LogError("CLIENT: ✗ LOBBY PANEL REFERENCE IS NULL!");
                Debug.LogError("CLIENT: Check MenuManager inspector - lobbyPanel field must be assigned!");
                if (joinLobbyConfirmButton) joinLobbyConfirmButton.interactable = true;
                return;
            }

            // Step 1: Join the lobby backend (but don't let relay failure stop us)
            Debug.Log("CLIENT: Step 1 - Joining lobby backend...");
            try
            {
                await LobbyManager.Instance.JoinLobby(lobbyCode);
                Debug.Log("CLIENT: ✓ Successfully joined lobby backend");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CLIENT: ⚠ Lobby join had issues but continuing: {e.Message}");
                // Continue anyway - the lobby join might have partially succeeded
            }
            
            // Step 2: IMMEDIATELY show lobby panel regardless of relay status
            Debug.Log("CLIENT: Step 2 - IMMEDIATE lobby panel transition...");
            Debug.Log($"CLIENT: Before transition - Current: {(currentActivePanel ? currentActivePanel.name : "null")}, Target: {lobbyPanel.name}");
            
            // Force deactivate current panel
            if (currentActivePanel != null && currentActivePanel != lobbyPanel)
            {
                Debug.Log($"CLIENT: Deactivating: {currentActivePanel.name}");
                currentActivePanel.SetActive(false);
            }
            
            // Force activate lobby panel
            Debug.Log($"CLIENT: Activating: {lobbyPanel.name}");
            lobbyPanel.SetActive(true);
            currentActivePanel = lobbyPanel;
            
            // CRITICAL: Force immediate Canvas update
            Canvas.ForceUpdateCanvases();
            
            // Verify transition worked
            Debug.Log($"CLIENT: After transition - Current: {(currentActivePanel ? currentActivePanel.name : "null")}, Active: {lobbyPanel.activeInHierarchy}");
            
            if (!lobbyPanel.activeInHierarchy)
            {
                Debug.LogError("CLIENT: ✗ PANEL TRANSITION FAILED! Trying fallback...");
                
                // Fallback: Brute force all panels
                foreach (var panel in new[] { mainMenuPanel, gamemodePanel, settingsPanel, createOrJoinPanel })
                {
                    if (panel != null) panel.SetActive(false);
                }
                lobbyPanel.SetActive(true);
                currentActivePanel = lobbyPanel;
                Canvas.ForceUpdateCanvases();
                
                Debug.Log($"CLIENT: Fallback result - Active: {lobbyPanel.activeInHierarchy}");
            }
            
            // Step 3: Initialize managers while panel is showing
            await Task.Delay(100); // Brief pause for activation
            
            Debug.Log("CLIENT: Step 3 - Initializing managers...");
            var lobbyPanelManagerUI = lobbyPanel.GetComponent<HockeyGame.UI.LobbyPanelManager>();
            
            Debug.Log($"CLIENT: Found UI manager: {lobbyPanelManagerUI != null}");
            
            bool managerInitialized = false;
            
            if (lobbyPanelManagerUI != null)
            {
                Debug.Log("CLIENT: ✓ Initializing UI.LobbyPanelManager");
                try
                {
                    lobbyPanelManagerUI.ForceInitialize();
                    await Task.Delay(50);
                    lobbyPanelManagerUI.SetLobbyCode(lobbyCode);
                    managerInitialized = true;
                    Debug.Log("CLIENT: ✓ UI manager initialized successfully");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"CLIENT: ✗ UI manager initialization failed: {e.Message}");
                }
            }
            else
            {
                Debug.LogError("CLIENT: ✗ No UI.LobbyPanelManager found!");
                Debug.LogError("CLIENT: Please add HockeyGame.UI.LobbyPanelManager component to your lobby panel");
            }
            
            // Step 4: Force multiple UI refreshs
            if (managerInitialized)
            {
                Debug.Log("CLIENT: Step 4 - Multiple UI refresh attempts...");
                for (int i = 0; i < 3; i++)
                {
                    LobbyManager.Instance.RefreshPlayerList();
                    await Task.Delay(100);
                    Debug.Log($"CLIENT: UI refresh attempt {i + 1}/3");
                }
                
                Debug.Log("CLIENT: ============ LOBBY JOIN COMPLETED SUCCESSFULLY ============");
            }
            else
            {
                Debug.LogError("CLIENT: ✗ No managers could be initialized!");
                
                // List all components on lobby panel for debugging
                var components = lobbyPanel.GetComponents<Component>();
                Debug.Log($"CLIENT: Components on lobby panel ({components.Length}):");
                foreach (var comp in components)
                {
                    Debug.Log($"  - {comp.GetType().Name}");
                }
            }
            
            // Final verification
            Debug.Log($"CLIENT: Final state - Panel: {lobbyPanel.name}, Active: {lobbyPanel.activeInHierarchy}, Current: {(currentActivePanel ? currentActivePanel.name : "null")}");
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CLIENT: ✗ Failed to join lobby: {e.Message}");
            Debug.LogError($"CLIENT: Stack trace: {e.StackTrace}");
            
            // Even on failure, try to show the lobby panel anyway
            Debug.Log("CLIENT: Emergency fallback - forcing lobby panel show...");
            ShowPanel(lobbyPanel);
            
            // Re-enable the button on failure
            if (joinLobbyConfirmButton) joinLobbyConfirmButton.interactable = true;
        }
    }

    private void ToggleDebugConsole()
    {
        if (debugConsole != null)
        {
            debugConsole.ToggleConsole();
        }
        else
        {
            // Create debug console if it doesn't exist
            var consoleGO = new GameObject("DebugConsole");
            debugConsole = consoleGO.AddComponent<DebugConsole>();
            DontDestroyOnLoad(consoleGO);
            debugConsole.ToggleConsole();
        }
    }

    // Add a public method to force panel switch for debugging
    public void ForceShowLobbyPanel()
    {
        Debug.Log("FORCED: Showing lobby panel");
        ShowPanel(lobbyPanel);
    }

    private void DisableAllPlayerCameras()
    {
        // FIXED: Remove PlayerCamera references and use direct Camera component access
        var allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var camera in allCameras)
        {
            // Only disable cameras that are not the main menu camera
            if (camera.gameObject.name.Contains("Player") || 
                camera.GetComponent<CameraFollow>() != null)
            {
                camera.gameObject.SetActive(false);
                Debug.Log($"MenuManager: Disabled player camera: {camera.gameObject.name}");
            }
        }
    }

    // Call this when returning to main menu to clean up
    public void CleanupOnMainMenu()
    {
        // Optionally reset static instance if you want to reload inspector values
        Instance = null;
        Destroy(gameObject);
    }
}