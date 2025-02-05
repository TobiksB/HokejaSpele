using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuScript : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Animator mainMenuAnimator;
    [SerializeField] private GameObject playerCreationPanel;
    [SerializeField] private string playSceneName = "PlayScene"; // Set this in inspector
    [SerializeField] private PlayerCreation playerCreation; // Add reference to PlayerCreation
    [SerializeField] private GameObject playerPrefabPreview;
    
    



    private bool hasPlayedAnimation = false; // Flag to track if animation has been played

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Hide panels on start
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (playerCreationPanel != null) playerCreationPanel.SetActive(false);

        if (mainMenuPanel == null)
        {
            Debug.LogError("Main menu panel reference missing!");
            return;
        }

        // Play the animation if it hasn't been played yet
        if (!hasPlayedAnimation)
        {
            mainMenuAnimator = mainMenuPanel.GetComponent<Animator>();
            if (mainMenuAnimator != null)
            {
                mainMenuAnimator.Play("MainMenuPanelIntro");
                hasPlayedAnimation = true; // Set the flag to true after playing the animation
            }
        }
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

              if (playerPrefabPreview != null)
            {
                playerPrefabPreview.SetActive(false);
            }

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

         if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            if (playerPrefabPreview != null)
            {
                playerPrefabPreview.SetActive(true);
            }
        }
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
