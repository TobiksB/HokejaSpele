using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

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

    public static void CompleteGameReset()
    {
        Debug.Log("GameStateManager: Starting complete game reset...");

        // Stop all coroutines
        var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mono in allMonoBehaviours)
        {
            if (mono != null)
            {
                mono.StopAllCoroutines();
            }
        }

        // Reset time scale
        Time.timeScale = 1f;

        // Shutdown networking completely
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
                Debug.LogWarning($"GameStateManager: Error shutting down NetworkManager: {e.Message}");
            }
        }

        // Clear temporary game data but preserve user settings
        ClearTemporaryGameData();

        // Destroy game objects but keep essential managers
        DestroyTemporaryObjects();

        // Reset all managers to initial state
        ResetAllManagers();

        // Clear audio
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }

        // Force garbage collection
        System.GC.Collect();

        Debug.Log("GameStateManager: Complete reset finished, loading main menu...");

        // Load main menu scene
        SceneManager.LoadScene("MainMenu");
    }

    private static void ClearTemporaryGameData()
    {
        Debug.Log("GameStateManager: Clearing temporary game data...");

        // Clear temporary PlayerPrefs (keep user settings)
        PlayerPrefs.DeleteKey("CurrentGameSession");
        PlayerPrefs.DeleteKey("TempPlayerData");
        PlayerPrefs.DeleteKey("CurrentTeam");
        PlayerPrefs.DeleteKey("CurrentMatch");
        PlayerPrefs.DeleteKey("AllPlayerTeams");
        PlayerPrefs.DeleteKey("MySelectedTeam");
        PlayerPrefs.DeleteKey("SinglePlayerMode");
        PlayerPrefs.DeleteKey("CurrentLobbyCode");
        PlayerPrefs.DeleteKey("LastJoinedLobby");

        // Keep these: MouseSensitivity, GameVolume, Fullscreen, PlayerName, etc.
        PlayerPrefs.Save();
    }

    private static void DestroyTemporaryObjects()
    {
        Debug.Log("GameStateManager: Destroying temporary objects...");

        // Only destroy DontDestroyOnLoad objects, leave scene UI alone
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

            // Keep essential managers - these should persist between scenes
            if (obj.GetComponent<GameSettingsManager>() != null ||
                obj.GetComponent<SettingsManager>() != null ||
                obj.GetComponent<AudioManager>() != null ||
                obj.GetComponent<GameStateManager>() != null)
            {
                Debug.Log($"GameStateManager: Keeping essential manager: {obj.name}");
                continue;
            }

            // Destroy temporary game objects that were moved to DontDestroyOnLoad
            Debug.Log($"GameStateManager: Destroying temporary object: {obj.name}");
            Destroy(obj);
        }

        // Don't destroy scene UI - it should remain intact and work with inspector assignments
        Debug.Log("GameStateManager: Scene UI objects preserved for inspector assignments");
    }

    private static void ResetAllManagers()
    {
        Debug.Log("GameStateManager: Resetting all managers...");

        // Reset LobbyManager
        if (LobbyManager.Instance != null)
        {
            try
            {
                LobbyManager.Instance.StopAllCoroutines();
                
                // Use reflection to reset private fields
                var lobbyManagerType = typeof(LobbyManager);
                
                // Reset lobby state
                var currentLobbyField = lobbyManagerType.GetField("currentLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (currentLobbyField != null)
                {
                    currentLobbyField.SetValue(LobbyManager.Instance, null);
                }

                // Reset player dictionaries
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

                Debug.Log("GameStateManager: LobbyManager reset complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameStateManager: Error resetting LobbyManager: {e.Message}");
            }
        }

        // Reset GameNetworkManager
        if (GameNetworkManager.Instance != null)
        {
            try
            {
                GameNetworkManager.Instance.StopAllCoroutines();
                Debug.Log("GameStateManager: GameNetworkManager reset complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameStateManager: Error resetting GameNetworkManager: {e.Message}");
            }
        }

        // Reset GameSettingsManager (force reload settings)
        if (GameSettingsManager.Instance != null)
        {
            try
            {
                GameSettingsManager.Instance.LoadSettings();
                Debug.Log("GameStateManager: GameSettingsManager reset complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameSettingsManager: Error resetting GameSettingsManager: {e.Message}");
            }
        }

        // Reset SettingsManager
        if (SettingsManager.Instance != null)
        {
            try
            {
                SettingsManager.Instance.LoadSettings();
                Debug.Log("GameStateManager: SettingsManager reset complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameStateManager: Error resetting SettingsManager: {e.Message}");
            }
        }
    }
}
