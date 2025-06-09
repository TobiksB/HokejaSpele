using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button exitButton;

    private Color redColor = new Color(1f, 0.2f, 0.2f, 1f);
    private Color blueColor = new Color(0f, 0.5f, 1f, 1f);

    private void Awake()
    {
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (exitButton != null)
            exitButton.onClick.AddListener(ExitGame);

        // Ensure the panel is hidden at the start
        gameObject.SetActive(false);
    }

    public void ShowWinner(string winner)
    {
        if (winnerText != null)
            winnerText.text = winner;
        if (scoreText != null)
            scoreText.text = "";
        gameObject.SetActive(true);
    }

    // Enhanced: Show winner and score, both colored
    public void ShowGameOver(int redScore, int blueScore)
    {
        string winner;
        Color winnerColor;
        if (redScore > blueScore)
        {
            winner = "Sarkanā komanda uzvarēja!";
            winnerColor = redColor;
        }
        else if (blueScore > redScore)
        {
            winner = "Zilā komanda uzvarēja!";
            winnerColor = blueColor;
        }
        else
        {
            winner = "Neizšķirts!";
            winnerColor = Color.gray;
        }

        if (winnerText != null)
        {
            winnerText.text = winner;
            winnerText.color = winnerColor;
        }

        if (scoreText != null)
        {
            scoreText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(redColor)}>{redScore}</color> - <color=#{ColorUtility.ToHtmlStringRGB(blueColor)}>{blueScore}</color>";
            scoreText.fontSize = 48;
        }

        gameObject.SetActive(true);
    }

    private void ReturnToMainMenu()
    {
        // Shutdown networking if running
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            try
            {
                if (Unity.Netcode.NetworkManager.Singleton.IsHost || Unity.Netcode.NetworkManager.Singleton.IsServer || Unity.Netcode.NetworkManager.Singleton.IsClient)
                {
                    Unity.Netcode.NetworkManager.Singleton.Shutdown();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GameOverPanel: Error shutting down NetworkManager: {e.Message}");
            }
        }

        // Reset lobby player list and chat
        if (LobbyManager.Instance != null)
        {
            try
            {
                LobbyManager.Instance.StopAllCoroutines();
                var lobbyType = typeof(LobbyManager);
                var playerNamesField = lobbyType.GetField("playerNames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var playerTeamsField = lobbyType.GetField("playerTeams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var playerReadyStatesField = lobbyType.GetField("playerReadyStates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var chatMessagesField = lobbyType.GetField("chatMessages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentLobbyField = lobbyType.GetField("currentLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (playerNamesField != null)
                {
                    var dict = playerNamesField.GetValue(LobbyManager.Instance) as System.Collections.IDictionary;
                    dict?.Clear();
                }
                if (playerTeamsField != null)
                {
                    var dict = playerTeamsField.GetValue(LobbyManager.Instance) as System.Collections.IDictionary;
                    dict?.Clear();
                }
                if (playerReadyStatesField != null)
                {
                    var dict = playerReadyStatesField.GetValue(LobbyManager.Instance) as System.Collections.IDictionary;
                    dict?.Clear();
                }
                if (chatMessagesField != null)
                {
                    var chatList = chatMessagesField.GetValue(LobbyManager.Instance) as System.Collections.IList;
                    chatList?.Clear();
                }
                if (currentLobbyField != null)
                {
                    currentLobbyField.SetValue(LobbyManager.Instance, null);
                }
                LobbyManager.Instance.ResetLobbyState();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GameOverPanel: Error resetting lobby player list or chat: {e.Message}");
            }
        }

        // Find and destroy GameSettingsManager to prevent duplicates when restarting
        var gameSettingsManager = FindObjectOfType<GameSettingsManager>();
        if (gameSettingsManager != null)
        {
            Debug.Log("GameOverPanel: Destroying GameSettingsManager before returning to MainMenu");
            Destroy(gameSettingsManager.gameObject);
        }
        else
        {
            Debug.Log("GameOverPanel: No GameSettingsManager found to destroy");
        }

        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void ExitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
