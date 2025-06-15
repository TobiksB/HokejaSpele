using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Unity.Netcode;

namespace HockeyGame.Game
{
    // Klase, kas pārvalda pauzes izvēlni hokeja spēlē.
    // Nodrošina spēles pauzēšanu un atsākšanu, piekļuvi iestatījumiem, iespēju atgriezties galvenajā izvēlnē, un spēles aizvēršanu.
    public class PauseMenuManager : MonoBehaviour
    {
        [Header("Pauzes izvēlne")]
        [SerializeField] private GameObject pausePanel; // Pauzes paneļa ietvars
        [SerializeField] private Button resumeButton; // Poga spēles turpināšanai
        [SerializeField] private Button settingsButton; // Poga iestatījumu atvēršanai
        [SerializeField] private Button mainMenuButton; // Poga atgriešanai uz galveno izvēlni
        [SerializeField] private Button exitButton; // Poga spēles aizvēršanai

        [Header("Iestatījumu panelis")]
        [SerializeField] private GameObject settingsPanel; // Iestatījumu paneļa ietvars
        [SerializeField] private Slider mouseSensitivitySlider; // Peles jutības slīdnis
        [SerializeField] private Slider volumeSlider; // Skaļuma slīdnis
        [SerializeField] private Toggle fullscreenToggle; // Pilnekrāna režīma pārslēgs
        [SerializeField] private TMP_Dropdown resolutionDropdown; // Izšķirtspējas izvelne
        [SerializeField] private Button settingsBackButton; // Poga atgriešanai uz pauzes izvēlni

        private bool isPaused = false; // Norāda, vai spēle ir pauzēta
        private float previousTimeScale; // Saglabā iepriekšējo laika mērogu pirms pauzes

        private void Awake()
        {
            // Ja nav pauzes paneļa, izveido vienkāršu versiju
            if (pausePanel == null)
            {
                CreateBasicPauseMenu();
            }
            // Sākumā paslēpj iestatījumu paneli
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        private void Start()
        {
            // Nodrošina, ka laika mērogs ir normāls
            EnsureTimeScaleIsNormal();

            // Sākumā paslēpj pauzes paneli
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            // Iestata pauzes paneļa pogu klausītājus
            if (resumeButton != null)
                resumeButton.onClick.AddListener(ResumeGame);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            if (exitButton != null)
                exitButton.onClick.AddListener(ExitGame);

            // Iestata iestatījumu paneļa pogas
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(CloseSettings);

            // --- PIESPIEDU KĀRTĀ AIZPILDA IESTATĪJUMU PANEĻA UI UZ STARTA ---
            // Šeit NEIESTATĪT klausītājus, tikai aizpildīt vērtības
            ForcePopulateSettingsPanelUI();
        }

        private void Update()
        {
            // Atver/aizver pauzes izvēlni ar Escape taustiņu
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Ja iestatījumu panelis ir atvērts, aizver to
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    CloseSettings();
                }
                else
                {
                    // Citādi pārslēdz pauzes režīmu
                    TogglePause();
                }
            }
#if UNITY_EDITOR
            // Unity redaktorā nodrošina, ka laika mērogs ir normāls, ja spēle nav pauzēta
            if (!isPaused && Time.timeScale != 1f)
            {
                EnsureTimeScaleIsNormal();
            }
#endif
        }

        // Izveido vienkāršu pauzes izvēlni, ja tā nav iestatīta inspekorā
        private void CreateBasicPauseMenu()
        {
            // Izveido pamata pauzes izvēlni
            pausePanel = new GameObject("PausePanel");
            pausePanel.transform.SetParent(transform);

            Canvas canvas = pausePanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            Image background = pausePanel.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.7f);

            pausePanel.SetActive(false);

            Debug.Log("Izveidota pamata pauzes izvēlne");
        }

        // Turpina spēli pēc pauzes
        public void ResumeGame()
        {
            if (!isPaused) return;

            isPaused = false;
            Time.timeScale = 1f;

            if (pausePanel != null)
                pausePanel.SetActive(false);

            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            Debug.Log("PauseMenuManager: Spēle turpināta");
        }

        // Pauzē spēli
        public void PauseGame()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            isPaused = true;

            if (pausePanel != null)
                pausePanel.SetActive(true);

            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            Debug.Log("PauseMenuManager: Spēle pauzēta");
        }

        // Pārslēdz pauzes režīmu
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
            Debug.Log($"PauseMenuManager: Pārslēgta pauze - isPaused: {isPaused}");
        }

        // Atgriežas uz galveno izvēlni (vecā metode)
        public void GoToMainMenu()
        {
            // Izslēdz visu tīklošanu
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
                    Debug.LogWarning($"PauseMenuManager: Kļūda izslēdzot NetworkManager: {e.Message}");
                }
            }

            // Atiestata priekštelpu spēlētāju sarakstu un tērzēšanu
            if (LobbyManager.Instance != null)
            {
                try
                {
                    LobbyManager.Instance.StopAllCoroutines();
                    // Izmanto refleksiju, lai notīrītu privātas vārdnīcas un tērzēšanu
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
                    // PIEVIENOTS: Atiestata visu iekšējo stāvokli, lai atļautu jaunas resursdatora/klienta sesijas
                    LobbyManager.Instance.ResetLobbyState();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"PauseMenuManager: Kļūda atiestatot priekštelpas spēlētāju sarakstu vai tērzēšanu: {e.Message}");
                }
            }

            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // Atgriežas uz galveno izvēlni (uzlabota versija)
        public void ReturnToMainMenu()
        {
            // Atrod un iznīcina GameSettingsManager, lai novērstu dublikātus, kad atgriežas uz MainMenu
            var gameSettingsManager = FindObjectOfType<GameSettingsManager>();
            if (gameSettingsManager != null)
            {
                Debug.Log("PauseMenuManager: Iznīcina GameSettingsManager pirms atgriešanās uz MainMenu");
                Destroy(gameSettingsManager.gameObject);
            }
            else
            {
                Debug.Log("PauseMenuManager: Nav atrasts iznīcināmais GameSettingsManager");
            }

            // Izslēdz tīklošanu, ja tā darbojas
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
                    Debug.LogWarning($"PauseMenuManager: Kļūda izslēdzot NetworkManager: {e.Message}");
                }
            }

            // Atiestata laika mērogu (gadījumā, ja tas bija pauzēts)
            Time.timeScale = 1f;

            // Ielādē galveno izvēlni
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // Iziet no spēles
        public void ExitGame()
        {
            // Atrod un iznīcina GameSettingsManager pirms iziešanas
            var gameSettingsManager = FindObjectOfType<GameSettingsManager>();
            if (gameSettingsManager != null)
            {
                Debug.Log("PauseMenuManager: Iznīcina GameSettingsManager pirms iziešanas no spēles");
                Destroy(gameSettingsManager.gameObject);
            }

            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Atver iestatījumu paneli
        private void OpenSettings()
        {
            // Vienmēr atjaunina UI un klausītājus, kad atver iestatījumus

            // --- SINHRONIZĒ NO SettingsPanel, JA TĀ EKSISTĒ ---
            var settingsPanelObj = GameObject.FindObjectOfType<SettingsPanel>();
            bool copiedDropdown = false;
            if (settingsPanelObj != null)
            {
                // Kopē vērtības no SettingsPanel UI uz PauseMenuManager UI
                if (mouseSensitivitySlider != null && settingsPanelObj.mouseSensitivitySlider != null)
                    mouseSensitivitySlider.value = settingsPanelObj.mouseSensitivitySlider.value;
                if (volumeSlider != null && settingsPanelObj.volumeSlider != null)
                    volumeSlider.value = settingsPanelObj.volumeSlider.value;
                if (fullscreenToggle != null && settingsPanelObj.fullscreenToggle != null)
                    fullscreenToggle.isOn = settingsPanelObj.fullscreenToggle.isOn;
                if (resolutionDropdown != null && settingsPanelObj.resolutionDropdown != null)
                {
                    // --- LABOJUMS: Kopē opcijas, izmantojot AddOptions ar virkņu sarakstu ---
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
            // --- KRITISKS: Vienmēr aizpilda izvēlni no GameSettingsManager, ja SettingsPanel trūkst vai tam nav opciju ---
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
                    // Rezerves variants: pievieno pašreizējo ekrāna izšķirtspēju, ja nav opciju
                    string currentRes = $"{Screen.currentResolution.width} x {Screen.currentResolution.height}";
                    resolutionDropdown.AddOptions(new System.Collections.Generic.List<string> { currentRes });
                    resolutionDropdown.SetValueWithoutNotify(0);
                }
                resolutionDropdown.RefreshShownValue();
            }

            SetupSettingsPanelListenersAndValues();
            // --- KRITISKS: Šeit iestata klausītājus PILNEKRĀNA pārslēgam un izvēlnei ---
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

        // Palīgmetode iestatījumu paneļa klausītāju un vērtību iestatīšanai (izsaukt TIKAI, kad atver paneli)
        private void SetupSettingsPanelListenersAndValues()
        {
            if (GameSettingsManager.Instance == null) return;

            // Peles jutība
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
                mouseSensitivitySlider.SetValueWithoutNotify(GameSettingsManager.Instance.mouseSensitivity);
                mouseSensitivitySlider.onValueChanged.AddListener(GameSettingsManager.Instance.SetMouseSensitivity);
            }
            // Skaļums
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveAllListeners();
                volumeSlider.SetValueWithoutNotify(GameSettingsManager.Instance.gameVolume);
                volumeSlider.onValueChanged.AddListener(GameSettingsManager.Instance.SetGameVolume);
            }
            // Pilnekrāna režīms
            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.SetIsOnWithoutNotify(GameSettingsManager.Instance.isFullscreen);
                fullscreenToggle.onValueChanged.AddListener(GameSettingsManager.Instance.SetFullscreen);
            }
            // Izšķirtspējas izvēlne
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

        // Aizver iestatījumu paneli
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

        // Apstrādā izšķirtspējas izvēlnes maiņu
        private void OnResolutionChanged(int index)
        {
            if (GameSettingsManager.Instance != null)
                GameSettingsManager.Instance.SetResolution(index, fullscreenToggle != null ? fullscreenToggle.isOn : false);
        }

        // Apstrādā pilnekrāna režīma pārslēgšanu
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
            Debug.Log($"PauseMenuManager: Pilnekrāna režīms pārslēgts uz {isFullscreen} izmantojot SetResolution");
        }

        // Nodrošina, ka laika mērogs ir normāls (1.0)
        private void EnsureTimeScaleIsNormal()
        {
            if (Time.timeScale != 1f)
            {
                Debug.LogWarning($"PauseMenuManager: Time.timeScale bija {Time.timeScale}, atiestatīts uz 1");
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// Nodrošina, ka iestatījumu paneļa UI vienmēr ir pilnībā aizpildīts, pat ja aina tiek ielādēta tieši.
        /// </summary>
        private void ForcePopulateSettingsPanelUI()
        {
            if (GameSettingsManager.Instance == null) return;

            // Tikai iestata vērtības, šeit neiestata klausītājus
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
            // Nodrošina, ka iestatījumu panelis vienmēr ir aizpildīts, kad šis objekts ir iespējots (ainas ielāde utt.)
            ForcePopulateSettingsPanelUI();
        }
    }
}
