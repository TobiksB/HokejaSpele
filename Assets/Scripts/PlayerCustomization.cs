using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class PlayerCustomization : MonoBehaviour
{
    public GameObject playerPrefab; 
    public GameObject customizationPanel;
    public GameObject mainMenuPanel;
    public Button openCustomizationButton;
    public Button closeCustomizationButton;
    [SerializeField] private Button startGameButton; 

    public Slider skinColorSlider;
    public Dropdown hairStyleDropdown;
    public Dropdown beardStyleDropdown;
    public Dropdown eyebrowStyleDropdown;

    private bool ValidateComponents()
    {
        bool isValid = true;
        
        if (playerPrefab == null)
        {
            Debug.LogError("Player Prefab is not assigned!");
            return false;
        }

        // Verify all required components exist on the prefab
        HockeyPlayer hockeyPlayer = playerPrefab.GetComponent<HockeyPlayer>();
        
        if (hockeyPlayer == null)
        {
            Debug.LogError("HockeyPlayer component missing on player prefab!");
            return false;
        }

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

        // Ensure the prefab is in a Resources folder
        if (playerPrefab != null)
        {
            // Move or copy your player prefab to a Resources folder in your project
            string prefabPath = "Prefabs/Player"; // Adjust this path to match your Resources folder structure
            GameObject resourcesPrefab = Resources.Load<GameObject>(prefabPath);
            if (resourcesPrefab != null)
            {
                var instance = SelectedPlayer.Instance; // Ensure instance exists
                SelectedPlayer.playerPrefab = resourcesPrefab;
                Debug.Log($"[PlayerCustomization] Set player prefab from Resources: {resourcesPrefab.name}");
            }
            else
            {
                Debug.LogError($"[PlayerCustomization] Could not load player prefab from Resources at path: {prefabPath}");
            }
        }

        // Ensure SelectedPlayer component exists in scene
        if (FindObjectOfType<SelectedPlayer>() == null)
        {
            new GameObject("SelectedPlayer").AddComponent<SelectedPlayer>();
        }

        // Store the prefab for later use
        SelectedPlayer.playerPrefab = playerPrefab;
        Debug.Log($"Player prefab {playerPrefab.name} set in PlayerCustomization");

        // Initialize UI elements
        openCustomizationButton.onClick.AddListener(OpenCustomizationPanel);
        closeCustomizationButton.onClick.AddListener(CloseCustomizationPanel);
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(() => {
                SaveCustomizationSettings();
                FindObjectOfType<SceneLoader>()?.LoadGameScene();
            });
        }
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
        // Wait for initialization
        yield return new WaitForSeconds(0.1f); // Give other scripts time to initialize

        try
        {
            // Populate dropdowns with dummy data or other sources
            PopulateDropdown(hairStyleDropdown, new List<string> { "Style1", "Style2" });
            PopulateDropdown(beardStyleDropdown, new List<string> { "Style1", "Style2" });
            PopulateDropdown(eyebrowStyleDropdown, new List<string> { "Style1", "Style2" });
            StartCoroutine(LoadCustomizationWhenReady());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during initialization: {e.Message}");
        }
    }

    private IEnumerator LoadCustomizationWhenReady()
    {
        // Wait until initialization is complete
        yield return new WaitForSeconds(0.1f);

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
        // Ensure prefab is set before scene transition
        if (SelectedPlayer.playerPrefab == null)
        {
            SelectedPlayer.playerPrefab = playerPrefab;
        }
        
        PlayerCustomizationData.Current.skinColorValue = skinColorSlider.value;
        PlayerCustomizationData.Current.hairStyle = hairStyleDropdown.value;
        PlayerCustomizationData.Current.beardStyle = beardStyleDropdown.value;
        PlayerCustomizationData.Current.eyebrowStyle = eyebrowStyleDropdown.value;
        PlayerCustomizationData.Current.SaveToPrefs();
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
        // Apply the color to the player
    }

    private void ChangeHairStyle(int index)
    {
        int newHairStyle = int.Parse(hairStyleDropdown.options[index].text);
        // Apply the hairstyle to the player
    }

    private void ChangeBeardStyle(int index)
    {
        int newBeardStyle = int.Parse(beardStyleDropdown.options[index].text);
        // Apply the beard style to the player
    }

    private void ChangeEyebrowStyle(int index)
    {
        int newEyebrowStyle = int.Parse(eyebrowStyleDropdown.options[index].text);
        // Apply the eyebrow style to the player
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