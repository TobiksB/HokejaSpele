using UnityEngine;
using UnityEngine.UI; // Pievienots Slider izmantošanai

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // Iestatījumu īpašības
    public float MouseSensitivity { get; set; } = 1f;
    public float Volume { get; set; } = 1f;
    public string PlayerName { get; private set; } = "Player";
    private const string PLAYER_NAME_KEY = "PlayerName";

    private const string MOUSE_SENS_KEY = "MouseSensitivity";
    private const string VOLUME_KEY = "Volume";

    // Audio iestatījumu īpašības
    private float musicVolume = 1f;
    private float sfxVolume = 1f;
    private float masterVolume = 1f;

    //  UI slaideri audio iestatījumiem
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
        Debug.Log("SettingsManager: Atiestatām uz noklusējuma vērtībām...");
        
        // Atiestatīt uz noklusējuma vērtībām
        MouseSensitivity = 1f;
        Volume = 1f;
        Screen.fullScreen = true;
        PlayerName = "Player";
        
        // Atiestatīt audio iestatījumus uz noklusējuma vērtībām
        musicVolume = 1f;
        sfxVolume = 1f;
        masterVolume = 1f;
        
        // Saglabāt noklusējuma vērtības
        SaveSettings();
        
        Debug.Log($"SettingsManager: Atiestatīšana pabeigta - Spēlētāja vārds: {PlayerName}");
    }

    public void LoadSettings()
    {
        // Ielādēt spēlētāja vārdu
        PlayerName = PlayerPrefs.GetString("PlayerName", "Player");
        
        // Ielādēt vienu skaļuma iestatījumu
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        
        Debug.Log($"SettingsManager: Ielādēti iestatījumi - Vārds: {PlayerName}, Skaļums: {masterVolume:F2}");
        
        // Pielietot audio iestatījumus pēc ielādes
        StartCoroutine(ApplyAudioSettingsDelayed());
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MOUSE_SENS_KEY, MouseSensitivity);
        PlayerPrefs.SetFloat(VOLUME_KEY, Volume);
        PlayerPrefs.SetString(PLAYER_NAME_KEY, PlayerName);

        //  Saglabāt audio iestatījumus
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);

        PlayerPrefs.Save();
    }

    //  Metode galvenā skaļuma iestatīšanai
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        
        // Nekavējoties pielietot skaļumu AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(masterVolume);
            Debug.Log($"SettingsManager: Iestatīts galvenais skaļums uz {masterVolume:F2} caur AudioManager");
        }
        else
        {
            Debug.LogWarning("SettingsManager: AudioManager.Instance ir null, izmantojam AudioListener rezerves variantu");
            // Rezerves variants - AudioListener
            AudioListener.volume = masterVolume;
        }
        
        SaveSettings();
        Debug.Log($"SettingsManager: Galvenais skaļums iestatīts uz {masterVolume:F2}");
    }



    //  Metode, lai nodrošinātu, ka AudioManager sākotnēji ir ar pareiziem iestatījumiem
    public void ApplyAudioSettings()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(masterVolume);
            Debug.Log($"SettingsManager: Pielietots galvenais skaļums: {masterVolume:F2}");
        }
        else
        {
            AudioListener.volume = masterVolume;
            Debug.LogWarning("SettingsManager: Pielietots skaļums caur AudioListener rezerves variantu");
        }
    }

    //  Īpašību piekļuves metodes lietotāja saskarnei
    public float MasterVolume => masterVolume;

    //  Pielietot audio iestatījumus ar aizturi, lai nodrošinātu, ka AudioManager ir gatavs
    private System.Collections.IEnumerator ApplyAudioSettingsDelayed()
    {
        // Gaidīt, līdz AudioManager tiek inicializēts
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
            Debug.LogWarning("SettingsManager: AudioManager nav atrasts pēc taimauta, iestatījumi var nebūt pielietoti");
        }
    }

    //  Metode slaideru klausītāju uzstādīšanai
    public void SetupAudioSliderListeners()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveAllListeners();
            musicSlider.onValueChanged.AddListener(SetMasterVolume);
            musicSlider.value = masterVolume;
            Debug.Log("SettingsManager: Uzstādīts mūzikas slaidera klausītājs");
        }
        
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.AddListener(SetMasterVolume);
            sfxSlider.value = masterVolume;
            Debug.Log("SettingsManager: Uzstādīts SFX slaidera klausītājs");
        }
        
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            masterVolumeSlider.value = masterVolume;
            Debug.Log("SettingsManager: Uzstādīts galvenā skaļuma slaidera klausītājs");
        }
    }
}
