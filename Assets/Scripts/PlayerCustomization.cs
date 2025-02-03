using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class PlayerCustomization : MonoBehaviour
{
    public GameObject customizationPanel;
    public GameObject mainMenuPanel;
    public Button openCustomizationButton;
    public Button closeCustomizationButton;

    public CharacterBase characterBase; // Reference to the CharacterBase script

    public Slider skinColorSlider;
    public Dropdown hairStyleDropdown;
    public Dropdown beardStyleDropdown;
    public Dropdown eyebrowStyleDropdown;

    private bool ValidateComponents()
    {
        bool isValid = true;
        
        if (customizationPanel == null)
        {
            Debug.LogError("Customization Panel is not assigned!");
            isValid = false;
        }
        if (mainMenuPanel == null)
        {
            Debug.LogError("Main Menu Panel is not assigned!");
            isValid = false;
        }
        if (openCustomizationButton == null)
        {
            Debug.LogError("Open Customization Button is not assigned!");
            isValid = false;
        }
        if (closeCustomizationButton == null)
        {
            Debug.LogError("Close Customization Button is not assigned!");
            isValid = false;
        }
        if (skinColorSlider == null)
        {
            Debug.LogError("Skin Color Slider is not assigned!");
            isValid = false;
        }
        if (hairStyleDropdown == null)
        {
            Debug.LogError("Hair Style Dropdown is not assigned!");
            isValid = false;
        }
        if (beardStyleDropdown == null)
        {
            Debug.LogError("Beard Style Dropdown is not assigned!");
            isValid = false;
        }
        if (eyebrowStyleDropdown == null)
        {
            Debug.LogError("Eyebrow Style Dropdown is not assigned!");
            isValid = false;
        }
        if (characterBase == null)
        {
            Debug.LogError("Character Base is not assigned!");
            isValid = false;
        }
        
        return isValid;
    }

    private void Start()
    {
        if (!ValidateComponents())
        {
            Debug.LogError("Component validation failed. Please check the Inspector for missing references.");
            enabled = false;
            return;
        }

        // Initialize UI elements
        openCustomizationButton.onClick.AddListener(OpenCustomizationPanel);
        closeCustomizationButton.onClick.AddListener(CloseCustomizationPanel);
        customizationPanel.SetActive(false);

        // Initialize sliders and dropdowns
        skinColorSlider.onValueChanged.AddListener(ChangeSkinColor);
        hairStyleDropdown.onValueChanged.AddListener(ChangeHairStyle);
        beardStyleDropdown.onValueChanged.AddListener(ChangeBeardStyle);
        eyebrowStyleDropdown.onValueChanged.AddListener(ChangeEyebrowStyle);

        StartCoroutine(InitializeCustomization());
    }

    private IEnumerator InitializeCustomization()
    {
        // Wait for CharacterBase to be ready
        yield return new WaitForSeconds(0.1f); // Give other scripts time to initialize

        try
        {
            PopulateDropdown(hairStyleDropdown, characterBase.GetAvailableHairStyles());
            PopulateDropdown(beardStyleDropdown, characterBase.GetAvailableBeardStyles());
            PopulateDropdown(eyebrowStyleDropdown, characterBase.GetAvailableEyebrowStyles());
            StartCoroutine(LoadCustomizationWhenReady());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during initialization: {e.Message}");
        }
    }

    private IEnumerator LoadCustomizationWhenReady()
    {
        // Wait until character is initialized
        while (!characterBase.IsInitialized)
        {
            yield return null;
        }
        
        // Now load the settings
        LoadCustomizationSettings();
    }

    private void LoadCustomizationSettings()
    {
        skinColorSlider.value = PlayerPrefs.GetFloat("SkinColor", 1.0f);
        hairStyleDropdown.value = PlayerPrefs.GetInt("HairStyle", 0);
        beardStyleDropdown.value = PlayerPrefs.GetInt("BeardStyle", 0);
        eyebrowStyleDropdown.value = PlayerPrefs.GetInt("EyebrowStyle", 0);

        ChangeSkinColor(skinColorSlider.value);
        ChangeHairStyle(hairStyleDropdown.value);
        ChangeBeardStyle(beardStyleDropdown.value);
        ChangeEyebrowStyle(eyebrowStyleDropdown.value);
    }

    private void SaveCustomizationSettings()
    {
        PlayerPrefs.SetFloat("SkinColor", skinColorSlider.value);
        PlayerPrefs.SetInt("HairStyle", hairStyleDropdown.value);
        PlayerPrefs.SetInt("BeardStyle", beardStyleDropdown.value);
        PlayerPrefs.SetInt("EyebrowStyle", eyebrowStyleDropdown.value);
        PlayerPrefs.Save();
    }

    public void OpenCustomizationPanel()
    {
        customizationPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
    }

    public void CloseCustomizationPanel()
    {
        SaveCustomizationSettings();
        customizationPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void ChangeSkinColor(float value)
    {
        // Convert the slider value to a color (this is just an example, you might want to use a color picker instead)
        Color newColor = new Color(value, value, value);
        characterBase.ChangeSkinColor(newColor);
    }

    private void ChangeHairStyle(int index)
    {
        int newHairStyle = int.Parse(hairStyleDropdown.options[index].text);
        characterBase.ChangeHairstyle(newHairStyle);
    }

    private void ChangeBeardStyle(int index)
    {
        int newBeardStyle = int.Parse(beardStyleDropdown.options[index].text);
        characterBase.ChangeBeardstyle(newBeardStyle);
    }

    private void ChangeEyebrowStyle(int index)
    {
        int newEyebrowStyle = int.Parse(eyebrowStyleDropdown.options[index].text);
        characterBase.ChangeEyebrowstyle(newEyebrowStyle);
    }

    private void PopulateDropdown(Dropdown dropdown, List<string> options)
    {
        if (dropdown != null && options != null)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
        }
    }
}