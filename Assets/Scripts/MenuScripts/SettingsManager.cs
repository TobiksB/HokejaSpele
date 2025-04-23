using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // Settings properties
    public float MouseSensitivity { get; set; } = 1f;
    public float Volume { get; set; } = 1f;

    private const string MOUSE_SENS_KEY = "MouseSensitivity";
    private const string VOLUME_KEY = "Volume";

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

    private void LoadSettings()
    {
        MouseSensitivity = PlayerPrefs.GetFloat(MOUSE_SENS_KEY, 1f);
        Volume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        
        // Apply loaded settings
        AudioListener.volume = Volume;
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MOUSE_SENS_KEY, MouseSensitivity);
        PlayerPrefs.SetFloat(VOLUME_KEY, Volume);
        PlayerPrefs.Save();

        // Apply settings
        AudioListener.volume = Volume;
    }

    public void ResetToDefaults()
    {
        MouseSensitivity = 1f;
        Volume = 1f;
        Screen.fullScreen = true;
        SaveSettings();
    }
}
