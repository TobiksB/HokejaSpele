using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkSpawnManager : NetworkBehaviour
{
    public static NetworkSpawnManager Instance { get; private set; }

    
    private Dictionary<ulong, string> playerTeams = new Dictionary<ulong, string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("NetworkSpawnManager: Instance created (NOTE: Spawning is now handled by GameNetworkManager's ConnectionApprovalCheck)");
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    // Get the team for a player based on their client ID
    public string GetPlayerTeam(ulong clientId)
    {
        if (playerTeams.TryGetValue(clientId, out string team))
        {
            return team;
        }

        // Assign team based on client ID
        team = (clientId % 2 == 0) ? "Red" : "Blue";
        playerTeams[clientId] = team;
        Debug.Log($"NetworkSpawnManager: Assigned {team} team to client {clientId}");
        
        return team;
    }

}