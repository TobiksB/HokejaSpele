using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkSpawnManager : MonoBehaviour
{
    public static NetworkSpawnManager Instance { get; private set; }
    
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnHeight = 0.69f; // Updated player spawn height
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(0f, 0.69f, 0f); // Raised slightly off ground
    [SerializeField] private float minSpawnHeight = -1f; // More lenient height check
    [SerializeField] private float validationRadius = 50f; // Increased radius
    private List<Transform> availableSpawnPoints;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        InitializeSpawnPoints();
    }

    private void Start()
    {
        ValidateSpawnPoints();
    }

    private void ValidateSpawnPoints()
    {
        availableSpawnPoints = new List<Transform>();
        
        // Add all valid spawn points
        foreach (var point in spawnPoints)
        {
            if (point != null)
            {
                if (IsValidSpawnPoint(point.position))
                {
                    availableSpawnPoints.Add(point);
                    Debug.Log($"Valid spawn point added: {point.name} at {point.position}");
                }
            }
        }

        // If no valid points, create default spawn points
        if (availableSpawnPoints.Count == 0)
        {
            Debug.Log("Creating default spawn points");
            CreateDefaultSpawnPoints();
        }
    }

    private bool IsValidSpawnPoint(Vector3 position)
    {
        // Simplified validation - just make sure it's not too low or too far
        if (position.y < minSpawnHeight) 
        {
            Debug.LogWarning($"Spawn point too low: {position}");
            return false;
        }

        // Check distance from center
        float distanceFromCenter = new Vector3(position.x, 0, position.z).magnitude;
        if (distanceFromCenter > validationRadius)
        {
            Debug.LogWarning($"Spawn point too far from center: {position}");
            return false;
        }

        // Don't check for obstacles anymore - trust the designer's placement
        return true;
    }

    private void CreateDefaultSpawnPoints()
    {
        GameObject spawnPoints = new GameObject("DefaultSpawnPoints");
        
        // Team-based spawn positions on each side of the rink
        Vector3[] positions = new Vector3[]
        {
            new Vector3(-15f, 0.69f, 0f),  // Left team spawn 1
            new Vector3(-13f, 0.69f, 2f),  // Left team spawn 2
            new Vector3(15f, 0.69f, 0f),   // Right team spawn 1
            new Vector3(13f, 0.69f, -2f)   // Right team spawn 2
        };

        // Create spawn points with exact positions
        foreach (Vector3 pos in positions)
        {
            GameObject spawnPoint = new GameObject($"SpawnPoint_{availableSpawnPoints.Count}");
            spawnPoint.transform.parent = spawnPoints.transform;
            spawnPoint.transform.position = pos;
            availableSpawnPoints.Add(spawnPoint.transform);
            Debug.Log($"Created spawn point at {pos}");
        }
    }

    private void InitializeSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned, creating default spawn points");
            CreateDefaultSpawnPoints();
        }
        else
        {
            // Force all spawn points to correct height
            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                {
                    Vector3 pos = point.position;
                    pos.y = spawnHeight;
                    point.position = pos;
                }
            }
        }
    }

    public Vector3 GetNextSpawnPoint()
    {
        if (availableSpawnPoints == null || availableSpawnPoints.Count == 0)
        {
            CreateDefaultSpawnPoints();
        }

        // Get team-appropriate spawn point
        int spawnIndex = availableSpawnPoints.Count >= 4 
            ? (NetworkManager.Singleton.IsHost ? 0 : 2) // Host spawns left, client spawns right
            : 0;

        Transform spawnPoint = availableSpawnPoints[spawnIndex];
        return new Vector3(spawnPoint.position.x, 0.69f, spawnPoint.position.z);
    }
}
