using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

// Klase, kas pārvalda spēles vispārējo stāvokli un nodrošina iespēju pilnīgi atiestatīt spēli
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Statiska metode, kas pilnībā atiestata spēles stāvokli
    public static void CompleteGameReset()
    {
        Debug.Log("GameStateManager: Sākas pilnīga spēles atiestatīšana...");

        // Aptur visas koroutīnas
        var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mono in allMonoBehaviours)
        {
            if (mono != null)
            {
                mono.StopAllCoroutines();
            }
        }

        // Atiestata laika mērogu
        Time.timeScale = 1f;

        // Pilnībā izslēdz tīklošanu
        if (NetworkManager.Singleton != null)
        {
            try
            {
                if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GameStateManager: Kļūda izslēdzot NetworkManager: {e.Message}");
            }
        }

        // Notīra īslaicīgos spēles datus, bet saglabā lietotāja iestatījumus
        ClearTemporaryGameData();

        // Iznīcina spēles objektus, bet saglabā būtiskos pārvaldniekus
        DestroyTemporaryObjects();

        // Atiestata visus pārvaldniekus uz sākotnējo stāvokli
        ResetAllManagers();

        // Notīra audio
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }

        // Piespiedu kārtā veic atmiņas atbrīvošanu
        System.GC.Collect();

        Debug.Log("GameStateManager: Pilnīga atiestatīšana pabeigta, ielādē galveno izvēlni...");

        // Ielādē galvenās izvēlnes ainu
        SceneManager.LoadScene("MainMenu");
    }

    // Notīra īslaicīgos spēles datus, bet saglabā lietotāja iestatījumus
    private static void ClearTemporaryGameData()
    {
        Debug.Log("GameStateManager: Notīra īslaicīgos spēles datus...");

        // Notīra īslaicīgos PlayerPrefs (saglabā lietotāja iestatījumus)
        PlayerPrefs.DeleteKey("CurrentGameSession");
        PlayerPrefs.DeleteKey("TempPlayerData");
        PlayerPrefs.DeleteKey("CurrentTeam");
        PlayerPrefs.DeleteKey("CurrentMatch");
        PlayerPrefs.DeleteKey("AllPlayerTeams");
        PlayerPrefs.DeleteKey("MySelectedTeam");
        PlayerPrefs.DeleteKey("SinglePlayerMode");
        PlayerPrefs.DeleteKey("CurrentLobbyCode");
        PlayerPrefs.DeleteKey("LastJoinedLobby");

        // Saglabā šos: MouseSensitivity, GameVolume, Fullscreen, PlayerName, utt.
        PlayerPrefs.Save();
    }

    // Iznīcina īslaicīgos objektus, bet saglabā būtiskos pārvaldniekus
    private static void DestroyTemporaryObjects()
    {
        Debug.Log("GameStateManager: Iznīcina īslaicīgos objektus...");

        // Iznīcina tikai DontDestroyOnLoad objektus, atstāj ainas UI neskartu
        var dontDestroyObjects = new System.Collections.Generic.List<GameObject>();
        
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.name == "DontDestroyOnLoad")
            {
                var rootObjects = scene.GetRootGameObjects();
                dontDestroyObjects.AddRange(rootObjects);
                break;
            }
        }

        foreach (GameObject obj in dontDestroyObjects)
        {
            if (obj == null) continue;

            // Saglabā būtiskos pārvaldniekus - tiem jāsaglabājas starp ainām
            if (obj.GetComponent<GameSettingsManager>() != null ||
                obj.GetComponent<SettingsManager>() != null ||
                obj.GetComponent<AudioManager>() != null ||
                obj.GetComponent<GameStateManager>() != null)
            {
                Debug.Log($"GameStateManager: Saglabā būtisko pārvaldnieku: {obj.name}");
                continue;
            }

            // Iznīcina īslaicīgos spēles objektus, kas tika pārvietoti uz DontDestroyOnLoad
            Debug.Log($"GameStateManager: Iznīcina īslaicīgo objektu: {obj.name}");
            Destroy(obj);
        }

        // Neiznīcina ainas UI - tam jāpaliek neskartam un jāstrādā ar inspektora piešķirēm
        Debug.Log("GameStateManager: Ainas UI objekti saglabāti inspektora piešķirēm");
    }

    // Atiestata visus pārvaldniekus uz sākotnējo stāvokli
    private static void ResetAllManagers()
    {
        Debug.Log("GameStateManager: Atiestata visus pārvaldniekus...");

        // Atiestata LobbyManager
        if (LobbyManager.Instance != null)
        {
            try
            {
                LobbyManager.Instance.StopAllCoroutines();
                
                var lobbyManagerType = typeof(LobbyManager);
                
                // Atiestata priekštelpas stāvokli
                var currentLobbyField = lobbyManagerType.GetField("currentLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (currentLobbyField != null)
                {
                    currentLobbyField.SetValue(LobbyManager.Instance, null);
                }

                // Atiestata spēlētāju vārdnīcas
                var playerTeamsField = lobbyManagerType.GetField("playerTeams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (playerTeamsField != null)
                {
                    var playerTeams = playerTeamsField.GetValue(LobbyManager.Instance) as System.Collections.Generic.Dictionary<string, string>;
                    playerTeams?.Clear();
                }

                var playerNamesField = lobbyManagerType.GetField("playerNames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (playerNamesField != null)
                {
                    var playerNames = playerNamesField.GetValue(LobbyManager.Instance) as System.Collections.Generic.Dictionary<string, string>;
                    playerNames?.Clear();
                }

                var playerReadyStatesField = lobbyManagerType.GetField("playerReadyStates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (playerReadyStatesField != null)
                {
                    var playerReadyStates = playerReadyStatesField.GetValue(LobbyManager.Instance) as System.Collections.Generic.Dictionary<string, bool>;
                    playerReadyStates?.Clear();
                }

                Debug.Log("GameStateManager: LobbyManager atiestatīšana pabeigta");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameStateManager: Kļūda atiestatot LobbyManager: {e.Message}");
            }
        }

        // Atiestata GameNetworkManager
        if (GameNetworkManager.Instance != null)
        {
            try
            {
                GameNetworkManager.Instance.StopAllCoroutines();
                Debug.Log("GameStateManager: GameNetworkManager atiestatīšana pabeigta");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameStateManager: Kļūda atiestatot GameNetworkManager: {e.Message}");
            }
        }

        // Atiestata GameSettingsManager (piespiedu kārtā pārlādē iestatījumus)
        if (GameSettingsManager.Instance != null)
        {
            try
            {
                GameSettingsManager.Instance.LoadSettings();
                Debug.Log("GameStateManager: GameSettingsManager atiestatīšana pabeigta");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameSettingsManager: Kļūda atiestatot GameSettingsManager: {e.Message}");
            }
        }

        // Atiestata SettingsManager
        if (SettingsManager.Instance != null)
        {
            try
            {
                SettingsManager.Instance.LoadSettings();
                Debug.Log("GameStateManager: SettingsManager atiestatīšana pabeigta");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameStateManager: Kļūda atiestatot SettingsManager: {e.Message}");
            }
        }
    }
}
