using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [Header("UI Elements - ASSIGN THESE IN INSPECTOR")]
    public TMP_InputField playerNameInput;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider mouseSensitivitySlider;
    public Slider volumeSlider; // Single volume slider for all sounds
    public Button applyButton;
    public Button resetButton;

    // ADDED: Text display components (optional)
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

    // Add this helper to ensure GameSettingsManager exists
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
                Debug.LogWarning("SettingsPanel: Created missing GameSettingsManager at runtime.");
            }
        }
    }

    private void ValidateUIElements()
    {
        Debug.Log("=== SETTINGS PANEL UI VALIDATION ===");
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
        // SIMPLIFIED: Single volume slider listener
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            Debug.Log("SettingsPanel: Set up volume slider listener");
        }
        
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            Debug.Log("SettingsPanel: Set up mouse sensitivity slider listener");
        }

        // Other UI elements
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

    // SIMPLIFIED: Single volume change handler
    private void OnVolumeChanged(float value)
    {
        Debug.Log($"SettingsPanel: Volume slider changed to {value:F2}");
        
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMasterVolume(value);
        }
        else
        {
            Debug.LogWarning("SettingsPanel: SettingsManager.Instance is null");
            // Fallback to direct AudioListener control
            AudioListener.volume = value;
        }
        
        // Update display if available
        if (volumeText != null)
        {
            volumeText.text = $"Volume: {(value * 100):F0}%";
        }
    }

    private void OnMouseSensitivityChanged(float value)
    {
        Debug.Log($"SettingsPanel: Mouse sensitivity slider changed to {value:F2}");
        
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.SetMouseSensitivity(value);
        }
        else
        {
            Debug.LogWarning("SettingsPanel: GameSettingsManager.Instance is null");
        }
        
        // Update display if available
        if (mouseSensitivityText != null)
        {
            mouseSensitivityText.text = $"Sensitivity: {value:F1}";
        }
    }

    private void LoadCurrentSettings()
    {
        // SIMPLIFIED: Load settings from managers and apply to sliders
        if (SettingsManager.Instance != null)
        {
            if (volumeSlider != null)
            {
                volumeSlider.value = SettingsManager.Instance.MasterVolume;
                if (volumeText != null)
                {
                    volumeText.text = $"Volume: {(SettingsManager.Instance.MasterVolume * 100):F0}%";
                }
            }
            
            if (playerNameInput != null)
            {
                playerNameInput.text = SettingsManager.Instance.PlayerName;
            }
            
            Debug.Log("SettingsPanel: Loaded current settings from SettingsManager");
        }
        
        if (GameSettingsManager.Instance != null)
        {
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.value = GameSettingsManager.Instance.mouseSensitivity;
                if (mouseSensitivityText != null)
                {
                    mouseSensitivityText.text = $"Sensitivity: {GameSettingsManager.Instance.mouseSensitivity:F1}";
                }
            }
            
            // Load other GameSettingsManager values
            if (resolutionDropdown != null)
            {
                PopulateResolutionDropdown();
            }
            
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = GameSettingsManager.Instance.isFullscreen;
            }
            
            Debug.Log("SettingsPanel: Loaded current settings from GameSettingsManager");
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
                Debug.Log($"Resolution dropdown populated with {options.Length} options");
            }
            else
            {
                Debug.LogError("SettingsPanel: No resolution options available");
                string currentRes = $"{Screen.currentResolution.width} x {Screen.currentResolution.height}";
                resolutionDropdown.AddOptions(new System.Collections.Generic.List<string> { currentRes });
                resolutionDropdown.value = 0;
                resolutionDropdown.RefreshShownValue();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SettingsPanel: Error populating resolution dropdown: {e.Message}");
        }
    }

    // Other UI element handlers
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
        
        Debug.Log($"SettingsPanel: Fullscreen toggled to {isFullscreen} using SetResolution");
    }

    private void ApplySettings()
    {
        if (GameSettingsManager.Instance == null)
        {
            Debug.LogError("SettingsPanel: Cannot apply settings - GameSettingsManager.Instance is null!");
            return;
        }

        Debug.Log("SettingsPanel: Applying all settings...");

        try
        {
            // Apply mouse sensitivity
            if (mouseSensitivitySlider != null)
            {
                GameSettingsManager.Instance.SetMouseSensitivity(mouseSensitivitySlider.value);
                Debug.Log($"Applied mouse sensitivity: {mouseSensitivitySlider.value}");
            }

            // Apply volume
            if (volumeSlider != null && SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetMasterVolume(volumeSlider.value);
                Debug.Log($"Applied volume: {volumeSlider.value}");
            }

            // Apply resolution and fullscreen
            if (resolutionDropdown != null && fullscreenToggle != null)
            {
                GameSettingsManager.Instance.SetResolution(resolutionDropdown.value, fullscreenToggle.isOn);
                GameSettingsManager.Instance.SetFullscreen(fullscreenToggle.isOn);
                
                Resolution currentRes = Screen.currentResolution;
                Screen.SetResolution(currentRes.width, currentRes.height, fullscreenToggle.isOn);
                
                Debug.Log($"Applied resolution index {resolutionDropdown.value} with fullscreen: {fullscreenToggle.isOn}");
            }

            // Apply player name
            if (playerNameInput != null && SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetPlayerName(playerNameInput.text);
                Debug.Log($"Applied player name: {playerNameInput.text}");
            }

            // Save all settings
            GameSettingsManager.Instance.SaveSettings();
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SaveSettings();
            }

            Debug.Log("SettingsPanel: All settings applied and saved successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SettingsPanel: Error applying settings: {e.Message}");
        }
    }

    private void ResetSettings()
    {
        if (GameSettingsManager.Instance == null)
        {
            Debug.LogError("SettingsPanel: Cannot reset settings - GameSettingsManager.Instance is null!");
            return;
        }

        Debug.Log("SettingsPanel: Resetting settings to defaults...");

        try
        {
            // Reset GameSettingsManager to defaults
            GameSettingsManager.Instance.ResetToDefaults();

            // Reset SettingsManager to defaults
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.ResetToDefaults();
            }

            // Refresh UI with default values
            PopulateResolutionDropdown();
            LoadCurrentSettings();

            Debug.Log("SettingsPanel: Settings reset to defaults successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SettingsPanel: Error resetting settings: {e.Message}");
        }
    }
}
