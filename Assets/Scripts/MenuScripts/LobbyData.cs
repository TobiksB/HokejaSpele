using System;
using System.Collections.Generic;

[System.Serializable]
public class LobbyData
{
    public string LobbyCode;
    public List<LobbyPlayerData> Players = new List<LobbyPlayerData>();
    public string GameMode;
    public int MaxPlayers;
    
    public void AddPlayer(LobbyPlayerData player)
    {
        if (player != null && !Players.Contains(player))
        {
            Players.Add(player);
        }
    }
    
    public void RemovePlayer(string playerName)
    {
        Players.RemoveAll(p => p.PlayerName == playerName);
    }
    
    public LobbyPlayerData GetPlayer(string playerName)
    {
        return Players.Find(p => p.PlayerName == playerName);
    }
    
    public int GetTeamCount(bool isBlueTeam)
    {
        return Players.FindAll(p => p.IsBlueTeam == isBlueTeam).Count;
    }
    
    public int GetReadyCount()
    {
        return Players.FindAll(p => p.IsReady).Count;
    }
    
    public bool CanStartMatch()
    {
        int readyCount = GetReadyCount();
        int totalPlayers = Players.Count;
        int blueTeam = GetTeamCount(true);
        int redTeam = GetTeamCount(false);
        
        return totalPlayers >= 2 && 
               readyCount >= 2 && 
               blueTeam >= 1 && 
               redTeam >= 1;
    }

    public string lobbyId;
    public string lobbyCode;
    public string lobbyName;
    public int maxPlayers;
    public int currentPlayers;
    public string gameMode;
    public bool isPrivate;
    public string hostName;
    public List<LobbyPlayerData> players;

    public LobbyData()
    {
        players = new List<LobbyPlayerData>();
    }

    public LobbyData(string id, string code, string name, int max, string mode, bool priv, string host)
    {
        lobbyId = id;
        lobbyCode = code;
        lobbyName = name;
        maxPlayers = max;
        gameMode = mode;
        isPrivate = priv;
        hostName = host;
        players = new List<LobbyPlayerData>();
        currentPlayers = 0;
    }
}

public enum GameMode
{
    None,
    Training,
    Mode2v2,
    Mode4v4
}
