using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Authentication;
using System.Collections.Generic;
using System.Threading.Tasks;
using HockeyGame.Game;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }
    private Lobby currentLobby;
    private Dictionary<string, string> playerTeams = new Dictionary<string, string>(); // PlayerID -> Team
    private Dictionary<string, string> playerNames = new Dictionary<string, string>(); // PlayerID -> PlayerName
    private float lobbyUpdateTimer;
    private const float LOBBY_UPDATE_INTERVAL = 1.5f;
    private GameMode selectedGameMode;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("LobbyManager initialized successfully.");
        }
        else
        {
            Debug.LogWarning("Duplicate LobbyManager detected. Destroying this instance.");
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
    }

    private async Task InitializeUnityServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }
    }

    public void SetGameMode(GameMode mode)
    {
        selectedGameMode = mode;
        Debug.Log($"Game mode set to: {mode}");
    }

    public async Task<string> CreateLobby(int maxPlayers)
    {
        try
        {
            Debug.Log("Initializing Unity Services...");
            await InitializeUnityServices();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"Creating lobby for game mode: {selectedGameMode}");
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = true,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    { "GameMode", new DataObject(DataObject.VisibilityOptions.Public, selectedGameMode.ToString()) },
                    { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "false") }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync("Hockey Game", maxPlayers, options);
            Debug.Log($"Lobby created successfully with code: {currentLobby.LobbyCode}");

            // Add the host player to the playerNames dictionary
            string playerId = AuthenticationService.Instance.PlayerId;
            playerNames[playerId] = "Player1";

            UpdatePlayerListUI();

            return currentLobby.LobbyCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            return null;
        }
    }

    public async Task JoinLobby(string lobbyCode)
    {
        try
        {
            await InitializeUnityServices();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

            // Assign a default name if not already set
            string playerId = AuthenticationService.Instance.PlayerId;
            if (!playerNames.ContainsKey(playerId))
            {
                int playerNumber = playerNames.Count + 1;
                playerNames[playerId] = $"Player{playerNumber}";
            }

            UpdatePlayerListUI();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
        }
    }

    private void UpdatePlayerListUI()
    {
        var lobbyPanel = GameObject.FindFirstObjectByType<LobbyPanelManager>();
        if (lobbyPanel != null)
        {
            var players = new List<LobbyPlayerData>();
            foreach (var kvp in playerNames)
            {
                players.Add(new LobbyPlayerData
                {
                    PlayerName = kvp.Value,
                    IsBlueTeam = playerTeams.ContainsKey(kvp.Key) && playerTeams[kvp.Key] == "Blue",
                    IsReady = false
                });
            }

            lobbyPanel.UpdatePlayerList(players);
        }
    }

    public void SetPlayerTeam(string playerId, string team)
    {
        if (playerTeams.ContainsKey(playerId))
        {
            playerTeams[playerId] = team;
        }
        else
        {
            playerTeams.Add(playerId, team);
        }

        UpdateLobbyData();
    }

    private async void UpdateLobbyData()
    {
        if (currentLobby == null) return;

        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "PlayerTeams", new DataObject(DataObject.VisibilityOptions.Member, SerializePlayerTeams()) }
                }
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to update lobby data: {e.Message}");
        }
    }

    private string SerializePlayerTeams()
    {
        // Manually populate the SerializableDictionary
        SerializableDictionary<string, string> serializableDict = new SerializableDictionary<string, string>();
        foreach (var kvp in playerTeams)
        {
            serializableDict.Add(kvp.Key, kvp.Value);
        }
        return JsonUtility.ToJson(serializableDict);
    }

    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") }
            }
        };
    }

    private async void HandleLobbyHeartbeat()
    {
        if (currentLobby == null) return;

        lobbyUpdateTimer -= Time.deltaTime;
        if (lobbyUpdateTimer <= 0f)
        {
            lobbyUpdateTimer = LOBBY_UPDATE_INTERVAL;
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send heartbeat: {e.Message}");
            }
        }
    }

    public void StartMatch()
    {
        if (currentLobby == null) return;

        // Notify all players to load the game scene
        if (selectedGameMode == GameMode.Mode2v2)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene2v2");
        }
        else if (selectedGameMode == GameMode.Mode4v4)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene4v4");
        }
    }
}

// Add this class for SerializableDictionary
[System.Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> keys = new List<TKey>();
    [SerializeField] private List<TValue> values = new List<TValue>();

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (KeyValuePair<TKey, TValue> pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        Clear();
        for (int i = 0; i < keys.Count; i++)
        {
            Add(keys[i], values[i]);
        }
    }
}
