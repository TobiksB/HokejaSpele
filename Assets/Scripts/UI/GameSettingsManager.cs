using UnityEngine;
using UnityEngine.Audio;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Settings")]
    [Range(0.1f, 10f)]
    public float mouseSensitivity = 1.0f;
    [Range(0f, 1f)]
    public float gameVolume = 1.0f;
    public bool isFullscreen = true;

    [Header("Audio")]
    [SerializeField] private AudioMixer masterMixer; // Assign your master mixer in inspector

    // Store available resolutions
    public Resolution[] AvailableResolutions { get; private set; }
    public int CurrentResolutionIndex { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Populate available resolutions and set current index
            AvailableResolutions = Screen.resolutions;
            CurrentResolutionIndex = GetCurrentResolutionIndex();

            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = value;
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();

        Debug.Log($"GameSettingsManager: Mouse sensitivity set to {value}");
    }

    public void SetGameVolume(float volume)
    {
        gameVolume = Mathf.Clamp01(volume);
        
        // Use AudioManager if available
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(gameVolume);
        }
        else
        {
            // Fallback to AudioListener
            AudioListener.volume = gameVolume;
        }
        
        PlayerPrefs.SetFloat("GameVolume", gameVolume);
        PlayerPrefs.Save();
    }

    // Set resolution by index from dropdown
    public void SetResolution(int resolutionIndex, bool applyFullscreen = true)
    {
        if (AvailableResolutions == null || AvailableResolutions.Length == 0)
            AvailableResolutions = Screen.resolutions;

        if (resolutionIndex < 0 || resolutionIndex >= AvailableResolutions.Length)
            resolutionIndex = GetCurrentResolutionIndex();

        Resolution res = AvailableResolutions[resolutionIndex];
        Screen.SetResolution(res.width, res.height, isFullscreen);
        CurrentResolutionIndex = resolutionIndex;
        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
        PlayerPrefs.Save();
    }

    // Returns a string array for populating a dropdown with available resolutions
    public string[] GetResolutionOptions()
    {
        if (AvailableResolutions == null || AvailableResolutions.Length == 0)
            AvailableResolutions = Screen.resolutions;

        string[] options = new string[AvailableResolutions.Length];
        for (int i = 0; i < AvailableResolutions.Length; i++)
        {
            var res = AvailableResolutions[i];
            options[i] = $"{res.width} x {res.height} @ {res.refreshRate}Hz";
        }
        return options;
    }

    // Returns the index of the current screen resolution in the AvailableResolutions array
    public int GetCurrentResolutionDropdownIndex()
    {
        if (AvailableResolutions == null || AvailableResolutions.Length == 0)
            AvailableResolutions = Screen.resolutions;

        Resolution current = Screen.currentResolution;
        for (int i = 0; i < AvailableResolutions.Length; i++)
        {
            if (AvailableResolutions[i].width == current.width &&
                AvailableResolutions[i].height == current.height &&
                AvailableResolutions[i].refreshRate == current.refreshRate)
            {
                return i;
            }
        }
        return 0;
    }

    // Helper to get the current resolution index
    private int GetCurrentResolutionIndex()
    {
        if (AvailableResolutions == null || AvailableResolutions.Length == 0)
            AvailableResolutions = Screen.resolutions;

        Resolution current = Screen.currentResolution;
        for (int i = 0; i < AvailableResolutions.Length; i++)
        {
            if (AvailableResolutions[i].width == current.width &&
                AvailableResolutions[i].height == current.height &&
                AvailableResolutions[i].refreshRate == current.refreshRate)
            {
                return i;
            }
        }
        return 0;
    }

    public void SetFullscreen(bool fullscreen)
    {
        isFullscreen = fullscreen;
        
        // CRITICAL: Use SetResolution instead of just Screen.fullScreen
        Resolution currentRes = Screen.currentResolution;
        Screen.SetResolution(currentRes.width, currentRes.height, fullscreen);
        
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        
        Debug.Log($"GameSettingsManager: Fullscreen set to {fullscreen} using SetResolution");
    }

    public void ToggleFullscreen()
    {
        SetFullscreen(!isFullscreen);
    }

    public void LoadSettings()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        gameVolume = PlayerPrefs.GetFloat("GameVolume", 1.0f);
        isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        int resIndex = PlayerPrefs.GetInt("ResolutionIndex", GetCurrentResolutionIndex());

        SetMouseSensitivity(mouseSensitivity);
        SetGameVolume(gameVolume);
        SetResolution(resIndex, applyFullscreen: true);
        SetFullscreen(isFullscreen);
    }

    public void ResetToDefaults()
    {
        Debug.Log("GameSettingsManager: Resetting to default values...");
        
        // Reset to default values
        mouseSensitivity = 1.0f;
        gameVolume = 1.0f;
        isFullscreen = false;
        
        // Reset resolution to native screen resolution
        Resolution nativeRes = Screen.currentResolution;
        int nativeIndex = 0;
        var resolutions = Screen.resolutions;
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == nativeRes.width && resolutions[i].height == nativeRes.height)
            {
                nativeIndex = i;
                break;
            }
        }
        CurrentResolutionIndex = nativeIndex;
        
        // Apply defaults immediately
        Screen.SetResolution(nativeRes.width, nativeRes.height, false);
        AudioListener.volume = gameVolume;
        
        // Save defaults
        SaveSettings();
    
        Debug.Log($"GameSettingsManager: Reset complete - Resolution: {nativeRes.width}x{nativeRes.height}, Sensitivity: {mouseSensitivity}, Volume: {gameVolume}, Fullscreen: {isFullscreen}");
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
        PlayerPrefs.SetFloat("GameVolume", gameVolume);
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.SetInt("ResolutionIndex", CurrentResolutionIndex);
        PlayerPrefs.Save();
        
        Debug.Log("GameSettingsManager: Settings saved to PlayerPrefs");
    }
}
