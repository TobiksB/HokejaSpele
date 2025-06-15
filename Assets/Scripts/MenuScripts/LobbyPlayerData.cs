//
// [System.Serializable]
public class LobbyPlayerData
{
    public string PlayerId;      // Spēlētāja unikālais identifikators
    public string PlayerName;    // Spēlētāja redzamais vārds
    public string Team;          // Spēlētāja komanda ("Red" vai "Blue")
    public bool IsReady;         // Vai spēlētājs ir atzīmējis gatavību
    public bool IsLocalPlayer;   // Vai šis ir lokālais spēlētājs (pašreizējais lietotājs)
    
    // Pievienotā IsBlueTeam īpašība, kas automātiski nosaka, vai spēlētājs ir zilajā komandā
    public bool IsBlueTeam => Team == "Blue";
    
    // Noklusējuma konstruktors
    public LobbyPlayerData()
    {
        PlayerId = "";
        PlayerName = "";
        Team = "";
        IsReady = false;
        IsLocalPlayer = false;
    }
    
    // Konstruktors ar parametriem
    public LobbyPlayerData(string playerId, string playerName, string team, bool isReady, bool isLocalPlayer = false)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        Team = team;
        IsReady = isReady;
        IsLocalPlayer = isLocalPlayer;
    }
}