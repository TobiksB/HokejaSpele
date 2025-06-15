using System;
using System.Collections.Generic;

// Klase, kas reprezentē spēles uzgaidāmās telpas (lobby) datus
// Tā glabā informāciju par telpu, spēlētājiem, spēles režīmu un statusu
[System.Serializable]
public class LobbyData
{
    public string LobbyCode; // Unikāls telpas kods pievienošanai
    public List<LobbyPlayerData> Players = new List<LobbyPlayerData>(); // Spēlētāju saraksts telpā
    public string GameMode; // Izvēlētais spēles režīms
    public int MaxPlayers; // Maksimālais spēlētāju skaits telpā
    
    // Pievieno jaunu spēlētāju telpai
    public void AddPlayer(LobbyPlayerData player)
    {
        if (player != null && !Players.Contains(player))
        {
            Players.Add(player);
        }
    }
    
    // Izņem spēlētāju no telpas pēc vārda
    public void RemovePlayer(string playerName)
    {
        Players.RemoveAll(p => p.PlayerName == playerName);
    }
    
    // Atrod spēlētāju telpā pēc vārda
    public LobbyPlayerData GetPlayer(string playerName)
    {
        return Players.Find(p => p.PlayerName == playerName);
    }
    
    // Atgriež spēlētāju skaitu noteiktā komandā
    public int GetTeamCount(bool isBlueTeam)
    {
        return Players.FindAll(p => p.IsBlueTeam == isBlueTeam).Count;
    }
    
    // Atgriež spēlētāju skaitu, kas ir gatavi sākt spēli
    public int GetReadyCount()
    {
        return Players.FindAll(p => p.IsReady).Count;
    }
    
    // Pārbauda, vai var sākt spēli
    // Nepieciešami vismaz 2 spēlētāji, vismaz 2 ir gatavi, un vismaz 1 katrā komandā
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

    // Paplašināti lobby parametri, kas tiek izmantoti ar Unity Relay un Lobby servisu
    public string lobbyId; // Unity Lobby servisa piešķirtais ID
    public string lobbyCode; // Kods pievienošanai (dublē LobbyCode, bet nepieciešams savietojamībai)
    public string lobbyName; // Telpas nosaukums
    public int maxPlayers; // Maksimālais spēlētāju skaits (dublē MaxPlayers)
    public int currentPlayers; // Pašreizējais spēlētāju skaits
    public string gameMode; // Spēles režīms (dublē GameMode)
    public bool isPrivate; // Vai telpa ir privāta (redzama tikai ar kodu)
    public string hostName; // Telpas veidotāja vārds
    public List<LobbyPlayerData> players; // Spēlētāju saraksts (dublē Players)

    // Noklusējuma konstruktors
    public LobbyData()
    {
        players = new List<LobbyPlayerData>();
    }

    // Konstruktors ar visiem parametriem
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

// Uzskaitījums ar pieejamajiem spēles režīmiem
public enum GameMode
{
    None, // Nav izvēlēts režīms
    Training, // Treniņa režīms
    Mode2v2, // 2 pret 2 spēlētāju režīms
    Mode4v4 // 4 pret 4 spēlētāju režīms
}
