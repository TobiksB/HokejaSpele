using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;

    private void Start()
    {
        LoadCurrentSettings();
        SetupListeners();
    }

    private void LoadCurrentSettings()
    {
        if (SettingsManager.Instance != null)
        {
            playerNameInput.text = SettingsManager.Instance.PlayerName;
            mouseSensitivitySlider.value = SettingsManager.Instance.MouseSensitivity;
            volumeSlider.value = SettingsManager.Instance.Volume;
        }
    }

    private void SetupListeners()
    {
        if (applyButton)
            applyButton.onClick.AddListener(ApplySettings);
        
        if (resetButton)
            resetButton.onClick.AddListener(ResetSettings);
    }

    private void ApplySettings()
    {
        if (SettingsManager.Instance == null) return;

        SettingsManager.Instance.SetPlayerName(playerNameInput.text);
        SettingsManager.Instance.MouseSensitivity = mouseSensitivitySlider.value;
        SettingsManager.Instance.Volume = volumeSlider.value;
        SettingsManager.Instance.SaveSettings();
    }

    private void ResetSettings()
    {
        if (SettingsManager.Instance == null) return;

        SettingsManager.Instance.ResetToDefaults();
        LoadCurrentSettings();
    }
}
