using UnityEngine;
using Unity.Netcode;

public class GameConfig4v4 : MonoBehaviour
{
    public static class Config
    {
        public const int PLAYERS_PER_TEAM = 4;
        public const float RINK_SCALE = 1.5f;
        public const float GAME_TIME = 400f; // ~6.5 minutes
    }

    void Awake()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = NetworkManager.Singleton.ConnectedClientsIds.Count < 8;
        response.CreatePlayerObject = true;
    }
}
