using UnityEngine;

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
        PlayerName = PlayerPrefs.GetString(PLAYER_NAME_KEY, "Player");

        // Apply loaded settings
        AudioListener.volume = Volume;
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MOUSE_SENS_KEY, MouseSensitivity);
        PlayerPrefs.SetFloat(VOLUME_KEY, Volume);
        PlayerPrefs.SetString(PLAYER_NAME_KEY, PlayerName);
        PlayerPrefs.Save();
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
        MouseSensitivity = 1f;
        Volume = 1f;
        Screen.fullScreen = true;
        PlayerName = "Player";
        SaveSettings();
    }
}
