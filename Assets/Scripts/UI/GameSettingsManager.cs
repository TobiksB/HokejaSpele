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
    }

    public void SetGameVolume(float value)
    {
        gameVolume = value;
        if (masterMixer != null)
        {
            // AudioMixer expects volume in dB, 0dB is full volume, -80dB is silence
            float dB = Mathf.Lerp(-80f, 0f, value);
            masterMixer.SetFloat("MasterVolume", dB);
        }
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("GameVolume", value);
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
        // Use the current resolution index to re-apply the mode
        SetResolution(CurrentResolutionIndex, applyFullscreen: true);
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
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
}
