using UnityEngine;
using UnityEngine.UI;

public class PlayerCreation : MonoBehaviour
{
    [Header("Equipment Materials")]
    [SerializeField] private Material helmetMaterial;
    [SerializeField] private Material jerseyMaterial;
    [SerializeField] private Material legsMaterial;
    [SerializeField] private Material shortsMaterial;
    [SerializeField] private Material glove1Material;
    [SerializeField] private Material glove2Material;
    [SerializeField] private Material skateMaterial;
    [SerializeField] private Material skate1Material;

    [Header("Color Sliders")]
    [SerializeField] private Slider redSlider;
    [SerializeField] private Slider greenSlider;
    [SerializeField] private Slider blueSlider;

    [SerializeField] private GameObject playerPrefab; // Assign your player prefab here

    private Material[] allMaterials;

    void Start()
    {
        // Ensure PlayerData exists
        if (PlayerData.Instance == null)
        {
            GameObject playerDataObj = new GameObject("PlayerData");
            playerDataObj.AddComponent<PlayerData>();
        }

        // Initialize materials array
        allMaterials = new Material[] 
        { 
            helmetMaterial, 
            jerseyMaterial, 
            legsMaterial, 
            shortsMaterial, 
            glove1Material, 
            glove2Material, 
            skateMaterial, 
            skate1Material 
        };

        // Add listeners to sliders
        redSlider.onValueChanged.AddListener(delegate { UpdateColors(); });
        greenSlider.onValueChanged.AddListener(delegate { UpdateColors(); });
        blueSlider.onValueChanged.AddListener(delegate { UpdateColors(); });

        // Initial color update
        UpdateColors();
    }

    void UpdateColors()
    {
        Color newColor = new Color(redSlider.value, greenSlider.value, blueSlider.value);
        
        foreach (Material mat in allMaterials)
        {
            if (mat != null)
            {
                mat.color = newColor;
            }
        }
    }

    public void SavePlayerColors()
    {
        // Save to PlayerPrefs as before
        PlayerPrefs.SetFloat("PlayerColorR", redSlider.value);
        PlayerPrefs.SetFloat("PlayerColorG", greenSlider.value);
        PlayerPrefs.SetFloat("PlayerColorB", blueSlider.value);
        PlayerPrefs.Save();

        // Store in PlayerData
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.playerColor = new Color(redSlider.value, greenSlider.value, blueSlider.value);
            PlayerData.Instance.playerPrefab = playerPrefab;
        }
    }
}
