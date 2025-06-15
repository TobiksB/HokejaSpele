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

// Pārvalda visu daudzspēlētāju lobija funkcionalitāti hokeja spēlē. Nodrošina spēlētāju pievienošanos, komandu sadalīšanu, gatavības statusa pārvaldību
// un tīkla savienojuma izveidi, izmantojot Unity Relay un Lobby servisus.
public class LobbyManager : MonoBehaviour
{
    // Statiskā instance savienojuma pārvaldībai, kas nodrošina vieglāku piekļuvi no citām klasēm
    public static LobbyManager Instance { get; private set; }
    private Lobby currentLobby; // Pašreizējā lobija dati
    private Dictionary<string, string> playerTeams = new Dictionary<string, string>(); // Saglabā spēlētāju komandu piederību
    private Dictionary<string, string> playerNames = new Dictionary<string, string>(); // Saglabā spēlētāju vārdus
    private Dictionary<string, bool> playerReadyStates = new Dictionary<string, bool>(); // Saglabā spēlētāju gatavības statusu
    private float heartbeatTimer; // Taimeris lobija "sirdspukstu" sūtīšanai, lai tas neaizvertos
    private float lobbyPollTimer; // Taimeris lobija atjaunināšanas pieprasījumiem
    private const float LOBBY_POLL_INTERVAL = 5.0f; // Lobija atjaunināšanas intervāls sekundēs
    private float lobbyPollBackoff = 0f; // Lauks atpakaļspiediena apstrādei, lai samazinātu API pieprasījumu daudzumu
    private const float LOBBY_POLL_BACKOFF_ON_429 = 10.0f; // Gaidīšanas laiks pēc ātruma ierobežojuma (429 kļūdas)
    private GameMode selectedGameMode; // Izvēlētais spēles režīms
    private List<string> chatMessages = new List<string>(); // Tērzēšanas ziņu vēsture
    private const int MAX_CHAT_MESSAGES = 50; // Maksimālais tērzēšanas ziņu skaits
    private float lastLobbyUpdateTime = 0f; // Pēdējais laiks, kad tika atjaunināts lobijs
    private const float MIN_UPDATE_INTERVAL = 1.0f; // Minimālais intervāls starp lobija atjauninājumiem
    private NetworkManager networkManager; // Atsauce uz tīkla pārvaldnieku
    private string relayJoinCode; // Unity Relay pievienošanās kods

    // Karogs, lai novērstu vairākus releju izveidojumus vienlaikus
    private bool relayCreationInProgress = false;
    private bool hostRelayCreated = false; // Vai resursdators ir izveidojis releju

    // Vienkārša NetworkManager pārvaldība ar prefabiem
    [Header("Tīkla konfigurācija")]
    [SerializeField] private GameObject networkManagerPrefab; // Pievieno savu NetworkManager prefabu šeit

    /// Inicializē un iestata LobbyManager. Pārbauda, vai jau eksistē dublēta instance un iznīcina to.
    /// Veic sākotnējo NetworkManager iestatīšanu.
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("LobbyManager inicializēts veiksmīgi.");

            // Iestatīt noklusējuma spēles režīmu uz 2v2, lai nodrošinātu, ka CanStartMatch darbojas pareizi
            selectedGameMode = GameMode.Mode2v2;

            //  Vienkārša NetworkManager iestatīšana
            EnsureNetworkManagerExists();
        }
        else
        {
            Debug.LogWarning("Atrasts dublēts LobbyManager. Iznīcinām šo instanci.");
            Destroy(gameObject);
        }
    }

    /// Nodrošina NetworkManager komponenta esamību un pareizu konfigurāciju.
    /// Meklē esošu NetworkManager vai izveido jaunu no prefaba.
    /// Noņem dublētus NetworkPrefabs un pārbauda NetworkObject komponentes, kas varētu izraisīt kļūdas.
    private void EnsureNetworkManagerExists()
    {
        // Izmantot FindObjectsByType nevis novecojušo FindObjectsOfType
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

     
        if (networkManagerPrefab != null)
        {
            Debug.Log("LobbyManager: Creating NetworkManager from prefab...");
            var networkManagerGO = Instantiate(networkManagerPrefab);
            networkManagerGO.name = "NetworkManager (From Prefab)";
            DontDestroyOnLoad(networkManagerGO);


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


            var netObj = networkManagerGO.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                Debug.LogWarning("NetworkManager prefabam ir NetworkObject komponente! Tas nav atļauts. Noņemu to.");
                DestroyImmediate(netObj);
            }

            networkManager = networkManagerGO.GetComponent<NetworkManager>();
            if (networkManager != null)
            {
             
                RemoveInvalidNetworkPrefabs(networkManager);

                //  Pārliecināties, ka PlayerPrefab ir piešķirts NetworkManager.NetworkConfig
                if (networkManager.NetworkConfig != null && networkManager.NetworkConfig.PlayerPrefab == null)
                {
                    // Mēģināt piešķirt Player prefabu no Resources vai inspektora
                    GameObject playerPrefab = null;
                    // Mēģināt inspektora atsauci no GameNetworkManager, ja pieejams
                    var gnm = FindFirstObjectByType<GameNetworkManager>();
                    if (gnm != null)
                    {
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
                    // Mēģināt Resources
                    if (playerPrefab == null)
                    {
                        playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
                        if (playerPrefab != null)
                        {
                            Debug.Log("[LobbyManager] Piešķirts PlayerPrefab no Resources/Prefabs/Player.");
                        }
                    }
                   
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
                        Debug.LogWarning("[LobbyManager] Nevarēja atrast PlayerPrefab, ko piešķirt NetworkManager. Lūdzu, piešķiriet to inspektorā vai Resources.");
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
            Debug.LogError("LobbyManager: Nav piešķirts NetworkManager prefabs! Lūdzu, piešķiriet NetworkManager prefabu inspektorā.");
            
            // Rezerves variants: izveidot pamata NetworkManager
            CreateBasicNetworkManager();
        }
    }

    
    /// Noņem NetworkManager no NetworkPrefabs saraksta, lai novērstu kļūdas, jo NetworkManager
    /// nevar būt NetworkPrefab.
    
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

    
    /// Izveido vienkāršu NetworkManager gadījumā, ja prefabs nav pieejams.
    /// Konfigurē pamata komponentes un iestatījumus, kas nepieciešami NetworkManager darbībai.
    /// Šis ir rezerves variants, ja prefabs nav piešķirts inspektorā.
    
    private void CreateBasicNetworkManager()
    {
        Debug.Log("LobbyManager: Veidojam pamata NetworkManager kā rezerves variantu...");
        var networkManagerGO = new GameObject("NetworkManager (Fallback)");
        networkManager = networkManagerGO.AddComponent<NetworkManager>();
        networkManager.NetworkConfig = new Unity.Netcode.NetworkConfig();
        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.NetworkConfig.ConnectionApproval = false;
        networkManager.NetworkConfig.Prefabs = new Unity.Netcode.NetworkPrefabs();

        var transport = networkManagerGO.AddComponent<UnityTransport>();
        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManagerGO.AddComponent<GameNetworkManager>();
        networkManagerGO.AddComponent<HockeyGame.Network.RelayManager>();
        DontDestroyOnLoad(networkManagerGO);
        Debug.Log("LobbyManager: Basic NetworkManager created with proper configuration");
    }

    
    /// Galvenā atjaunināšanas metode, kas regulāri notiek katru kadru.
    /// Pārvalda lobija "sirdspukstus" un atjauninājumus aptaujāšanu.
    /// Aptur lobija operācijas, ja nav MainMenu scenā.
    
    private void Update()
    {
        // KRITISKI: Apstrādāt lobija operācijas tikai MainMenu scenā
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            // Apturēt visas lobija operācijas, kad nav MainMenu
            return;
        }
        
        HandleLobbyHeartbeat();
        PollLobbyForUpdates();
    }

    
    /// Nodrošina regulārus "sirdspukstus" Unity Lobby servisam.
    /// Lobijs automātiski aizveras, ja nesaņem regulārus heartbeat signālus no resursdatora.
    /// Šī metode nodrošina, ka lobijs paliek aktīvs.
    
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

    // Lauki klienta releja savienojuma pārvaldībai
    private float clientRelayWaitTime = 0f;
    private const float CLIENT_RELAY_TIMEOUT = 10f; // Maksimālais gaidīšanas laiks klientam

    // Karogs, kas novērš vairākkārtēju scēnas sākšanu
    private bool clientStartedGame = false;

    
    /// Palīgmetode, kas ļauj izpildīt Lobby API izsaukumus ar eksponenciālo atpakaļspiedienu.
    /// Ja saņem 429 kļūdu (Too Many Requests), palielina gaidīšanas laiku un mēģina vēlreiz.
    

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

    
    /// Regulāri pārbauda lobija stāvokli, lai iegūtu jaunāko informāciju.
    /// Atjaunina lokālos spēlētāju datus, komandas, gatavības statusu.
    /// Arī pārvalda klienta releja pievienošanās loģiku un spēles sākšanu.
    
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

                    // --- Klienta releju pievienošanās loģika ---
                    //  Sākt klientu tikai, ja RelayCode ir norādīts UN GameStarted ir "true" ---
                    bool gameStarted = currentLobby.Data != null && currentLobby.Data.ContainsKey("GameStarted") && currentLobby.Data["GameStarted"].Value == "true";
                    if (!IsLobbyHost() && currentLobby.Data != null && currentLobby.Data.ContainsKey("RelayCode"))
                    {
                        string relayCode = currentLobby.Data["RelayCode"].Value;
                        Debug.Log($"[LobbyManager] KLIENTS: Mans SpēlētājaId: {AuthenticationService.Instance.PlayerId}");
                        Debug.Log($"[LobbyManager] KLIENTS: Releja kods saņemts: '{relayCode}'");

                        // LABOTS: Pārbaudīt, vai releja kods ir mainījies, un atiestatīt klienta stāvokli, ja tā
                        if (!string.IsNullOrEmpty(relayCode) && relayCode != relayJoinCode)
                        {
                            Debug.LogWarning($"[LobbyManager] KLIENTS: Releja kods mainījies no '{relayJoinCode}' uz '{relayCode}' - atiestatām klienta releja stāvokli");
                            relayConfiguredForClient = false;
                            relayJoinCode = relayCode; // Atjaunot saglabāto releja kodu
                        }

                        if (!string.IsNullOrEmpty(relayCode) && !relayConfiguredForClient)
                        {
                            try
                            {
                                Debug.Log($"[LobbyManager] KLIENTS: Pievienojamies relejam ar kodu: {relayCode}");
                                await JoinRelay(relayCode);
                                relayConfiguredForClient = true;
                                Debug.Log("[LobbyManager] KLIENTS: Relejs veiksmīgi konfigurēts");
                                
                                var transport = NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                                if (transport != null)
                                {
                                    bool isTransportReady = transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport;
                                    if (isTransportReady)
                                    {
                                        var connData = transport.ConnectionData;
                                        if (string.IsNullOrEmpty(connData.Address) || connData.Port <= 0)
                                        {
                                            Debug.LogWarning("[LobbyManager] KLIENTS: Transports rāda nederīgus savienojuma datus, bet releja protokols ir iestatīts");
                                        }
                                    }
                                }
                                
                                if (relayConfiguredForClient)
                                {
                                    Debug.Log("[LobbyManager] KLIENTS: Releja konfigurācija pabeigta, gatavs spēles sākšanai");
                                    OnClientReadyForGame?.Invoke();
                                }
                                else
                                {
                                    Debug.LogWarning("[LobbyManager] KLIENTS: Releja pievienošanās pabeigta, bet konfigurācijas pārbaude neizdevās");
                                }
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError($"[LobbyManager] KLIENTS: Neizdevās pievienoties relejam: {e.Message}");
                                relayConfiguredForClient = false;
                            }
                        }

                        // --- Sākt klienta spēli tikai vienreiz, pēc releja konfigurācijas, kad GameStarted ir true un MainMenu ekrānā ---
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
                                    Debug.Log("[LobbyManager] KLIENTS: Sākam spēli pēc releja pievienošanās un GameStarted karoga (relayReady OK)");
                                    GameNetworkManager.Instance.StartGame("GameScene2v2"); // vai izmantot pareizo scēnu
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[LobbyManager] KLIENTS: Relejs vēl nav pilnībā konfigurēts transportā, gaidām pirms klienta sākšanas.");
                                // Atiestatīt karodziņu, ja transports nav gatavs
                                relayConfiguredForClient = false;
                            }
                        }
                        // --- PIESPIEDU SĀKŠANA atkāpšanās scenārijs: ja relejs ir konfigurēts un spēle sākta, bet clientStartedGame joprojām ir false, piespiest to ---
                        else if (relayConfiguredForClient && gameStarted && !clientStartedGame)
                        {
                            var transport = NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                            if (transport != null && transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
                            {
                                Debug.LogWarning("[LobbyManager] KLIENTS: Atkāpšanās - Piespiedu StartGame, jo relejs ir konfigurēts un GameStarted ir true.");
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
                            Debug.LogWarning("[LobbyManager] KLIENTS: Beidzies taimauts gaidot releja koda atslēgu lobija datos.");
                        }
                    }
                    else
                    {
                        // Resursdatora ceļš
                        if (currentLobby.Data != null && currentLobby.Data.ContainsKey("RelayCode"))
                        {
                            string relayCode = currentLobby.Data["RelayCode"].Value;
                            Debug.Log($"[LobbyManager] RESURSDATORS: Pašreizējais releja kods lobijā: '{relayCode}'");
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
                    Debug.LogWarning("[LobbyManager] PollLobbyForUpdates sasniedza ātruma ierobežojumu, atpakaļspiešana 30s");
                    lobbyPollBackoff = 30f; // PALIELINĀTS: Ilgāka atpakaļspiešana ātruma ierobežojuma gadījumā
                }
                else
                {
                    Debug.LogError($"[LobbyManager] PollLobbyForUpdates kļūda: {ex.Message}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyManager] PollLobbyForUpdates negaidīta kļūda: {ex.Message}");
            }
        }
    }

    
    /// Metode, kas izveido jaunu lobiju un atgriež pievienošanās kodu
    

    public async Task<string> CreateLobby(int maxPlayers)
    {
        try
        {
            Debug.Log($"Izveidojam 2v2 lobiju {maxPlayers} spēlētājiem...");

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await InitializeUnityServices();
            }

            //  Izveidot LobbyChatManager, ja tas neeksistē
            if (HockeyGame.UI.LobbyChatManager.Instance == null)
            {
                var chatManagerGO = new GameObject("LobbyChatManager");
                chatManagerGO.AddComponent<HockeyGame.UI.LobbyChatManager>();
                Debug.Log("LobbyManager: Izveidots LobbyChatManager gadījums");
            }

            //  Inicializēt lokālos spēlētāja datus NEKAVĒJOTIES un PAREIZI
            string playerId = AuthenticationService.Instance.PlayerId;
            string playerName = SettingsManager.Instance != null ? 
                SettingsManager.Instance.PlayerName : 
                $"Player{UnityEngine.Random.Range(1000, 9999)}";
            
            //   inicializācija - nedzēst esošos datus, ja tie eksistē
            if (playerNames == null) playerNames = new Dictionary<string, string>();
            if (playerTeams == null) playerTeams = new Dictionary<string, string>();
            if (playerReadyStates == null) playerReadyStates = new Dictionary<string, bool>();
            
            //  Iestatīt spēlētāja datus NEKAVĒJOTIES
            playerNames[playerId] = playerName;
            playerTeams[playerId] = "Red";
            playerReadyStates[playerId] = false;
            
            Debug.Log($"TŪLĪTĒJA INICIALIZĀCIJA: Spēlētāja dati iestatīti - ID: {playerId}, Vārds: {playerName}, Komanda: Red, Gatavs: false");
            Debug.Log($"TŪLĪTĒJA INICIALIZĀCIJA: Vārdnīcu skaits - Vārdi: {playerNames.Count}, Komandas: {playerTeams.Count}, Gatavība: {playerReadyStates.Count}");

            // Piespiest tūlītēju UI atjaunināšanu uzreiz pēc datu iestatīšanas
            Debug.Log($"TŪLĪTĒJS UI ATJAUNINĀJUMS: Piespiedu UI atjaunināšana ar {playerNames.Count} spēlētājiem");
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
                    // Tērzēšanas sloti tiks izveidoti tikai tad, kad tie būs nepieciešami
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync($"Hockey 2v2 Game {System.DateTime.Now.Ticks}", maxPlayers, options);

            Debug.Log($"✓ 2v2 Lobijs veiksmīgi izveidots");
            Debug.Log($"  Lobija kods: {currentLobby.LobbyCode}");
            
            // NESĀKT RESURSDATORU TŪLĪT - tikai konfigurēt releja transportu
            // Resursdators sāksies, kad spēle faktiski sāksies caur StartMatch()
            Debug.Log("LobbyManager: Lobijs izveidots, relejs konfigurēts. Resursdators sāksies, kad spēle sāksies.");
            
            //Gala validācija un UI atjaunošana
            Debug.Log($"GALA VALIDĀCIJA: Vārdnīcas - {playerNames.Count}, Komandas: {playerTeams.Count}, Gatavība: {playerReadyStates.Count}");
            UpdatePlayerListUI();
            
            //Iestatīt lobija kodu UI
            if (HockeyGame.UI.LobbyPanelManager.Instance != null)
            {
                HockeyGame.UI.LobbyPanelManager.Instance.SetLobbyCode(currentLobby.LobbyCode);
                Debug.Log($"Iestatīts lobija kods UI: {currentLobby.LobbyCode}");
            }
            else
            {
                Debug.LogError("LobbyPanelManager.Instance ir null - nevar iestatīt lobija kodu UI!");
            }
            
            return currentLobby.LobbyCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Neizdevās izveidot 2v2 lobiju: {e}");
            return null;
        }
    }

    
    /// Pārbauda, vai Unity Relay ir pareizi konfigurēts.
    /// Veic transporta konfigurācijas validāciju, lai pārliecinātos par savienojuma iestatījumiem.
    
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

    
    /// Atgriež pašreizējo releja pievienošanās kodu.
    
    public string GetCurrentRelayJoinCode()
    {
        return relayJoinCode;
    }

    
    /// Izveido Unity Relay servera piešķīrumu, lai hostētu tīkla spēli.
    /// To drīkst izsaukt tikai lobija resursdators (host).
    /// Nodrošina, ka vienlaikus nevar notikt vairākas releja izveides.
    /// Konfigurē Unity Transport ar iegūtajiem releja datiem.
    
    public async Task<string> CreateRelay()
    {
        if (!IsLobbyHost())
        {
            Debug.LogWarning("[LobbyManager] Tikai resursdatoram vajadzētu izveidot releju piešķirumus!");
            return null;
        }

        //  Novērst vairākas releju izveidošanas
        if (relayCreationInProgress)
        {
            // Ja kāds cits process jau izveido releju, pagaidām līdz tas pabeidz darbu
            Debug.LogWarning("[LobbyManager] Releja izveide jau notiek, gaidiet...");
            
            // Gaidīt, līdz pašreizējā izveide ir pabeigta
            float timeout = 30f;
            float elapsed = 0f;
            while (relayCreationInProgress && elapsed < timeout)
            {
                await Task.Delay(100);
                elapsed += 0.1f;
            }
            
            if (!string.IsNullOrEmpty(relayJoinCode))
            {
                Debug.Log($"[LobbyManager] Izmantojam releja kodu no vienlaicīgas izveidošanas: {relayJoinCode}");
                return relayJoinCode;
            }
        }

        //  Ja relejs jau izveidots, atgriezt esošo kodu
        if (hostRelayCreated && !string.IsNullOrEmpty(relayJoinCode))
        {
            Debug.Log($"[LobbyManager] Relejs jau izveidots, atgriežam esošo kodu: {relayJoinCode}");
            return relayJoinCode;
        }

        relayCreationInProgress = true;
        
        try
        {
            Debug.Log("LobbyManager: Uzsākam releja izveidošanas procesu...");
            
            // Pārliecināties, ka servisi ir inicializēti
            if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
            {
                Debug.Log("Inicializējam Unity servisu...");
                await UnityServices.InitializeAsync();
                await Task.Delay(1000);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Ielogojos anonīmi...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await Task.Delay(500);
            }

            // SOLIS 1: Izveidot releja piešķiršanu
            Debug.Log("Veidojam releja piešķīrumu 4 spēlētājiem (3 savienojumi)...");
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            
            if (allocation == null)
            {
                throw new System.Exception("Releja piešķīruma vērtība ir null");
            }
            
            // SOLIS 2: Iegūt pievienošanās kodu
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            if (string.IsNullOrEmpty(joinCode))
            {
                throw new System.Exception("Releja pievienošanās kods ir tukšs vai nav norādīts");
            }

            Debug.Log($"✓ Releja piešķīrums izveidots veiksmīgi");
            Debug.Log($"  Pievienošanās kods: {joinCode}");
            Debug.Log($"  Serveris: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
            Debug.Log($"  Piešķīruma ID: {allocation.AllocationId}");

            // SOLIS 3: Konfigurēt transportu
            var networkManager = FindOrCreateNetworkManager();
            var transport = networkManager?.GetComponent<UnityTransport>();
            
            if (transport == null)
            {
                Debug.LogError("UnityTransport nav atrasts - veidojam jaunu...");
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
                networkManager.NetworkConfig.NetworkTransport = transport;
            }

            // Ensure NetworkManager is stopped before configuring relay
            if (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
            {
                Debug.LogWarning("NetworkManager darbojas - izslēdzam pirms releja konfigurēšanas");
                networkManager.Shutdown();

                // Gaidīt pilnīgu izslēgšanos
                int attempts = 0;
                while ((networkManager.IsHost || networkManager.IsServer || networkManager.IsClient) && attempts < 50)
                {
                    await Task.Delay(100);
                    attempts++;
                }
            }

            // Konfigurēt transportu resursdatoram
            Debug.Log("Konfigurējam transportu releja resursdatoram...");
            
            // Validēt piešķīruma datus
            if (string.IsNullOrEmpty(allocation.RelayServer.IpV4) || allocation.RelayServer.Port <= 0)
            {
                throw new System.Exception($"Nederīgi releja servera dati: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
            }
            
            if (allocation.AllocationIdBytes == null || allocation.Key == null || allocation.ConnectionData == null)
            {
                throw new System.Exception("Releja piešķīruma dati ir nepilnīgi");
            }

            // Configure relay data
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            // SOLIS 4: Apiešanas risinājums Unity Transport adreses kļūdai
            await Task.Delay(500);
            
            var connectionData = transport.ConnectionData; // iegustam transporta savienojuma datus
            Debug.Log($"Transporta konfigurācijas rezultāts:");
            Debug.Log($"  Adrese: {connectionData.Address}");
            Debug.Log($"  Ports: {connectionData.Port}");
            Debug.Log($"  Protokols: {transport.Protocol}");

            bool isRelayConfigured = false; // pienemam ka relay nav konfigurets
            
            if (transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
            // Ja tiek izmantots Relay protokols, 
            // konfigurācija tiek uzskatīta par derīgu.
            {
                isRelayConfigured = true;
                Debug.Log("✓ Releja konfigurācija VALIDĒTA - izmanto releja protokolu (Unity Transport adreses kļūdas apiešanas risinājums)");
            }
            else if (connectionData.Address == allocation.RelayServer.IpV4 && connectionData.Port == allocation.RelayServer.Port)
            //Ja adrese un ports precīzi sakrīt ar piešķīruma (allocation) servera datiem, arī tiek uzskatīts, ka viss ir pareizi.
            {
                isRelayConfigured = true;
                Debug.Log("✓ Releja konfigurācija VALIDĒTA - precīza adreses atbilstība");
            }
            // ja adrese nav tuksa vai nav lokala tiek pienemts savienojums 
            else if (!string.IsNullOrEmpty(connectionData.Address) &&
                     connectionData.Address != "127.0.0.1" &&
                     connectionData.Address != "localhost")
            {
                isRelayConfigured = true;
                Debug.Log("✓ Releja konfigurācija VALIDĒTA - ne-localhost adrese");
            }

            if (!isRelayConfigured)
            {
                Debug.LogWarning("Releja konfigurācijas validācija neizdevās, bet tas var būt Unity Transport adreses kļūdas dēļ");
                Debug.LogWarning("Turpinām tik un tā, jo piešķīrums izdevās un protokols ir iestatīts pareizi");
                isRelayConfigured = true; // Piespiedu pieņemšana pagaidām, lai netraucetu parejo darbību
            }

            //  Saglabāt releja kodu un atzīmēt kā izveidotu
            relayJoinCode = joinCode;
            hostRelayCreated = true;

            Debug.Log("✓ Releja izveide un konfigurācija pabeigta veiksmīgi!");
            Debug.Log($"  Gala transporta protokols: {transport.Protocol}");
            Debug.Log($"  Saglabātais releja kods: {relayJoinCode}");
            Debug.Log($"  Piezīme: Adrese var rādīt 127.0.0.1 Unity kļūdas dēļ, bet relejs ir pareizi konfigurēts"); // sis notiek ja mes piespiezam pienemt 
            
            return joinCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Neizdevās izveidot releju: {e.Message}");
            Debug.LogError($"Steka izsekošana: {e.StackTrace}");
            
            //  Ja netiek izveidots tad nonemam nost true no hostrelaycreated
            hostRelayCreated = false;
            relayJoinCode = null;
            throw;
        }
        finally
        {
            relayCreationInProgress = false;
        }
    }

    
    /// Piespiedu spēles sākšana, pat ja ne visi nosacījumi ir izpildīti.
    /// Saglabā komandu datus, izveido Unity Relay (ja nepieciešams) un uzsāk spēli.
    /// Izmantojama testēšanai vai kad administratīvi nepieciešams sākt spēli.
    
    public async void ForceStartMatch()
    {
        if (!CanStartMatch())
        {
            Debug.LogWarning("Nevar sākt spēli - prasības nav izpildītas");
            return;
        }

        Debug.Log("Sākam 2v2 spēli...");

        StoreTeamDataForGameScene();

        string targetScene = selectedGameMode == GameMode.Mode4v4 ? "GameScene4v4" : "GameScene2v2";
        Debug.Log($"Sākam tīkloto spēles scēnu: {targetScene}");

        if (IsLobbyHost())
        {
            // Veidot releju tikai tad, ja tas vēl nav izveidots
            if (!hostRelayCreated && string.IsNullOrEmpty(relayJoinCode))
            {
                Debug.Log("[LobbyManager] RESURSDATORS: Veidojam releja piešķīrumu spēles sākšanai...");
                string relayCode = await CreateRelay();
                if (string.IsNullOrEmpty(relayCode))
                {
                    Debug.LogError("[LobbyManager] RESURSDATORS: Neizdevās izveidot releja piešķīrumu spēles sākšanai!");
                    return;
                }
                hostRelayCreated = true;
                relayJoinCode = relayCode;
            }
            else
            {
                Debug.Log($"[LobbyManager] RESURSDATORS: Izmantojam esošo releja kodu: '{relayJoinCode}'");
            }

            Debug.Log($"[LobbyManager] RESURSDATORS: Iestatām releja kodu lobijā: '{relayJoinCode}'");

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
                Debug.Log("[LobbyManager] RESURSDATORS: Atjaunināts lobijs ar releja kodu un GameStarted karodziņu spēles sākšanai.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] RESURSDATORS: Neizdevās atjaunināt lobiju ar releja kodu: {e.Message}");
                return;
            }

            // Gaidiet, līdz lobija dati izplatās, pirms sākt spēli ---
            Debug.Log("[LobbyManager] RESURSDATORS: Gaidām, līdz releja kods tiek izplatīts lobija datos...");
            float waitTime = 0f;
            const float maxWait = 60f; // Palielinām maksimālo gaidīšanas laiku līdz 60s
            bool relayCodePropagated = false;
            while (waitTime < maxWait)
            {
                await Task.Delay(2000); // Gaidām 2s starp mēģinājumiem
                waitTime += 2f;
                try
                {
                    var refreshedLobby = await LobbyApiWithBackoff(() => LobbyService.Instance.GetLobbyAsync(currentLobby.Id), "GetLobbyAsync (StartMatch-propagate)");
                    if (refreshedLobby.Data != null && refreshedLobby.Data.ContainsKey("RelayCode") && !string.IsNullOrEmpty(refreshedLobby.Data["RelayCode"].Value)
                        && refreshedLobby.Data.ContainsKey("GameStarted") && refreshedLobby.Data["GameStarted"].Value == "true")
                    {
                        relayCodePropagated = true;
                        Debug.Log("[LobbyManager] RESURSDATORS: Releja kods un GameStarted karogs tagad ir redzami lobija datos.");
                        break;
                    }
                }
                catch (Unity.Services.Lobbies.LobbyServiceException ex)
                {
                    if (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                    {
                        Debug.LogWarning("[LobbyManager] RESURSDATORS: Ātruma ierobežojums sasniegts izplatīšanas laikā, gaidām 15s pirms atkārtotas mēģināšanas...");
                        await Task.Delay(15000); // Gaidām 15s pirms nākamā mēģinājuma
                        waitTime += 15f;
                    }
                    else
                    {
                        Debug.LogError($"[LobbyManager] RESURSDATORS: Kļūda, gaidot releja koda izplatīšanu: {ex.Message}");
                        break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyManager] RESURSDATORS: Kļūda, gaidot releja koda izplatīšanu: {e.Message}");
                    break;
                }
            }
            if (!relayCodePropagated)
            {
                Debug.LogWarning("[LobbyManager] RESURSDATORS: Releja kods/GameStarted neizplatījās laicīgi, turpinām tik un tā.");
            }

            // Tikai resursdatoram vajadzētu sākt tīkloto spēli
            if (GameNetworkManager.Instance != null)
            {
                Debug.Log("[LobbyManager] RESURSDATORS: Sākam tīkloto spēli caur GameNetworkManager");
                GameNetworkManager.Instance.StartGame(targetScene);
            }
            else
            {
                Debug.LogError("[LobbyManager] RESURSDATORS: GameNetworkManager.Instance ir null! Nevar sākt tīkloto spēli.");
                // Rezerves variants - vienspēlētāja režīms
                Debug.Log("Pārslēdzamies uz vienspēlētāja režīmu");
                PlayerPrefs.SetInt("SinglePlayerMode", 1);
                PlayerPrefs.Save();
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
            }
        }
        else
        {
            Debug.Log("[LobbyManager] KLIENTS: Gaidām, līdz resursdators sāk spēli un atjaunina releja kodu...");
            // Klienti pārbaudīs releja kodu un GameStarted karogu PollLobbyForUpdates un pievienosies relejam/sāks klientu, kad tas būs pieejams
            // NELĀDĒT scēnu tieši šeit!
            return;
        }
    }
    // =====================
    // LOBIJA PIEVIENOŠANĀS METODE
    // =====================
    // Šī metode ļauj klientam pievienoties esošam lobijam pēc koda.
    // Soļi un paskaidrojumi:
    // 1. Inicializē Unity Services un autentifikē lietotāju anonīmi, ja tas vēl nav izdarīts (nepieciešams, lai izmantotu Lobby API).
    // 2. Izsauc LobbyService.Instance.JoinLobbyByCodeAsync ar norādīto kodu un spēlētāja datiem (iegūstam lobija objektu no servera).
    // 3. Pēc veiksmīgas pievienošanās atjauno lokālos vārdnīcu datus (playerNames, playerTeams, playerReadyStates), lai tie atbilstu lobija stāvoklim.
    // 4. Ja lobijā jau ir relay kods, klients var sākt relay pievienošanās procesu (JoinRelay), kas ļauj pieslēgties tīkla spēlei, kad host to uzsāk.
    // 5. Ja notiek kļūda, tiek izvadīts kļūdas paziņojums, bet UI tiek atjaunināts, lai atspoguļotu aktuālo stāvokli.
    // Šī loģika nodrošina, ka klients vienmēr ir sinhronizēts ar lobija stāvokli un gatavs pievienoties spēlei, kad host to uzsāk.
    public async Task JoinLobby(string lobbyCode) // Pievienošanās esošam lobijam pēc koda
    {
        Debug.Log($"Mēģinām pievienoties lobijam ar kodu: {lobbyCode}");
        
        // Pārliecināties, ka Unity Services un Authentication ir inicializēti savak inicialize
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            Debug.Log("JoinLobby: Inicializējam Unity Services...");
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn) // ja speletajs nav pierakstijis, tiek veikts anonims pieraksts
        {
            Debug.Log("JoinLobby: Ielogojoties anonīmi...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        
        try
        {
            var joinOptions = new JoinLobbyByCodeOptions // tiek izveidots join option objekts kura ieklauts ir speletaja info un ad ar to pievienojas
            {
                Player = GetLobbyPlayer() // sis info 
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);
            Debug.Log($"Veiksmīgi pievienojies lobijam: {currentLobby.Name}");
            
            // parbauda un inicalize vārdnīcas kur tiek glabati speletaju vārdi, komandas un gatavības stāvoklis
            if (playerNames == null) playerNames = new Dictionary<string, string>();
            if (playerTeams == null) playerTeams = new Dictionary<string, string>();
            if (playerReadyStates == null) playerReadyStates = new Dictionary<string, bool>();
            
            //  sinhronize lokalos datus ar datiem no lobija (vards, komanda, gataviba)
            UpdateLocalPlayerDataFromLobby();
            
            relayConfiguredForClient = false;
            // iegust relay kodu no lobija datiem, ja tas ir pieejams
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
        
        // atjauno speletaja UI
        UpdatePlayerListUI();
        
        Debug.Log("Lobby join process completed");
    }

    
    /// Atjaunina lokālos datus ar jaunāko informāciju no lobija
    
    private void UpdateLocalPlayerDataFromLobby()
    {
        if (currentLobby == null) return;
        
        // Pārliecināties, ka vārdnīcas ir inicializētas
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

    
    /// Pārbauda, vai var sākt spēli, pamatojoties uz spēlētāju skaitu, komandu sadalījumu
    /// un gatavības statusu. Loģika atšķiras atkarībā no izvēlētā spēles režīma.
    
    /// <returns>true, ja spēli var sākt, citādi false</returns>
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
            //  Require minimum 2 players for proper multiplayer
            bool twoPlayers = totalPlayers >= 2;
            bool bothReady = totalReady >= 2;
            bool onePerTeam = blueTeam >= 1 && redTeam >= 1;
            
            if (twoPlayers && bothReady && onePerTeam)
            {
                Debug.Log("CanStartMatch: 2+ players ready on different teams - can start");
                return true;
            }
            
            //  Single player testing mode - force proper multiplayer
            Debug.Log($"CanStartMatch: Not ready - need 2+ players ({totalPlayers}), all ready ({totalReady}), on different teams (Blue={blueTeam}, Red={redTeam})");
            return false;
        }
        
        // fallback for other modes
        bool hasMinimumPlayers = totalPlayers >= 2; // Vajag 2 cilvekus ka minimums
        bool allPlayersReady = totalReady >= 2; // Vajag 2 gatavus spēlētājus
        bool hasTeamAssignment = blueTeam >= 1 && redTeam >= 1; // Vajag 1 spēlētāju katrā komandā

        Debug.Log($"CanStartMatch: TotalPlayers={totalPlayers}, Blue={blueTeam}, Red={redTeam}, Ready={totalReady}");
        return hasMinimumPlayers && allPlayersReady && hasTeamAssignment;
    }

    
    /// Atjaunina lobija datus serverī ar lokālajām izmaiņām.
    /// Izmanto Unity Lobby API, lai sinhronizētu spēlētāja datus (vārdu, komandu, gatavību).
    
    private async void UpdateLobbyData()
    {
        if (currentLobby == null) return; // kamer nav lobijs neko nedara
        if (Time.realtimeSinceStartup - lastLobbyUpdateTime < MIN_UPDATE_INTERVAL) // netjauno parak biezi ar ieksejo timeri unity
        {
            Debug.Log("Skipping lobby update due to rate limit");
            return;
        }
        try
        {
            // Atjauno pedejas atjauninasanas laiku, iegust speletaja ID un sagatvo opcijas objektu UpdatePlayerOptions
            lastLobbyUpdateTime = Time.realtimeSinceStartup;
            string playerId = AuthenticationService.Instance.PlayerId;
            var options = new UpdatePlayerOptions();
            // Ja speletajs pievienots komandai tad playerTeams.ContainKey(....)
            // Tiek sagatavoti 3 dati, vards, komanda un gatavības stāvoklis
            if (playerTeams.ContainsKey(playerId))
            {
                // dati tiek iestatiti ka PlayerDataObject ar redzamibu member nozime to ka redzami tikai lobby dalibniekiem
                options.Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerNames[playerId]) },
                    { "Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerTeams[playerId]) },
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerReadyStates.ContainsKey(playerId) ? playerReadyStates[playerId].ToString().ToLower() : "false") }
                };
                await LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id, playerId, options);
                // nosuta speletaju info uz lobby serveri, izmantojot Unity Multiplayer pakalpojumus
                // Tādejādi citi spēlētāji atuajuno statusu bez nepieciešamības restartēt lobiju.
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to update player data: {e.Message}");
            // Ja atjaunošanas laiā notiek kļūda izvada ziņojumu 
        }
    }

    
    /// Nodrošina unikālus spēlētāju vārdus, pievienojot skaitli, ja vārds jau eksistē.
    /// Pārbauda visus spēlētāju vārdus un pievieno sufiksu, ja nepieciešams.
    
    private string EnsureUniquePlayerName(string baseName, string playerId) // Vārds, ko spēlētājs izvēlas izmantot un Unikals ID
    {
        // Cauriet visus vārdu playerNames vārdnīca, ja kāds cits spēlētājs jau izmanto šo vārdu tad tas nav unikāls 
        bool isUnique = true;
        foreach (var kvp in playerNames)
        {
            if (kvp.Key != playerId && kvp.Value == baseName)
            {
                isUnique = false;
                break;
            }
        }
        
        // ja vārds ir unikāls vienkārši atgriež to
        if (isUnique)
        {
            Debug.Log($"LobbyManager: Player name '{baseName}' is unique");
            return baseName;
        }

        // Ja vārds nav unikāls, pievieno sufiksu, lai padarītu to unikālu piemērām Jānis_1 utt
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
            
            if (suffix > 100)
            {
                uniqueName = $"{baseName}_{UnityEngine.Random.Range(1000, 9999)}";
                break;
            }
        }
        // atgriež unikālo vārdu
        Debug.Log($"LobbyManager: Made unique player name: '{baseName}' -> '{uniqueName}'");
        return uniqueName;
    }

    
    /// Iestata spēlētāja komandu un atjaunina UI.
    /// Saglabā komandas izvēli lokāli un aktualizē serverī.
    
    public void SetPlayerTeam(string playerId, string team)
    {
        if (playerTeams.ContainsKey(playerId))
            playerTeams[playerId] = team;
        else
            playerTeams.Add(playerId, team);

        // Also save individual team choice to PlayerPrefs for backup
        PlayerPrefs.SetString($"PlayerTeam_{playerId}", team);
        PlayerPrefs.Save();
        
        Debug.Log($"LobbyManager: Set team {team} for player {playerId} and saved to PlayerPrefs");

        UpdateLobbyData();
        RefreshPlayerList();
    }

    
    /// Saglabā spēlētāju komandu datus, lai tie būtu pieejami spēles scēnā.
    /// Kombinē datus formātā, kas ļauj tos ielādēt spēles scēnā.
    
    private void StoreTeamDataForGameScene()
    {
        Debug.Log("========== STORING TEAM DATA FOR GAME SCENE ==========");
        
        List<string> teamDataEntries = new List<string>();
        
        //  ja nav neviena spēlētāja vai komandu datu tiek pārtraukta darbība uz izvadīta kļūda
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
        
        var allClientIds = new List<string>();
        // izveido sarakstu ar visiem spēlētāju ID un sakārto tos pēc alfabēta, lai būtu konsistenti
        foreach (var kvp in playerNames)
        {
            allClientIds.Add(kvp.Key);
        }
        allClientIds.Sort(); 
        
        Debug.Log($"LobbyManager: All player IDs (sorted): [{string.Join(", ", allClientIds)}]");

        // Atrod viņa vārdu(playerNames)
        // pārbauda vai viņs jau izvēlējies komandu(playerTeams)
        for (int i = 0; i < allClientIds.Count; i++)
        {
            string authId = allClientIds[i];
            string playerName = playerNames.ContainsKey(authId) ? playerNames[authId] : $"Player_{authId.Substring(0, Mathf.Min(4, authId.Length))}";

            // Izmanto spēlētāja izvēlēto komandu, ja tā ir pieejama, pretējā gadījumā izmanto sarkano
            string team = "Red"; // Default fallback
            if (playerTeams.ContainsKey(authId))
            {
                team = playerTeams[authId];
                Debug.Log($"LobbyManager: Player {i}: {playerName} (ID: {authId}) has selected team: {team}");
            }
            else
            {
                // izmanto noklusējuma komandu, ja nav izvēlēts tikai tada gadijuma
                team = (i % 2 == 0) ? "Red" : "Blue";
                Debug.LogWarning($"LobbyManager: Player {i}: {playerName} (ID: {authId}) has NO team selection, using fallback: {team}");
            }

            // parbauda vai izveleta komanda atbilst pieļaujamajām komandām
            if (team != "Red" && team != "Blue")
            {
                Debug.LogWarning($"LobbyManager: Invalid team '{team}' for player {playerName}, defaulting to Red");
                team = "Red";
            }

            teamDataEntries.Add($"{playerName}:{team}:{authId}");
        }
        // serializē visus datus viena tekstā un saglabā
        string serializedData = string.Join("|", teamDataEntries);
        PlayerPrefs.SetString("AllPlayerTeams", serializedData);
        Debug.Log($"LobbyManager: Stored combined team data: {serializedData}");
        
        // valide pareizo speletaju datu
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
        
        foreach (var authId in allClientIds)
        {
            string team = playerTeams.ContainsKey(authId) ? playerTeams[authId] : "Red";
            // saglaba inviduālos komandu datus katram spēlētājiem
            PlayerPrefs.SetString($"PlayerTeam_{authId}", team);
            Debug.Log($"LobbyManager: Stored individual team choice - {authId} -> {team}");
        }
        
        // saglaba lokālo spēlētāju komandu izvēli
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
                string myTeam = "Red";
                PlayerPrefs.SetString("MySelectedTeam", myTeam);
                Debug.LogWarning($"LobbyManager: No team selection found for local player, using default: {myTeam}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LobbyManager: Error storing local player team: {e.Message}");
        }
        // parliecinās vai visi dati ir fiziski ierakstīti ierīce
        PlayerPrefs.Save();
        Debug.Log($"LobbyManager: ✓ ALL TEAM DATA SAVED SUCCESSFULLY");
        Debug.Log($"====================================================");
    }

    
    /// Klienta pieslēgšanās Unity Relay, izmantojot pievienošanās kodu.
    /// Konfigurē UnityTransport komponenti ar klienta releju datiem.
    
    public async Task JoinRelay(string joinCode)
    {
        // ja spēlētājs ir host tiek izvadīts brīdinājums un metode tiek pārtraukta
        if (IsLobbyHost())
        {
            Debug.LogWarning("[LobbyManager] Host should never join relay as client!");
            return;
        }
        Debug.Log($"[Relay] Client PlayerId: {AuthenticationService.Instance.PlayerId}, JoinCode: {joinCode}");
        // ja netiek padots relay kods tiek izmesta kļūda
        if (string.IsNullOrEmpty(joinCode))
        {
            throw new System.Exception("Join code is null or empty");
        }
        // Maksimāli 3 mēģinājumi pievienoties relay
        int maxRetries = 3;
        int retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                Debug.Log($"[LobbyManager] JoinRelay attempt {retryCount + 1}: Joining allocation with code {joinCode}");
                // Iegūst relay servera informāciju kas vajadzīga lai pieslēgots kā klients
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                if (allocation == null)
                {
                    throw new System.Exception("Join allocation returned null");
                }
                
                // tiek iegūts transporta komponents no networkmanager - nepieciešams, lai konfigurētu savieonojumu ar releju
                var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    throw new System.Exception("UnityTransport not found on NetworkManager");
                }
                
                Debug.Log($"[LobbyManager] JoinRelay attempt {retryCount + 1}: Setting client relay data");
                // te ir visi dati kas nepieciešami lai pieslēgotos relay serverim
                transport.SetClientRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData,
                    allocation.HostConnectionData
                );
                
           
                await Task.Delay(500);
                
          
                Debug.Log($"[LobbyManager] JoinRelay attempt {retryCount + 1}: Validating transport configuration");
                Debug.Log($"  Protocol: {transport.Protocol}");
                // ja transporta protokols nav iestaitts uz relay režimu tiek izmesta kļuda
                if (transport.Protocol != Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
                {
                    throw new System.Exception($"Transport protocol not set to relay after SetClientRelayData. Current: {transport.Protocol}");
                }
                
                Debug.Log($"[LobbyManager] JoinRelay attempt {retryCount + 1}: SUCCESS - relay configured");
                // ja viss ir veiksmīgi tad tiek atzīmēts, ka relay savienojums ir veiksmīģi konfigurēts
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

    
    /// Izveido Player objektu Unity Lobby servisam
    
    private Unity.Services.Lobbies.Models.Player GetLobbyPlayer()
    {
        // katram Unity lietotājam ir unikāls ID
        string playerId = AuthenticationService.Instance.PlayerId;
        // ja ir setingsmanager izmanto taja definēto lietotājvārdu
        string playerName = SettingsManager.Instance != null ?
            SettingsManager.Instance.PlayerName :
            $"Player{UnityEngine.Random.Range(1000, 9999)}";
            
   
        playerName = EnsureUniquePlayerName(playerName, playerId);
        
        // Vārdnības glabā visu spēlētāju stāvokli 
        if (playerNames == null) playerNames = new Dictionary<string, string>();
        if (playerTeams == null) playerTeams = new Dictionary<string, string>();
        if (playerReadyStates == null) playerReadyStates = new Dictionary<string, bool>();
        // Noklusējuma komanda ir "Red", ja nav norādīta un gatavības statuss false
        playerNames[playerId] = playerName;
        playerTeams[playerId] = "Red";
        playerReadyStates[playerId] = false;
        Debug.Log($"GetLobbyPlayer: ENSURED lobby player data - Name: {playerName}, Team: Red, ID: {playerId}");
        // Tiek atgriezts ka Unity player objekts ko nosūta uz lobby
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

    // Notikums, lai paziņotu, kad klients ir gatavs pievienoties spēlei (relejs konfigurēts)
    public event System.Action OnClientReadyForGame;


    // bool mainīgais kas pārbauda vai klients ir konfigurēts ar releju
    private bool relayConfiguredForClient = false;

    
    /// Pārbauda, vai klients ir gatavs pievienoties spēlei.
    /// Veic papildu validāciju, lai pārliecinātos, ka transports ir pareizi konfigurēts.
    
    /// true, ja klients ir gatavs pievienoties spēlei, citādi false
    public bool IsClientReadyForGame()
    {
        // mainīga vētība tiks iestatīta pēc veiksmīgas releja konfigurācijas
        if (!relayConfiguredForClient)
        {
            return false;
        }
        
        // unity transport komponente instance kas ir atbildīga par tīkla savienojumu 
        var transport = NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        // notikums ja nav konfigurēts networkmanager vai transporta kompenente nav piesaistīta
        if (transport == null)
        {
            Debug.LogWarning("[LobbyManager] IsClientReadyForGame: Transport is null");
            return false;
        }
        
        // tiek pārbaudīts vai tīkla savienojums izmanto tieši relay protokolu
        bool isReady = transport.Protocol == Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport;
        
        if (!isReady)
        {
            Debug.LogWarning($"[LobbyManager] IsClientReadyForGame: Transport protocol is {transport.Protocol}, expected RelayUnityTransport");

            relayConfiguredForClient = false;
        }
        
        return isReady;
    }

    
    /// Atgriež pašreizējo lobija objektu.
    
    public Lobby GetCurrentLobby()
    {
        return currentLobby;
    }

    
    /// Pārbauda, vai pašreizējais spēlētājs ir lobija resursdators (host).
    
    public bool IsLobbyHost()
    {
   
        if (currentLobby == null || currentLobby.HostId == null)
            return false;
        return AuthenticationService.Instance.PlayerId == currentLobby.HostId;
    }

    
    /// Atjauno spēlētāju saraksta UI ar jaunākajiem datiem.
    /// Pārbūvē UI un atjaunina pogu stāvokļus.
    
    public void RefreshPlayerList()
    {
        UpdatePlayerListUI();
        UpdateStartButtonState();
    }

    
    /// Pārslēdz spēlētāja gatavības statusu.
    /// Ja visi spēlētāji ir gatavi un izpildīti citi nosacījumi, resursdators var automātiski sākt spēli.
    
    public void SetPlayerReady(string playerId)
    {
        bool currentState = IsPlayerReady(playerId);
        playerReadyStates[playerId] = !currentState;
        UpdateLobbyData();
        UpdateStartButtonState();
        RefreshPlayerList();

        if (IsLobbyHost() && playerId == AuthenticationService.Instance.PlayerId && playerReadyStates[playerId])
        {
  
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
            
            // Pārbaudam vai ir vismaz 2 spēlētāji, abi gatavi un katrā komandā ir vismaz 1 spēlētājs
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

    
    /// Pārbauda, vai spēlētājs ir atzīmējis gatavību.
   
    public bool IsPlayerReady(string playerId)
    {
        return playerReadyStates.ContainsKey(playerId) && playerReadyStates[playerId];
    }

    
    /// Pārbauda, vai spēlētājs ir zilajā komandā.
    
 
    public bool IsPlayerBlueTeam(string playerId)
    {
        return playerTeams.ContainsKey(playerId) && playerTeams[playerId] == "Blue";
    }

    
    /// Sāk spēli (paredzēts saukšanai no UI).
    /// Publiska metode, ko var izsaukt no UI pogām.
    
    public void StartMatch()
    {
        StartMatchAsync();
    }

    
    /// Asinhronā versija spēles sākšanai.
    /// Nodrošina, ka spēles sākšanas process notiek asinhronā kontekstā.
    
    private async void StartMatchAsync()
    {
        await StartMatchInternal();
    }

    
    /// Iekšējā implementācija spēles sākšanai.
    
    private async Task StartMatchInternal()
    {

        ForceStartMatch();
    }

    
    /// Pievieno jaunu tērzēšanas ziņu.
    /// Uztur maksimālo ziņu skaitu, noņemot vecākās ziņas, ja nepieciešams.

    public void AddChatMessage(string message)
    {
        if (chatMessages.Count >= MAX_CHAT_MESSAGES)
            chatMessages.RemoveAt(0);
        chatMessages.Add(message);
       
    }

    
    /// Iestata izvēlēto spēles režīmu.
   
    public void SetGameMode(GameMode mode)
    {
        selectedGameMode = mode;
        Debug.Log($"Game mode set to: {mode}");
    }

    
    /// Atgriež vārdnīcu ar spēlētāja ID un komandu kartējumu.

    public Dictionary<string, string> GetAuthIdToTeamMapping()
    {
  
        return new Dictionary<string, string>(playerTeams);
    }

    
    /// Atrod vai izveido NetworkManager komponenti.
    /// Vispirms meklē esošu NetworkManager vai izveido jaunu, ja nepieciešams.

    public NetworkManager FindOrCreateNetworkManager()
    {

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

    
    /// Atjaunina spēlētāju saraksta UI ar jaunākajiem datiem.
    /// Izveido LobbyPlayerData objektus un nodod tos UI atjaunināšanai.
    
    public void UpdatePlayerListUI()
    {
        // izveidot speletaju ui
        var players = new List<LobbyPlayerData>();
        

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
      
            var playerData = new LobbyPlayerData();
            playerData.PlayerId = playerId;
            playerData.PlayerName = playerName;
            playerData.Team = team;
            playerData.IsReady = isReady;
            playerData.IsLocalPlayer = playerId == AuthenticationService.Instance.PlayerId;
            
            players.Add(playerData);
        }

        Debug.Log($"LobbyManager: Built player list with {players.Count} players");
        
       
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


    }

    
    /// Īpašība, kas norāda, vai resursdators var sākt spēli.
    /// Pārbauda, vai ir pietiekams spēlētāju skaits, vai visi ir gatavi un vai komandu sadalījums ir pareizs.
    
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

    
    /// Atjaunina sākšanas pogas stāvokli atkarībā no spēlētāju gatavības.
    /// Iespējo vai atspējo sākšanas pogu, pamatojoties uz CanForceStart stāvokli.
    
    public void UpdateStartButtonState()
    {

    }

    
    /// Izsaukts, kad lietotājs nospiež piespiedu sākšanas pogu.
    /// Pārbauda, vai lietotājs ir resursdators, un sāk spēli.
    
    public void ForceStartButtonPressed()
    {
        if (IsLobbyHost())
        {
            Debug.Log("[LobbyManager] Host pressed FORCE START button - starting match immediately.");
            ForceStartMatch();
        }
    }

    
    /// Inicializē Unity Services un autentifikāciju.
    /// Nodrošina, ka Unity servisi ir inicializēti un lietotājs ir autentificēts.
    
    /// <returns>Task, kas atspoguļo asinhronās operācijas izpildi</returns>
    public async Task InitializeUnityServices()
    {
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            Debug.Log("Inicializējam Unity servisu...");
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("Ielogojos anonīmi...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        Debug.Log("Unity Services initialized successfully");
    }


    
    /// Atiestata visu lobija stāvokli.
    /// Tiek izsaukts, ejot atpakaļ uz galveno izvēlni vai sākot jaunu spēli.
    /// Notīra visus datu vārdnīcas un atiestata konfigurācijas.
    
    public void ResetLobbyState()
    {
        // reseto visus lobija datus un stāvokli sis ir lai ejot atpakal uz galveno izvēlni vai sākot jaunu spēli
        hostRelayCreated = false;
        relayJoinCode = null;
        relayCreationInProgress = false;
        relayConfiguredForClient = false;
        clientStartedGame = false;
        selectedGameMode = GameMode.None;
        heartbeatTimer = 0f;
        lobbyPollTimer = 0f;
        lobbyPollBackoff = 0f;
        lastLobbyUpdateTime = 0f;
        playerTeams?.Clear();
        playerNames?.Clear();
        playerReadyStates?.Clear();
        chatMessages?.Clear();
        currentLobby = null;
        Debug.Log("LobbyManager: ResetLobbyState called, all state cleared.");
    }
}
