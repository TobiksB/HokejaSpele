using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance { get; private set; }

    private GameObject prefabToAssign;

    [Header("Prefabs")]
    [SerializeField] public GameObject playerPrefabReference; 

    [Header("Network Settings")]
    [SerializeField] private float sceneLoadTimeout = 30f;
    [SerializeField] private float hostStartDelay = 1f;

    [Header("Player Spawn Points")]
    [SerializeField] private Transform[] redTeamSpawns;
    [SerializeField] private Transform[] blueTeamSpawns;

    private bool isLoadingScene = false;
    private string pendingSceneName = "";

    private Dictionary<string, int> teamSpawnCounters = new Dictionary<string, int>();

    private Dictionary<ulong, string> clientAuthIds = new Dictionary<ulong, string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameNetworkManager: Instance created and set to DontDestroyOnLoad");

            ValidateSpawnPoints();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
            {
                NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
                NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null; 
                Debug.Log($"[GameNetworkManager] Inspector playerPrefabReference: {(playerPrefabReference != null ? playerPrefabReference.name : "null")}")
;
                Debug.Log($"[GameNetworkManager] NetworkConfig.PlayerPrefab BEFORE: {(NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab.name : "null")}");

                prefabToAssign = playerPrefabReference;
                if (prefabToAssign == null)
                {
                    prefabToAssign = Resources.Load<GameObject>("Prefabs/Player");
                    if (prefabToAssign != null)
                    {
                        Debug.Log("[GameNetworkManager] Loaded PlayerPrefab from Resources/Prefabs/Player");
                    }
                }
                if (prefabToAssign == null && NetworkManager.Singleton.NetworkConfig.Prefabs != null)
                {
                    foreach (var np in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                    {
                        if (np.Prefab != null && np.Prefab.name.ToLower().Contains("player"))
                        {
                            prefabToAssign = np.Prefab;
                            Debug.Log($"[GameNetworkManager] Fallback: Found PlayerPrefab in NetworkPrefabs: {prefabToAssign.name}");
                            break;
                        }
                    }
                }
                Debug.Log($"[GameNetworkManager] Stored PlayerPrefab for manual spawning: {(prefabToAssign != null ? prefabToAssign.name : "null")}");
            }
            else
            {
                Debug.LogWarning("[GameNetworkManager] Could not set PlayerPrefab on NetworkManager.Singleton (missing reference or prefab)");
            }

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            var allManagers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            foreach (var nm in allManagers)
            {
                if (nm != NetworkManager.Singleton)
                {
                    Debug.LogWarning("GameNetworkManager: Destroying duplicate NetworkManager in scene");
                    Destroy(nm.gameObject);
                }
            }
            if (NetworkManager.Singleton != null)
            {
                DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback = ConnectionApprovalCheck;
                Debug.Log("GameNetworkManager: Set ConnectionApprovalCallback");
            }

            // Inicializē spawn skaitītājus katrai komandai
            teamSpawnCounters["Red"] = 0;
            teamSpawnCounters["Blue"] = 0;
        }
        else
        {
            Debug.Log("GameNetworkManager: Duplicate instance destroyed");
            Destroy(gameObject);
        }

        // NetworkSpawnManager nav nepieciešams - ConnectionApprovalCheck apstrādā visus spawnus tieši
    }

    private NetworkManager GetTrueNetworkManager()
    {
        // Vienmēr atrod īsto DontDestroyOnLoad instanci
        var allManagers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
        foreach (var nm in allManagers)
        {
            if (nm != null && nm.gameObject.scene.name == "DontDestroyOnLoad")
                return nm;
        }
        // Ja nav, izmanto Singleton
        return NetworkManager.Singleton;
    }

    public void StartGame(string sceneName)
    {
        if (LobbyManager.Instance != null && !LobbyManager.Instance.IsLobbyHost())
        {
            // Allow client to start if relay is ready and IsClientReadyForGame
            if (!LobbyManager.Instance.IsClientReadyForGame())
            {
                Debug.LogWarning("[GameNetworkManager] StartGame called on CLIENT but relay not ready. Waiting for relay...");
                return;
            }
            Debug.LogWarning("[GameNetworkManager] StartGame called on CLIENT with relay ready. Proceeding to start client.");
        }

        Debug.LogWarning($"[GameNetworkManager] StartGame CALLED on {(NetworkManager.Singleton?.IsHost == true ? "HOST" : "CLIENT")} with scene: {sceneName}, isLoadingScene={isLoadingScene}, IsClient={NetworkManager.Singleton?.IsClient}, IsHost={NetworkManager.Singleton?.IsHost}");
        
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu" && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("GameNetworkManager: Hosts jau darbojas MainMenu - izslēdz pirms spēles sākšanas");
            NetworkManager.Singleton.Shutdown();
            // Pagaida vienu kadru, lai pabeigtu izslēgšanu
            StartCoroutine(DelayedGameStart(sceneName));
            return;
        }
        
        // Ja aina jau tiek ielādēta, ignorē atkārtotu pieprasījumu
        if (isLoadingScene)
        {
            Debug.LogWarning("GameNetworkManager: Ainas ielāde jau notiek, pieprasījums ignorēts");
            return;
        }

        pendingSceneName = sceneName;

        // Pārbauda, vai NetworkManager eksistē un ir pareizi konfigurēts
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("GameNetworkManager: NetworkManager.Singleton nav atrasts! Nevar startēt tīkla spēli.");
            return;
        }

        // LABOTS: Nodrošina, ka hosts nekad nemēģina startēt kā klients
        bool shouldBeHost = ShouldStartAsHost();
        if (shouldBeHost)
        {
            Debug.Log("[GameNetworkManager] Host detected, will only start as host (never as client)");
        }
        else
        {
            if (LobbyManager.Instance != null && LobbyManager.Instance.IsLobbyHost())
            {
                Debug.LogWarning("[GameNetworkManager] Host should never start as client. Skipping StartClient.");
                return;
            }
            Debug.Log("[GameNetworkManager] Client detected, will only start as client");
        }

        if (!shouldBeHost)
        {
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null || transport.Protocol != Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
            {
                Debug.LogError("[GameNetworkManager] Client relay transport is NOT configured! Aborting StartClient. Wait for relay to be set up.");
                isLoadingScene = false;
                return;
            }

            if (LobbyManager.Instance != null && !LobbyManager.Instance.IsClientReadyForGame())
            {
                Debug.Log("[GameNetworkManager] Client relay not configured yet, waiting for relay before starting client...");
                LobbyManager.Instance.OnClientReadyForGame -= StartClientAfterRelayReady;
                LobbyManager.Instance.OnClientReadyForGame += StartClientAfterRelayReady;
                pendingSceneName = sceneName;
                return;
            }
            else
            {
                Debug.Log("[GameNetworkManager] Client relay already configured, starting client immediately...");
            }
        }

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
        {
            Debug.Log($"GameNetworkManager: Shutting down existing connection before starting fresh");
            Debug.Log($"  Was Host: {NetworkManager.Singleton.IsHost}");
            Debug.Log($"  Was Server: {NetworkManager.Singleton.IsServer}");
            Debug.Log($"  Was Client: {NetworkManager.Singleton.IsClient}");
            
            NetworkManager.Singleton.Shutdown();
            
            // Wait for shutdown to complete, then start fresh
            StartCoroutine(WaitForShutdownThenStart(shouldBeHost, sceneName));
            return;
        }

        bool shouldLoadScene = false;
        // Start fresh connection
        if (shouldBeHost)
        {
            Debug.Log("GameNetworkManager: Starting as host...");
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeScene != sceneName)
            {
                Debug.Log($"[GameNetworkManager] Host is in '{activeScene}', needs to be in '{sceneName}'. Loading scene first...");
                shouldLoadScene = true;
            }
            else
            {
                StartCoroutine(StartHostAndLoadScene(sceneName));
            }
        }
        else
        {
            Debug.Log("GameNetworkManager: Starting as client...");
            StartCoroutine(StartClientAndWaitForScene());
        }

        if (shouldLoadScene)
        {
            StartCoroutine(LoadSceneThenStartHost(sceneName));
        }
    }

    private System.Collections.IEnumerator WaitForShutdownThenStart(bool shouldBeHost, string sceneName)
    {
        Debug.Log("GameNetworkManager: Waiting for NetworkManager shutdown to complete...");
        
        // Wait for shutdown to complete
        float shutdownTimeout = 5f;
        float elapsed = 0f;
        
        while ((NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient) && elapsed < shutdownTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (elapsed >= shutdownTimeout)
        {
            Debug.LogWarning("GameNetworkManager: Shutdown timeout - forcing restart anyway");
        }
        else
        {
            Debug.Log("GameNetworkManager: Shutdown completed, starting fresh connection");
        }
        
        // Now start fresh
        if (shouldBeHost)
        {
            Debug.Log("GameNetworkManager: Starting as host after shutdown...");
            StartCoroutine(StartHostAndLoadScene(sceneName));
        }
        else
        {
            Debug.Log("GameNetworkManager: Starting as client after shutdown...");
            StartCoroutine(StartClientAndWaitForScene());
        }
    }

    private System.Collections.IEnumerator DelayedGameStart(string sceneName)
    {
        yield return null; // Wait one frame for shutdown
        
        // Now start the game properly
        StartGame(sceneName);
    }

    private bool ShouldStartAsHost()
    {
        // Check if we're the lobby host
        if (LobbyManager.Instance != null && LobbyManager.Instance.IsLobbyHost())
        {
            Debug.Log("GameNetworkManager: We are the lobby host, starting as network host");
            return true;
        }
        
        // Fallback: if no lobby or not lobby host, assume we should be client
        Debug.Log("GameNetworkManager: We are not the lobby host, starting as network client");
        return false;
    }

    private void StartClientAfterRelayReady()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnClientReadyForGame -= StartClientAfterRelayReady;
        }
        Debug.Log("GameNetworkManager: Relay ready, starting client...");
        
        if (!string.IsNullOrEmpty(pendingSceneName))
        {
            Debug.Log($"GameNetworkManager: Using pending scene name: {pendingSceneName}");
        }
        
        StartCoroutine(StartClientAndWaitForScene());
    }

    private IEnumerator StartClientAndWaitForScene()
    {
        isLoadingScene = true;
        Debug.LogWarning("GameNetworkManager: [CLIENT] Starting client for networked game...");

        // --- CRITICAL: Only allow StartClient in MainMenu ---
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            Debug.LogError("GameNetworkManager: Client must call StartClient() in MainMenu scene! Aborting client start.");
            isLoadingScene = false;
            yield break;
        }

        // --- CRITICAL: Always set PlayerPrefab on NetworkManager.Singleton before starting client ---
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
        {
            GameObject prefabToAssign = playerPrefabReference;
            if (prefabToAssign == null)
            {
                prefabToAssign = Resources.Load<GameObject>("Prefabs/Player");
            }

            if (prefabToAssign != null)
            {
                NetworkManager.Singleton.NetworkConfig.PlayerPrefab = prefabToAssign;
                Debug.Log($"GameNetworkManager: Set PlayerPrefab on NetworkManager.Singleton: {prefabToAssign.name}");
            }
        }

        if (NetworkManager.Singleton != null)
        {
            string authId = "";
            try
            {
                authId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            }
            catch { }
            if (!string.IsNullOrEmpty(authId))
            {
                NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(authId);
                Debug.Log($"GameNetworkManager: Set ConnectionData to authId: {authId}");
            }
        }

        // Validate transport configuration
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport == null || transport.Protocol != Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
        {
            Debug.LogError("GameNetworkManager: Client transport is NOT configured for relay! Aborting StartClient.");
            isLoadingScene = false;
            yield break;
        }

        // Start client - ConnectionApprovalCheck will handle spawning
        bool clientStarted = false;
        try
        {
            Debug.LogWarning("GameNetworkManager: [CLIENT] Calling NetworkManager.Singleton.StartClient()");
            clientStarted = NetworkManager.Singleton.StartClient();
            Debug.LogWarning($"GameNetworkManager: [CLIENT] StartClient() returned: {clientStarted}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GameNetworkManager: Exception while starting client: {e.Message}");
            isLoadingScene = false;
            yield break;
        }
        
        if (!clientStarted)
        {
            Debug.LogError("GameNetworkManager: Failed to start as client!");
            isLoadingScene = false;
            yield break;
        }

        // Wait for connection
        float timeout = 30f;
        float elapsed = 0f;
        while (elapsed < timeout && !NetworkManager.Singleton.IsConnectedClient)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogError($"GameNetworkManager: Client failed to connect within {timeout} seconds");
            isLoadingScene = false;
            yield break;
        }

        Debug.Log("GameNetworkManager: Client connected successfully!");

        // Load scene if needed
        string expectedScene = pendingSceneName;
        string currentSceneNow = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(expectedScene) && currentSceneNow != expectedScene)
        {
            Debug.LogWarning($"GameNetworkManager: Client loading scene {expectedScene}");
            UnityEngine.SceneManagement.SceneManager.LoadScene(expectedScene);
            
            float sceneLoadWait = 10f;
            float sceneElapsed = 0f;
            while (sceneElapsed < sceneLoadWait)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == expectedScene)
                    break;
                sceneElapsed += Time.deltaTime;
                yield return null;
            }
        }

        Debug.Log("GameNetworkManager: Client setup complete - ConnectionApprovalCheck will handle player spawning");
        isLoadingScene = false;
    }

    private IEnumerator StartHostAndLoadScene(string sceneName)
    {
        isLoadingScene = true;
        Debug.Log("GameNetworkManager: Starting host for networked game...");

        // Atspējo PlayerPrefab uz laiku, lai novērstu automātisku spēlētāju spawn
        var allManagers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
        foreach (var nm in allManagers)
        {
            if (nm != null && nm.NetworkConfig != null)
            {
                nm.NetworkConfig.PlayerPrefab = null; // Atspējo automātisko spawn
            }
        }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
        {
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null; // Atspējo automātisko spawn
            Debug.Log("GameNetworkManager: PlayerPrefab atspējots, lai novērstu automātisko spawn");
        }

        bool hostStarted = false;
        try
        {
            Debug.Log("GameNetworkManager: Calling NetworkManager.StartHost()...");
            hostStarted = NetworkManager.Singleton.StartHost();
            Debug.Log($"GameNetworkManager: StartHost() returned: {hostStarted}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GameNetworkManager: Exception while starting host: {e.Message}");
            isLoadingScene = false;
            yield break;
        }
        
        if (!hostStarted)
        {
            Debug.LogError("GameNetworkManager: StartHost() returned false!");
            isLoadingScene = false;
            yield break;
        }

        yield return new WaitForSeconds(hostStartDelay);

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogError("GameNetworkManager: Host failed to start properly!");
            isLoadingScene = false;
            yield break;
        }

        Debug.Log($"GameNetworkManager: ✓ Host started successfully!");

        // Wait for clients
        int expectedPlayers = GetExpectedPlayerCount();
        Debug.Log($"GameNetworkManager: Waiting for {expectedPlayers} total players...");

        float maxWaitTime = 120f;
        float elapsed = 0f;
        
        while (elapsed < maxWaitTime)
        {
            int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
            if (connectedCount >= expectedPlayers)
            {
                Debug.Log($"GameNetworkManager: All expected clients connected! ({connectedCount}/{expectedPlayers})");
                yield return new WaitForSeconds(3f);
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"GameNetworkManager: Proceeding to load scene");
        LoadSceneAsServer(sceneName);
    }

    // FIXED: Add method to get expected player count from lobby
    private int GetExpectedPlayerCount()
    {
        int expectedPlayers = 1; // Host always counts as 1
        
        if (LobbyManager.Instance != null)
        {
            // Use reflection to access private currentLobby field
            var lobbyField = typeof(LobbyManager).GetField("currentLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (lobbyField != null)
            {
                var lobby = lobbyField.GetValue(LobbyManager.Instance) as Unity.Services.Lobbies.Models.Lobby;
                if (lobby != null)
                {
                    expectedPlayers = lobby.Players.Count;
                    Debug.Log($"GameNetworkManager: Expected players from lobby: {expectedPlayers}");
                }
            }
            
            // Fallback: check player count from LobbyManager dictionaries
            if (expectedPlayers == 1)
            {
                var playerNamesField = typeof(LobbyManager).GetField("playerNames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (playerNamesField != null)
                {
                    var playerNames = playerNamesField.GetValue(LobbyManager.Instance) as System.Collections.Generic.Dictionary<string, string>;
                    if (playerNames != null && playerNames.Count > 0)
                    {
                        expectedPlayers = playerNames.Count;
                        Debug.Log($"GameNetworkManager: Expected players from playerNames: {expectedPlayers}");
                    }
                }
            }
        }
        
        // Ensure minimum of 2 players for multiplayer
        if (expectedPlayers < 2)
        {
            Debug.LogWarning($"GameNetworkManager: Expected players ({expectedPlayers}) less than 2, setting to 2");
            expectedPlayers = 2;
        }
        
        return expectedPlayers;
    }

    private void LoadSceneAsServer(string sceneName)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("GameNetworkManager: LoadSceneAsServer called but not server!");
            isLoadingScene = false;
            return;
        }

        try
        {
            Debug.Log($"GameNetworkManager: Loading networked scene: {sceneName}");
            
            // Use NetworkManager's scene management for proper client synchronization
            var sceneLoadResult = NetworkManager.Singleton.SceneManager.LoadScene(
                sceneName, 
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );

            if (sceneLoadResult != Unity.Netcode.SceneEventProgressStatus.Started)
            {
                Debug.LogError($"GameNetworkManager: Failed to start scene load. Status: {sceneLoadResult}");
                isLoadingScene = false;
                return;
            }

            Debug.Log($"GameNetworkManager: Scene load started successfully for {sceneName}");
            
            // Set up scene load completion monitoring
            StartCoroutine(MonitorSceneLoad(sceneName));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GameNetworkManager: Exception during scene load: {e.Message}");
            isLoadingScene = false;
        }
    }

    private IEnumerator MonitorSceneLoad(string sceneName)
    {
        float timeElapsed = 0f;
        
        while (timeElapsed < sceneLoadTimeout && isLoadingScene)
        {
            // Check if scene load completed
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneName)
            {
                Debug.Log($"GameNetworkManager: Scene {sceneName} loaded successfully for all clients");
                isLoadingScene = false;
                OnSceneLoadComplete(sceneName);
                yield break;
            }
            
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        
        if (isLoadingScene)
        {
            Debug.LogError($"GameNetworkManager: Scene load timeout after {sceneLoadTimeout} seconds");
            isLoadingScene = false;
        }
    }

    private void OnSceneLoadComplete(string sceneName)
    {
        Debug.Log($"GameNetworkManager: Scene {sceneName} fully loaded, network game ready");

        // restarte spawn counterus
        ResetSpawnCounters();

        // Pievieno atpakal playerprefabu
        if (prefabToAssign != null)
        {
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = prefabToAssign;
            Debug.Log($"GameNetworkManager: Re-assigned PlayerPrefab after scene load: {prefabToAssign.name}");
        }

        // Assign to all NetworkManagers
        var allManagers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
        foreach (var nm in allManagers)
        {
            if (nm != null && nm.NetworkConfig != null)
            {
                if (prefabToAssign != null)
                {
                    nm.NetworkConfig.PlayerPrefab = prefabToAssign;
                }
                nm.ConnectionApprovalCallback = ConnectionApprovalCheck;
            }
        }
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
        {
            if (prefabToAssign != null)
            {
                NetworkManager.Singleton.NetworkConfig.PlayerPrefab = prefabToAssign;
            }
            NetworkManager.Singleton.ConnectionApprovalCallback = ConnectionApprovalCheck;
            Debug.Log("GameNetworkManager: Reassigned ConnectionApprovalCallback after scene load");
        }

        // Clear single player mode
        PlayerPrefs.SetInt("SinglePlayerMode", 0);
        PlayerPrefs.Save();
        
        Debug.Log("GameNetworkManager: Scene load complete - manually spawning all players at inspector spawn points");
        
        if (NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(ManuallySpawnAllPlayers());
        }
    }

    private IEnumerator ManuallySpawnAllPlayers()
    {
        Debug.Log("GameNetworkManager: Starting manual spawn process for all connected players");
        
        // Wait a moment for scene to fully load
        yield return new WaitForSeconds(1f);
        
        // Get all connected client IDs
        var connectedClients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        Debug.Log($"GameNetworkManager: Found {connectedClients.Count} connected clients to spawn");
        
        // Spawn each client manually
        foreach (ulong clientId in connectedClients)
        {
            Debug.Log($"GameNetworkManager: Attempting to spawn client {clientId}");
            
            // Check if client already has a player object
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            {
                if (clientData.PlayerObject != null)
                {
                    Debug.LogWarning($"GameNetworkManager: Client {clientId} already has player object, skipping spawn");
                    continue;
                }
            }
            
            // Manually spawn the player
            ManuallySpawnPlayer(clientId);
            
            // Small delay between spawns to avoid overwhelming the network
            yield return new WaitForSeconds(0.2f);
        }
        
        Debug.Log("GameNetworkManager: Manual spawn process completed for all players");
    }

    private string GetTeamForClient(ulong clientId)
    {
        Debug.Log($"========== GET TEAM FOR CLIENT {clientId} ==========");
        
        try
        {
            string teamData = PlayerPrefs.GetString("AllPlayerTeams", "");
            if (!string.IsNullOrEmpty(teamData))
            {
                Debug.Log($"GameNetworkManager: Found stored team data: {teamData}");
                var entries = teamData.Split('|');
                foreach (var entry in entries)
                {
                    var parts = entry.Split(':');
                    if (parts.Length >= 3)
                    {
                        string playerName = parts[0];
                        string team = parts[1];
                        string authId = parts[2];
                        
                        Debug.Log($"GameNetworkManager: Checking entry - Name: {playerName}, Team: {team}, AuthId: {authId}");
                        
                        string clientAuthId = GetAuthIdForClient(clientId);
                        if (!string.IsNullOrEmpty(clientAuthId) && clientAuthId == authId)
                        {
                            Debug.Log($"GameNetworkManager: ✓ EXACT AUTH ID MATCH for client {clientId}: {team}");
                            return team;
                        }
                        
                        if (clientId == NetworkManager.Singleton.LocalClientId)
                        {
                            try
                            {
                                string localAuthId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                                if (localAuthId == authId)
                                {
                                    Debug.Log($"GameNetworkManager: ✓ LOCAL AUTH ID MATCH for client {clientId}: {team}");
                                    return team;
                                }
                            }
                            catch (System.Exception e)
                            {
                                // Ja rodas kļūda, iegūstot lokālo autentifikācijas ID, izvada brīdinājumu
                                Debug.LogWarning($"GameNetworkManager: Error getting local auth ID: {e.Message}");
                            }
                        }
                    }
                }
                // Ja nav atrasta atbilstoša autentifikācijas ID klientam, izvada brīdinājumu
                Debug.LogWarning($"GameNetworkManager: No auth ID match found for client {clientId} in stored team data");
            }
            else
            {
                // Ja nav saglabātu komandu datu PlayerPrefs, izvada brīdinājumu
                Debug.LogWarning("GameNetworkManager: No stored team data found in PlayerPrefs");
            }
        }
        catch (System.Exception e)
        {
            // Ja rodas kļūda, lasot komandu datus, izvada brīdinājumu
            Debug.LogWarning($"GameNetworkManager: Error reading team data: {e.Message}");
        }
        
        // 2. metode: mēģina iegūt individuālo komandas izvēli tikai lokālajam klientam
        try
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                string fallbackTeam = PlayerPrefs.GetString("MySelectedTeam", "");
                if (!string.IsNullOrEmpty(fallbackTeam))
                {
                    Debug.Log($"GameNetworkManager: ✓ FALLBACK TEAM for local client {clientId}: {fallbackTeam}");
                    return fallbackTeam;
                }
            }
        }
        catch (System.Exception e)
        {
            // Ja rodas kļūda, lasot rezerves komandas izvēli, izvada brīdinājumu
            Debug.LogWarning($"GameNetworkManager: Error reading fallback team: {e.Message}");
        }
        
        // 3. metode: noteikta piešķiršana, pamatojoties uz klienta ID, konsekvences nodrošināšanai
        var allClientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        allClientIds.Sort(); // Sort to ensure consistent ordering across all clients
        
        int clientIndex = allClientIds.IndexOf(clientId);
        if (clientIndex == -1)
        {
            Debug.LogWarning($"GameNetworkManager: Client {clientId} not found in connected clients list!");
            clientIndex = (int)(clientId % 2);
        }
        
        string assignedTeam = (clientIndex % 2 == 0) ? "Red" : "Blue";
        
        Debug.Log($"GameNetworkManager: ✓ DETERMINISTIC ASSIGNMENT for client {clientId}:");
        Debug.Log($"  Client Index: {clientIndex} of {allClientIds.Count} total clients");
        Debug.Log($"  All Client IDs (sorted): [{string.Join(", ", allClientIds)}]");
        Debug.Log($"  Assigned Team: {assignedTeam}");
        Debug.LogWarning($"  WARNING: Using fallback assignment - team data should be stored properly in lobby");
        Debug.Log($"==============================================");
        
        return assignedTeam;
    }

    private Vector3 GetSpawnPositionFromInspector(ulong clientId, string team)
    {
        Debug.Log($"========== GET SPAWN POSITION FROM INSPECTOR ==========");
        Debug.Log($"Client: {clientId}, Team: {team}");
        
        Transform[] teamSpawns = team == "Blue" ? blueTeamSpawns : redTeamSpawns;
        
        Debug.Log($"Selected spawn array: {(team == "Blue" ? "blueTeamSpawns" : "redTeamSpawns")}");
        Debug.Log($"Team spawns array null check: {(teamSpawns != null ? "NOT NULL" : "NULL")}");
        
        if (teamSpawns != null)
        {
            Debug.Log($"Team spawns count: {teamSpawns.Length}");
            for (int i = 0; i < teamSpawns.Length; i++)
            {
                if (teamSpawns[i] != null)
                {
                    Debug.Log($"  Spawn {i}: {teamSpawns[i].name} at {teamSpawns[i].position}");
                }
                else
                {
                    Debug.LogError($"  Spawn {i}: NULL TRANSFORM!");
                }
            }
        }
        
        if (teamSpawns == null || teamSpawns.Length == 0)
        {
            Debug.LogError($"GameNetworkManager: ✗ NO {team} TEAM SPAWN TRANSFORMS ASSIGNED!");
            Debug.LogError("Please assign spawn points in the GameNetworkManager inspector!");
            
            Vector3 fallback;
            if (team == "Blue")
            {
                fallback = new Vector3(8f, 0.71f, 0f); // Blue side (right side of rink)
            }
            else
            {
                fallback = new Vector3(-8f, 0.71f, 0f); // Red side (left side of rink)
            }
            
            Debug.LogError($"Using emergency fallback position for {team} team: {fallback}");
            return fallback;
        }
        
        int spawnIndex = GetNextSpawnIndexForTeam(team, teamSpawns.Length);
        Debug.Log($"Sequential spawn index for {team} team: {spawnIndex}");
        
        if (spawnIndex < 0 || spawnIndex >= teamSpawns.Length)
        {
            Debug.LogError($"GameNetworkManager: Invalid spawn index {spawnIndex} for team {team} (max: {teamSpawns.Length - 1})");
            spawnIndex = 0; // Use first spawn as fallback
        }
        
        Transform spawnTransform = teamSpawns[spawnIndex];
        
        if (spawnTransform == null)
        {
            Debug.LogError($"GameNetworkManager: ✗ {team} team spawn transform at index {spawnIndex} is NULL!");
            
            // Find first valid transform
            for (int i = 0; i < teamSpawns.Length; i++)
            {
                if (teamSpawns[i] != null)
                {
                    spawnTransform = teamSpawns[i];
                    Debug.LogWarning($"Using valid spawn at index {i}: {spawnTransform.name}");
                    break;
                }
            }
        }
        
        if (spawnTransform == null)
        {
            Debug.LogError($"GameNetworkManager: ✗ ALL {team} team spawn transforms are NULL!");
            Vector3 fallback = team == "Blue" ? new Vector3(8f, 0.71f, 0f) : new Vector3(-8f, 0.71f, 0f);
            Debug.LogError($"Using final emergency fallback: {fallback}");
            return fallback;
        }
        
        
        Vector3 spawnPos = spawnTransform.position;
        
        if (float.IsNaN(spawnPos.x) || float.IsNaN(spawnPos.y) || float.IsNaN(spawnPos.z))
        {
            Debug.LogError($"GameNetworkManager: Invalid spawn position detected: {spawnPos}");
            spawnPos = team == "Blue" ? new Vector3(8f, 0.71f, 0f) : new Vector3(-8f, 0.71f, 0f);
        }
        else
        {
            spawnPos.y = 0.71f; // Force hockey ground level
        }
        
        Debug.Log($"✓ SUCCESS: Using spawn transform '{spawnTransform.name}'");
        Debug.Log($"  Original position: {spawnTransform.position}");
        Debug.Log($"  Final spawn position: {spawnPos}");
        Debug.Log($"=============================================");
        
        return spawnPos;
    }

   
    private void ManuallySpawnPlayer(ulong clientId)
    {
        Debug.Log($"========== MANUAL SPAWN PLAYER {clientId} ==========");
        
     
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError($"GameNetworkManager: ManuallySpawnPlayer called on non-server! Client {clientId} will not be spawned.");
            return;
        }
        
        if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab == null)
        {
            Debug.LogError("GameNetworkManager: Cannot spawn player - PlayerPrefab is null");
            return;
        }
        

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
        {
            if (clientData.PlayerObject != null)
            {
                Debug.LogWarning($"GameNetworkManager: Client {clientId} already has a player object, skipping spawn");
                return;
            }
        }
        
        string team = GetTeamForClient(clientId);
        Debug.Log($"GameNetworkManager: Client {clientId} assigned to team: {team}");
        
        Vector3 spawnPos = GetSpawnPositionFromInspector(clientId, team);
        Debug.Log($"GameNetworkManager: Client {clientId} spawn position: {spawnPos}");
        

        Quaternion spawnRot;
        if (team == "Blue")
        {
            spawnRot = Quaternion.Euler(0, 90, 0); // Blue team faces right (towards Red goal)
        }
        else
        {
            spawnRot = Quaternion.Euler(0, -90, 0); // Red team faces left (towards Blue goal)
        }
        
        Debug.Log($"GameNetworkManager: Client {clientId} spawn rotation: {spawnRot.eulerAngles}");
        
        try
        {
            // Instantiate player object at EXACT position
            GameObject playerObject = Instantiate(NetworkManager.Singleton.NetworkConfig.PlayerPrefab, spawnPos, spawnRot);
            
            if (playerObject == null)
            {
                Debug.LogError($"GameNetworkManager: Failed to instantiate player object for client {clientId}");
                return;
            }
            
    
            playerObject.transform.position = spawnPos;
            playerObject.transform.rotation = spawnRot;
            
            Debug.Log($"GameNetworkManager: Instantiated player object at {playerObject.transform.position} with rotation {playerObject.transform.rotation.eulerAngles}");
            
            var networkObject = playerObject.GetComponent<Unity.Netcode.NetworkObject>();
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                Debug.Log($"GameNetworkManager: Spawned NetworkObject for client {clientId}");
                
                StartCoroutine(FinalizePlayerSpawn(playerObject, clientId, team, spawnPos, spawnRot));
            }
            else
            {
                Debug.LogError("GameNetworkManager: Player prefab does not have NetworkObject component!");
                Destroy(playerObject);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GameNetworkManager: Exception while spawning player for client {clientId}: {e.Message}");
        }
        
        Debug.Log($"================================================");
    }

    private IEnumerator FinalizePlayerSpawn(GameObject playerObject, ulong clientId, string team, Vector3 spawnPos, Quaternion spawnRot)
    {
        yield return null; // Wait one frame for network spawn to complete
        
        if (playerObject == null)
        {
            Debug.LogError($"GameNetworkManager: Player object destroyed before finalization for client {clientId}");
            yield break;
        }
        
        Debug.Log($"GameNetworkManager: Finalizing spawn for client {clientId}");
        
        for (int i = 0; i < 3; i++)
        {
            // Force position and rotation
            playerObject.transform.position = spawnPos;
            playerObject.transform.rotation = spawnRot;
            
            // Reset physics to exact position
            var rigidbody = playerObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.position = spawnPos;
                rigidbody.rotation = spawnRot;
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
            
            var pm = playerObject.GetComponent<PlayerMovement>(); // renamed from playerMovement to pm
            if (pm != null && pm.IsServer)
            {
                // Use reflection to access NetworkPosition since it might be private
                var networkPosField = typeof(PlayerMovement).GetField("NetworkPosition", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (networkPosField != null)
                {
                    var networkVar = networkPosField.GetValue(pm);
                    if (networkVar != null)
                    {
                        var valueProperty = networkVar.GetType().GetProperty("Value");
                        if (valueProperty != null && valueProperty.CanWrite)
                        {
                            valueProperty.SetValue(networkVar, spawnPos);
                            Debug.Log($"GameNetworkManager: Set NetworkPosition via reflection for client {clientId}");
                        }
                    }
                }
                
                // Also try direct method if available
                try
                {
                    var setNetworkPosMethod = typeof(PlayerMovement).GetMethod("SetNetworkPosition", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (setNetworkPosMethod != null)
                    {
                        setNetworkPosMethod.Invoke(pm, new object[] { spawnPos });
                        Debug.Log($"GameNetworkManager: Called SetNetworkPosition method for client {clientId}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"GameNetworkManager: Could not call SetNetworkPosition: {e.Message}");
                }
            }
            
            Debug.Log($"GameNetworkManager: Position enforcement attempt {i + 1}: {playerObject.transform.position}");
            
            // Wait between attempts
            if (i < 2) yield return new WaitForSeconds(0.2f);
        }
        
        // Set team on the PlayerMovement script using the enum
        var playerMovement = playerObject.GetComponent<PlayerMovement>();
        if (playerMovement != null && NetworkManager.Singleton.IsServer)
        {
            PlayerMovement.Team teamEnum = team == "Blue" ? PlayerMovement.Team.Blue : PlayerMovement.Team.Red;
            playerMovement.SetTeamServerRpc(teamEnum);
        }


        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"GameNetworkManager: Setting up camera for local client {clientId}");
            
            // Wait a bit more for position to stabilize
            yield return new WaitForSeconds(0.5f);
            
            var pmLocal = playerObject.GetComponent<PlayerMovement>();
            if (pmLocal != null)
            {
                pmLocal.EnsurePlayerCamera();
            }
        }
        
        var networkObject = playerObject.GetComponent<Unity.Netcode.NetworkObject>();
        Debug.Log($"GameNetworkManager: ✓ SUCCESSFULLY SPAWNED client {clientId} ({team} team)");
        Debug.Log($"  Final Position: {playerObject.transform.position}");
        Debug.Log($"  Final Rotation: {playerObject.transform.rotation.eulerAngles}");
        Debug.Log($"  Owner Client ID: {networkObject?.OwnerClientId}");
        Debug.Log($"  Network Object ID: {networkObject?.NetworkObjectId}");
    }

    private void ApplyTeamColorWithSync(GameObject playerObject, string team, ulong clientId)
    {
        Debug.Log($"GameNetworkManager: Applying team color for {team} team with FORCED client sync");
        
        // Apply colors immediately on server
        ApplyTeamColorDirectly(playerObject, team);
        
        var networkObject = playerObject.GetComponent<Unity.Netcode.NetworkObject>();
        if (networkObject != null && NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"GameNetworkManager: FORCING team color sync to ALL clients for NetworkObject {networkObject.NetworkObjectId}");
            
            ForceTeamColorClientRpc(networkObject.NetworkObjectId, team);
            
            // Multiple follow-up calls to ensure delivery
            StartCoroutine(ForceTeamColorSyncRepeated(networkObject.NetworkObjectId, team));
        }
    }

    private IEnumerator ForceTeamColorSyncRepeated(ulong networkObjectId, string team)
    {
        // Immediate second attempt
        yield return new WaitForSeconds(0.05f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        // Third attempt
        yield return new WaitForSeconds(0.1f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        // Fourth attempt
        yield return new WaitForSeconds(0.2f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        // Fifth attempt
        yield return new WaitForSeconds(0.5f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        // Final attempt
        yield return new WaitForSeconds(1f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        Debug.Log($"GameNetworkManager: Completed 6 team color sync attempts for {team} team");
    }

    [Unity.Netcode.ClientRpc]
    private void ForceTeamColorClientRpc(ulong networkObjectId, string team)
    {
        Debug.Log($"GameNetworkManager: [ClientRpc] RECEIVED team color command for NetworkObject {networkObjectId}, team: {team}");
        Debug.Log($"GameNetworkManager: [ClientRpc] Running on: IsServer={NetworkManager.Singleton.IsServer}, IsHost={NetworkManager.Singleton.IsHost}, IsClient={NetworkManager.Singleton.IsClient}");
        
        GameObject playerObject = FindPlayerObjectByNetworkId(networkObjectId);
        
        if (playerObject != null)
        {
            Debug.Log($"GameNetworkManager: [ClientRpc] Found player object {playerObject.name}, applying {team} color NOW");
            
            // Apply team color with maximum force
            bool success = ForceTeamColorOnClient(playerObject, team);
            
            if (success)
            {
                Debug.Log($"GameNetworkManager: [ClientRpc] ✓ SUCCESS - {team} color applied on client");
            }
            else
            {
                Debug.LogError($"GameNetworkManager: [ClientRpc] ✗ FAILED to apply {team} color on client");
                StartCoroutine(AggressiveClientColorRetry(networkObjectId, team));
            }
        }
        else
        {
            Debug.LogWarning($"GameNetworkManager: [ClientRpc] Player object not found for NetworkObject {networkObjectId}, starting search retry");
            StartCoroutine(AggressiveClientColorRetry(networkObjectId, team));
        }
    }

    private IEnumerator AggressiveClientColorRetry(ulong networkObjectId, string team)
    {
        int maxAttempts = 20;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            yield return new WaitForSeconds(0.1f);
            
            GameObject playerObject = FindPlayerObjectByNetworkId(networkObjectId);
            
            if (playerObject != null)
            {
                Debug.Log($"GameNetworkManager: [ClientRetry {attempt + 1}] Found player object, applying {team} color");
                
                bool success = ForceTeamColorOnClient(playerObject, team);
                
                if (success)
                {
                    Debug.Log($"GameNetworkManager: [ClientRetry {attempt + 1}] ✓ SUCCESS - {team} color applied after {attempt + 1} attempts");
                    yield break;
                }
            }
        }
        
        Debug.LogError($"GameNetworkManager: [ClientRetry] COMPLETE FAILURE - Could not apply {team} color after {maxAttempts} attempts");
    }

    private bool ForceTeamColorOnClient(GameObject playerObject, string team)
    {
        if (playerObject == null) return false;
        
        Debug.Log($"GameNetworkManager: CLIENT FORCE applying {team} team color to {playerObject.name}");
        
        // EXTREME colors for visibility
        Color teamColor;
        if (team == "Blue")
        {
            teamColor = new Color(0f, 0.5f, 1f, 1f); // Bright cyan-blue
        }
        else // Red team
        {
            teamColor = new Color(1f, 0.2f, 0.2f, 1f); // Bright red
        }
        
        Debug.Log($"GameNetworkManager: CLIENT using EXTREME color {teamColor} for {team} team");
        
        // Get ALL possible renderers
        var allRenderers = new List<Renderer>();
        allRenderers.AddRange(playerObject.GetComponentsInChildren<Renderer>(true));
        allRenderers.AddRange(playerObject.GetComponents<Renderer>());
        
        // Also check parent and siblings if any
        if (playerObject.transform.parent != null)
        {
            allRenderers.AddRange(playerObject.transform.parent.GetComponentsInChildren<Renderer>(true));
        }
        
        int coloredCount = 0;
        
        foreach (var renderer in allRenderers)
        {
            if (renderer == null) continue;
            
            try
            {
                // EXTREME approach: Replace ALL materials
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        // Create new material with team color
                        Material newMat = new Material(materials[i]);
                        newMat.color = teamColor;
                        
                        // Apply to ALL possible color properties
                        string[] colorProps = { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_Albedo", "_DiffuseColor", "_EmissionColor" };
                        foreach (string prop in colorProps)
                        {
                            if (newMat.HasProperty(prop))
                            {
                                newMat.SetColor(prop, teamColor);
                            }
                        }
                        
                        materials[i] = newMat;
                    }
                }
                
                // Force assign new materials
                renderer.materials = materials;
                renderer.material = materials[0]; // Force refresh
                
                // Force renderer refresh
                renderer.enabled = false;
                renderer.enabled = true;
                
                coloredCount++;
                
                Debug.Log($"GameNetworkManager: CLIENT ✓ Colored renderer {renderer.name} with {team} color");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameNetworkManager: CLIENT error coloring renderer {renderer.name}: {e.Message}");
            }
        }
        
        bool success = coloredCount > 0;
        Debug.Log($"GameNetworkManager: CLIENT team color result: {coloredCount} renderers colored, success: {success}");
        
        return success;
    }

    private string GetAuthIdForClient(ulong clientId)
    {
        if (clientAuthIds.ContainsKey(clientId))
        {
            return clientAuthIds[clientId];
        }
        
        // For local client, get directly from auth service
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            try
            {
                return Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            }
            catch (System.Exception e)
            {
                // Ja rodas kļūda, iegūstot lokālo autentifikācijas ID, izvada brīdinājumu
                Debug.LogWarning($"GameNetworkManager: Error getting local auth ID: {e.Message}");
            }
        }
        
        return "";
    }

    private void ValidateSpawnPoints()
    {
        Debug.Log("========== VALIDATING SPAWN POINTS ==========");
        
        bool hasErrors = false;
        
        // Check Red team spawns
        if (redTeamSpawns == null || redTeamSpawns.Length == 0)
        {
            Debug.LogError("GameNetworkManager: RED TEAM SPAWNS not assigned in inspector!");
            hasErrors = true;
        }
        else
        {
            Debug.Log($"Red team spawns: {redTeamSpawns.Length} assigned");
            for (int i = 0; i < redTeamSpawns.Length; i++)
            {
                if (redTeamSpawns[i] == null)
                {
                    Debug.LogError($"Red team spawn {i} is NULL!");
                    hasErrors = true;
                }
            }
        }
        
        // Check Blue team spawns
        if (blueTeamSpawns == null || blueTeamSpawns.Length == 0)
        {
            Debug.LogError("GameNetworkManager: BLUE TEAM SPAWNS not assigned in inspector!");
            hasErrors = true;
        }
        else
        {
            Debug.Log($"Blue team spawns: {blueTeamSpawns.Length} assigned");
            for (int i = 0; i < blueTeamSpawns.Length; i++)
            {
                if (blueTeamSpawns[i] == null)
                {
                    Debug.LogError($"Blue team spawn {i} is NULL!");
                    hasErrors = true;
                }
            }
        }
        
        if (hasErrors)
        {
            Debug.LogError("GameNetworkManager: SPAWN POINT VALIDATION FAILED!");
            Debug.LogError("Please assign all spawn point transforms in the inspector!");
        }
        else
        {
            Debug.Log("GameNetworkManager: ✓ All spawn points validated successfully");
        }
        
        Debug.Log("===============================================");
    }

    private IEnumerator LoadSceneThenStartHost(string sceneName)
    {
        isLoadingScene = true;
        Debug.Log($"GameNetworkManager: Loading scene {sceneName} before starting host...");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);

        // Wait for scene to load
        float timeout = 20f;
        float elapsed = 0f;
        while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != sceneName && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != sceneName)
        {
            Debug.LogError($"GameNetworkManager: Failed to load scene '{sceneName}' in time.");
            isLoadingScene = false;
            yield break;
        }
        
        Debug.Log($"GameNetworkManager: Scene '{sceneName}' loaded. Now starting host...");
        StartCoroutine(StartHostAndLoadScene(sceneName));
    }

    // NEW: Get next sequential spawn index for a team
    private int GetNextSpawnIndexForTeam(string team, int maxSpawns)
    {
        // Initialize counter if not exists
        if (!teamSpawnCounters.ContainsKey(team))
        {
            teamSpawnCounters[team] = 0;
        }
        
        // Get current counter value
        int spawnIndex = teamSpawnCounters[team];
        
        // Increment counter for next player (wrap around if needed)
        teamSpawnCounters[team] = (teamSpawnCounters[team] + 1) % maxSpawns;
        
        Debug.Log($"GameNetworkManager: {team} team spawn counter: {spawnIndex} (next will be {teamSpawnCounters[team]})");
        
        return spawnIndex;
    }

    // NEW: Reset spawn counters when starting a new game
    private void ResetSpawnCounters()
    {
        teamSpawnCounters["Red"] = 0;
        teamSpawnCounters["Blue"] = 0;
        Debug.Log("GameNetworkManager: Reset spawn counters for new game");
    }

    // ENHANCED: Connection approval with better auth ID storage and validation
    private void ConnectionApprovalCheck(Unity.Netcode.NetworkManager.ConnectionApprovalRequest request, Unity.Netcode.NetworkManager.ConnectionApprovalResponse response)
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        Debug.Log($"========== CONNECTION APPROVAL CHECK ==========");
        Debug.Log($"Client ID: {request.ClientNetworkId}");
        Debug.Log($"Current Scene: {currentScene}");
        Debug.Log($"Is Server: {NetworkManager.Singleton.IsServer}");
        Debug.Log($"Is Host: {NetworkManager.Singleton.IsHost}");
        Debug.Log($"PlayerPrefab assigned: {NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null}");
        
        // CRITICAL: Store auth ID from connection data for team assignment
        if (request.Payload != null && request.Payload.Length > 0)
        {
            try
            {
                string authId = System.Text.Encoding.UTF8.GetString(request.Payload);
                if (!string.IsNullOrEmpty(authId))
                {
                    clientAuthIds[request.ClientNetworkId] = authId;
                    Debug.Log($"GameNetworkManager: ✓ Stored auth ID '{authId}' for client {request.ClientNetworkId}");
                    
                    // ADDED: Immediately validate stored team data for this auth ID
                    string teamData = PlayerPrefs.GetString("AllPlayerTeams", "");
                    if (!string.IsNullOrEmpty(teamData))
                    {
                        Debug.Log($"GameNetworkManager: Validating team data for auth ID '{authId}'");
                        var entries = teamData.Split('|');
                        bool foundTeamAssignment = false;
                        foreach (var entry in entries)
                        {
                            var parts = entry.Split(':');
                            if (parts.Length >= 3 && parts[2] == authId)
                            {
                                foundTeamAssignment = true;
                                Debug.Log($"GameNetworkManager: ✓ Found team assignment for {authId}: {parts[1]}");
                                break;
                            }
                        }
                        if (!foundTeamAssignment)
                        {
                            Debug.LogWarning($"GameNetworkManager: ⚠ No team assignment found for auth ID '{authId}' in stored data");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"GameNetworkManager: ⚠ No stored team data found for validation");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GameNetworkManager: Error reading auth ID from connection data: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"GameNetworkManager: ⚠ No connection data payload for client {request.ClientNetworkId} - team assignment will use fallback");
        }
        
        // Block player spawning in MainMenu
        if (currentScene == "MainMenu")
        {
            Debug.LogWarning($"GameNetworkManager: ✗ BLOCKING player spawn in MainMenu for client {request.ClientNetworkId}");
            response.Approved = true;
            response.CreatePlayerObject = false; // No spawning in MainMenu
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
            return;
        }

        if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab == null && prefabToAssign != null)
        {
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = prefabToAssign;
            Debug.Log($"GameNetworkManager: ✓ Set PlayerPrefab for spawning: {prefabToAssign.name}");
        }
        
        if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab == null)
        {
            Debug.LogError("GameNetworkManager: ✗ NO PLAYERPREFAB available for spawning!");
            response.Approved = false;
            return;
        }

        Debug.Log($"GameNetworkManager: ✓ APPROVING connection for client {request.ClientNetworkId}");
        Debug.Log($"GameNetworkManager: ✓ MANUAL SPAWNING will handle all positioning and team assignment");
        
        response.Approved = true;
        response.CreatePlayerObject = false; // CRITICAL: No automatic spawning
        response.Position = Vector3.zero; // Ignored since CreatePlayerObject = false
        response.Rotation = Quaternion.identity; // Ignored since CreatePlayerObject = false
        
        Debug.Log($"GameNetworkManager: ✓ CONNECTION APPROVED - manual spawn will handle client {request.ClientNetworkId}");
        Debug.Log($"===============================================");
    }

    private GameObject FindPlayerObjectByNetworkId(ulong networkObjectId)
    {
        // Method 1: SpawnedObjects (fastest)
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject))
        {
            if (networkObject != null && networkObject.gameObject != null)
            {
                return networkObject.gameObject;
            }
        }
        
        // Method 2: Search all NetworkObjects in scene
        var allNetworkObjects = FindObjectsByType<Unity.Netcode.NetworkObject>(FindObjectsSortMode.None);
        foreach (var obj in allNetworkObjects)
        {
            if (obj != null && obj.NetworkObjectId == networkObjectId)
            {
                return obj.gameObject;
            }
        }
        
        // Method 3: Search connected clients
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            if (kvp.Value.PlayerObject != null && kvp.Value.PlayerObject.NetworkObjectId == networkObjectId)
            {
                return kvp.Value.PlayerObject.gameObject;
            }
        }
        
        return null;
    }

    private bool ApplyTeamColorDirectly(GameObject playerObject, string team)
    {
        if (playerObject == null)
        {
            Debug.LogError("GameNetworkManager: Cannot apply team color - player object is null");
            return false;
        }
        
        Debug.Log($"GameNetworkManager: Applying {team} team color to {playerObject.name}");
        
        // FIXED: Use much more vibrant and distinct colors
        Color teamColor;
        if (team == "Blue")
        {
            teamColor = new Color(0f, 0.3f, 1f, 1f); // Bright blue
        }
        else // Red team
        {
            teamColor = new Color(1f, 0f, 0f, 1f); // Bright red
        }
        
        Debug.Log($"GameNetworkManager: Using color {teamColor} for {team} team");
        
        // Get ALL renderers (including inactive and nested)
        var renderers = playerObject.GetComponentsInChildren<Renderer>(true);
        int coloredRenderers = 0;
        int totalRenderers = renderers.Length;
        
        Debug.Log($"GameNetworkManager: Found {totalRenderers} renderers on {playerObject.name}");
        
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            
            try
            {
                // FIXED: Create new material instances for each renderer
                Material[] originalMaterials = renderer.materials;
                Material[] newMaterials = new Material[originalMaterials.Length];
                bool rendererModified = false;
                
                for (int i = 0; i < originalMaterials.Length; i++)
                {
                    Material originalMat = originalMaterials[i];
                    if (originalMat == null) continue;
                    
                    // Create completely new material instance
                    Material newMat = new Material(originalMat);
                    
                    // Apply team color to multiple properties
                    newMat.color = teamColor;
                    
                    if (newMat.HasProperty("_Color"))
                        newMat.SetColor("_Color", teamColor);
                    if (newMat.HasProperty("_BaseColor"))
                        newMat.SetColor("_BaseColor", teamColor);
                    if (newMat.HasProperty("_MainColor"))
                        newMat.SetColor("_MainColor", teamColor);
                    if (newMat.HasProperty("_TintColor"))
                        newMat.SetColor("_TintColor", teamColor);
                    
                    newMaterials[i] = newMat;
                    rendererModified = true;
                }
                
                if (rendererModified)
                {
                    renderer.materials = newMaterials;
                    coloredRenderers++;
                    
                    // Force immediate refresh
                    renderer.enabled = false;
                    renderer.enabled = true;
                    
                    Debug.Log($"GameNetworkManager: ✓ Applied {team} color to renderer {renderer.name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameNetworkManager: Error applying color to renderer {renderer.name}: {e.Message}");
            }
        }
        
        bool success = coloredRenderers > 0;
        Debug.Log($"GameNetworkManager: Team color application - {coloredRenderers}/{totalRenderers} renderers colored. Success: {success}");
        
        return success;
    }
}