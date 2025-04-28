using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HockeyGame.Game;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }
    private Lobby currentLobby;
    private Dictionary<string, string> playerTeams = new Dictionary<string, string>(); // PlayerID -> Team
    private Dictionary<string, string> playerNames = new Dictionary<string, string>(); // PlayerID -> PlayerName
    private Dictionary<string, bool> playerReadyStates = new Dictionary<string, bool>(); // Add this line
    private float heartbeatTimer;
    private float lobbyPollTimer;
    private const float LOBBY_POLL_INTERVAL = 1.5f;
    private GameMode selectedGameMode;
    private List<string> chatMessages = new List<string>();
    private const int MAX_CHAT_MESSAGES = 50;

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
        PollLobbyForUpdates();
    }

    private async void Start()
    {
        try
        {
            Debug.Log("Initializing Unity Services...");
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            Debug.Log($"Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
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
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Player not signed in. Signing in...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (LobbyPanelManager.Instance == null)
            {
                Debug.LogError("LobbyPanelManager.Instance is null! Ensure LobbyPanel is active in the scene.");
                return null;
            }

            // Add the host player to the playerNames dictionary with the saved name
            string playerId = AuthenticationService.Instance.PlayerId;
            string playerName = SettingsManager.Instance != null ? 
                SettingsManager.Instance.PlayerName : 
                $"Player{playerNames.Count + 1}";
            playerNames[playerId] = playerName;

            Debug.Log($"Creating lobby with player name: {playerName}");
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

            // Try to update UI immediately
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
            Debug.Log("Initializing Unity Services...");
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            string playerId = AuthenticationService.Instance.PlayerId;
            string playerName = SettingsManager.Instance != null ? 
                SettingsManager.Instance.PlayerName : 
                $"Player{playerNames.Count + 1}";

            // Create player data before joining
            JoinLobbyByCodeOptions joinOptions = new JoinLobbyByCodeOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                        { "Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "") },
                        { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") }
                    }
                }
            };

            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);
            
            // Add the joined player to local data
            playerNames[playerId] = playerName;
            Debug.Log($"Successfully joined lobby. Players in lobby: {currentLobby.Players.Count}");

            // Sync all current players
            foreach (Player player in currentLobby.Players)
            {
                string pId = player.Id;
                if (player.Data != null)
                {
                    if (player.Data.ContainsKey("PlayerName"))
                    {
                        playerNames[pId] = player.Data["PlayerName"].Value;
                    }
                    if (player.Data.ContainsKey("Team"))
                    {
                        playerTeams[pId] = player.Data["Team"].Value;
                    }
                    if (player.Data.ContainsKey("IsReady"))
                    {
                        playerReadyStates[pId] = player.Data["IsReady"].Value == "true";
                    }
                    Debug.Log($"Synced player: {playerNames[pId]} (ID: {pId})");
                }
            }

            UpdatePlayerListUI();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
        }
    }

    public void RefreshPlayerList()
    {
        UpdatePlayerListUI();
    }

    public bool CanStartMatch()
    {
        if (currentLobby == null) return false;

        int readyPlayers = 0;
        int requiredPlayers = selectedGameMode == GameMode.Mode2v2 ? 4 : 8;

        foreach (var kvp in playerReadyStates)
        {
            if (kvp.Value) readyPlayers++;
        }

        Debug.Log($"Ready players: {readyPlayers}/{requiredPlayers}");
        return readyPlayers >= requiredPlayers;
    }

    private void UpdateStartButtonState()
    {
        if (LobbyPanelManager.Instance != null)
        {
            LobbyPanelManager.Instance.UpdateStartButton(CanStartMatch());
        }
    }

    public void SetPlayerReady(string playerId)
    {
        bool currentState = IsPlayerReady(playerId);
        playerReadyStates[playerId] = !currentState;
        
        Debug.Log($"Player {playerId} ready state: {!currentState}");
        UpdateLobbyData();
        UpdateStartButtonState();
        RefreshPlayerList();
    }

    public bool IsPlayerReady(string playerId)
    {
        return playerReadyStates.ContainsKey(playerId) && playerReadyStates[playerId];
    }

    public bool IsPlayerBlueTeam(string playerId)
    {
        return playerTeams.ContainsKey(playerId) && playerTeams[playerId] == "Blue";
    }

    private void UpdatePlayerListUI()
    {
        if (LobbyPanelManager.Instance == null)
        {
            Debug.LogWarning("LobbyPanelManager.Instance is null, waiting for initialization...");
            StartCoroutine(WaitForLobbyPanel());
            return;
        }

        var players = new List<LobbyPlayerData>();
        foreach (var kvp in playerNames)
        {
            var playerData = new LobbyPlayerData
            {
                PlayerName = kvp.Value,
                IsBlueTeam = playerTeams.ContainsKey(kvp.Key) && playerTeams[kvp.Key] == "Blue",
                IsReady = IsPlayerReady(kvp.Key)
            };
            players.Add(playerData);
            Debug.Log($"Refreshing player: {playerData.PlayerName} - Team: {(playerData.IsBlueTeam ? "Blue" : "Red")}");
        }

        LobbyPanelManager.Instance.UpdatePlayerList(players);
    }

    private IEnumerator WaitForLobbyPanel()
    {
        float timeoutDuration = 5f;
        float elapsedTime = 0f;

        while (LobbyPanelManager.Instance == null && elapsedTime < timeoutDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (LobbyPanelManager.Instance != null)
        {
            var players = new List<LobbyPlayerData>();
            foreach (var kvp in playerNames)
            {
                players.Add(new LobbyPlayerData
                {
                    PlayerName = kvp.Value,
                    IsBlueTeam = playerTeams.ContainsKey(kvp.Key) && playerTeams[kvp.Key] == "Blue",
                    IsReady = IsPlayerReady(kvp.Key)
                });
            }
            LobbyPanelManager.Instance.UpdatePlayerList(players);
        }
        else
        {
            Debug.LogError("Failed to find LobbyPanelManager after timeout");
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
            string playerId = AuthenticationService.Instance.PlayerId;
            UpdatePlayerOptions options = new UpdatePlayerOptions();
            
            if (playerTeams.ContainsKey(playerId))
            {
                options.Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerNames[playerId]) },
                    { "Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerTeams[playerId]) },
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerReadyStates.ContainsKey(playerId) ? playerReadyStates[playerId].ToString().ToLower() : "false") }
                };

                await LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id, playerId, options);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to update player data: {e.Message}");
        }
    }

    public async void SendChatMessage(string message)
    {
        if (currentLobby == null) return;

        try
        {
            // First get the latest lobby data
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);

            // Get existing chat messages
            List<string> existingMessages = new List<string>();
            if (currentLobby.Data != null && currentLobby.Data.ContainsKey("ChatMessages"))
            {
                string currentChat = currentLobby.Data["ChatMessages"].Value;
                if (!string.IsNullOrEmpty(currentChat))
                {
                    existingMessages = currentChat.Split('|').ToList();
                }
            }

            // Add new message
            existingMessages.Add(message);

            // Keep only last MAX_CHAT_MESSAGES
            while (existingMessages.Count > MAX_CHAT_MESSAGES)
            {
                existingMessages.RemoveAt(0);
            }

            // Update lobby data
            string updatedChat = string.Join("|", existingMessages);
            var options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "ChatMessages", new DataObject(DataObject.VisibilityOptions.Member, updatedChat) }
                }
            };

            // Update the lobby
            await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, options);

            Debug.Log($"Sent chat message successfully: {message}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to send chat message: {e.Message}");
        }
    }

    private void UpdateChatMessages(string serializedChat)
    {
        if (string.IsNullOrEmpty(serializedChat)) return;

        // Get all messages from the serialized string
        var messages = serializedChat.Split('|');

        // Find new messages that aren't in our local cache
        var newMessages = messages.Skip(chatMessages.Count);

        // Add new messages to UI
        if (newMessages.Any() && LobbyPanelManager.Instance != null)
        {
            foreach (string msg in newMessages)
            {
                LobbyPanelManager.Instance.AddChatMessage(msg);
                chatMessages.Add(msg);
            }
            Debug.Log($"Added {newMessages.Count()} new chat messages");
        }
    }

    private async void PollLobbyForUpdates()
    {
        if (currentLobby == null) return;

        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer <= 0f)
        {
            lobbyPollTimer = LOBBY_POLL_INTERVAL;
            try
            {
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                if (lobby != null)
                {
                    currentLobby = lobby;
                    
                    // Add this check for chat messages
                    if (lobby.Data != null && 
                        lobby.Data.ContainsKey("ChatMessages") && 
                        lobby.Data["ChatMessages"].Value != string.Join("|", chatMessages))
                    {
                        UpdateChatMessages(lobby.Data["ChatMessages"].Value);
                    }

                    bool playersChanged = false;
                    foreach (Player player in lobby.Players)
                    {
                        string playerId = player.Id;
                        if (player.Data != null)
                        {
                            if (player.Data.ContainsKey("PlayerName") && 
                                (!playerNames.ContainsKey(playerId) || 
                                playerNames[playerId] != player.Data["PlayerName"].Value))
                            {
                                playerNames[playerId] = player.Data["PlayerName"].Value;
                                playersChanged = true;
                            }
                            if (player.Data.ContainsKey("Team"))
                            {
                                playerTeams[playerId] = player.Data["Team"].Value;
                                playersChanged = true;
                            }
                            if (player.Data.ContainsKey("IsReady"))
                            {
                                bool isReady = player.Data["IsReady"].Value == "true";
                                if (!playerReadyStates.ContainsKey(playerId) || 
                                    playerReadyStates[playerId] != isReady)
                                {
                                    playerReadyStates[playerId] = isReady;
                                    playersChanged = true;
                                }
                            }
                        }
                    }

                    if (playersChanged)
                    {
                        Debug.Log($"Players updated in lobby. Total players: {lobby.Players.Count}");
                        UpdatePlayerListUI();
                        UpdateStartButtonState();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to poll lobby: {e.Message}");
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

    private Player GetPlayer()
    {
        string playerId = AuthenticationService.Instance.PlayerId;
        string playerName = SettingsManager.Instance != null ? 
            SettingsManager.Instance.PlayerName : 
            $"Player{playerNames.Count + 1}";

        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                { "Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "") },
                { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") }
            }
        };
    }

    private async void HandleLobbyHeartbeat()
    {
        if (currentLobby == null) return;

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer <= 0f)
        {
            heartbeatTimer = LOBBY_POLL_INTERVAL;
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
                Debug.Log("Sent lobby heartbeat");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send heartbeat: {e.Message}");
            }
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
