using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI atsauces")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TMP_Text playerNameDisplay;

    private void Start()
    {
        Debug.Log("MainMenuManager: Start() izsaukta");
        
        //  Vienmēr reinicializēt, kad ielādējas MainMenu
        StartCoroutine(InitializeMainMenuWithDelay());
    }

    private System.Collections.IEnumerator InitializeMainMenuWithDelay()
    {
        // Uzgaidam kadru, lai aina pilnībā ielādētos
        yield return null;
        
        //  Inicializēt tikai tad, ja mēs patiešām esam MainMenu ainā
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            Debug.LogWarning($"MainMenuManager: Nav MainMenu aina (pašreizējā: {currentScene}), iznīcinām šo instanci");
            Destroy(gameObject);
            yield break;
        }
        
        //  Vienmēr validēt un atrast UI atsauces (tās var pazust, atgriežoties no spēles)
        bool referencesValid = ValidateUIReferences();
        if (!referencesValid)
        {
            Debug.LogWarning("MainMenuManager: UI atsauces nav derīgas, mēģinam tās atrast...");
            FindUIReferences();
            
            // Validējam vēlreiz pēc meklēšanas
            referencesValid = ValidateUIReferences();
            if (!referencesValid)
            {
                Debug.LogError("MainMenuManager: Neizdevās atrast UI atsauces pēc meklēšanas!");
            }
        }
        
        SetupButtonListeners();
        LoadAndDisplaySettings();
        
        Debug.Log("MainMenuManager: Inicializācija pabeigta");
    }

    private void OnEnable()
    {
        // Pierakstāmies uz ainas ielādes notikumiem, lai apstrādātu atgriešanos no spēles
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Reinicializēt tikai tad, ja šī ir MainMenu aina
        if (scene.name == "MainMenu" && gameObject != null)
        {
            Debug.Log("MainMenuManager: MainMenu aina ielādēta, reinicializējam...");
            StartCoroutine(InitializeMainMenuWithDelay());
        }
    }

    //  Metode, lai validētu visas UI atsauces
    private bool ValidateUIReferences()
    {
        bool allValid = true;
        
        if (playButton == null) { Debug.LogError("MainMenuManager: playButton ir null"); allValid = false; }
        if (settingsButton == null) { Debug.LogError("MainMenuManager: settingsButton ir null"); allValid = false; }
        if (exitButton == null) { Debug.LogError("MainMenuManager: exitButton ir null"); allValid = false; }
        if (settingsPanel == null) { Debug.LogError("MainMenuManager: settingsPanel ir null"); allValid = false; }
        if (playerNameDisplay == null) { Debug.LogError("MainMenuManager: playerNameDisplay ir null"); allValid = false; }
        
        Debug.Log($"MainMenuManager: UI atsauču validācijas rezultāts: {allValid}");
        return allValid;
    }

    //  Metode, lai atrastu UI atsauces, ja tās ir pazudušas
    private void FindUIReferences()
    {
        Debug.Log("MainMenuManager: Meklējam UI atsauces...");
        
        // Mēģinām atrast pogas pēc nosaukuma un taga
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
                Debug.Log("MainMenuManager: Atrasts SettingsPanel");
            }
        }
        
        if (playerNameDisplay == null)
        {
            playerNameDisplay = FindUIComponent<TMPro.TextMeshProUGUI>("PlayerNameDisplay", "Player Name", "PlayerNameText");
        }
    }

    // Palīgmetode, lai atrastu UI komponentes ar vairākiem iespējamiem nosaukumiem
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
                    Debug.Log($"MainMenuManager: Atrasts {typeof(T).Name}: {name}");
                    return component;
                }
            }
        }
        
        // Ja neatrod pēc nosaukuma, meklējam pēc komponentes tipa
        var allComponents = FindObjectsByType<T>(FindObjectsSortMode.None);
        if (allComponents.Length > 0)
        {
            Debug.Log($"MainMenuManager: Atrasts {typeof(T).Name} pēc tipa meklēšanas: {allComponents[0].name}");
            return allComponents[0];
        }
        
        Debug.LogWarning($"MainMenuManager: Nevarēja atrast {typeof(T).Name} ar nosaukumiem: {string.Join(", ", possibleNames)}");
        return null;
    }

    // Palīgmetode, lai atrastu GameObject objektus
    private GameObject FindUIObject(params string[] possibleNames)
    {
        foreach (string name in possibleNames)
        {
            var foundObject = GameObject.Find(name);
            if (foundObject != null)
            {
                Debug.Log($"MainMenuManager: Atrasts GameObject: {name}");
                return foundObject;
            }
        }
        
        Debug.LogWarning($"MainMenuManager: Nevarēja atrast GameObject ar nosaukumiem: {string.Join(", ", possibleNames)}");
        return null;
    }

    private void SetupButtonListeners()
    {
        // Vispirms noņemam visus esošos klausītājus, lai izvairītos no dublikātiem
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayButtonClicked);
            Debug.Log("MainMenuManager: Iestatīts spēles pogas klausītājs");
        }
        
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            Debug.Log("MainMenuManager: Iestatīts iestatījumu pogas klausītājs");
        }
        
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitButtonClicked);
            Debug.Log("MainMenuManager: Iestatīts izejas pogas klausītājs");
        }
    }

    private void LoadAndDisplaySettings()
    {
        // Pārliecināmies, ka SettingsManager eksistē
        if (SettingsManager.Instance == null)
        {
            var existingSettings = FindObjectOfType<SettingsManager>();
            if (existingSettings != null)
            {
                Debug.Log("MainMenuManager: Atrasts esošais SettingsManager");
            }
            else
            {
                GameObject settingsObj = new GameObject("SettingsManager");
                settingsObj.AddComponent<SettingsManager>();
                DontDestroyOnLoad(settingsObj);
                Debug.Log("MainMenuManager: Izveidots jauns SettingsManager");
            }
        }
        
        //  Izsaucam LoadSettings bez parametriem (tagad tas ir publisks)
        if (SettingsManager.Instance != null)
        {
            // Ielādējam iestatījumus - šī tagad ir publiska metode
            if (playerNameDisplay != null)
            {
                playerNameDisplay.text = SettingsManager.Instance.PlayerName;
                Debug.Log($"MainMenuManager: Attēlots spēlētāja vārds: {SettingsManager.Instance.PlayerName}");
            }
        }
    }

    private void OnPlayButtonClicked()
    {
        Debug.Log("MainMenuManager: Nospiesta spēles poga");
        // Jūsu esošā spēles pogas loģika šeit
    }

    private void OnSettingsButtonClicked()
    {
        Debug.Log("MainMenuManager: Nospiesta iestatījumu poga");
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }
    }

    private void OnExitButtonClicked()
    {
        Debug.Log("MainMenuManager: Nospiesta izejas poga");
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    public void ReturnToMainMenu()
    {
        // Notīrām pastāvīgos pārvaldniekus, lai atjaunotu inspektora vērtības
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.CleanupOnMainMenu();
        }
        // Atkārtojam citiem pārvaldniekiem, ja nepieciešams (piem., AudioManager, SettingsManager)
        // AudioManager.Instance?.CleanupOnMainMenu();

        // Ielādējam galvenās izvēlnes ainu
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void OnDestroy()
    {
        // Notīrām notikumu klausītājus
        if (playButton != null)
            playButton.onClick.RemoveAllListeners();
        if (settingsButton != null)
            settingsButton.onClick.RemoveAllListeners();
        if (exitButton != null)
            exitButton.onClick.RemoveAllListeners();
    }
}
