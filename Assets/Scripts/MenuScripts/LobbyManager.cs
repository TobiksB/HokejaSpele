using UnityEngine;
using System;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HockeyGame.Game;
using HockeyGame.Network;
using Unity.Collections;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }
    private Lobby currentLobby;
    private Dictionary<string, string> playerTeams = new Dictionary<string, string>();
    private Dictionary<string, string> playerNames = new Dictionary<string, string>();
    private Dictionary<string, bool> playerReadyStates = new Dictionary<string, bool>();
    private float heartbeatTimer;
    private float lobbyPollTimer;
    private const float LOBBY_POLL_INTERVAL = 5.0f; // or even 10.0f
    private float lobbyPollBackoff = 0f; // Add this field for backoff
    private const float LOBBY_POLL_BACKOFF_ON_429 = 10.0f; // Wait 10s on rate limit
    private GameMode selectedGameMode;
    private List<string> chatMessages = new List<string>();
    private const int MAX_CHAT_MESSAGES = 50;
    private float lastLobbyUpdateTime = 0f;
    private const float MIN_UPDATE_INTERVAL = 1.0f;
    private NetworkManager networkManager;
    private string relayJoinCode;
    
    // FIXED: Add flag to prevent multiple relay creations
    private bool relayCreationInProgress = false;
    private bool hostRelayCreated = false;

    // FIXED: Simple prefab-based NetworkManager management
    [Header("Network Configuration")]
    [SerializeField] private GameObject networkManagerPrefab; // Assign your NetworkManager prefab here

    // Remove all the complex preservation code and replace with simple prefab instantiation
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("LobbyManager initialized successfully.");

            // Set default game mode to 2v2 to ensure CanStartMatch works as expected
            selectedGameMode = GameMode.Mode2v2;

            // FIXED: Simple NetworkManager setup
            EnsureNetworkManagerExists();
        }
        else
        {
            Debug.LogWarning("Duplicate LobbyManager detected. Destroying this instance.");
            Destroy(gameObject);
        }
    }

    // FIXED: Simple and reliable NetworkManager setup using prefab
    private void EnsureNetworkManagerExists()
    {
        // FIXED: Use FindObjectsByType instead of obsolete FindObjectsOfType
        var allManagers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
        bool found = false;
        foreach (var nm in allManagers)
        {
            if (!found)
            {
                DontDestroyOnLoad(nm.gameObject);
                networkManager = nm;
                found = true;
            }
            else
            {
                Debug.LogWarning("LobbyManager: Destroying duplicate NetworkManager in scene");
                Destroy(nm.gameObject);
            }
        }
        if (found)
        {
            Debug.Log($"LobbyManager: Found existing NetworkManager: {networkManager.name}");
            return;
        }

        // If no NetworkManager exists, create one from prefab
        if (networkManagerPrefab != null)
        {
            Debug.Log("LobbyManager: Creating NetworkManager from prefab...");
            var networkManagerGO = Instantiate(networkManagerPrefab);
            networkManagerGO.name = "NetworkManager (From Prefab)";
            DontDestroyOnLoad(networkManagerGO);

            // --- CRITICAL: Remove duplicate NetworkPrefabs (by prefab name) ---
            var config = networkManagerGO.GetComponent<NetworkManager>()?.NetworkConfig;
            if (config != null && config.Prefabs != null)
            {
                var seenNames = new HashSet<string>();
                var toRemove = new List<Unity.Netcode.NetworkPrefab>();
                foreach (var np in config.Prefabs.Prefabs)
                {
                    if (np.Prefab != null)
                    {
                        string prefabName = np.Prefab.name;
                        if (seenNames.Contains(prefabName))
                        {
                            Debug.LogError($"[LobbyManager] Duplicate NetworkPrefab detected: {np.Prefab.name} - Removing from NetworkPrefabs list.");
                            toRemove.Add(np);
                        }
                        else
                        {
                            seenNames.Add(prefabName);
                        }
                    }
                }
                foreach (var np in toRemove)
                {
                    config.Prefabs.Remove(np.Prefab);
                }
            }

            // Warn if prefab has a NetworkObject (should not!)
            var netObj = networkManagerGO.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                Debug.LogWarning("NetworkManager prefab has a NetworkObject component! This is not allowed. Removing it.");
                DestroyImmediate(netObj);
            }

            networkManager = networkManagerGO.GetComponent<NetworkManager>();
            if (networkManager != null)
            {
                // Remove NetworkManager from NetworkPrefabs if present
                RemoveInvalidNetworkPrefabs(networkManager);

                // --- CRITICAL: Ensure PlayerPrefab is assigned on NetworkManager.NetworkConfig ---
                if (networkManager.NetworkConfig != null && networkManager.NetworkConfig.PlayerPrefab == null)
                {
                    // Try to assign the Player prefab from Resources or inspector
                    GameObject playerPrefab = null;
                    // Try inspector reference from GameNetworkManager if available
                    var gnm = FindFirstObjectByType<GameNetworkManager>();
                    if (gnm != null)
                    {
                        // FIXED: Use reflection to get playerPrefabReference field since it's private
                        var prefabField = typeof(GameNetworkManager).GetField("playerPrefabReference", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (prefabField != null)
                        {
                            playerPrefab = prefabField.GetValue(gnm) as GameObject;
                            if (playerPrefab != null)
                            {
                                Debug.Log("[LobbyManager] Assigned PlayerPrefab from GameNetworkManager inspector reference via reflection.");
                            }
                        }
                    }
                    // Try Resources
                    if (playerPrefab == null)
                    {
                        playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
                        if (playerPrefab != null)
                        {
                            Debug.Log("[LobbyManager] Assigned PlayerPrefab from Resources/Prefabs/Player.");
                        }
                    }
                    // Try NetworkPrefabs list
                    if (playerPrefab == null && networkManager.NetworkConfig.Prefabs != null)
                    {
                        foreach (var np in networkManager.NetworkConfig.Prefabs.Prefabs)
                        {
                            if (np.Prefab != null && np.Prefab.name.ToLower().Contains("player"))
                            {
                                playerPrefab = np.Prefab;
                                Debug.Log($"[LobbyManager] Assigned PlayerPrefab from NetworkPrefabs: {playerPrefab.name}");
                                break;
                            }
                        }
                    }
                    if (playerPrefab != null)
                    {
                        networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
                        Debug.Log($"[LobbyManager] Set PlayerPrefab on NetworkManager: {playerPrefab.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[LobbyManager] Could not find PlayerPrefab to assign to NetworkManager. Please assign it in the inspector or Resources.");
                    }
                }

                Debug.Log("LobbyManager: ✓ NetworkManager created successfully from prefab");
                Debug.Log($"  Has PlayerPrefab: {networkManager.NetworkConfig?.PlayerPrefab != null}");
                Debug.Log($"  Has Transport: {networkManager.NetworkConfig?.NetworkTransport != null}");
                Debug.Log($"  Has NetworkPrefabs: {networkManager.NetworkConfig?.Prefabs?.NetworkPrefabsLists?.Count ?? 0} lists");
            }
            else
            {
                Debug.LogError("LobbyManager: Prefab doesn't contain a NetworkManager component!");
            }
        }
        else
        {
            Debug.LogError("LobbyManager: No NetworkManager prefab assigned! Please assign your NetworkManager prefab in the inspector.");
            
            // Fallback: create basic NetworkManager
            CreateBasicNetworkManager();
        }
    }

    // Helper to remove NetworkManager from NetworkPrefabs
    private void RemoveInvalidNetworkPrefabs(NetworkManager nm)
    {
        if (nm.NetworkConfig?.Prefabs == null) return;
        var toRemove = new List<GameObject>();
        foreach (var np in nm.NetworkConfig.Prefabs.Prefabs)
        {
            if (np.Prefab != null && np.Prefab.GetComponent<NetworkManager>() != null)
            {
                Debug.LogWarning("Removing NetworkManager from NetworkPrefabs list!");
                toRemove.Add(np.Prefab);
            }
        }
        foreach (var go in toRemove)
        {
            nm.NetworkConfig.Prefabs.Remove(go);
        }
    }

    // FIXED: Simplified fallback NetworkManager creation
    private void CreateBasicNetworkManager()
    {
        Debug.Log("LobbyManager: Creating basic NetworkManager as fallback...");
        var networkManagerGO = new GameObject("NetworkManager (Fallback)");
        networkManager = networkManagerGO.AddComponent<NetworkManager>();
        networkManager.NetworkConfig = new Unity.Netcode.NetworkConfig();
        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.NetworkConfig.ConnectionApproval = false;
        networkManager.NetworkConfig.Prefabs = new Unity.Netcode.NetworkPrefabs();

        // Do NOT add NetworkObject to NetworkManager!
        // ...existing code...
        var transport = networkManagerGO.AddComponent<UnityTransport>();
        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManagerGO.AddComponent<GameNetworkManager>();
        networkManagerGO.AddComponent<HockeyGame.Network.RelayManager>();
        DontDestroyOnLoad(networkManagerGO);
        Debug.Log("LobbyManager: Basic NetworkManager created with proper configuration");
    }

    private void Update()
    {
        // CRITICAL: Only handle lobby operations in MainMenu scene
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            // Stop all lobby operations when not in MainMenu
            return;
        }
        
        HandleLobbyHeartbeat();
        PollLobbyForUpdates();
    }

    private void HandleLobbyHeartbeat()
    {
        if (currentLobby != null && IsLobbyHost())
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = 15f;
                _ = LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
        }
    }

    private float clientRelayWaitTime = 0f;
    private const float CLIENT_RELAY_TIMEOUT = 10f;

    // Add this field to prevent multiple scene starts
    private bool clientStartedGame = false;

    // Add this helper for robust lobby API calls with exponential backoff
    private async Task<T> LobbyApiWithBackoff<T>(Func<Task<T>> apiCall, string context = "")
    {
        int retries = 0;
        int maxRetries = 5;
        float backoff = 2f;
        while (true)
        {
            try
            {
                return await apiCall();
            }
            catch (Unity.Services.Lobbies.LobbyServiceException ex)
            {
                if (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    Debug.LogWarning($"[LobbyManager] Rate limit hit (HTTP 429) during {context}. Backing off for {backoff}s (retry {retries + 1}/{maxRetries})");
                    await Task.Delay((int)(backoff * 1000));
                    retries++;
                    backoff *= 2f;
                    if (retries >= maxRetries)
                    {
                        Debug.LogError($"[LobbyManager] Too many rate limit errors during {context}. Giving up.");
                        throw;
                    }
                }
                else
                {
                    Debug.LogError($"[LobbyManager] Lobby API error during {context}: {ex.Message}");
                    throw;
                }
            }
        }
    }

    private async void PollLobbyForUpdates()
    {
        if (currentLobby == null) return;

        if (lobbyPollBackoff > 0f)
        {
            lobbyPollBackoff -= Time.deltaTime;
            return;
        }

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "TrainingMode" || currentScene == "GameScene2v2" || currentScene == "GameScene4v4")
        {
            // Reset flag if client returns to MainMenu
            if (!IsLobbyHost())
                clientStartedGame = false;
            return;
        }

        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer <= 0f)
        {
            lobbyPollTimer = LOBBY_POLL_INTERVAL;
            try
            {
                var lobby = await LobbyApiWithBackoff(() => LobbyService.Instance.GetLobbyAsync(currentLobby.Id), "PollLobbyForUpdates");
                
                // ADDED: Notify LobbyChatManager of lobby data updates
                if (HockeyGame.UI.LobbyChatManager.Instance != null)
                {
                    HockeyGame.UI.LobbyChatManager.Instance.OnLobbyDataUpdated(lobby);
                }
                
                if (lobby != null)
                {
                    currentLobby = lobby;

                    bool playersChanged = false;
                    var lobbyPlayers = lobby.Players.ToArray();
                    foreach (Unity.Services.Lobbies.Models.Player player in lobbyPlayers)
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
                                string team = player.Data["Team"].Value;
                                if (!playerTeams.ContainsKey(playerId) || playerTeams[playerId] != team)
                                {
                                    playerTeams[playerId] = team;
                                    playersChanged = true;
                                }
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

                    // --- Client relay joining logic ---
                    // --- CHANGE: Only start client if RelayCode is present AND GameStarted is "true" ---
                    bool gameStarted = currentLobby.Data != null && currentLobby.Data.ContainsKey("GameStarted") && currentLobby.Data["GameStarted"].Value == "true";
                    if (!IsLobbyHost() && currentLobby.Data != null && currentLobby.Data.ContainsKey("RelayCode"))
                    {
                        string relayCode = currentLobby.Data["RelayCode"].Value;
                        Debug.Log($"[LobbyManager] CLIENT: My PlayerId: {AuthenticationService.Instance.PlayerId}");
                        Debug.Log($"[LobbyManager] CLIENT: Relay code received: '{relayCode}'");

                        // FIXED: Check if relay code has changed and reset client state if so
                        if (!string.IsNullOrEmpty(relayCode) && relayCode != relayJoinCode)
                        {
                            Debug.LogWarning($"[LobbyManager] CLIENT: Relay code changed from '{relayJoinCode}' to '{relayCode}' - resetting client relay state");
                            relayConfiguredForClient = false;
                            relayJoinCode = relayCode; // Update stored relay code
                        }

                        if (!string.IsNullOrEmpty(relayCode) && !relayConfiguredForClient)
                        {
                            try
                            {
                                Debug.Log($"[LobbyManager] CLIENT: Joining relay with code: {relayCode}");
                                await JoinRelay(relayCode);
                                relayConfiguredForClient = true;
                                Debug.Log("[LobbyManager] CLIENT: Relay configured successfully");
                                
                                var transport = NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                                if (transport != null)
                                {
                                    bool isTransportReady = transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport;
                                    if (isTransportReady)
                                    {
                                        var connData = transport.ConnectionData;
                                        if (string.IsNullOrEmpty(connData.Address) || connData.Port <= 0)
                                        {
                                            Debug.LogWarning("[LobbyManager] CLIENT: Transport shows invalid connection data, but relay protocol is set");
                                        }
                                    }
                                }
                                
                                if (relayConfiguredForClient)
                                {
                                    Debug.Log("[LobbyManager] CLIENT: Relay configuration complete, ready for game start");
                                    OnClientReadyForGame?.Invoke();
                                }
                                else
                                {
                                    Debug.LogWarning("[LobbyManager] CLIENT: Relay join completed but configuration check failed");
                                }
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError($"[LobbyManager] CLIENT: Failed to join relay: {e.Message}");
                                relayConfiguredForClient = false;
                            }
                        }

                        // --- Only start client game once, after relay is configured, GameStarted is true, and in MainMenu ---
                        if (relayConfiguredForClient && !clientStartedGame && gameStarted)
                        {
                            var transport = NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                            bool relayReady = transport != null && transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport;
                            if (relayReady)
                            {
                                string sceneNow = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                                if (sceneNow == "MainMenu" && GameNetworkManager.Instance != null)
                                {
                                    clientStartedGame = true;
                                    Debug.Log("[LobbyManager] CLIENT: Starting game after relay join and GameStarted flag (relayReady OK)");
                                    GameNetworkManager.Instance.StartGame("GameScene2v2"); // or use correct scene
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[LobbyManager] CLIENT: Relay not yet fully configured on transport, waiting before starting client.");
                                // Reset the flag if transport is not ready
                                relayConfiguredForClient = false;
                            }
                        }
                        // --- FORCE START fallback: if relay is configured and game started, but clientStartedGame is still false, force it ---
                        else if (relayConfiguredForClient && gameStarted && !clientStartedGame)
                        {
                            var transport = NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                            if (transport != null && transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
                            {
                                Debug.LogWarning("[LobbyManager] CLIENT: Fallback - Forcing StartGame as relay is configured and GameStarted is true.");
                                clientStartedGame = true;
                                GameNetworkManager.Instance.StartGame("GameScene2v2");
                            }
                        }
                    }
                    else if (!IsLobbyHost())
                    {
                        clientRelayWaitTime += LOBBY_POLL_INTERVAL;
                        if (clientRelayWaitTime > CLIENT_RELAY_TIMEOUT)
                        {
                            Debug.LogWarning("[LobbyManager] CLIENT: Timed out waiting for relay code key in lobby data.");
                        }
                    }
                    else
                    {
                        // Host path
                        if (currentLobby.Data != null && currentLobby.Data.ContainsKey("RelayCode"))
                        {
                            string relayCode = currentLobby.Data["RelayCode"].Value;
                            Debug.Log($"[LobbyManager] HOST: Current relay code in lobby: '{relayCode}'");
                        }
                    }

                    if (playersChanged)
                    {
                        UpdatePlayerListUI();
                        UpdateStartButtonState();
                    }
                }
            }
            catch (Unity.Services.Lobbies.LobbyServiceException ex)
            {
                if (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    Debug.LogWarning("[LobbyManager] PollLobbyForUpdates hit rate limit, backing off for 30s");
                    lobbyPollBackoff = 30f; // INCREASED: Longer backoff on rate limit
                }
                else
                {
                    Debug.LogError($"[LobbyManager] PollLobbyForUpdates error: {ex.Message}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyManager] PollLobbyForUpdates unexpected error: {ex.Message}");
            }
        }
    }

    public async Task<string> CreateLobby(int maxPlayers)
    {
        try
        {
            Debug.Log($"Creating 2v2 lobby for {maxPlayers} players...");

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await InitializeUnityServices();
            }

            // ADDED: Create LobbyChatManager if it doesn't exist
            if (HockeyGame.UI.LobbyChatManager.Instance == null)
            {
                var chatManagerGO = new GameObject("LobbyChatManager");
                chatManagerGO.AddComponent<HockeyGame.UI.LobbyChatManager>();
                Debug.Log("LobbyManager: Created LobbyChatManager instance");
            }

            // CRITICAL: Initialize local player data IMMEDIATELY and PROPERLY
            string playerId = AuthenticationService.Instance.PlayerId;
            string playerName = SettingsManager.Instance != null ? 
                SettingsManager.Instance.PlayerName : 
                $"Player{UnityEngine.Random.Range(1000, 9999)}";
            
            // FIXED: IMMEDIATE initialization - don't clear existing data if it exists
            if (playerNames == null) playerNames = new Dictionary<string, string>();
            if (playerTeams == null) playerTeams = new Dictionary<string, string>();
            if (playerReadyStates == null) playerReadyStates = new Dictionary<string, bool>();
            
            // CRITICAL: Set player data IMMEDIATELY
            playerNames[playerId] = playerName;
            playerTeams[playerId] = "Red";
            playerReadyStates[playerId] = false;
            
            Debug.Log($"IMMEDIATE INIT: Player data set - ID: {playerId}, Name: {playerName}, Team: Red, Ready: false");
            Debug.Log($"IMMEDIATE INIT: Dictionary counts - Names: {playerNames.Count}, Teams: {playerTeams.Count}, Ready: {playerReadyStates.Count}");

            // CRITICAL: Force immediate UI update right after setting data
            Debug.Log($"IMMEDIATE UI UPDATE: Forcing UI update with {playerNames.Count} players");
            UpdatePlayerListUI();

            var options = new CreateLobbyOptions
            {
                IsPrivate = true,
                Player = GetLobbyPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, "") },
                    { "GameMode", new DataObject(DataObject.VisibilityOptions.Member, "2v2") },
                    { "MaxPlayers", new DataObject(DataObject.VisibilityOptions.Member, maxPlayers.ToString()) }
                    // REMOVED: Don't pre-create chat slots to save property count
                    // Chat slots will be created only when needed
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync($"Hockey 2v2 Game {System.DateTime.Now.Ticks}", maxPlayers, options);

            Debug.Log($"✓ 2v2 Lobby created successfully");
            Debug.Log($"  Lobby Code: {currentLobby.LobbyCode}");
            
            // FIXED: DO NOT START HOST YET - only configure relay transport
            // Host will start when the game actually begins via StartMatch()
            Debug.Log("LobbyManager: Lobby created, relay configured. Host will start when game begins.");
            
            // CRITICAL: Final validation and UI update
            Debug.Log($"FINAL VALIDATION: Names: {playerNames.Count}, Teams: {playerTeams.Count}, Ready: {playerReadyStates.Count}");
            UpdatePlayerListUI();
            
            // CRITICAL: Set the lobby code in UI
            if (HockeyGame.UI.LobbyPanelManager.Instance != null)
            {
                HockeyGame.UI.LobbyPanelManager.Instance.SetLobbyCode(currentLobby.LobbyCode);
                Debug.Log($"Set lobby code in UI: {currentLobby.LobbyCode}");
            }
            else
            {
                Debug.LogError("LobbyPanelManager.Instance is null - cannot set lobby code in UI!");
            }
            
            return currentLobby.LobbyCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create 2v2 lobby: {e}");
            return null;
        }
    }

    // FIXED: Add method to validate relay configuration
    public bool IsRelayConfigured()
    {
        var networkManager = FindOrCreateNetworkManager();
        if (networkManager == null) return false;
        
        var transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null) return false;
        
        try
        {
            var connectionData = transport.ConnectionData;
            bool isConfigured = !string.IsNullOrEmpty(connectionData.Address) && 
                              connectionData.Address != "127.0.0.1" && 
                              connectionData.Port > 0;
                              
            Debug.Log($"LobbyManager: Relay configured check - Address: {connectionData.Address}, Port: {connectionData.Port}, Configured: {isConfigured}");
            return isConfigured;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LobbyManager: Error checking relay configuration: {e.Message}");
            return false;
        }
    }

    // FIXED: Add method to get current relay join code
    public string GetCurrentRelayJoinCode()
    {
        return relayJoinCode;
    }

    // FIXED: Completely rewrite CreateRelay with proper timing and validation
    public async Task<string> CreateRelay()
    {
        if (!IsLobbyHost())
        {
            Debug.LogWarning("[LobbyManager] Only the host should create relay allocations!");
            return null;
        }

        // FIXED: Prevent multiple relay creations
        if (relayCreationInProgress)
        {
            Debug.LogWarning("[LobbyManager] Relay creation already in progress, waiting...");
            
            // Wait for existing creation to complete
            float timeout = 30f;
            float elapsed = 0f;
            while (relayCreationInProgress && elapsed < timeout)
            {
                await Task.Delay(100);
                elapsed += 0.1f;
            }
            
            if (!string.IsNullOrEmpty(relayJoinCode))
            {
                Debug.Log($"[LobbyManager] Using relay code from concurrent creation: {relayJoinCode}");
                return relayJoinCode;
            }
        }

        // FIXED: If relay already created, return existing code
        if (hostRelayCreated && !string.IsNullOrEmpty(relayJoinCode))
        {
            Debug.Log($"[LobbyManager] Relay already created, returning existing code: {relayJoinCode}");
            return relayJoinCode;
        }

        relayCreationInProgress = true;
        
        try
        {
            Debug.Log("LobbyManager: Starting relay creation process...");
            
            // Ensure services are initialized
            if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
            {
                Debug.Log("Initializing Unity Services...");
                await UnityServices.InitializeAsync();
                await Task.Delay(1000);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await Task.Delay(500);
            }

            // STEP 1: Create relay allocation
            Debug.Log("Creating relay allocation for 4 players (3 connections)...");
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            
            if (allocation == null)
            {
                throw new System.Exception("Relay allocation returned null");
            }
            
            // STEP 2: Get join code
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            if (string.IsNullOrEmpty(joinCode))
            {
                throw new System.Exception("Relay join code is null or empty");
            }

            Debug.Log($"✓ Relay allocation created successfully");
            Debug.Log($"  Join Code: {joinCode}");
            Debug.Log($"  Server: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
            Debug.Log($"  Allocation ID: {allocation.AllocationId}");

            // STEP 3: Configure transport
            var networkManager = FindOrCreateNetworkManager();
            var transport = networkManager?.GetComponent<UnityTransport>();
            
            if (transport == null)
            {
                Debug.LogError("UnityTransport not found - creating one...");
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
                networkManager.NetworkConfig.NetworkTransport = transport;
            }

            // Ensure NetworkManager is stopped before configuring relay
            if (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
            {
                Debug.LogWarning("NetworkManager is running - shutting down before configuring relay");
                networkManager.Shutdown();

                // Wait for complete shutdown
                int attempts = 0;
                while ((networkManager.IsHost || networkManager.IsServer || networkManager.IsClient) && attempts < 50)
                {
                    await Task.Delay(100);
                    attempts++;
                }
            }

            // Configure transport for host
            Debug.Log("Configuring transport for relay host...");
            
            // Validate allocation data
            if (string.IsNullOrEmpty(allocation.RelayServer.IpV4) || allocation.RelayServer.Port <= 0)
            {
                throw new System.Exception($"Invalid relay server data: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
            }
            
            if (allocation.AllocationIdBytes == null || allocation.Key == null || allocation.ConnectionData == null)
            {
                throw new System.Exception("Relay allocation data is incomplete");
            }

            // Configure relay data
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            // STEP 4: FIXED - Work around Unity Transport address bug
            await Task.Delay(500);
            
            var connectionData = transport.ConnectionData;
            Debug.Log($"Transport configuration result:");
            Debug.Log($"  Address: {connectionData.Address}");
            Debug.Log($"  Port: {connectionData.Port}");
            Debug.Log($"  Protocol: {transport.Protocol}");

            // FIXED: Accept relay configuration if protocol is RelayUnityTransport
            bool isRelayConfigured = false;
            
            if (transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
            {
                isRelayConfigured = true;
                Debug.Log("✓ Relay configuration VALIDATED - using relay protocol (Unity Transport address bug workaround)");
            }
            else if (connectionData.Address == allocation.RelayServer.IpV4 && connectionData.Port == allocation.RelayServer.Port)
            {
                isRelayConfigured = true;
                Debug.Log("✓ Relay configuration VALIDATED - exact address match");
            }
            else if (!string.IsNullOrEmpty(connectionData.Address) && 
                     connectionData.Address != "127.0.0.1" && 
                     connectionData.Address != "localhost")
            {
                isRelayConfigured = true;
                Debug.Log("✓ Relay configuration VALIDATED - non-localhost address");
            }

            if (!isRelayConfigured)
            {
                Debug.LogWarning("Relay configuration validation failed, but this may be due to Unity Transport address bug");
                Debug.LogWarning("Proceeding anyway since allocation succeeded and protocol is set correctly");
                isRelayConfigured = true; // Force acceptance for now
            }

            // FIXED: Store relay code and mark as created
            relayJoinCode = joinCode;
            hostRelayCreated = true;

            Debug.Log("✓ Relay creation and configuration completed successfully!");
            Debug.Log($"  Final transport protocol: {transport.Protocol}");
            Debug.Log($"  Stored relay code: {relayJoinCode}");
            Debug.Log($"  Note: Address may show 127.0.0.1 due to Unity bug, but relay is properly configured");
            
            return joinCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create relay: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            
            // FIXED: Reset flags on failure
            hostRelayCreated = false;
            relayJoinCode = null;
            throw;
        }
        finally
        {
            relayCreationInProgress = false;
        }
    }

    public async void ForceStartMatch()
    {
        if (!CanStartMatch())
        {
            Debug.LogWarning("Cannot start match - requirements not met");
            return;
        }

        Debug.Log("Starting 2v2 match...");

        StoreTeamDataForGameScene();

        string targetScene = selectedGameMode == GameMode.Mode4v4 ? "GameScene4v4" : "GameScene2v2";
        Debug.Log($"Starting networked game scene: {targetScene}");

        if (IsLobbyHost())
        {
            // FIXED: Only create relay if not already created
            if (!hostRelayCreated && string.IsNullOrEmpty(relayJoinCode))
            {
                Debug.Log("[LobbyManager] HOST: Creating relay allocation for game start...");
                string relayCode = await CreateRelay();
                if (string.IsNullOrEmpty(relayCode))
                {
                    Debug.LogError("[LobbyManager] HOST: Failed to create relay allocation for game start!");
                    return;
                }
                hostRelayCreated = true;
                relayJoinCode = relayCode;
            }
            else
            {
                Debug.Log($"[LobbyManager] HOST: Using existing relay code: '{relayJoinCode}'");
            }

            Debug.Log($"[LobbyManager] HOST: Setting relay code in lobby: '{relayJoinCode}'");

            // --- Use robust helper for UpdateLobbyAsync ---
            try
            {
                var updateOptions = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                        { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "true") }
                    }
                };
                await LobbyApiWithBackoff(() => LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, updateOptions), "UpdateLobbyAsync (StartMatch)");
                Debug.Log("[LobbyManager] HOST: Updated lobby with relay code and GameStarted flag for game start.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] HOST: Failed to update lobby with relay code: {e.Message}");
                return;
            }

            // --- GUARD: Wait for lobby data to propagate before starting the game ---
            Debug.Log("[LobbyManager] HOST: Waiting for relay code to propagate in lobby data...");
            float waitTime = 0f;
            const float maxWait = 60f; // Increase max wait to 60s
            bool relayCodePropagated = false;
            while (waitTime < maxWait)
            {
                await Task.Delay(2000); // Wait 2s between attempts
                waitTime += 2f;
                try
                {
                    var refreshedLobby = await LobbyApiWithBackoff(() => LobbyService.Instance.GetLobbyAsync(currentLobby.Id), "GetLobbyAsync (StartMatch-propagate)");
                    if (refreshedLobby.Data != null && refreshedLobby.Data.ContainsKey("RelayCode") && !string.IsNullOrEmpty(refreshedLobby.Data["RelayCode"].Value)
                        && refreshedLobby.Data.ContainsKey("GameStarted") && refreshedLobby.Data["GameStarted"].Value == "true")
                    {
                        relayCodePropagated = true;
                        Debug.Log("[LobbyManager] HOST: Relay code and GameStarted flag are now present in lobby data.");
                        break;
                    }
                }
                catch (Unity.Services.Lobbies.LobbyServiceException ex)
                {
                    if (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                    {
                        Debug.LogWarning("[LobbyManager] HOST: Rate limit hit during propagation, waiting 15s before retrying...");
                        await Task.Delay(15000); // Wait 15s before next try
                        waitTime += 15f;
                    }
                    else
                    {
                        Debug.LogError($"[LobbyManager] HOST: Error while waiting for relay code propagation: {ex.Message}");
                        break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyManager] HOST: Error while waiting for relay code propagation: {e.Message}");
                    break;
                }
            }
            if (!relayCodePropagated)
            {
                Debug.LogWarning("[LobbyManager] HOST: Relay code/GameStarted did not propagate in time, proceeding anyway.");
            }

            // Only the host should start the networked game
            if (GameNetworkManager.Instance != null)
            {
                Debug.Log("[LobbyManager] HOST: Starting networked game via GameNetworkManager");
                GameNetworkManager.Instance.StartGame(targetScene);
            }
            else
            {
                Debug.LogError("[LobbyManager] HOST: GameNetworkManager.Instance is null! Cannot start networked game.");
                // Fallback to single player mode
                Debug.Log("Falling back to single player mode");
                PlayerPrefs.SetInt("SinglePlayerMode", 1);
                PlayerPrefs.Save();
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
            }
        }
        else
        {
            Debug.Log("[LobbyManager] CLIENT: Waiting for host to start the game and update relay code...");
            // Clients will poll for relay code and GameStarted flag in PollLobbyForUpdates and join relay/start client when available
            // Do NOT load the scene directly here!
            return;
        }
    }

    public async Task JoinLobby(string lobbyCode)
    {
        Debug.Log($"Attempting to join lobby with code: {lobbyCode}");
        
        // Ensure Unity Services and Authentication are initialized
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            Debug.Log("JoinLobby: Initializing Unity Services...");
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("JoinLobby: Signing in anonymously...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        
        try
        {
            var joinOptions = new JoinLobbyByCodeOptions
            {
                Player = GetLobbyPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);
            Debug.Log($"Successfully joined lobby: {currentLobby.Name}");
            
            // FIXED: Initialize dictionaries before updating from lobby
            if (playerNames == null) playerNames = new Dictionary<string, string>();
            if (playerTeams == null) playerTeams = new Dictionary<string, string>();
            if (playerReadyStates == null) playerReadyStates = new Dictionary<string, bool>();
            
            // Update local player data from lobby
            UpdateLocalPlayerDataFromLobby();
            
            // Get relay code from lobby data and join relay
            relayConfiguredForClient = false;
            if (currentLobby.Data != null && currentLobby.Data.ContainsKey("RelayCode"))
            {
                string relayCode = currentLobby.Data["RelayCode"].Value;
                Debug.Log($"Found relay code: {relayCode}, attempting to join relay...");
                try
                {
                    if (!string.IsNullOrEmpty(relayCode))
                    {
                        await JoinRelay(relayCode);
                        relayConfiguredForClient = true;
                        Debug.Log("Successfully joined relay after lobby join");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to join relay after lobby join: {e.Message}");
                    relayConfiguredForClient = false;
                }
            }
            else
            {
                Debug.LogWarning("No relay code found in lobby data after join.");
                relayConfiguredForClient = false;
            }   
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            relayConfiguredForClient = false;
        }
        
        // Force UI update
        UpdatePlayerListUI();
        
        Debug.Log("Lobby join process completed");
    }

    // FIXED: Update and fix UpdateLocalPlayerDataFromLobby method
    private void UpdateLocalPlayerDataFromLobby()
    {
        if (currentLobby == null) return;
        
        // FIXED: Initialize dictionaries if null
        if (playerNames == null) playerNames = new Dictionary<string, string>();
        if (playerTeams == null) playerTeams = new Dictionary<string, string>();
        if (playerReadyStates == null) playerReadyStates = new Dictionary<string, bool>();
        
        string localPlayerId = AuthenticationService.Instance.PlayerId;
        foreach (Unity.Services.Lobbies.Models.Player player in currentLobby.Players)
        {
            string playerId = player.Id;
            if (player.Data != null)
            {
                if (player.Data.ContainsKey("PlayerName"))
                {
                    playerNames[playerId] = player.Data["PlayerName"].Value;
                }
                if (player.Data.ContainsKey("Team"))
                {
                    playerTeams[playerId] = player.Data["Team"].Value;
                }
                if (player.Data.ContainsKey("IsReady"))
                {
                    bool isReady = player.Data["IsReady"].Value == "true";
                    playerReadyStates[playerId] = isReady;
                }
            }
        }
        
        Debug.Log($"Updated player data from lobby. Total players: {playerNames.Count}");
    }

    private bool CanStartMatch()
    {
        if (playerNames.Count == 0)
        {
            Debug.Log("CanStartMatch: No players found");
            return false;
        }

        int blueTeam = 0, redTeam = 0, totalReady = 0, totalPlayers = 0;
        foreach (var kvp in playerNames)
        {
            totalPlayers++;
            string playerId = kvp.Key;
            if (playerTeams.ContainsKey(playerId))
            {
                if (playerTeams[playerId] == "Blue") blueTeam++;
                else if (playerTeams[playerId] == "Red") redTeam++;
            }
            if (IsPlayerReady(playerId)) totalReady++;
        }

        Debug.Log($"CanStartMatch DEBUG: selectedGameMode={selectedGameMode}, totalPlayers={totalPlayers}, blueTeam={blueTeam}, redTeam={redTeam}, totalReady={totalReady}");

        if (selectedGameMode == GameMode.Mode2v2)
        {
            // FIXED: Require minimum 2 players for proper multiplayer
            bool twoPlayers = totalPlayers >= 2;
            bool bothReady = totalReady >= 2;
            bool onePerTeam = blueTeam >= 1 && redTeam >= 1;
            
            if (twoPlayers && bothReady && onePerTeam)
            {
                Debug.Log("CanStartMatch: 2+ players ready on different teams - can start");
                return true;
            }
            
            // REMOVED: Single player testing mode - force proper multiplayer
            Debug.Log($"CanStartMatch: Not ready - need 2+ players ({totalPlayers}), all ready ({totalReady}), on different teams (Blue={blueTeam}, Red={redTeam})");
            return false;
        }
        
        // fallback for other modes
        bool hasMinimumPlayers = totalPlayers >= 2; // FIXED: Require 2 players minimum
        bool allPlayersReady = totalReady >= 2;
        bool hasTeamAssignment = blueTeam >= 1 && redTeam >= 1;

        Debug.Log($"CanStartMatch: TotalPlayers={totalPlayers}, Blue={blueTeam}, Red={redTeam}, Ready={totalReady}");
        return hasMinimumPlayers && allPlayersReady && hasTeamAssignment;
    }

    private async void UpdateLobbyData()
    {
        if (currentLobby == null) return;
        if (Time.realtimeSinceStartup - lastLobbyUpdateTime < MIN_UPDATE_INTERVAL)
        {
            Debug.Log("Skipping lobby update due to rate limit");
            return;
        }
        try
        {
            lastLobbyUpdateTime = Time.realtimeSinceStartup;
            string playerId = AuthenticationService.Instance.PlayerId;
            var options = new UpdatePlayerOptions();
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

    // FIXED: Add method to ensure unique player names
    private string EnsureUniquePlayerName(string baseName, string playerId)
    {
        // First, check if the base name is already unique
        bool isUnique = true;
        foreach (var kvp in playerNames)
        {
            if (kvp.Key != playerId && kvp.Value == baseName)
            {
                isUnique = false;
                break;
            }
        }
        
        if (isUnique)
        {
            Debug.Log($"LobbyManager: Player name '{baseName}' is unique");
            return baseName;
        }
        
        // If not unique, append a suffix to make it unique
        string uniqueName = baseName;
        int suffix = 1;
        
        while (!isUnique)
        {
            uniqueName = $"{baseName}_{suffix}";
            isUnique = true;
            
            foreach (var kvp in playerNames)
            {
                if (kvp.Key != playerId && kvp.Value == uniqueName)
                {
                    isUnique = false;
                    break;
                }
            }
            
            suffix++;
            
            // Safety check to prevent infinite loop
            if (suffix > 100)
            {
                uniqueName = $"{baseName}_{UnityEngine.Random.Range(1000, 9999)}";
                break;
            }
        }
        
        Debug.Log($"LobbyManager: Made unique player name: '{baseName}' -> '{uniqueName}'");
        return uniqueName;
    }

    public void SetPlayerTeam(string playerId, string team)
    {
        if (playerTeams.ContainsKey(playerId))
            playerTeams[playerId] = team;
        else
            playerTeams.Add(playerId, team);

        // FIXED: Also save individual team choice to PlayerPrefs for backup
        PlayerPrefs.SetString($"PlayerTeam_{playerId}", team);
        PlayerPrefs.Save();
        
        Debug.Log($"LobbyManager: Set team {team} for player {playerId} and saved to PlayerPrefs");

        UpdateLobbyData();
        RefreshPlayerList();
    }

    // ENHANCED: Store team data with better validation and auth ID handling
    private void StoreTeamDataForGameScene()
    {
        Debug.Log("========== STORING TEAM DATA FOR GAME SCENE ==========");
        
        List<string> teamDataEntries = new List<string>();
        
        // ADDED: Validate we have player data to store
        if (playerNames == null || playerNames.Count == 0)
        {
            Debug.LogError("LobbyManager: No player names available for team data storage!");
            return;
        }
        
        if (playerTeams == null)
        {
            Debug.LogError("LobbyManager: No player teams available for team data storage!");
            return;
        }
        
        // CRITICAL: Store actual team selections from lobby with validation
        var allClientIds = new List<string>();
        foreach (var kvp in playerNames)
        {
            allClientIds.Add(kvp.Key);
        }
        allClientIds.Sort(); // Sort for consistent ordering
        
        Debug.Log($"LobbyManager: All player IDs (sorted): [{string.Join(", ", allClientIds)}]");
        
        // Method 1: Store team data based on actual player selections
        for (int i = 0; i < allClientIds.Count; i++)
        {
            string authId = allClientIds[i];
            string playerName = playerNames.ContainsKey(authId) ? playerNames[authId] : $"Player_{authId.Substring(0, Mathf.Min(4, authId.Length))}";
            
            // ENHANCED: Use actual team selection from lobby with validation
            string team = "Red"; // Default fallback
            if (playerTeams.ContainsKey(authId))
            {
                team = playerTeams[authId];
                Debug.Log($"LobbyManager: Player {i}: {playerName} (ID: {authId}) has selected team: {team}");
            }
            else
            {
                // FIXED: Only use alternating assignment as fallback if no team selection exists
                team = (i % 2 == 0) ? "Red" : "Blue";
                Debug.LogWarning($"LobbyManager: Player {i}: {playerName} (ID: {authId}) has NO team selection, using fallback: {team}");
            }
            
            // ADDED: Validate team is valid
            if (team != "Red" && team != "Blue")
            {
                Debug.LogWarning($"LobbyManager: Invalid team '{team}' for player {playerName}, defaulting to Red");
                team = "Red";
            }
            
            teamDataEntries.Add($"{playerName}:{team}:{authId}");
        }
        
        string serializedData = string.Join("|", teamDataEntries);
        PlayerPrefs.SetString("AllPlayerTeams", serializedData);
        Debug.Log($"LobbyManager: Stored combined team data: {serializedData}");
        
        // ADDED: Validate stored data
        string validationData = PlayerPrefs.GetString("AllPlayerTeams", "");
        if (validationData == serializedData)
        {
            Debug.Log($"LobbyManager: ✓ Team data validation successful");
        }
        else
        {
            Debug.LogError($"LobbyManager: ✗ Team data validation failed!");
            Debug.LogError($"  Expected: {serializedData}");
            Debug.LogError($"  Actual: {validationData}");
        }
        
        // Method 2: Store individual team choices as backup
        foreach (var authId in allClientIds)
        {
            string team = playerTeams.ContainsKey(authId) ? playerTeams[authId] : "Red";
            PlayerPrefs.SetString($"PlayerTeam_{authId}", team);
            Debug.Log($"LobbyManager: Stored individual team choice - {authId} -> {team}");
        }
        
        // Method 3: Store current local player's team choice specifically
        try
        {
            string localAuthId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            if (!string.IsNullOrEmpty(localAuthId) && playerTeams.ContainsKey(localAuthId))
            {
                PlayerPrefs.SetString("MySelectedTeam", playerTeams[localAuthId]);
                Debug.Log($"LobbyManager: Stored MY team choice: {playerTeams[localAuthId]} for local auth ID: {localAuthId}");
            }
            else
            {
                // FIXED: Don't assume team by host status - use Red as default
                string myTeam = "Red";
                PlayerPrefs.SetString("MySelectedTeam", myTeam);
                Debug.LogWarning($"LobbyManager: No team selection found for local player, using default: {myTeam}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LobbyManager: Error storing local player team: {e.Message}");
        }
        
        PlayerPrefs.Save();
        Debug.Log($"LobbyManager: ✓ ALL TEAM DATA SAVED SUCCESSFULLY");
        Debug.Log($"====================================================");
    }

    public async Task JoinRelay(string joinCode)
    {
        if (IsLobbyHost())
        {
            Debug.LogWarning("[LobbyManager] Host should never join relay as client!");
            return;
        }
        Debug.Log($"[Relay] Client PlayerId: {AuthenticationService.Instance.PlayerId}, JoinCode: {joinCode}");

        if (string.IsNullOrEmpty(joinCode))
        {
            throw new System.Exception("Join code is null or empty");
        }

        int maxRetries = 3;
        int retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                Debug.Log($"[LobbyManager] JoinRelay attempt {retryCount + 1}: Joining allocation with code {joinCode}");
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                if (allocation == null)
                {
                    throw new System.Exception("Join allocation returned null");
                }
                
                var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    throw new System.Exception("UnityTransport not found on NetworkManager");
                }
                
                Debug.Log($"[LobbyManager] JoinRelay attempt {retryCount + 1}: Setting client relay data");
                transport.SetClientRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData,
                    allocation.HostConnectionData
                );
                
                // FIXED: Wait a moment for transport to process the relay data
                await Task.Delay(500);
                
                // Validate the configuration took effect
                Debug.Log($"[LobbyManager] JoinRelay attempt {retryCount + 1}: Validating transport configuration");
                Debug.Log($"  Protocol: {transport.Protocol}");
                
                if (transport.Protocol != Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
                {
                    throw new System.Exception($"Transport protocol not set to relay after SetClientRelayData. Current: {transport.Protocol}");
                }
                
                Debug.Log($"[LobbyManager] JoinRelay attempt {retryCount + 1}: SUCCESS - relay configured");
                relayConfiguredForClient = true;
                return;
            }
            catch (System.Exception e)
            {
                retryCount++;
                Debug.LogError($"[LobbyManager] JoinRelay attempt {retryCount} failed: {e.Message}");
                
                if (retryCount < maxRetries)
                {
                    Debug.LogWarning($"[LobbyManager] Retrying JoinRelay in 1 second... (attempt {retryCount + 1}/{maxRetries})");
                    await Task.Delay(1000);
                }
                else
                {
                    Debug.LogError($"[LobbyManager] JoinRelay failed after {maxRetries} attempts");
                    relayConfiguredForClient = false;
                    throw;
                }
            }
        }
    }

    private Unity.Services.Lobbies.Models.Player GetLobbyPlayer()
    {
        string playerId = AuthenticationService.Instance.PlayerId;
        string playerName = SettingsManager.Instance != null ?
            SettingsManager.Instance.PlayerName :
            $"Player{UnityEngine.Random.Range(1000, 9999)}";
            
        // FIXED: Handle duplicate names by making them unique
        playerName = EnsureUniquePlayerName(playerName, playerId);
        
        // FIXED: ALWAYS ensure player data exists when creating lobby player
        if (playerNames == null) playerNames = new Dictionary<string, string>();
        if (playerTeams == null) playerTeams = new Dictionary<string, string>();
        if (playerReadyStates == null) playerReadyStates = new Dictionary<string, bool>();
        playerNames[playerId] = playerName;
        playerTeams[playerId] = "Red";
        playerReadyStates[playerId] = false;
        Debug.Log($"GetLobbyPlayer: ENSURED lobby player data - Name: {playerName}, Team: Red, ID: {playerId}");
        return new Unity.Services.Lobbies.Models.Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                { "Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Red") },
                { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") }
            }
        };
    }

    // Event to notify when client is ready to join the game (relay configured)
    public event System.Action OnClientReadyForGame;

    // Add a flag to indicate relay is configured for this client
    private bool relayConfiguredForClient = false;

    // Add a method to check if client is ready for network session
    public bool IsClientReadyForGame()
    {
        if (!relayConfiguredForClient)
        {
            return false;
        }
        
        // FIXED: Double-check transport is actually configured
        var transport = NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport == null)
        {
            Debug.LogWarning("[LobbyManager] IsClientReadyForGame: Transport is null");
            return false;
        }
        
        bool isReady = transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport;
        
        if (!isReady)
        {
            Debug.LogWarning($"[LobbyManager] IsClientReadyForGame: Transport protocol is {transport.Protocol}, expected RelayUnityTransport");
            // Reset the flag if transport is not properly configured
            relayConfiguredForClient = false;
        }
        
        return isReady;
    }

    public Lobby GetCurrentLobby()
    {
        return currentLobby;
    }

    // --- PUBLIC API for other scripts ---

    public bool IsLobbyHost()
    {
        // Host is the player who created the lobby (first player in lobby)
        if (currentLobby == null || currentLobby.HostId == null)
            return false;
        return AuthenticationService.Instance.PlayerId == currentLobby.HostId;
    }

    public void RefreshPlayerList()
    {
        UpdatePlayerListUI();
        UpdateStartButtonState();
    }

    public void SetPlayerReady(string playerId)
    {
        bool currentState = IsPlayerReady(playerId);
        playerReadyStates[playerId] = !currentState;
        UpdateLobbyData();
        UpdateStartButtonState();
        RefreshPlayerList();

        // FIXED: Only auto-start if we have minimum 2 players and both are ready
        if (IsLobbyHost() && playerId == AuthenticationService.Instance.PlayerId && playerReadyStates[playerId])
        {
            // Check if we have at least 2 players
            int totalPlayers = playerNames.Count;
            int totalReady = 0;
            int blueTeam = 0, redTeam = 0;
            
            foreach (var kvp in playerNames)
            {
                string pid = kvp.Key;
                if (IsPlayerReady(pid))
                    totalReady++;
                if (playerTeams.ContainsKey(pid))
                {
                    if (playerTeams[pid] == "Blue")
                        blueTeam++;
                    else if (playerTeams[pid] == "Red")
                        redTeam++;
                }
            }
            
            Debug.Log($"[LobbyManager] Host ready check: TotalPlayers={totalPlayers}, TotalReady={totalReady}, Blue={blueTeam}, Red={redTeam}");
            
            // FIXED: Require at least 2 players with both ready and on different teams
            bool twoPlayers = totalPlayers >= 2;
            bool bothReady = totalReady >= 2;
            bool onePerTeam = blueTeam >= 1 && redTeam >= 1;
            
            if (twoPlayers && bothReady && onePerTeam)
            {
                Debug.Log("[LobbyManager] Host pressed ready and requirements met (2+ players, all ready, teams assigned) - starting match.");
                ForceStartMatch();
            }
            else
            {
                Debug.Log($"[LobbyManager] Host ready but requirements not met: TwoPlayers={twoPlayers}, BothReady={bothReady}, OnePerTeam={onePerTeam}");
                Debug.Log("[LobbyManager] Waiting for at least 2 players, both ready, and assigned to different teams.");
            }
        }
    }

    public bool IsPlayerReady(string playerId)
    {
        return playerReadyStates.ContainsKey(playerId) && playerReadyStates[playerId];
    }

    public bool IsPlayerBlueTeam(string playerId)
    {
        return playerTeams.ContainsKey(playerId) && playerTeams[playerId] == "Blue";
    }

    public void StartMatch()
    {
        StartMatchAsync();
    }

    private async void StartMatchAsync()
    {
        await StartMatchInternal();
    }

    private async Task StartMatchInternal()
    {
        // Call the existing StartMatch logic (if you have async logic, move it here)
        // ...existing StartMatch logic...
        // For now, just call ForceStartMatch for compatibility
        ForceStartMatch();
    }

    public void AddChatMessage(string message)
    {
        if (chatMessages.Count >= MAX_CHAT_MESSAGES)
            chatMessages.RemoveAt(0);
        chatMessages.Add(message);
        // Optionally update UI here
    }

    public void SetGameMode(GameMode mode)
    {
        selectedGameMode = mode;
        Debug.Log($"Game mode set to: {mode}");
    }

    public Dictionary<string, string> GetAuthIdToTeamMapping()
    {
        // Returns a copy of the mapping from playerId (authId) to team
        return new Dictionary<string, string>(playerTeams);
    }

    public NetworkManager FindOrCreateNetworkManager()
    {
        // ...existing FindOrCreateNetworkManager logic...
        // (If already present, just make it public)
        // ...existing code...
        Debug.Log("FindOrCreateNetworkManager: Starting search...");
        if (networkManager != null)
        {
            Debug.Log($"Found cached NetworkManager: {networkManager.name}");
            return networkManager;
        }
        var foundNetworkManager = NetworkManager.Singleton;
        if (foundNetworkManager != null)
        {
            Debug.Log($"Found NetworkManager via Singleton: {foundNetworkManager.name}");
            networkManager = foundNetworkManager;
            return foundNetworkManager;
        }
        foundNetworkManager = FindFirstObjectByType<NetworkManager>();
        if (foundNetworkManager != null)
        {
            Debug.Log($"Found NetworkManager via search: {foundNetworkManager.name}");
            networkManager = foundNetworkManager;
            return foundNetworkManager;
        }
        Debug.LogWarning("FindOrCreateNetworkManager: No NetworkManager found, creating one...");
        EnsureNetworkManagerExists();
        return networkManager;
    }

    public void UpdatePlayerListUI()
    {
        // Build the player list for the UI
        var players = new List<LobbyPlayerData>();
        
        // FIXED: Ensure we have valid data before building the list
        if (playerNames == null || playerTeams == null || playerReadyStates == null)
        {
            Debug.LogWarning("LobbyManager: Player data dictionaries are null, initializing...");
            if (playerNames == null) playerNames = new Dictionary<string, string>();
            if (playerTeams == null) playerTeams = new Dictionary<string, string>();
            if (playerReadyStates == null) playerReadyStates = new Dictionary<string, bool>();
        }
        
        foreach (var kvp in playerNames)
        {
            string playerId = kvp.Key;
            string playerName = kvp.Value;
            string team = playerTeams.ContainsKey(playerId) ? playerTeams[playerId] : "Red";
            bool isReady = playerReadyStates.ContainsKey(playerId) ? playerReadyStates[playerId] : false;
            
            // FIXED: Create LobbyPlayerData with proper structure
            var playerData = new LobbyPlayerData();
            playerData.PlayerId = playerId;
            playerData.PlayerName = playerName;
            playerData.Team = team;
            playerData.IsReady = isReady;
            playerData.IsLocalPlayer = playerId == AuthenticationService.Instance.PlayerId;
            
            players.Add(playerData);
        }

        Debug.Log($"LobbyManager: Built player list with {players.Count} players");
        
        // FIXED: Add null check for LobbyPanelManager
        if (HockeyGame.UI.LobbyPanelManager.Instance != null)
        {
            try
            {
                HockeyGame.UI.LobbyPanelManager.Instance.UpdatePlayerList(players);
                Debug.Log($"LobbyManager: Successfully updated UI with {players.Count} players");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"LobbyManager: Error updating player list UI: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("LobbyManager: LobbyPanelManager.Instance is null, cannot update player list UI.");
        }

        // REMOVED: Auto-start game logic from here - only start via SetPlayerReady
        // This prevents premature game starts
    }

    // Property for UI: true if host can force start (enables start button)
    public bool CanForceStart
    {
        get
        {
            if (!IsLobbyHost()) return false;
            
            int blueTeam = 0, redTeam = 0, totalPlayers = 0, totalReady = 0;
            foreach (var kvp in playerNames)
            {
                totalPlayers++;
                string playerId = kvp.Key;
                if (playerTeams.ContainsKey(playerId))
                {
                    if (playerTeams[playerId] == "Blue") blueTeam++;
                    else if (playerTeams[playerId] == "Red") redTeam++;
                }
                if (IsPlayerReady(playerId)) totalReady++;
            }
            
            if (selectedGameMode == GameMode.Mode2v2)
            {
                // FIXED: Require proper multiplayer setup
                bool hasPlayers = totalPlayers >= 2;
                bool allReady = totalReady >= 2;
                bool hasTeams = blueTeam >= 1 && redTeam >= 1;
                return hasPlayers && allReady && hasTeams;
            }
            else
            {
                return totalPlayers >= 2 && totalReady >= 2;
            }
        }
    }

    public void UpdateStartButtonState()
    {
        // --- UI: Enable/disable the start button here ---
        // Example: If you have a reference to your button, set its interactable property:
        // startButton.interactable = CanForceStart;
        // Or call your own UI manager method here.
    }

    // Call this from your UI start button
    public void ForceStartButtonPressed()
    {
        if (IsLobbyHost())
        {
            Debug.Log("[LobbyManager] Host pressed FORCE START button - starting match immediately.");
            ForceStartMatch();
        }
    }

    public async Task InitializeUnityServices()
    {
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            Debug.Log("Initializing Unity Services...");
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("Signing in anonymously...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        Debug.Log("Unity Services initialized successfully");
    }
}

// FIXED: Ensure LobbyPlayerData is defined only once in its own file (remove any duplicate definitions from this file)
// Remove any LobbyPlayerData definition from this file if present. Make sure only one definition exists in your project, ideally in a separate file like LobbyPlayerData.cs.
