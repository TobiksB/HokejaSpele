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
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (PlayerData.Instance != null && PlayerData.Instance.playerPrefab != null && spawnPoint != null)
        {
            GameObject player = Instantiate(PlayerData.Instance.playerPrefab, 
                                         spawnPoint.position, 
                                         spawnPoint.rotation);
            
            // Get all renderers and their materials
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
            Material[] materials = renderers.SelectMany(r => r.materials).ToArray();

            foreach (Material mat in materials)
            {
                if (mat != null)
                {
                    mat.color = PlayerData.Instance.playerColor;
                }
            }
        }
        else
        {
            Debug.LogError("Missing required references for player spawn!");
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
