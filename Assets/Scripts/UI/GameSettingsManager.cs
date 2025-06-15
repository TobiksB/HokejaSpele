using UnityEngine;
using UnityEngine.Audio;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Iestatījumi")]
    [Range(0.1f, 10f)]
    public float mouseSensitivity = 1.0f;
    [Range(0f, 1f)]
    public float gameVolume = 1.0f;
    public bool isFullscreen = true;

    [Header("Audio")]    [SerializeField] private AudioMixer masterMixer; // Piesaisti savu galveno mikseri inspektorā

    // Saglabā pieejamās izšķirtspējas
    public Resolution[] AvailableResolutions { get; private set; }
    public int CurrentResolutionIndex { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);            // Aizpilda pieejamās izšķirtspējas un iestata pašreizējo indeksu
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

        Debug.Log($"GameSettingsManager: Peles jutība iestatīta uz {value}");
    }

    public void SetGameVolume(float volume)
    {
        gameVolume = Mathf.Clamp01(volume);
        
        // Izmantot AudioManager, ja pieejams
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(gameVolume);
        }
        else
        {
            // Rezerves variants ar AudioListener
            AudioListener.volume = gameVolume;
        }
        
        PlayerPrefs.SetFloat("GameVolume", gameVolume);
        PlayerPrefs.Save();
    }

    // Iestata izšķirtspēju pēc indeksa no nolaižamās izvēlnes
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

    // Atgriež teksta masīvu nolaižamās izvēlnes aizpildīšanai ar pieejamajām izšķirtspējām
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

    // Atgriež pašreizējās ekrāna izšķirtspējas indeksu AvailableResolutions masīvā
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

    // Palīgmetode, lai iegūtu pašreizējās izšķirtspējas indeksu
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
        
        //  Izmantot SetResolution nevis tikai Screen.fullScreen
        Resolution currentRes = Screen.currentResolution;
        Screen.SetResolution(currentRes.width, currentRes.height, fullscreen);
        
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        
        Debug.Log($"GameSettingsManager: Pilnekrāna režīms iestatīts uz {fullscreen} izmantojot SetResolution");
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
        Debug.Log("GameSettingsManager: Atiestatīt uz noklusējuma vērtībām...");
        
        // Atiestatīt uz noklusējuma vērtībām
        mouseSensitivity = 1.0f;
        gameVolume = 1.0f;
        isFullscreen = false;
        
        // Atiestatīt izšķirtspēju uz ekrāna iedzimto izšķirtspēju
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
        
        // Nekavējoties piemērot noklusējuma iestatījumus
        Screen.SetResolution(nativeRes.width, nativeRes.height, false);
        AudioListener.volume = gameVolume;
        
        // Saglabāt noklusējuma iestatījumus
        SaveSettings();
    
        Debug.Log($"GameSettingsManager: Atiestatīšana pabeigta - Izšķirtspēja: {nativeRes.width}x{nativeRes.height}, Jutība: {mouseSensitivity}, Skaņa: {gameVolume}, Pilnekrāna režīms: {isFullscreen}");
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
        PlayerPrefs.SetFloat("GameVolume", gameVolume);
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.SetInt("ResolutionIndex", CurrentResolutionIndex);
        PlayerPrefs.Save();
        
        Debug.Log("GameSettingsManager: Iestatījumi saglabāti PlayerPrefs");
    }
}
