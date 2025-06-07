using UnityEngine;
using UnityEngine.UI; // Added for Slider

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // Settings properties
    public float MouseSensitivity { get; set; } = 1f;
    public float Volume { get; set; } = 1f;
    public string PlayerName { get; private set; } = "Player";
    private const string PLAYER_NAME_KEY = "PlayerName";

    private const string MOUSE_SENS_KEY = "MouseSensitivity";
    private const string VOLUME_KEY = "Volume";

    // ADDED: Audio settings properties
    private float musicVolume = 1f;
    private float sfxVolume = 1f;
    private float masterVolume = 1f;

    // ADDED: UI Sliders for audio settings
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider masterVolumeSlider;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetPlayerName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            PlayerName = name;
            SaveSettings();
        }
    }

    public void ResetToDefaults()
    {
        Debug.Log("SettingsManager: Resetting to default values...");
        
        // Reset to default values
        MouseSensitivity = 1f;
        Volume = 1f;
        Screen.fullScreen = true;
        PlayerName = "Player";
        
        // ADDED: Reset audio settings to defaults
        musicVolume = 1f;
        sfxVolume = 1f;
        masterVolume = 1f;
        
        // Save defaults
        SaveSettings();
        
        Debug.Log($"SettingsManager: Reset complete - Player Name: {PlayerName}");
    }

    public void LoadSettings()
    {
        // Load player name
        PlayerName = PlayerPrefs.GetString("PlayerName", "Player");
        
        // Load single volume setting
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        
        Debug.Log($"SettingsManager: Loaded settings - Name: {PlayerName}, Volume: {masterVolume:F2}");
        
        // Apply audio settings after loading
        StartCoroutine(ApplyAudioSettingsDelayed());
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MOUSE_SENS_KEY, MouseSensitivity);
        PlayerPrefs.SetFloat(VOLUME_KEY, Volume);
        PlayerPrefs.SetString(PLAYER_NAME_KEY, PlayerName);

        // ADDED: Save audio settings
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);

        PlayerPrefs.Save();
    }

    // ADDED: Method to set master volume
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        
        // Apply volume to AudioManager immediately
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(masterVolume);
            Debug.Log($"SettingsManager: Set master volume to {masterVolume:F2} via AudioManager");
        }
        else
        {
            Debug.LogWarning("SettingsManager: AudioManager.Instance is null, using AudioListener fallback");
            // Fallback to AudioListener
            AudioListener.volume = masterVolume;
        }
        
        SaveSettings();
        Debug.Log($"SettingsManager: Master volume set to {masterVolume:F2}");
    }

    // REMOVED: SetMusicVolume and SetSFXVolume methods - using single volume control now

    // ADDED: Method to ensure AudioManager has correct settings on startup
    public void ApplyAudioSettings()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(masterVolume);
            Debug.Log($"SettingsManager: Applied master volume: {masterVolume:F2}");
        }
        else
        {
            AudioListener.volume = masterVolume;
            Debug.LogWarning("SettingsManager: Applied volume via AudioListener fallback");
        }
    }

    // ADDED: Property accessors for UI
    public float MasterVolume => masterVolume;

    // ADDED: Apply audio settings with delay to ensure AudioManager is ready
    private System.Collections.IEnumerator ApplyAudioSettingsDelayed()
    {
        // Wait for AudioManager to be initialized
        float timeout = 5f;
        float elapsed = 0f;
        
        while (AudioManager.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (AudioManager.Instance != null)
        {
            ApplyAudioSettings();
        }
        else
        {
            Debug.LogWarning("SettingsManager: AudioManager not found after timeout, settings may not be applied");
        }
    }

    // ADDED: Method to set up slider listeners
    public void SetupAudioSliderListeners()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveAllListeners();
            musicSlider.onValueChanged.AddListener(SetMasterVolume);
            musicSlider.value = masterVolume;
            Debug.Log("SettingsManager: Set up music slider listener");
        }
        
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.AddListener(SetMasterVolume);
            sfxSlider.value = masterVolume;
            Debug.Log("SettingsManager: Set up SFX slider listener");
        }
        
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            masterVolumeSlider.value = masterVolume;
            Debug.Log("SettingsManager: Set up master volume slider listener");
        }
    }
}
