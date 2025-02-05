using UnityEngine;
using System.Linq;

public class PlayerColorLoader : MonoBehaviour
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
    [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
    [SerializeField] private Transform spawnPoint; // Assign this in the inspector

    void Start()
    {
        Debug.Log($"[PlayerColorLoader] Checking for player prefab... Current value: {(SelectedPlayer.playerPrefab != null ? SelectedPlayer.playerPrefab.name : "null")}");
        
        if (SelectedPlayer.playerPrefab == null)
        {
            Debug.LogError("[PlayerColorLoader] No player prefab selected! Returning to main menu...");
            // Wait one frame before loading main menu to ensure proper error logging
            StartCoroutine(LoadMainMenuDelayed());
            return;
        }
        SpawnPlayer();
    }

    private System.Collections.IEnumerator LoadMainMenuDelayed()
    {
        yield return null;
        FindObjectOfType<SceneLoader>()?.LoadMainMenu();
    }

    void SpawnPlayer()
    {
        if (spawnPoint == null)
        {
            Debug.LogError("Spawn point is not set! Using default position.");
            spawnPoint = transform;
        }

        GameObject player = Instantiate(SelectedPlayer.playerPrefab, 
                                     spawnPoint.position, 
                                     spawnPoint.rotation);
        player.tag = "Player"; // Add this line
        
        Debug.Log($"Player {player.name} spawned at {spawnPoint.position}");
        
        // Get all renderers and their materials
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.materials)
                {
                    if (mat != null)
                    {
                        // Load saved color
                        float r = PlayerPrefs.GetFloat("PlayerColorR", 1f);
                        float g = PlayerPrefs.GetFloat("PlayerColorG", 1f);
                        float b = PlayerPrefs.GetFloat("PlayerColorB", 1f);
                        mat.color = new Color(r, g, b);
                    }
                }
            }
        }
        else
        {
            Debug.LogError("Missing required references for player spawn! Check if player prefab is selected in the main menu.");
        }
    }

    void LoadPlayerColors()
    {
        float r = PlayerPrefs.GetFloat("PlayerColorR", 1f);
        float g = PlayerPrefs.GetFloat("PlayerColorG", 1f);
        float b = PlayerPrefs.GetFloat("PlayerColorB", 1f);
        
        Color savedColor = new Color(r, g, b);

        Material[] materials = { 
            helmetMaterial, 
            jerseyMaterial, 
            legsMaterial, 
            shortsMaterial, 
            glove1Material, 
            glove2Material, 
            skateMaterial, 
            skate1Material 
        };

        foreach (Material mat in materials)
        {
            if (mat != null)
            {
                mat.color = savedColor;
            }
        }
    }
}
