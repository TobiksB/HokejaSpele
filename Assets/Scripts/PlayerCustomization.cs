using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

    private void Start()
    {
        openCustomizationButton.onClick.AddListener(OpenCustomizationPanel);
        closeCustomizationButton.onClick.AddListener(CloseCustomizationPanel);

        if (customizationPanel != null)
        {
            customizationPanel.SetActive(false); // Ensure the panel is hidden at the start
        }

        // Add listeners to the sliders and dropdowns
        skinColorSlider.onValueChanged.AddListener(ChangeSkinColor);
        hairStyleDropdown.onValueChanged.AddListener(ChangeHairStyle);
        beardStyleDropdown.onValueChanged.AddListener(ChangeBeardStyle);
        eyebrowStyleDropdown.onValueChanged.AddListener(ChangeEyebrowStyle);

        // Populate dropdowns with available styles
        PopulateDropdown(hairStyleDropdown, characterBase.GetAvailableHairStyles());
        PopulateDropdown(beardStyleDropdown, characterBase.GetAvailableBeardStyles());
        PopulateDropdown(eyebrowStyleDropdown, characterBase.GetAvailableEyebrowStyles());
    }

    public void OpenCustomizationPanel()
    {
        customizationPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
    }

    public void CloseCustomizationPanel()
    {
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
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
    }
}