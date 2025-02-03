using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsScript : MonoBehaviour
{
    public GameObject settingsPanel;
    public GameObject mainMenuPanel;
    public Slider musicSlider;
    public Slider effectsSlider;
    // public Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Button closeButton;
    public Button openSettingsButton; // Add this line

    private void Start()
    {
        closeButton.onClick.AddListener(CloseSettingsPanel);
        openSettingsButton.onClick.AddListener(OpenSettingsPanel); // Add this line
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        // resolutionDropdown.onValueChanged.AddListener(SetResolution);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        effectsSlider.onValueChanged.AddListener(SetEffectsVolume);

        LoadSettings();

        // Populate resolution dropdown
        // resolutionDropdown.ClearOptions();
        // List<string> options = new List<string>();
        // int currentResolutionIndex = 0;
        // for (int i = 0; i < Screen.resolutions.Length; i++)
        // {
        //     Resolution res = Screen.resolutions[i];
        //     options.Add(res.width + " x " + res.height);
        //     if (res.width == Screen.currentResolution.width && res.height == Screen.currentResolution.height)
        //     {
        //         currentResolutionIndex = i;
        //     }
        // }
        // resolutionDropdown.AddOptions(options);
        // resolutionDropdown.value = currentResolutionIndex;
        // resolutionDropdown.RefreshShownValue();
    }

    private void LoadSettings()
    {
        musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1.0f);
        effectsSlider.value = PlayerPrefs.GetFloat("EffectsVolume", 1.0f);
        fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("MusicVolume", musicSlider.value);
        PlayerPrefs.SetFloat("EffectsVolume", effectsSlider.value);
        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void OpenSettingsPanel()
    {
        settingsPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
    }

    private void CloseSettingsPanel()
    {
        SaveSettings();
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    // private void SetResolution(int resolutionIndex)
    // {
    //     Resolution res = Screen.resolutions[resolutionIndex];
    //     Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    // }

    private void SetMusicVolume(float volume)
    {
        // Implement your music volume logic here
    }

    private void SetEffectsVolume(float volume)
    {
        // Implement your effects volume logic here
    }
}
