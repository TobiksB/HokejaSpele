using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [Header("UI Elementi - PIEŠĶIRT ŠOS INSPEKTORĀ")]
    public TMP_InputField playerNameInput;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider mouseSensitivitySlider;
    public Slider volumeSlider; // Viens skaļuma slīdnis visām skaņām
    public Button applyButton;
    public Button resetButton;

    // PIEVIENOTS: Teksta attēlošanas komponenti (neobligāti)
    public TMPro.TextMeshProUGUI volumeText;
    public TMPro.TextMeshProUGUI mouseSensitivityText;

    private void Start()
    {
        EnsureGameSettingsManagerExists();
        ValidateUIElements();
        LoadCurrentSettings();
        SetupSliderListeners();
    }

    private void OnEnable()
    {
        EnsureGameSettingsManagerExists();
        LoadCurrentSettings();
    }

    // Pievienot šo palīgfunkciju, lai nodrošinātu, ka GameSettingsManager eksistē
    private void EnsureGameSettingsManagerExists()
    {
        if (GameSettingsManager.Instance == null)
        {
            var gsm = FindObjectOfType<GameSettingsManager>();
            if (gsm == null)
            {
                var go = new GameObject("GameSettingsManager");
                go.AddComponent<GameSettingsManager>();
                DontDestroyOnLoad(go);
                Debug.LogWarning("SettingsPanel: Izveidots trūkstošais GameSettingsManager izpildes laikā.");
            }
        }
    }

    private void ValidateUIElements()
    {
        Debug.Log("=== IESTATĪJUMU PANEĻA UI VALIDĀCIJA ===");
        Debug.Log($"playerNameInput: {(playerNameInput != null ? "✓ OK" : "✗ NULL")}");
        Debug.Log($"resolutionDropdown: {(resolutionDropdown != null ? "✓ OK" : "✗ NULL")}");
        Debug.Log($"fullscreenToggle: {(fullscreenToggle != null ? "✓ OK" : "✗ NULL")}");
        Debug.Log($"mouseSensitivitySlider: {(mouseSensitivitySlider != null ? "✓ OK" : "✗ NULL")}");
        Debug.Log($"volumeSlider: {(volumeSlider != null ? "✓ OK" : "✗ NULL")}");
        Debug.Log($"applyButton: {(applyButton != null ? "✓ OK" : "✗ NULL")}");
        Debug.Log($"resetButton: {(resetButton != null ? "✓ OK" : "✗ NULL")}");
        Debug.Log("=====================================");
    }

    private void SetupSliderListeners()
    {
        // VIENKĀRŠOTS: Viena skaļuma slīdņa klausītājs
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            Debug.Log("SettingsPanel: Iestatīts skaļuma slīdņa klausītājs");
        }
        
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            Debug.Log("SettingsPanel: Iestatīts peles jutības slīdņa klausītājs");
        }

        // Citi UI elementi
        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveAllListeners();
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
        }

        if (playerNameInput != null)
        {
            playerNameInput.onEndEdit.RemoveAllListeners();
            playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);
        }

        if (applyButton != null)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(ApplySettings);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ResetSettings);
        }
    }

    // VIENKĀRŠOTS: Vienots skaļuma izmaiņu apstrādātājs
    private void OnVolumeChanged(float value)
    {
        Debug.Log($"SettingsPanel: Skaļuma slīdnis mainīts uz {value:F2}");
        
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMasterVolume(value);
        }
        else
        {
            Debug.LogWarning("SettingsPanel: SettingsManager.Instance ir null");
            // Alternatīva tiešai AudioListener kontrolei
            AudioListener.volume = value;
        }
        
        // Atjaunināt attēlojumu, ja pieejams
        if (volumeText != null)
        {
            volumeText.text = $"Skaļums: {(value * 100):F0}%";
        }
    }

    private void OnMouseSensitivityChanged(float value)
    {
        Debug.Log($"SettingsPanel: Peles jutības slīdnis mainīts uz {value:F2}");
        
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.SetMouseSensitivity(value);
        }
        else
        {
            Debug.LogWarning("SettingsPanel: GameSettingsManager.Instance ir null");
        }
        
        // Atjaunināt attēlojumu, ja pieejams
        if (mouseSensitivityText != null)
        {
            mouseSensitivityText.text = $"Jutība: {value:F1}";
        }
    }

    private void LoadCurrentSettings()
    {
        // VIENKĀRŠOTS: Ielādēt iestatījumus no pārvaldniekiem un piemērot slīdņiem
        if (SettingsManager.Instance != null)
        {
            if (volumeSlider != null)
            {
                volumeSlider.value = SettingsManager.Instance.MasterVolume;
                if (volumeText != null)
                {
                    volumeText.text = $"Skaļums: {(SettingsManager.Instance.MasterVolume * 100):F0}%";
                }
            }
            
            if (playerNameInput != null)
            {
                playerNameInput.text = SettingsManager.Instance.PlayerName;
            }
            
            Debug.Log("SettingsPanel: Ielādēti pašreizējie iestatījumi no SettingsManager");
        }
        
        if (GameSettingsManager.Instance != null)
        {
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.value = GameSettingsManager.Instance.mouseSensitivity;
                if (mouseSensitivityText != null)
                {
                    mouseSensitivityText.text = $"Jutība: {GameSettingsManager.Instance.mouseSensitivity:F1}";
                }
            }
            
            // Ielādēt citas GameSettingsManager vērtības
            if (resolutionDropdown != null)
            {
                PopulateResolutionDropdown();
            }
            
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = GameSettingsManager.Instance.isFullscreen;
            }
            
            Debug.Log("SettingsPanel: Ielādēti pašreizējie iestatījumi no GameSettingsManager");
        }
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null || GameSettingsManager.Instance == null) return;

        try
        {
            resolutionDropdown.ClearOptions();
            var options = GameSettingsManager.Instance.GetResolutionOptions();
            
            if (options != null && options.Length > 0)
            {
                resolutionDropdown.AddOptions(new System.Collections.Generic.List<string>(options));
                resolutionDropdown.value = GameSettingsManager.Instance.CurrentResolutionIndex;
                resolutionDropdown.RefreshShownValue();
                Debug.Log($"Izšķirtspējas izvēlne piepildīta ar {options.Length} opcijām");
            }
            else
            {
                Debug.LogError("SettingsPanel: Nav pieejamas izšķirtspējas opcijas");
                string currentRes = $"{Screen.currentResolution.width} x {Screen.currentResolution.height}";
                resolutionDropdown.AddOptions(new System.Collections.Generic.List<string> { currentRes });
                resolutionDropdown.value = 0;
                resolutionDropdown.RefreshShownValue();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SettingsPanel: Kļūda piepildot izšķirtspējas izvēlni: {e.Message}");
        }
    }

    // Citu UI elementu apstrādātāji
    private void OnPlayerNameChanged(string newName)
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SetPlayerName(newName);
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
        
        Debug.Log($"SettingsPanel: Pilnekrāna režīms pārslēgts uz {isFullscreen} izmantojot SetResolution");
    }

    private void ApplySettings()
    {
        if (GameSettingsManager.Instance == null)
        {
            Debug.LogError("SettingsPanel: Nevar piemērot iestatījumus - GameSettingsManager.Instance ir null!");
            return;
        }

        Debug.Log("SettingsPanel: Piemēroju visus iestatījumus...");

        try
        {
            // Piemērot peles jutību
            if (mouseSensitivitySlider != null)
            {
                GameSettingsManager.Instance.SetMouseSensitivity(mouseSensitivitySlider.value);
                Debug.Log($"Piemērota peles jutība: {mouseSensitivitySlider.value}");
            }

            // Piemērot skaļumu
            if (volumeSlider != null && SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetMasterVolume(volumeSlider.value);
                Debug.Log($"Piemērots skaļums: {volumeSlider.value}");
            }

            // Piemērot izšķirtspēju un pilnekrāna režīmu
            if (resolutionDropdown != null && fullscreenToggle != null)
            {
                GameSettingsManager.Instance.SetResolution(resolutionDropdown.value, fullscreenToggle.isOn);
                GameSettingsManager.Instance.SetFullscreen(fullscreenToggle.isOn);
                
                Resolution currentRes = Screen.currentResolution;
                Screen.SetResolution(currentRes.width, currentRes.height, fullscreenToggle.isOn);
                
                Debug.Log($"Piemērots izšķirtspējas indekss {resolutionDropdown.value} ar pilnekrānu: {fullscreenToggle.isOn}");
            }

            // Piemērot spēlētāja vārdu
            if (playerNameInput != null && SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetPlayerName(playerNameInput.text);
                Debug.Log($"Piemērots spēlētāja vārds: {playerNameInput.text}");
            }

            // Saglabāt visus iestatījumus
            GameSettingsManager.Instance.SaveSettings();
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SaveSettings();
            }

            Debug.Log("SettingsPanel: Visi iestatījumi piemēroti un veiksmīgi saglabāti!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SettingsPanel: Kļūda piemērojot iestatījumus: {e.Message}");
        }
    }

    private void ResetSettings()
    {
        if (GameSettingsManager.Instance == null)
        {
            Debug.LogError("SettingsPanel: Nevar atiestatīt iestatījumus - GameSettingsManager.Instance ir null!");
            return;
        }

        Debug.Log("SettingsPanel: Atiestatu iestatījumus uz noklusējuma vērtībām...");

        try
        {
            // Atiestatīt GameSettingsManager uz noklusējuma vērtībām
            GameSettingsManager.Instance.ResetToDefaults();

            // Atiestatīt SettingsManager uz noklusējuma vērtībām
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.ResetToDefaults();
            }

            // Atsvaidzināt UI ar noklusējuma vērtībām
            PopulateResolutionDropdown();
            LoadCurrentSettings();

            Debug.Log("SettingsPanel: Iestatījumi veiksmīgi atiestatīti uz noklusējuma vērtībām!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SettingsPanel: Kļūda atiestatot iestatījumus: {e.Message}");
        }
    }
}
