//
// [System.Serializable]
public class LobbyPlayerData
{
    public string PlayerId;
    public string PlayerName;
    public string Team;
    public bool IsReady;
    public bool IsLocalPlayer;
    
    // Add the missing IsBlueTeam property
    public bool IsBlueTeam => Team == "Blue";
    
    public LobbyPlayerData()
    {
        PlayerId = "";
        PlayerName = "";
        Team = "";
        IsReady = false;
        IsLocalPlayer = false;
    }
    
    public LobbyPlayerData(string playerId, string playerName, string team, bool isReady, bool isLocalPlayer = false)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        Team = team;
        IsReady = isReady;
        IsLocalPlayer = isLocalPlayer;
    }
}