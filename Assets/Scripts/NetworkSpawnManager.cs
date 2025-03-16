using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkSpawnManager : MonoBehaviour
{
    public static NetworkSpawnManager Instance { get; private set; }
    
    [SerializeField] private Transform[] spawnPoints;
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
    }

    private void Start()
    {
        availableSpawnPoints = new List<Transform>(spawnPoints);
    }

    public Vector3 GetNextSpawnPoint()
    {
        if (availableSpawnPoints.Count == 0)
        {
            availableSpawnPoints = new List<Transform>(spawnPoints);
        }

        int randomIndex = Random.Range(0, availableSpawnPoints.Count);
        Transform spawnPoint = availableSpawnPoints[randomIndex];
        availableSpawnPoints.RemoveAt(randomIndex);

        return spawnPoint.position;
    }
}
