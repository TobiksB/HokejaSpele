using UnityEngine;
using Unity.Netcode;

public class GameConfig2v2 : MonoBehaviour
{
    public static class Config
    {
        public const int PLAYERS_PER_TEAM = 2;
        public const float RINK_SCALE = 1.0f;
        public const float GAME_TIME = 300f; // 5 minutes
        public const float QUARTER_LENGTH = 300f; // 5 minutes per quarter
        public const int TOTAL_QUARTERS = 3;
        public static bool DEBUG_MODE = true;  // Add this line
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
        // In debug mode, always approve connections
        if (Config.DEBUG_MODE)
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            return;
        }
        
        response.Approved = NetworkManager.Singleton.ConnectedClientsIds.Count < 4;
        response.CreatePlayerObject = true;
    }
}
