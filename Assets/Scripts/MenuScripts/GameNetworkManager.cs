using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


/// Pārvalda spēles tīklošanas funkcionalitāti.
/// Nodrošina spēlētāju objektu radīšanu, komandu sadalīšanu un tīkla savienojumu pārvaldību.
/// Koordinē darbības starp spēles scenām un tīkla resursiem.

public class GameNetworkManager : MonoBehaviour
{
    
    /// Statiskā instance, kas nodrošina vieglu piekļuvi no citām klasēm.
    
    public static GameNetworkManager Instance { get; private set; }

    
    /// Prefabs, kuru piešķirt NetworkManager.NetworkConfig.
    
    private GameObject prefabToAssign;

    
    /// Spēlētāja prefaba atsauce, ko izmantot tīklotajā spēlē.
    /// Pārveidots par publisku, lai LobbyManager varētu tam piekļūt.
    
    [Header("Prefabi")]
    [SerializeField] public GameObject playerPrefabReference; // Pārveidots par publisku, lai labotu LobbyManager piekļuvi

    
    /// Tīkla iestatījumu konfigurācija.
    
    [Header("Tīkla iestatījumi")]
    [SerializeField] private float sceneLoadTimeout = 30f; // Ainas ielādes noilgums sekundēs
    [SerializeField] private float hostStartDelay = 1f; // Aizkave pirms hosta sākšanas

    
    /// Spēlētāju komandu parādīšanās punkti.
    
    [Header("Spēlētāju parādīšanās punkti")]
    [SerializeField] private Transform[] redTeamSpawns; // Sarkanās komandas parādīšanās punkti
    [SerializeField] private Transform[] blueTeamSpawns; // Zilās komandas parādīšanās punkti
    
    
    
    private bool isLoadingScene = false;
    private string pendingSceneName = "";

    
    /// Seko parādīšanās punktu izmantošanai secīgai spēlētāju izvietošanai.
    
    private Dictionary<string, int> teamSpawnCounters = new Dictionary<string, int>();

    
    /// Vārdnīca, lai saglabātu klientu autentifikācijas ID savienojuma apstiprināšanas laikā.
    
    private Dictionary<ulong, string> clientAuthIds = new Dictionary<ulong, string>();

    
    /// Inicializē un konfigurē GameNetworkManager.
    /// Iestata NetworkManager, savienojuma apstiprināšanas loģiku un pārbauda parādīšanās punktus.
    
    private void Awake()
    {
        // Pārbauda, vai šī ir vienīgā GameNetworkManager instance, un iestata to kā Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameNetworkManager: Instance izveidota un iestatīta kā DontDestroyOnLoad");

            // Pārbauda, vai parādīšanās punkti ir piešķirti inspektorā
            ValidateSpawnPoints();

            // Piespiež PlayerPrefab piešķiršanu NetworkManager.Singleton, lai novērstu Unity kļūdas
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
            {
                NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
                // Atspējo automātisko spēlētāju radīšanu, lai to pārvaldītu ConnectionApprovalCheck
                NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
                // Izvada informāciju par visiem iespējamajiem prefab avotiem
                Debug.Log($"[GameNetworkManager] Inspektora playerPrefabReference: {(playerPrefabReference != null ? playerPrefabReference.name : "null")}");
                Debug.Log($"[GameNetworkManager] NetworkConfig.PlayerPrefab PIRMS: {(NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab.name : "null")}");

                // Vienmēr piešķir prefab, pat ja tas jau ir iestatīts, lai novērstu Unity kļūdas
                prefabToAssign = playerPrefabReference;
                if (prefabToAssign == null)
                {
                    prefabToAssign = Resources.Load<GameObject>("Prefabs/Player");
                    if (prefabToAssign != null)
                    {
                        Debug.Log("[GameNetworkManager] Ielādēts PlayerPrefab no Resources/Prefabs/Player");
                    }
                }
                if (prefabToAssign == null && NetworkManager.Singleton.NetworkConfig.Prefabs != null)
                {
                    foreach (var np in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                    {
                        if (np.Prefab != null && np.Prefab.name.ToLower().Contains("player"))
                        {
                            prefabToAssign = np.Prefab;
                            Debug.Log($"[GameNetworkManager] Rezerves variants: Atrasts PlayerPrefab NetworkPrefabs sarakstā: {prefabToAssign.name}");
                            break;
                        }
                    }
                }
                // Saglabā prefab, bet nepievieno to uzreiz - to izdarīs ConnectionApprovalCheck
                Debug.Log($"[GameNetworkManager] Saglabāts PlayerPrefab manuālai izvietošanai: {(prefabToAssign != null ? prefabToAssign.name : "null")}");
            }
            else
            {
                Debug.LogWarning("[GameNetworkManager] Nevarēja iestatīt PlayerPrefab uz NetworkManager.Singleton (trūkst atsauces vai prefaba)");
            }

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
      
            // Izņem dublikātu NetworkManager objektus, lai novērstu konfliktus
            var allManagers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            foreach (var nm in allManagers)
            {
                if (nm != NetworkManager.Singleton)
                {
                    Debug.LogWarning("GameNetworkManager: Iznīcina dublikātu NetworkManager ainā");
                    Destroy(nm.gameObject);
                }
            }
            if (NetworkManager.Singleton != null)
            {
                DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
            }

            // Iestata ConnectionApprovalCallback, lai pārvaldītu klientu apstiprināšanu un spēlētāju radīšanu
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback = ConnectionApprovalCheck;
                Debug.Log("GameNetworkManager: Iestatīts ConnectionApprovalCallback");
            }

            // Inicializē komandu parādīšanās secības skaitītājus
            teamSpawnCounters["Red"] = 0;
            teamSpawnCounters["Blue"] = 0;
        }
        else
        {
            Debug.Log("GameNetworkManager: Dublikāta instance iznīcināta");
            Destroy(gameObject);
        }

    }

 

    
    /// Atrod īsto NetworkManager instanci no DontDestroyOnLoad.
    /// Nodrošina pareizās NetworkManager instances iegūšanu, pat ja scenā ir vairākas.
    
    private NetworkManager GetTrueNetworkManager()
    {
        // Vienmēr atgriež īsto DontDestroyOnLoad instanci
        var allManagers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
        foreach (var nm in allManagers)
        {
            if (nm != null && nm.gameObject.scene.name == "DontDestroyOnLoad")
                return nm;
        }
        // Rezerves variants: Singleton
        return NetworkManager.Singleton;
    }

    
    /// Sāk spēli norādītajā scēnā, konfigurējot tīkla lomu (hosts vai klients).
    /// Pārvalda scēnas ielādi un tīkla savienojumu izveidi vai pievienošanos.
    
    public void StartGame(string sceneName)
    {
        //  Atļauj tikai hostam tiešā veidā izsaukt StartGame, bet atļauj arī klientam, ja relejs ir gatavs un IsClientReadyForGame
        if (LobbyManager.Instance != null && !LobbyManager.Instance.IsLobbyHost())
        {
            // Allow client to start if relay is ready and IsClientReadyForGame
            //  Atļauj klientam sākt, ja relejs ir gatavs un IsClientReadyForGame
            if (!LobbyManager.Instance.IsClientReadyForGame())
            {
                Debug.LogWarning("[GameNetworkManager] StartGame called on CLIENT but relay not ready. Waiting for relay...");
                //  StartGame izsaukts KLIENTĀ, bet relejs nav gatavs. Gaida releju...
                return;
            }
            Debug.LogWarning("[GameNetworkManager] StartGame called on CLIENT with relay ready. Proceeding to start client.");
            //  StartGame izsaukts KLIENTĀ ar gatavu releju. Turpina ar klienta palaišanu.
        }

        Debug.LogWarning($"[GameNetworkManager] StartGame CALLED on {(NetworkManager.Singleton?.IsHost == true ? "HOST" : "CLIENT")} with scene: {sceneName}, isLoadingScene={isLoadingScene}, IsClient={NetworkManager.Singleton?.IsClient}, IsHost={NetworkManager.Singleton?.IsHost}");
        
        // Pārbauda, vai nemēģinām sākt tīklošanu MainMenu scenā
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu" && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("GameNetworkManager: Host is already running in MainMenu - shutting down before starting game");
            //  Hosts jau darbojas MainMenu - aizveram to pirms spēles sākšanas
            NetworkManager.Singleton.Shutdown();
            // Wait a frame for shutdown to complete
            //  Gaidām vienu kadru, lai aizvēršana pabeidzas
            StartCoroutine(DelayedGameStart(sceneName));
            return;
        }
        
        if (isLoadingScene)
        {
            Debug.LogWarning("GameNetworkManager: Scene load already in progress, ignoring request");
            //  Scēnas ielāde jau notiek, ignorējam pieprasījumu
            return;
        }

        pendingSceneName = sceneName;

        //  Pārbaudam, vai NetworkManager eksistē un ir pareizi konfigurēts
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("GameNetworkManager: NetworkManager.Singleton is null! Cannot start networked game.");
            //  NetworkManager.Singleton ir null! Nevar sākt tīkla spēli.
            return;
        }

        //   Nodrošina, ka hosts nekad nemēģina sākt kā klients
        bool shouldBeHost = ShouldStartAsHost();
        if (shouldBeHost)
        {
            Debug.Log("[GameNetworkManager] Host detected, will only start as host (never as client)");
            //  Hosts konstatēts, sāksim tikai kā hosts (nekad kā klients)
        }
        else
        {
            //  Prevent host from starting as client ---
            //  Neļaut hostam sākt kā klientam
            if (LobbyManager.Instance != null && LobbyManager.Instance.IsLobbyHost())
            {
                Debug.LogWarning("[GameNetworkManager] Host should never start as client. Skipping StartClient.");
                //  Hostam nekad nevajadzētu sākt kā klientam. Izlaižam StartClient.
                return;
            }
            Debug.Log("[GameNetworkManager] Client detected, will only start as client");
        }

        //   Klientam jāpārliecinās, ka relejs ir konfigurēts pirms klienta sākšanas
        if (!shouldBeHost)
        {
            //   Pārbaudīt, vai releja transports ir konfigurēts pirms klienta sākšanas
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null || transport.Protocol != Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
            {
                Debug.LogError("[GameNetworkManager] Client relay transport is NOT configured! Aborting StartClient. Wait for relay to be set up.");
                //  Klienta releja transports NAV konfigurēts! Pārtraucam StartClient. Jāgaida, kamēr relejs tiks iestatīts.
                isLoadingScene = false;
                return;
            }

            if (LobbyManager.Instance != null && !LobbyManager.Instance.IsClientReadyForGame())
            {
                Debug.Log("[GameNetworkManager] Client relay not configured yet, waiting for relay before starting client...");
                //  Klienta relejs vēl nav konfigurēts, gaidām releju pirms klienta sākšanas...
                LobbyManager.Instance.OnClientReadyForGame -= StartClientAfterRelayReady;
                LobbyManager.Instance.OnClientReadyForGame += StartClientAfterRelayReady;
                pendingSceneName = sceneName;
                return;
            }
            else
            {
                Debug.Log("[GameNetworkManager] Client relay already configured, starting client immediately...");
                // Klienta relejs jau ir konfigurēts, sākam klientu nekavējoties...
            }
        }

        // Vienmēr sākam no jauna - vispirms aizveram jebkuru esošo savienojumu
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
        {
            Debug.Log($"GameNetworkManager: Shutting down existing connection before starting fresh");
            // Aizveram esošo savienojumu pirms jauna sākuma
            Debug.Log($"  Was Host: {NetworkManager.Singleton.IsHost}");
            // Bija Hosts: {NetworkManager.Singleton.IsHost}
            Debug.Log($"  Was Server: {NetworkManager.Singleton.IsServer}");
            //  Bija Serveris: {NetworkManager.Singleton.IsServer}
            Debug.Log($"  Was Client: {NetworkManager.Singleton.IsClient}");
            // Bija Klients: {NetworkManager.Singleton.IsClient}
            
            NetworkManager.Singleton.Shutdown();
            
            //  Gaidām, kamēr aizvēršana pabeidzas, tad sākam no jauna
            StartCoroutine(WaitForShutdownThenStart(shouldBeHost, sceneName));
            return;
        }

        bool shouldLoadScene = false;
        //  Sākam jaunu savienojumu
        if (shouldBeHost)
        {
            Debug.Log("GameNetworkManager: Starting as host...");
            //  Sākam kā hosts...
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeScene != sceneName)
            {
                Debug.Log($"[GameNetworkManager] Host is in '{activeScene}', needs to be in '{sceneName}'. Loading scene first...");
                //  Hosts ir scenā '{activeScene}', tam jābūt scenā '{sceneName}'. Vispirms ielādējam scēnu...
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
            //  Sākam kā klients...
            StartCoroutine(StartClientAndWaitForScene());
        }

        if (shouldLoadScene)
        {
            StartCoroutine(LoadSceneThenStartHost(sceneName));
        }
    }

    
    /// Gaida, kamēr NetworkManager aizveras, un pēc tam sāk jaunu savienojumu.
    /// Nodrošina tīru NetworkManager restartēšanu pirms jaunas spēles sākšanas.

    private System.Collections.IEnumerator WaitForShutdownThenStart(bool shouldBeHost, string sceneName)
    {
        Debug.Log("GameNetworkManager: Waiting for NetworkManager shutdown to complete...");
        //  Gaidām, kamēr NetworkManager aizvēršana pabeidzas...
        

        //  Gaidām, kamēr aizvēršana pabeidzas
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
            //  Aizvēršanas noilgums - vienalga piespiedu restartēšana
        }
        else
        {
            Debug.Log("GameNetworkManager: Shutdown completed, starting fresh connection");
            //  Aizvēršana pabeigta, sākam jaunu savienojumu
        }
        
 
        //  Tagad sākam no jauna
        if (shouldBeHost)
        {
            Debug.Log("GameNetworkManager: Starting as host after shutdown...");
            // Sākam kā hosts pēc aizvēršanas...
            StartCoroutine(StartHostAndLoadScene(sceneName));
        }
        else
        {
            Debug.Log("GameNetworkManager: Starting as client after shutdown...");
            //  Sākam kā klients pēc aizvēršanas...
            StartCoroutine(StartClientAndWaitForScene());
        }
    }

 
    
    /// Atliek spēles sākšanu par vienu kadru.
    /// Nodrošina, ka NetworkManager aizvēršana pabeidzas pirms jaunas spēles sākšanas.

    private System.Collections.IEnumerator DelayedGameStart(string sceneName)
    {
        yield return null; // Wait one frame for shutdown
        // Gaidām vienu kadru aizvēršanai
        
    
        //  Tagad pareizi sākam spēli
        StartGame(sceneName);
    }

    
    /// Nosaka, vai spēli jāsāk kā hostam vai klientam.
    /// Pamatojas uz LobbyManager lomu un citiem faktoriem.
    
    private bool ShouldStartAsHost()
    {

        //  Pārbaudām, vai esam lobby hosts
        if (LobbyManager.Instance != null && LobbyManager.Instance.IsLobbyHost())
        {
            Debug.Log("GameNetworkManager: We are the lobby host, starting as network host");
            //  Mēs esam lobby hosts, sākam kā tīkla hosts
            return true;
        }
        
    
        //  Papildiespēja: ja nav lobby vai neesam lobby hosts, pieņemam, ka jābūt klientam
        Debug.Log("GameNetworkManager: We are not the lobby host, starting as network client");
        //  Mēs neesam lobby hosts, sākam kā tīkla klients
        return false;
    }

    
    /// Izsauc StartClient pēc tam, kad relejs ir gatavs.
    /// Tiek piesaistīts LobbyManager.OnClientReadyForGame notikumam.
    
    private void StartClientAfterRelayReady()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnClientReadyForGame -= StartClientAfterRelayReady;
        }
        Debug.Log("GameNetworkManager: Relay ready, starting client...");
        // Relejs gatavs, sākam klientu...
        
        // Use the stored scene name
        // Izmantojam saglabāto scēnas nosaukumu
        if (!string.IsNullOrEmpty(pendingSceneName))
        {
            Debug.Log($"GameNetworkManager: Using pending scene name: {pendingSceneName}");
            //  Izmantojam gaidāmās scēnas nosaukumu: {pendingSceneName}
        }
        
        StartCoroutine(StartClientAndWaitForScene());
    }

    
    /// Sāk spēlētāju kā klientu un gaida scēnas ielādi no servera.
    /// Konfigurē klienta savienojuma datus un sāk savienojumu ar hostu.
    
    private IEnumerator StartClientAndWaitForScene()
    {
        isLoadingScene = true;
        Debug.LogWarning("GameNetworkManager: [CLIENT] Starting client for networked game...");
        //  Sākam klientu tīkla spēlei...

        // Atļaut StartClient tikai MainMenu scenā
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            Debug.LogError("GameNetworkManager: Client must call StartClient() in MainMenu scene! Aborting client start.");
            //  Klientam jāizsauc StartClient() MainMenu scenā! Pārtraucam klienta sākšanu.
            isLoadingScene = false;
            yield break;
        }

        //  Vienmēr iestatīt PlayerPrefab uz NetworkManager.Singleton pirms klienta sākšanas
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
                //  Iestatīts PlayerPrefab uz NetworkManager.Singleton: {prefabToAssign.name}
            }
        }


        //  Nodot autentifikācijas ID kā savienojuma datus
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
                //  Iestatīti ConnectionData uz authId: {authId}
            }
        }


        // Validējam transporta konfigurāciju
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport == null || transport.Protocol != Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.RelayUnityTransport)
        {
            Debug.LogError("GameNetworkManager: Client transport is NOT configured for relay! Aborting StartClient.");
            // Klienta transports NAV konfigurēts relejam! Pārtraucam StartClient.
            isLoadingScene = false;
            yield break;
        }

        //  Sākam klientu - ConnectionApprovalCheck pārvaldīs spawning
        bool clientStarted = false;
        try
        {
            Debug.LogWarning("GameNetworkManager: [CLIENT] Calling NetworkManager.Singleton.StartClient()");
            // Izsaucam NetworkManager.Singleton.StartClient()
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

        // Gaidām savienojumu
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
            //  Klientam neizdevās savienoties {timeout} sekunžu laikā
            isLoadingScene = false;
            yield break;
        }

        Debug.Log("GameNetworkManager: Client connected successfully!");
        // Klients veiksmīgi savienojies!


        //  Ielādējam scēnu, ja nepieciešams
        string expectedScene = pendingSceneName;
        string currentSceneNow = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(expectedScene) && currentSceneNow != expectedScene)
        {
            Debug.LogWarning($"GameNetworkManager: Client loading scene {expectedScene}");
            // Klients ielādē scēnu {expectedScene}
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

    
    /// Sāk spēlētāju kā resursdatoru (host) un ielādē norādīto scēnu.
    /// Konfigurē NetworkManager un gaida klientu pievienošanos.
    

    private IEnumerator StartHostAndLoadScene(string sceneName)
    {
        isLoadingScene = true;
        Debug.Log("GameNetworkManager: Starting host for networked game...");

        //  Disable PlayerPrefab temporarily to prevent automatic spawning
        var allManagers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
        foreach (var nm in allManagers)
        {
            if (nm != null && nm.NetworkConfig != null)
            {
                nm.NetworkConfig.PlayerPrefab = null; // Disable auto-spawn
            }
        }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
        {
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null; // Disable auto-spawn
            Debug.Log("GameNetworkManager: Disabled PlayerPrefab to prevent automatic spawning");
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


    
    /// Nosaka sagaidāmo spēlētāju skaitu spēlē.
    /// Izmanto datus no LobbyManager vai noklusējuma vērtību minimālai spēlei.
    
    private int GetExpectedPlayerCount()
    {
        int expectedPlayers = 1; 
        
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
            
            // check player count from LobbyManager dictionaries
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
        
        if (expectedPlayers < 2)
        {
            Debug.LogWarning($"GameNetworkManager: Expected players ({expectedPlayers}) less than 2, setting to 2");
            expectedPlayers = 2;
        }
        
        return expectedPlayers;
    }

    
    /// Ielādē scēnu kā serveris, izmantojot NetworkManager scēnu pārvaldību.
    /// Izsauc MonitorSceneLoad, lai sekotu līdzi scēnas ielādes statusam.
    
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

    
    /// Uzrauga scēnas ielādes progresu un izsauc OnSceneLoadComplete, kad pabeigts.
    /// Nodrošina scēnas ielādes pabeigšanu noteiktā laika periodā vai ziņo par kļūdu.
    
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

    
    /// Apstrādā darbības pēc scēnas ielādes pabeigšanas.
    /// Atjauno PlayerPrefab piešķīrumus un manuāli rada visus savienotos spēlētājus.
    
    private void OnSceneLoadComplete(string sceneName)
    {
        Debug.Log($"GameNetworkManager: Scene {sceneName} fully loaded, network game ready");

        ResetSpawnCounters();

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

        PlayerPrefs.SetInt("SinglePlayerMode", 0);
        PlayerPrefs.Save();
        
        Debug.Log("GameNetworkManager: Scene load complete - manually spawning all players at inspector spawn points");
        
        if (NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(ManuallySpawnAllPlayers());
        }
    }

    
    /// Manuāli rada visus savienotos spēlētājus pēc scēnas ielādes.
    /// Iterē caur visiem savienotajiem klientiem un izsauc ManuallySpawnPlayer katram.
    
    private IEnumerator ManuallySpawnAllPlayers()
    {
        Debug.Log("GameNetworkManager: Starting manual spawn process for all connected players");
        
        yield return new WaitForSeconds(1f);
        
        var connectedClients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        Debug.Log($"GameNetworkManager: Found {connectedClients.Count} connected clients to spawn");
        
        foreach (ulong clientId in connectedClients)
        {
            Debug.Log($"GameNetworkManager: Attempting to spawn client {clientId}");
            
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            {
                if (clientData.PlayerObject != null)
                {
                    Debug.LogWarning($"GameNetworkManager: Client {clientId} already has player object, skipping spawn");
                    continue;
                }
            }
            // manuali nospawno speletaju pec klienta ID
            ManuallySpawnPlayer(clientId);
            
            yield return new WaitForSeconds(0.2f);
        }
        
        Debug.Log("GameNetworkManager: Manual spawn process completed for all players");
    }

    
    /// Nosaka komandu klientam, pamatojoties uz saglabātajiem datiem vai determiniskiem algoritmiem.
    /// Meklē komandas informāciju vairākos avotos un izmanto atkāpšanās mehānismus.

    public string GetTeamForClient(ulong clientId)
    {
        Debug.Log($"========== GET TEAM FOR CLIENT {clientId} ==========");

        // Always use local authId for local client
        string localAuthId = "";
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            try
            {
                localAuthId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            }
            catch { }
        }

        // 1. meginat lasit informaciju no PlayerPrefs
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

                        // lokaliem klientiem meginat atrast vinu authId
                        if (!string.IsNullOrEmpty(localAuthId) && clientId == NetworkManager.Singleton.LocalClientId && localAuthId == authId)
                        {
                            Debug.Log($"GameNetworkManager: ✓ LOCAL AUTH ID MATCH for client {clientId}: {team}");
                            return team;
                        }

                        // prieks servera, meginam atrast atbilstibu
                        string clientAuthId = GetAuthIdForClient(clientId);
                        if (!string.IsNullOrEmpty(clientAuthId) && clientAuthId == authId)
                        {
                            Debug.Log($"GameNetworkManager: ✓ EXACT AUTH ID MATCH for client {clientId}: {team}");
                            return team;
                        }
                    }
                }
                Debug.LogWarning($"GameNetworkManager: No auth ID match found for client {clientId} in stored team data");
            }
            else
            {
                Debug.LogWarning("GameNetworkManager: No stored team data found in PlayerPrefs");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"GameNetworkManager: Error reading team data: {e.Message}");
        }

        // 2. mēģināt izmantot atsevišķu komandas izvēles rezerves variantu tikai lokālajam klientam
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
            Debug.LogWarning($"GameNetworkManager: Error reading fallback team: {e.Message}");
        }

        // 3. meginat izmantot pieskirsanu balstoties uz klienta ID
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

    
    
    /// Iegūst spēlētāja parādīšanās pozīciju, pamatojoties uz inspektorā definētajiem punktiem.
    /// Izvēlas pareizo komandas parādīšanās punktu masīvu un validē visas vērtības.
    

    public Vector3 GetSpawnPositionFromInspector(ulong clientId, string team)
    {
        Debug.Log($"========== GET SPAWN POSITION FROM INSPECTOR ==========");
        Debug.Log($"Client: {clientId}, Team: {team}");
        
        // CRITICAL: Select correct team spawn array
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
                fallback = new Vector3(8f, 0.71f, 0f); // zila komanda (labā puse no ledus)
            }
            else
            {
                fallback = new Vector3(-8f, 0.71f, 0f); // sarkana komanda (kreisā puse no ledus)
            }
            
            Debug.LogError($"Using emergency fallback position for {team} team: {fallback}");
            return fallback;
        }

        // spawnosanas tiek pieksirts secigi 
        int spawnIndex = GetNextSpawnIndexForTeam(team, teamSpawns.Length);
        Debug.Log($"Sequential spawn index for {team} team: {spawnIndex}");

        // parbaude vai indekkss ir derīgs
        if (spawnIndex < 0 || spawnIndex >= teamSpawns.Length)
        {
            Debug.LogError($"GameNetworkManager: Invalid spawn index {spawnIndex} for team {team} (max: {teamSpawns.Length - 1})");
            spawnIndex = 0; // Use first spawn as fallback
        }
        
        Transform spawnTransform = teamSpawns[spawnIndex];
        
        if (spawnTransform == null)
        {
            Debug.LogError($"GameNetworkManager: ✗ {team} team spawn transform at index {spawnIndex} is NULL!");
            
            // atrast atbilstosu transform poziciju (spawn)
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

        // izmantot konkretu transformācijas pozīciju ar validāciju
        Vector3 spawnPos = spawnTransform.position;
        
        // : Validate spawn position is reasonable
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

    
    /// Manuāli rada spēlētāja objektu klientam pareizajā pozīcijā un ar pareizo komandu.
    /// Veic kļūdu pārbaudes un validāciju visos soļos.
    
    private void ManuallySpawnPlayer(ulong clientId)
    {
        Debug.Log($"========== MANUAL SPAWN PLAYER {clientId} ==========");
        
        //  Additional validations before spawning
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

        // parbauda vai lietotajs jau nav nospawnots
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
        {
            if (clientData.PlayerObject != null)
            {
                Debug.LogWarning($"GameNetworkManager: Client {clientId} already has a player object, skipping spawn");
                return;
            }
        }
        
        // iegut komandu sadali ar logging
        string team = GetTeamForClient(clientId);
        Debug.Log($"GameNetworkManager: Client {clientId} assigned to team: {team}");
        
        //  iegut spawnosanas poziciju no inspektora
        Vector3 spawnPos = GetSpawnPositionFromInspector(clientId, team);
        Debug.Log($"GameNetworkManager: Client {clientId} spawn position: {spawnPos}");
        
        // konkreti norote speletajus lai tie skatitos uz centru
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
            GameObject playerObject = Instantiate(NetworkManager.Singleton.NetworkConfig.PlayerPrefab, spawnPos, spawnRot);
            
            if (playerObject == null)
            {
                Debug.LogError($"GameNetworkManager: Failed to instantiate player object for client {clientId}");
                return;
            }
            
            //  piespiez spēlētāja objektu pareizajā pozīcijā un rotācijā
            playerObject.transform.position = spawnPos;
            playerObject.transform.rotation = spawnRot;
            
            Debug.Log($"GameNetworkManager: Instantiated player object at {playerObject.transform.position} with rotation {playerObject.transform.rotation.eulerAngles}");
            
            // iegut networkobject komponenti un piespiest spawnot to kā spēlētāja objektu
            var networkObject = playerObject.GetComponent<Unity.Netcode.NetworkObject>();
            if (networkObject != null)
            {
                //  nospawno speletaju ka konkretu objektu
                networkObject.SpawnAsPlayerObject(clientId);
                Debug.Log($"GameNetworkManager: Spawned NetworkObject for client {clientId}");
                
                //  gaida briid lidz spawnosanas notiek
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

    
    /// Pabeidz spēlētāja parādīšanās procesu ar precīzu pozīcijas un komandas iestatīšanu.
    /// Nodrošina, ka spēlētājs tiek pareizi inicializēts tīklā ar visiem nepieciešamajiem datiem.
    
    private IEnumerator FinalizePlayerSpawn(GameObject playerObject, ulong clientId, string team, Vector3 spawnPos, Quaternion spawnRot)
    {
        yield return null; // gaidit mazu bridi lidz spēlētājs ir pilnībā inicializēts/nospawnots
        
        if (playerObject == null)
        {
            Debug.LogError($"GameNetworkManager: Player object destroyed before finalization for client {clientId}");
            yield break;
        }
        
        Debug.Log($"GameNetworkManager: Finalizing spawn for client {clientId}");
        
        //  vairaku meginajumu nodrošināšana, lai spēlētājs būtu precīzā pozīcijā
        for (int i = 0; i < 3; i++)
        {
            // piespiezt spēlētāja objektu uz precīzu pozīciju un rotāciju
            playerObject.transform.position = spawnPos;
            playerObject.transform.rotation = spawnRot;
            
            // reseto rigidbody lai butu precīza pozīcija un rotācija
            var rigidbody = playerObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.position = spawnPos;
                rigidbody.rotation = spawnRot;
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
            
            //  atjaunot speletaja poziciju tikla ja tam ir playermovement
            var pm = playerObject.GetComponent<PlayerMovement>(); 
            if (pm != null && pm.IsServer)
            {
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
            
            if (i < 2) yield return new WaitForSeconds(0.2f);
        }
        
        // iestatit speletaja komandu izmantojot enum vertibu
        var playerMovement = playerObject.GetComponent<PlayerMovement>();
        if (playerMovement != null && NetworkManager.Singleton.IsServer)
        {
            PlayerMovement.Team teamEnum = team == "Blue" ? PlayerMovement.Team.Blue : PlayerMovement.Team.Red;
            playerMovement.SetTeamServerRpc(teamEnum);
        }

        //  sinhronizēt komandas vizuālos elementus visiem klientiem
        var netObj = playerObject.GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null && NetworkManager.Singleton.IsServer)
        {
            SyncTeamVisualsClientRpc(netObj.NetworkObjectId, team);
        }

        //  uzstada kameru prieks lokala klienta
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"GameNetworkManager: Setting up camera for local client {clientId}");
            
            // mazliet uzgaida lidz pozicija nostabilizejas
            yield return new WaitForSeconds(0.5f);
            
            var pmLocal = playerObject.GetComponent<PlayerMovement>();
            if (pmLocal != null)
            {
                pmLocal.EnsurePlayerCamera();
            }
        }
        
        Debug.Log($"GameNetworkManager: ✓ SUCCESSFULLY SPAWNED client {clientId} ({team} team)");
        Debug.Log($"  Final Position: {playerObject.transform.position}");
        Debug.Log($"  Final Rotation: {playerObject.transform.rotation.eulerAngles}");
        Debug.Log($"  Owner Client ID: {netObj?.OwnerClientId}");
        Debug.Log($"  Network Object ID: {netObj?.NetworkObjectId}");
    }

    
    /// Sinhronizē komandas vizuālos elementus visiem klientiem, izmantojot ClientRpc.
    /// Nodrošina konsistentu komandas vizuālo attēlojumu visiem spēlētājiem.
   
    [Unity.Netcode.ClientRpc]
    private void SyncTeamVisualsClientRpc(ulong networkObjectId, string team)
    {
        var playerObj = FindPlayerObjectByNetworkId(networkObjectId);
        if (playerObj != null)
        {
            var visuals = playerObj.GetComponent<PlayerTeamVisuals>();
            if (visuals != null)
            {
                visuals.SetTeamNetworked(team);
                Debug.Log($"GameNetworkManager: [ClientRpc] SetTeamNetworked({team}) called for {playerObj.name}");
            }
            else
            {
                Debug.LogWarning($"GameNetworkManager: [ClientRpc] PlayerTeamVisuals not found on {playerObj.name}");
            }
        }
        else
        {
            Debug.LogWarning($"GameNetworkManager: [ClientRpc] Player object not found for NetworkObjectId {networkObjectId}");
        }
    }

    
    /// Pielieto komandas krāsu spēlētāja objektam un sinhronizē to visiem klientiem.
    /// Nodrošina, ka komandas krāsas tiek pareizi pielietotas un redzamas visiem spēlētājiem.
    

    private void ApplyTeamColorWithSync(GameObject playerObject, string team, ulong clientId)
    {
        Debug.Log($"GameNetworkManager: Applying team color for {team} team with FORCED client sync");
        
        ApplyTeamColorDirectly(playerObject, team);
        
        var networkObject = playerObject.GetComponent<Unity.Netcode.NetworkObject>();
        if (networkObject != null && NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"GameNetworkManager: FORCING team color sync to ALL clients for NetworkObject {networkObject.NetworkObjectId}");
            
            ForceTeamColorClientRpc(networkObject.NetworkObjectId, team);
            
            StartCoroutine(ForceTeamColorSyncRepeated(networkObject.NetworkObjectId, team));
        }
    }

    
    /// Atkārto komandas krāsas sinhronizācijas mēģinājumus, lai nodrošinātu pareizu piemērošanu.
    /// Dažādos laika intervālos izsauc ForceTeamColorClientRpc, lai maksimizētu veiksmes iespējas.
    
 
    private IEnumerator ForceTeamColorSyncRepeated(ulong networkObjectId, string team)
    {
        yield return new WaitForSeconds(0.05f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        yield return new WaitForSeconds(0.1f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        yield return new WaitForSeconds(0.2f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        yield return new WaitForSeconds(0.5f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        yield return new WaitForSeconds(1f);
        ForceTeamColorClientRpc(networkObjectId, team);
        
        Debug.Log($"GameNetworkManager: Completed 6 team color sync attempts for {team} team");
    }

    
    /// Piespiedu kārtā iestata komandas krāsu klienta pusē, izmantojot ClientRpc.
    /// Izsauc ForceTeamColorOnClient, lai pielietotu krāsu ar maksimālu prioritāti.

    [Unity.Netcode.ClientRpc]
    private void ForceTeamColorClientRpc(ulong networkObjectId, string team)
    {
        Debug.Log($"GameNetworkManager: [ClientRpc] RECEIVED team color command for NetworkObject {networkObjectId}, team: {team}");
        Debug.Log($"GameNetworkManager: [ClientRpc] Running on: IsServer={NetworkManager.Singleton.IsServer}, IsHost={NetworkManager.Singleton.IsHost}, IsClient={NetworkManager.Singleton.IsClient}");
        
        GameObject playerObject = FindPlayerObjectByNetworkId(networkObjectId);
        
        if (playerObject != null)
        {
            Debug.Log($"GameNetworkManager: [ClientRpc] Found player object {playerObject.name}, applying {team} color NOW");
            
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

    
    /// Agresīvi mēģina atrast un nokrāsot spēlētāja objektu klienta pusē.
    /// Vairākkārt mēģina līdz noteiktam mēģinājumu skaitam, lai nodrošinātu veiksmīgu piemērošanu.
    
  
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

    
    /// Piespiedu kārtā pielieto komandas krāsu objektam klienta pusē.
    /// Izmanto dažādas stratēģijas, lai maksimizētu veiksmes iespējas dažādos Unity renderētājos.
    

    private bool ForceTeamColorOnClient(GameObject playerObject, string team)
    {
        if (playerObject == null) return false;
        
        Debug.Log($"GameNetworkManager: CLIENT FORCE applying {team} team color to {playerObject.name}");
        
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
        
        var allRenderers = new List<Renderer>();
        allRenderers.AddRange(playerObject.GetComponentsInChildren<Renderer>(true));
        allRenderers.AddRange(playerObject.GetComponents<Renderer>());
        
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
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        Material newMat = new Material(materials[i]);
                        newMat.color = teamColor;
                        
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
                
                renderer.materials = materials;
                renderer.material = materials[0]; 
                
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

    
    /// Iegūst autentifikācijas ID klientam no saglabātajiem datiem.
    /// Izmanto clientAuthIds vārdnīcu vai lokālo AuthenticationService instanci.
    
   
    private string GetAuthIdForClient(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            try
            {
                return Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GameNetworkManager: Error getting local auth ID: {e.Message}");
            }
        }
        if (clientAuthIds.ContainsKey(clientId))
        {
            return clientAuthIds[clientId];
        }
        return "";
    }

    
    /// Pārbauda un validē parādīšanās punktu masīvus inspektorā.
    /// Izdod brīdinājumus, ja parādīšanās punkti nav iestatīti vai ir null.
    
    private void ValidateSpawnPoints()
    {
        Debug.Log("========== VALIDATING SPAWN POINTS ==========");
        
        bool hasErrors = false;
        
        // parbauda sarkanas komandas spawns
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
        
        // parbauda zilas komandas spawns
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

    
    /// Vispirms ielādē scēnu un pēc tam sāk hostu.
    /// Izmantojama, kad resursdatoram jāmaina aina pirms tīkla spēles sākšanas.
    
 
    private IEnumerator LoadSceneThenStartHost(string sceneName)
    {
        isLoadingScene = true;
        Debug.Log($"GameNetworkManager: Loading scene {sceneName} before starting host...");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);

        // gaida lidz aina ieladesies
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

    
    /// Iegūst nākamo parādīšanās punkta indeksu komandai.
    /// Nodrošina secīgu parādīšanās punktu izmantošanu, lai izvairītos no pārklāšanās.
 
    private int GetNextSpawnIndexForTeam(string team, int maxSpawns)
    {
        if (!teamSpawnCounters.ContainsKey(team))
        {
            teamSpawnCounters[team] = 0;
        }
        
        int spawnIndex = teamSpawnCounters[team];
        
        teamSpawnCounters[team] = (teamSpawnCounters[team] + 1) % maxSpawns;
        
        Debug.Log($"GameNetworkManager: {team} team spawn counter: {spawnIndex} (next will be {teamSpawnCounters[team]})");
        
        return spawnIndex;
    }

    
    /// Atiestata parādīšanās punktu skaitītājus jaunai spēlei.
    /// Nodrošina, ka parādīšanās punktu piešķiršana sākas no nulles katrai jaunai spēlei.
    
    private void ResetSpawnCounters()
    {
        teamSpawnCounters["Red"] = 0;
        teamSpawnCounters["Blue"] = 0;
        Debug.Log("GameNetworkManager: Reset spawn counters for new game");
    }

    
    /// Apstiprina vai noraida klienta pievienošanās pieprasījumu un iestatīta savienojuma parametrus.
    /// Saglabā autentifikācijas ID no savienojuma datiem un pārvalda spēlētāju radīšanas loģiku.
    
  
    private void ConnectionApprovalCheck(Unity.Netcode.NetworkManager.ConnectionApprovalRequest request, Unity.Netcode.NetworkManager.ConnectionApprovalResponse response)
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        Debug.Log($"========== CONNECTION APPROVAL CHECK ==========");
        Debug.Log($"Client ID: {request.ClientNetworkId}");
        Debug.Log($"Current Scene: {currentScene}");
        Debug.Log($"Is Server: {NetworkManager.Singleton.IsServer}");
        Debug.Log($"Is Host: {NetworkManager.Singleton.IsHost}");
        Debug.Log($"PlayerPrefab assigned: {NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null}");

        // saglaba auth ID no savienojuma datiem, lai izmantotu komandas piešķiršanai
        if (request.Payload != null && request.Payload.Length > 0)
        {
            try
            {
                string authId = System.Text.Encoding.UTF8.GetString(request.Payload);
                if (!string.IsNullOrEmpty(authId))
                {
                    clientAuthIds[request.ClientNetworkId] = authId;
                    Debug.Log($"GameNetworkManager: ✓ Stored auth ID '{authId}' for client {request.ClientNetworkId}");

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
        response.CreatePlayerObject = false; // izslegts lai netiku automatiski spawnots 
        response.Position = Vector3.zero; 
        response.Rotation = Quaternion.identity; 
        
        Debug.Log($"GameNetworkManager: ✓ CONNECTION APPROVED - manual spawn will handle client {request.ClientNetworkId}");
        Debug.Log($"===============================================");
    }

    
    /// Atrod spēlētāja objektu pēc tīkla ID.
    /// Izmanto dažādas meklēšanas stratēģijas, lai maksimizētu atrašanas veiksmes iespējas.
    
    
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
        
        // mekle visus network objectus, speles aina
        var allNetworkObjects = FindObjectsByType<Unity.Netcode.NetworkObject>(FindObjectsSortMode.None);
        foreach (var obj in allNetworkObjects)
        {
            if (obj != null && obj.NetworkObjectId == networkObjectId)
            {
                return obj.gameObject;
            }
        }
        
        // mekle pievienojosos klientus
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            if (kvp.Value.PlayerObject != null && kvp.Value.PlayerObject.NetworkObjectId == networkObjectId)
            {
                return kvp.Value.PlayerObject.gameObject;
            }
        }
        
        return null;
    }

    
    /// Pielieto komandas krāsu spēlētāja objektam tiešā veidā.
    /// Veic vizuālo elemetu manipulāciju, lai piešķirtu pareizo komandas izskatu.
    

    private bool ApplyTeamColorDirectly(GameObject playerObject, string team)
    {
        if (playerObject == null)
        {
            Debug.LogError("GameNetworkManager: Cannot apply team color - player object is null");
            return false;
        }
        
        Debug.Log($"GameNetworkManager: Applying {team} team color to {playerObject.name}");
        
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