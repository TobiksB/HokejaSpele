using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    [SerializeField] private GameObject puckPrefab;
    [SerializeField] private Transform puckSpawnPoint;
    private GameObject currentPuck;

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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnPuck();
        }
    }

    private void SpawnPuck()
    {
        if (currentPuck != null)
        {
            Destroy(currentPuck);
        }

        Vector3 spawnPos = puckSpawnPoint != null ? puckSpawnPoint.position : Vector3.zero;
        currentPuck = Instantiate(puckPrefab, spawnPos, Quaternion.identity);
        
        // Ensure all components are initialized before spawning
        var puckComponent = currentPuck.GetComponent<Puck>();
        if (puckComponent != null)
        {
            puckComponent.enabled = true;
        }

        var networkObject = currentPuck.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn(true);
        }
    }
}
