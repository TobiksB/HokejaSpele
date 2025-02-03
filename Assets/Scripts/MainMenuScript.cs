using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuScript : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject playerCreationPanel;
    [SerializeField] private string playSceneName = "PlayScene"; // Set this in inspector
    [SerializeField] private PlayerCreation playerCreation; // Add reference to PlayerCreation

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Hide panels on start
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (playerCreationPanel != null) playerCreationPanel.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            if (playerCreationPanel != null) playerCreationPanel.SetActive(false);
        }
    }

    public void OpenPlayerCreation()
    {
        if (playerCreationPanel != null)
        {
            playerCreationPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void ClosePlayerCreationPanel()
    {
        if (playerCreationPanel != null) playerCreationPanel.SetActive(false);
    }

    /*
    public void CloseCurrentPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (playerCreationPanel != null) playerCreationPanel.SetActive(false);
    }
    */

    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    public void StartGame()
    {
        if (playerCreation != null)
        {
            playerCreation.SavePlayerColors();
            
            if (PlayerData.Instance != null)
            {
                PlayerData.Instance.ValidateData();
                if (PlayerData.Instance.playerPrefab != null)
                {
                    SceneManager.LoadScene(playSceneName);
                }
                else
                {
                    Debug.LogError("Cannot start game: Player prefab not assigned!");
                }
            }
            else
            {
                Debug.LogError("Cannot start game: PlayerData instance not found!");
            }
        }
    }
}
