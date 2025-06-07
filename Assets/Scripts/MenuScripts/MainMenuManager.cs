using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TMP_Text playerNameDisplay;

    private void Start()
    {
        Debug.Log("MainMenuManager: Start() called");
        
        // CRITICAL: Always reinitialize when MainMenu loads
        StartCoroutine(InitializeMainMenuWithDelay());
    }

    private System.Collections.IEnumerator InitializeMainMenuWithDelay()
    {
        // Wait a frame for scene to fully load
        yield return null;
        
        // CRITICAL: Only initialize if we're actually in the MainMenu scene
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            Debug.LogWarning($"MainMenuManager: Not in MainMenu scene (current: {currentScene}), destroying this instance");
            Destroy(gameObject);
            yield break;
        }
        
        // FIXED: Always validate and find UI references (they may be lost when returning from game)
        bool referencesValid = ValidateUIReferences();
        if (!referencesValid)
        {
            Debug.LogWarning("MainMenuManager: UI references invalid, attempting to find them...");
            FindUIReferences();
            
            // Validate again after finding
            referencesValid = ValidateUIReferences();
            if (!referencesValid)
            {
                Debug.LogError("MainMenuManager: Failed to find UI references after search!");
            }
        }
        
        SetupButtonListeners();
        LoadAndDisplaySettings();
        
        Debug.Log("MainMenuManager: Initialization complete");
    }

    private void OnEnable()
    {
        // Subscribe to scene loaded events to handle returns from game
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Only reinitialize if this is the MainMenu scene
        if (scene.name == "MainMenu" && gameObject != null)
        {
            Debug.Log("MainMenuManager: MainMenu scene loaded, reinitializing...");
            StartCoroutine(InitializeMainMenuWithDelay());
        }
    }

    // ADDED: Method to validate all UI references
    private bool ValidateUIReferences()
    {
        bool allValid = true;
        
        if (playButton == null) { Debug.LogError("MainMenuManager: playButton is null"); allValid = false; }
        if (settingsButton == null) { Debug.LogError("MainMenuManager: settingsButton is null"); allValid = false; }
        if (exitButton == null) { Debug.LogError("MainMenuManager: exitButton is null"); allValid = false; }
        if (settingsPanel == null) { Debug.LogError("MainMenuManager: settingsPanel is null"); allValid = false; }
        if (playerNameDisplay == null) { Debug.LogError("MainMenuManager: playerNameDisplay is null"); allValid = false; }
        
        Debug.Log($"MainMenuManager: UI references validation result: {allValid}");
        return allValid;
    }

    // ADDED: Method to find UI references if they're lost
    private void FindUIReferences()
    {
        Debug.Log("MainMenuManager: Searching for UI references...");
        
        // Try to find buttons by name and tag
        if (playButton == null)
        {
            playButton = FindUIComponent<Button>("PlayButton", "Play Button", "StartButton");
        }
        
        if (settingsButton == null)
        {
            settingsButton = FindUIComponent<Button>("SettingsButton", "Settings Button", "OptionsButton");
        }
        
        if (exitButton == null)
        {
            exitButton = FindUIComponent<Button>("ExitButton", "Exit Button", "QuitButton");
        }
        
        if (settingsPanel == null)
        {
            var foundPanel = FindUIObject("SettingsPanel", "Settings Panel", "OptionsPanel");
            if (foundPanel != null)
            {
                settingsPanel = foundPanel;
                Debug.Log("MainMenuManager: Found SettingsPanel");
            }
        }
        
        if (playerNameDisplay == null)
        {
            playerNameDisplay = FindUIComponent<TMPro.TextMeshProUGUI>("PlayerNameDisplay", "Player Name", "PlayerNameText");
        }
    }

    // Helper method to find UI components with multiple possible names
    private T FindUIComponent<T>(params string[] possibleNames) where T : Component
    {
        foreach (string name in possibleNames)
        {
            var foundObject = GameObject.Find(name);
            if (foundObject != null)
            {
                var component = foundObject.GetComponent<T>();
                if (component != null)
                {
                    Debug.Log($"MainMenuManager: Found {typeof(T).Name}: {name}");
                    return component;
                }
            }
        }
        
        // If not found by name, search by component type
        var allComponents = FindObjectsByType<T>(FindObjectsSortMode.None);
        if (allComponents.Length > 0)
        {
            Debug.Log($"MainMenuManager: Found {typeof(T).Name} by type search: {allComponents[0].name}");
            return allComponents[0];
        }
        
        Debug.LogWarning($"MainMenuManager: Could not find {typeof(T).Name} with any of the names: {string.Join(", ", possibleNames)}");
        return null;
    }

    // Helper method to find GameObjects
    private GameObject FindUIObject(params string[] possibleNames)
    {
        foreach (string name in possibleNames)
        {
            var foundObject = GameObject.Find(name);
            if (foundObject != null)
            {
                Debug.Log($"MainMenuManager: Found GameObject: {name}");
                return foundObject;
            }
        }
        
        Debug.LogWarning($"MainMenuManager: Could not find GameObject with any of the names: {string.Join(", ", possibleNames)}");
        return null;
    }

    private void SetupButtonListeners()
    {
        // Remove any existing listeners first to avoid duplicates
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayButtonClicked);
            Debug.Log("MainMenuManager: Setup play button listener");
        }
        
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            Debug.Log("MainMenuManager: Setup settings button listener");
        }
        
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitButtonClicked);
            Debug.Log("MainMenuManager: Setup exit button listener");
        }
    }

    private void LoadAndDisplaySettings()
    {
        // Ensure SettingsManager exists
        if (SettingsManager.Instance == null)
        {
            var existingSettings = FindObjectOfType<SettingsManager>();
            if (existingSettings != null)
            {
                Debug.Log("MainMenuManager: Found existing SettingsManager");
            }
            else
            {
                GameObject settingsObj = new GameObject("SettingsManager");
                settingsObj.AddComponent<SettingsManager>();
                DontDestroyOnLoad(settingsObj);
                Debug.Log("MainMenuManager: Created new SettingsManager");
            }
        }
        
        // FIXED: Call LoadSettings without parameters (it's now public)
        if (SettingsManager.Instance != null)
        {
            // Load settings - this is now a public method
            if (playerNameDisplay != null)
            {
                playerNameDisplay.text = SettingsManager.Instance.PlayerName;
                Debug.Log($"MainMenuManager: Displayed player name: {SettingsManager.Instance.PlayerName}");
            }
        }
    }

    private void OnPlayButtonClicked()
    {
        Debug.Log("MainMenuManager: Play button clicked");
        // Your existing play button logic here
    }

    private void OnSettingsButtonClicked()
    {
        Debug.Log("MainMenuManager: Settings button clicked");
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }
    }

    private void OnExitButtonClicked()
    {
        Debug.Log("MainMenuManager: Exit button clicked");
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    public void ReturnToMainMenu()
    {
        // Clean up persistent managers to restore inspector values
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.CleanupOnMainMenu();
        }
        // Repeat for other managers if needed (e.g., AudioManager, SettingsManager)
        // AudioManager.Instance?.CleanupOnMainMenu();

        // Load main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void OnDestroy()
    {
        // Clean up event listeners
        if (playButton != null)
            playButton.onClick.RemoveAllListeners();
        if (settingsButton != null)
            settingsButton.onClick.RemoveAllListeners();
        if (exitButton != null)
            exitButton.onClick.RemoveAllListeners();
    }
}
