using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System.Collections;
using HockeyGame.Game;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Paneļi")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gamemodePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject createOrJoinPanel;

    [Header("Pogas")]
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

    [Header("Pievienošanās Lobijam UI")]
    [SerializeField] private TMP_InputField joinLobbyCodeInput;
    [SerializeField] private Button joinLobbyConfirmButton;

    [Header("Atkļūdošana")]
    [SerializeField] private Button debugConsoleButton;
    [SerializeField] private DebugConsole debugConsole;
    [SerializeField] private BuildLogger buildLogger;

    private GameMode selectedGameMode = GameMode.None;

    private GameObject currentActivePanel;

    private void Awake()
    {
        // Singletona šablons
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // NOŅEMT ŠO RINDU
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Pārbaudiet, vai esam MainMenu ainā, un iznīciniet visas kameras
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            Debug.LogWarning($"MenuManager: Nav MainMenu ainā ({currentScene}), iznīcinām");
            Destroy(gameObject);
            return;
        }
        
        //  Atspējojiet visas ar spēli saistītās kameras, kurām nevajadzētu būt MainMenu
        Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            if (cam.name.Contains("Player") || cam.name.Contains("Game") || cam.name.Contains("Local"))
            {
                Debug.LogError($"IZNĪCINĀM spēles kameru {cam.name}, kas atrasta MainMenu ainā!");
                Destroy(cam.gameObject);
            }
        }
        
        // Vispirms inicializējiet faila reģistrētāju atkļūdošanai
        FileLogger.LogToFile("=== HOKEJA SPĒLE SĀKTA GALVENAJĀ IZVĒLNĒ ===");
        FileLogger.LogToFile("MenuManager: Inicializē spēli");
        
        // Iestatiet sākotnējos paneļus un pārliecinieties, ka tie eksistē ainā
        foreach (var panel in new[] { mainMenuPanel, gamemodePanel, settingsPanel, lobbyPanel, createOrJoinPanel })
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        SetupButtonListeners();

        // Pievienojiet atkļūdošanas konsoles iestatīšanu
        if (debugConsoleButton) debugConsoleButton.onClick.AddListener(ToggleDebugConsole);
        
        // Parādiet atkļūdošanas pogu būvējumos, slēpiet redaktorā
        #if !UNITY_EDITOR
        if (debugConsoleButton) debugConsoleButton.gameObject.SetActive(true);
        #else
        if (debugConsoleButton) debugConsoleButton.gameObject.SetActive(false);
        #endif

        // Inicializējiet reģistrētāju būvējumos
        #if !UNITY_EDITOR
        if (buildLogger == null)
        {
            var loggerGO = new GameObject("BuildLogger");
            buildLogger = loggerGO.AddComponent<BuildLogger>();
            DontDestroyOnLoad(loggerGO);
        }
        #endif

        ShowPanel(mainMenuPanel);
        
        FileLogger.LogToFile("MenuManager: Inicializācija pabeigta");
    }

    private void SetupButtonListeners()
    {
        // Noņemiet visus klausītājus pirms pievienošanas, lai novērstu uzkrāšanos/klikšķināšanu
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
        Debug.Log($"Rāda paneli: {panel.name}, Aktīvs: {panel.activeInHierarchy}");

        // Ja šis ir lobija panelis, pārliecinieties, ka tas ir pareizi inicializēts
        if (panel == lobbyPanel)
        {
            // Izmantojiet tikai UI.LobbyPanelManager
            var lobbyPanelManagerUI = panel.GetComponent<HockeyGame.UI.LobbyPanelManager>();
            
            if (lobbyPanelManagerUI != null)
            {
                Debug.Log("Atrasts UI.LobbyPanelManager, inicializē...");
                lobbyPanelManagerUI.ForceInitialize();
            }
            else
            {
                Debug.LogError("Uz lobija paneļa nav atrasts UI.LobbyPanelManager komponents!");
                Debug.LogError("Lūdzu, pievienojiet HockeyGame.UI.LobbyPanelManager komponentu savam lobija paneļa GameObject");
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
        Debug.Log($"Izvēlēts spēles režīms: {mode}");

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
        Debug.Log("Ielādē TrainingMode ainu tieši");
        UnityEngine.SceneManagement.SceneManager.LoadScene("TrainingMode");
    }

    private async void OpenCreateLobby()
    {
        try
        {
            FileLogger.LogToFile("=== VEIDO 2v2 LOBIJU ===");
            Debug.Log("Sāk 2v2 lobija izveides procesu...");
            
            // Vispirms gaidiet servisu inicializāciju
            if (!Unity.Services.Core.UnityServices.State.Equals(Unity.Services.Core.ServicesInitializationState.Initialized))
            {
                FileLogger.LogToFile("Inicializē Unity Services...");
                Debug.Log("Inicializē Unity Services...");
                await Unity.Services.Core.UnityServices.InitializeAsync();
            }

            // Gaidiet autentifikāciju
            if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            {
                FileLogger.LogToFile("Piesakās anonīmi...");
                Debug.Log("Piesakās anonīmi...");
                await Unity.Services.Authentication.AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            //  Parādiet paneli VISPIRMS un pārliecinieties, ka tas ir pilnībā inicializēts
            FileLogger.LogToFile("Rāda lobija paneli...");
            ShowPanel(lobbyPanel);
            await Task.Delay(200); // Ilgāka gaidīšana paneļa aktivizēšanai

            // Izmantojiet tikai UI.LobbyPanelManager
            var lobbyPanelManagerUI = lobbyPanel.GetComponent<HockeyGame.UI.LobbyPanelManager>();

            if (lobbyPanelManagerUI != null)
            {
                Debug.Log("Izmanto UI.LobbyPanelManager 2v2 lobijam");
                lobbyPanelManagerUI.ForceInitialize();
                await Task.Delay(200); // Gaidiet pilnīgu inicializāciju

                // Tagad izveidot lobiju PĒC tam, kad UI ir gatavs
                Debug.Log("UI gatavs, tagad veido lobiju...");
                
                string lobbyCode = null;
                int retryCount = 0;
                while (string.IsNullOrEmpty(lobbyCode) && retryCount < 3)
                {
                    int maxPlayers = 4; // Vienmēr 4 priekš 2v2
                    Debug.Log($"Veido 2v2 lobiju mēģinājums {retryCount + 1} priekš {maxPlayers} spēlētājiem...");
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
                    Debug.Log($"Veiksmīgi izveidots 2v2 lobijs ar kodu: {lobbyCode}");
                    
                    //  Piespiediet vēl vienu UI atjaunināšanu pēc tam, kad viss ir iestatīts
                    await Task.Delay(100);
                    if (LobbyManager.Instance != null)
                    {
                        LobbyManager.Instance.RefreshPlayerList();
                    }
                }
                else
                {
                    throw new System.Exception("Neizdevās izveidot 2v2 lobiju pēc vairākiem mēģinājumiem");
                }
            }
            else
            {
                throw new System.Exception("UI.LobbyPanelManager komponents nav atrasts! Lūdzu, pievienojiet to savam lobija panelim.");
            }
        }
        catch (System.Exception e)
        {
            FileLogger.LogError($"Kļūda OpenCreateLobby: {e.Message}");
            FileLogger.LogError($"Steka izsekošana: {e.StackTrace}");
            Debug.LogError($"Kļūda OpenCreateLobby priekš 2v2: {e.Message}");
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
            Debug.LogError("KLIENTS: LobbyManager.Instance ir null vai joinLobbyCodeInput ir tukšs.");
            return;
        }

        string lobbyCode = joinLobbyCodeInput.text.Trim();
        
        try
        {
            Debug.Log($"KLIENTS: ============ SĀKAS LOBIJA PIEVIENOŠANĀS PROCESS ============");
            
            //  pievienošanās pogu, lai novērstu vairākus klikšķus
            if (joinLobbyConfirmButton) joinLobbyConfirmButton.interactable = false;
            
            // : Vispirms pārbaudiet, vai lobija panelis eksistē
            if (lobbyPanel == null)
            {
                Debug.LogError("KLIENTS: ✗ LOBIJA PANEĻA ATSAUCE IR NULL!");
                Debug.LogError("KLIENTS: Pārbaudiet MenuManager inspektoru - lobbyPanel laukam jābūt piešķirtam!");
                if (joinLobbyConfirmButton) joinLobbyConfirmButton.interactable = true;
                return;
            }

            // 1. solis: Pievienojieties lobija aizmugurējam API (bet neļaujiet releja kļūmēm mūs apstādināt)
            Debug.Log("KLIENTS: 1. solis - Pievienojas lobija aizmugurējam API...");
            try
            {
                await LobbyManager.Instance.JoinLobby(lobbyCode);
                Debug.Log("KLIENTS: ✓ Veiksmīgi pievienojies lobija aizmugurējam API");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"KLIENTS: ⚠ Lobija pievienošanās bija problēmas, bet turpinām: {e.Message}");
                // Turpiniet jebkurā gadījumā - lobija pievienošanās varētu būt daļēji izdevusies
            }
            
            // 2. solis: nekavējoties parādiet lobija paneli neatkarīgi no releja statusa
            Debug.Log("KLIENTS: 2. solis - NEKAVĒJOŠA lobija paneļa pāreja...");
            Debug.Log($"KLIENTS: Pirms pārejas - Pašreizējais: {(currentActivePanel ? currentActivePanel.name : "null")}, Mērķis: {lobbyPanel.name}");
            
            // Piespiedu deaktivizējiet pašreizējo paneli
            if (currentActivePanel != null && currentActivePanel != lobbyPanel)
            {
                Debug.Log($"KLIENTS: Deaktivizē: {currentActivePanel.name}");
                currentActivePanel.SetActive(false);
            }
            
            // Piespiedu aktivizējiet lobija paneli
            Debug.Log($"KLIENTS: Aktivizē: {lobbyPanel.name}");
            lobbyPanel.SetActive(true);
            currentActivePanel = lobbyPanel;
            
            //  Piespiedu tūlītēja Canvas atjaunināšana
            Canvas.ForceUpdateCanvases();
            
            // Pārbaudiet, vai pāreja strādāja
            Debug.Log($"KLIENTS: Pēc pārejas - Pašreizējais: {(currentActivePanel ? currentActivePanel.name : "null")}, Aktīvs: {lobbyPanel.activeInHierarchy}");
            
            if (!lobbyPanel.activeInHierarchy)
            {
                Debug.LogError("KLIENTS: ✗ PANEĻA PĀREJA NEIZDEVĀS! Mēģinu rezerves variantu...");
                
                // Rezerves variants: Rupji piespiediet visus paneļus
                foreach (var panel in new[] { mainMenuPanel, gamemodePanel, settingsPanel, createOrJoinPanel })
                {
                    if (panel != null) panel.SetActive(false);
                }
                lobbyPanel.SetActive(true);
                currentActivePanel = lobbyPanel;
                Canvas.ForceUpdateCanvases();
                
                Debug.Log($"KLIENTS: Rezerves varianta rezultāts - Aktīvs: {lobbyPanel.activeInHierarchy}");
            }
            
            // 3. solis: Inicializējiet pārvaldniekus, kamēr panelis tiek rādīts
            await Task.Delay(100); // Īsa pauze aktivizācijai
            
            Debug.Log("KLIENTS: 3. solis - Inicializē pārvaldniekus...");
            var lobbyPanelManagerUI = lobbyPanel.GetComponent<HockeyGame.UI.LobbyPanelManager>();
            
            Debug.Log($"KLIENTS: Atrasts UI pārvaldnieks: {lobbyPanelManagerUI != null}");
            
            bool managerInitialized = false;
            
            if (lobbyPanelManagerUI != null)
            {
                Debug.Log("KLIENTS: ✓ Inicializē UI.LobbyPanelManager");
                try
                {
                    lobbyPanelManagerUI.ForceInitialize();
                    await Task.Delay(50);
                    lobbyPanelManagerUI.SetLobbyCode(lobbyCode);
                    managerInitialized = true;
                    Debug.Log("KLIENTS: ✓ UI pārvaldnieks inicializēts veiksmīgi");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"KLIENTS: ✗ UI pārvaldnieka inicializācija neizdevās: {e.Message}");
                }
            }
            else
            {
                Debug.LogError("KLIENTS: ✗ Nav atrasts UI.LobbyPanelManager!");
                Debug.LogError("KLIENTS: Lūdzu, pievienojiet HockeyGame.UI.LobbyPanelManager komponentu savam lobija panelim");
            }
            
            // 4. solis: Piespiedu vairākkārtējas UI atsvaidzināšanas
            if (managerInitialized)
            {
                Debug.Log("KLIENTS: 4. solis - Vairāki UI atsvaidzināšanas mēģinājumi...");
                for (int i = 0; i < 3; i++)
                {
                    LobbyManager.Instance.RefreshPlayerList();
                    await Task.Delay(100);
                    Debug.Log($"KLIENTS: UI atsvaidzināšanas mēģinājums {i + 1}/3");
                }
                
                Debug.Log("KLIENTS: ============ LOBIJA PIEVIENOŠANĀS PABEIGTA VEIKSMĪGI ============");
            }
            else
            {
                Debug.LogError("KLIENTS: ✗ Neviens pārvaldnieks nevarēja tikt inicializēts!");
                
                // Uzskaitiet visus komponentus uz lobija paneļa atkļūdošanai
                var components = lobbyPanel.GetComponents<Component>();
                Debug.Log($"KLIENTS: Komponenti uz lobija paneļa ({components.Length}):");
                foreach (var comp in components)
                {
                    Debug.Log($"  - {comp.GetType().Name}");
                }
            }
            
            // Galīgā verifikācija
            Debug.Log($"KLIENTS: Galīgais stāvoklis - Panelis: {lobbyPanel.name}, Aktīvs: {lobbyPanel.activeInHierarchy}, Pašreizējais: {(currentActivePanel ? currentActivePanel.name : "null")}");
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"KLIENTS: ✗ Neizdevās pievienoties lobijam: {e.Message}");
            Debug.LogError($"KLIENTS: Steka izsekošana: {e.StackTrace}");
            
            // Pat neveiksmīgi, mēģiniet tomēr parādīt lobija paneli
            Debug.Log("KLIENTS: Ārkārtas rezerves variants - piespriedu lobija paneļa rādīšana...");
            ShowPanel(lobbyPanel);
            
            // Atkārtoti iespējojiet pogu neveiksmīgos gadījumos
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
            // Izveidojiet atkļūdošanas konsoli, ja tā neeksistē
            var consoleGO = new GameObject("DebugConsole");
            debugConsole = consoleGO.AddComponent<DebugConsole>();
            DontDestroyOnLoad(consoleGO);
            debugConsole.ToggleConsole();
        }
    }

    // Pievienojiet publisku metodi, lai piespiedu kārtā pārslēgtu paneļus atkļūdošanai
    public void ForceShowLobbyPanel()
    {
        Debug.Log("PIESPIEDU: Rāda lobija paneli");
        ShowPanel(lobbyPanel);
    }

    private void DisableAllPlayerCameras()
    {
        //  Noņemiet PlayerCamera atsauces un izmantojiet tiešu Camera komponentes piekļuvi
        var allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var camera in allCameras)
        {
            // Atspējojiet tikai tās kameras, kas nav galvenās izvēlnes kamera
            if (camera.gameObject.name.Contains("Player") || 
                camera.GetComponent<CameraFollow>() != null)
            {
                camera.gameObject.SetActive(false);
                Debug.Log($"MenuManager: Atspējota spēlētāja kamera: {camera.gameObject.name}");
            }
        }
    }

    // Izsauciet šo, kad atgriežaties galvenajā izvēlnē, lai veiktu tīrīšanu
    public void CleanupOnMainMenu()
    {
        // Pēc izvēles atiestatiet statisko gadījumu, ja vēlaties atkārtoti ielādēt inspektora vērtības
        Instance = null;
        Destroy(gameObject);
    }
}