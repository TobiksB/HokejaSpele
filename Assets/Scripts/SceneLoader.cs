using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadGameScene()
    {
        if (SelectedPlayer.playerPrefab == null)
        {
            Debug.LogError("No player prefab selected! Please customize your player first.");
            return;
        }
        SceneManager.LoadScene("GameScene"); // Replace with your actual game scene name
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu"); // Replace with your actual menu scene name
    }
}
