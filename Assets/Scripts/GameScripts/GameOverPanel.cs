using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button exitButton;

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
        gameObject.SetActive(true);
    }

    // Add this method for compatibility with GameManager and RootGameManager
    public void ShowGameOver(int redScore, int blueScore)
    {
        string winner;
        if (redScore > blueScore)
            winner = "Red Team Wins!";
        else if (blueScore > redScore)
            winner = "Blue Team Wins!";
        else
            winner = "Draw!";

        ShowWinner(winner);
    }

    private void ReturnToMainMenu()
    {
        // Shutdown networking if running
        if (Unity.Netcode.NetworkManager.Singleton != null)
            Unity.Netcode.NetworkManager.Singleton.Shutdown();

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
