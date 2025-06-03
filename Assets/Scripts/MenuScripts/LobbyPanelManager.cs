using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Netcode;

// This file is intentionally removed to resolve duplicate manager conflicts
// Use HockeyGame.UI.LobbyPanelManager instead
public class LobbyPanelManager : MonoBehaviour
{
    public static LobbyPanelManager Instance { get; private set; }

    [Header("Panel References")]
    [SerializeField] private RectTransform mainPanel;

    [Header("Lobby Code")]
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Button copyCodeButton;

    [Header("Player List")]
    [SerializeField] private ScrollRect playerListScrollRect;
    [SerializeField] private RectTransform playerListContent;
    [SerializeField] private PlayerListItem playerListItemPrefab;

    [Header("Team Selection")]
    [SerializeField] private Button blueTeamButton;
    [SerializeField] private Button redTeamButton;
    [SerializeField] private Image blueTeamIndicator;
    [SerializeField] private Image redTeamIndicator;

    [Header("Chat")]
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private RectTransform chatContent;
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private TMP_Text chatText;

    [Header("Game Control")]
    [SerializeField] private Button startMatchButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Image readyIndicator;

    [Header("Debug")]
    [SerializeField] private Button debugStartButton;

    private void Awake()
    {
        Debug.Log($"MenuScripts.LobbyPanelManager.Awake on {gameObject.name}");
        if (Instance == null)
        {
            Instance = this;
            VerifyReferences();
            Debug.Log($"MenuScripts.LobbyPanelManager set as Instance");
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"Multiple MenuScripts.LobbyPanelManager instances found. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    private void VerifyReferences()
    {
        if (playerListContent == null)
        {
            Debug.LogError("MenuScripts.LobbyPanelManager: playerListContent reference is missing!");
            playerListContent = GetComponentInChildren<ScrollRect>()?.content as RectTransform;
            if (playerListContent != null)
                Debug.Log("MenuScripts.LobbyPanelManager: Found playerListContent in children.");
        }

        if (playerListItemPrefab == null)
        {
            Debug.LogError("MenuScripts.LobbyPanelManager: playerListItemPrefab reference is missing!");
            // Look for the UI version directly in the scene first
            var uiPrefab = Object.FindFirstObjectByType<PlayerListItem>();
            if (uiPrefab != null)
            {
                playerListItemPrefab = uiPrefab;
                Debug.Log("MenuScripts.LobbyPanelManager: Found PlayerListItem in scene.");
            }
            else
            {
                // Try to load from Resources
                playerListItemPrefab = Resources.Load<PlayerListItem>("Prefabs/UI/PlayerListItem");
                if (playerListItemPrefab != null)
                    Debug.Log("MenuScripts.LobbyPanelManager: Loaded PlayerListItem from Resources.");
            }
        }

        Debug.Log($"MenuScripts.LobbyPanelManager: References verified - Content: {playerListContent != null}, Prefab: {playerListItemPrefab != null}");
    }

    private void OnEnable()
    {
        Debug.Log($"MenuScripts.LobbyPanelManager.OnEnable on {gameObject.name}");
        
        // Always set this instance as the active one when enabled
        Instance = this;
        
        SetupScrollViews();
        SetupButtons();
        
        // Notify any code waiting for an active lobby panel
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RefreshPlayerList();
        }
    }

    private void OnDisable()
    {
        Debug.Log($"MenuScripts.LobbyPanelManager.OnDisable on {gameObject.name}");
        
        // If this instance is being disabled and it's the current Instance,
        // look for another active instance to take over
        if (Instance == this)
        {
            var otherManagers = FindObjectsByType<LobbyPanelManager>(FindObjectsSortMode.None);
            foreach (var manager in otherManagers)
            {
                if (manager != this && manager.gameObject.activeInHierarchy)
                {
                    Instance = manager;
                    Debug.Log($"MenuScripts.LobbyPanelManager Instance switched to {manager.gameObject.name}");
                    break;
                }
            }
        }
    }

    private void SetupButtons()
    {
        if (copyCodeButton) copyCodeButton.onClick.AddListener(CopyLobbyCode);
        if (blueTeamButton) blueTeamButton.onClick.AddListener(() => OnTeamSelect("Blue"));
        if (redTeamButton) redTeamButton.onClick.AddListener(() => OnTeamSelect("Red"));
        if (startMatchButton) startMatchButton.onClick.AddListener(OnStartMatch);
        if (readyButton) readyButton.onClick.AddListener(OnReadySelect);
        if (sendButton) sendButton.onClick.AddListener(SendMessage);

        if (startMatchButton && !IsHost())
        {
            startMatchButton.interactable = false;
        }

        if (debugStartButton)
        {
            #if UNITY_EDITOR
            debugStartButton.gameObject.SetActive(true);
            debugStartButton.onClick.AddListener(() => LobbyManager.Instance.ForceStartMatch());
            #else
            debugStartButton.gameObject.SetActive(false);
            #endif
        }
    }

    public void UpdatePlayerList(List<LobbyPlayerData> players)
    {
        Debug.Log($"MenuScripts.LobbyPanelManager: UpdatePlayerList called with {players?.Count ?? 0} players");
        
        if (!playerListContent)
        {
            Debug.LogError("MenuScripts.LobbyPanelManager: playerListContent is null!");
            VerifyReferences();
            if (!playerListContent) return;
        }
        
        if (!playerListItemPrefab)
        {
            Debug.LogError("MenuScripts.LobbyPanelManager: playerListItemPrefab is null!");
            VerifyReferences();
            if (!playerListItemPrefab) return;
        }

        // Clear existing items
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("MenuScripts.LobbyPanelManager: No players to display in list");
            return;
        }

        // Add player items
        foreach (var player in players)
        {
            Debug.Log($"MenuScripts.LobbyPanelManager: Creating UI item for player: {player.PlayerName}");
            try
            {
                var item = Instantiate(playerListItemPrefab, playerListContent);
                if (item)
                {
                    // Set RectTransform settings for the item
                    RectTransform itemRT = item.GetComponent<RectTransform>();
                    itemRT.anchorMin = new Vector2(0, 0);
                    itemRT.anchorMax = new Vector2(1, 0);
                    itemRT.sizeDelta = new Vector2(0, 50); // Fixed height
                    
                    item.SetPlayerInfo(player.PlayerName, player.IsBlueTeam, player.IsReady);
                    Debug.Log($"MenuScripts.LobbyPanelManager: ✓ Successfully created item for {player.PlayerName}");
                }
                else
                {
                    Debug.LogError("MenuScripts.LobbyPanelManager: ✗ Failed to instantiate player list item");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MenuScripts.LobbyPanelManager: ✗ Error creating player item: {e.Message}");
            }
        }

        // Force layout update
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContent);

        Debug.Log($"MenuScripts.LobbyPanelManager: ✓ Player list updated successfully with {players.Count} items");
    }

    public void SetLobbyCode(string code)
    {
        Debug.Log($"SetLobbyCode called with: {code}");
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = $"Lobby Code: {code}";
            Debug.Log($"Lobby code UI updated to: {lobbyCodeText.text}");
        }
        else
        {
            Debug.LogError("lobbyCodeText is null! Cannot display lobby code.");
        }
    }

    private void OnTeamSelect(string team)
    {
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            if (LobbyManager.Instance != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                LobbyManager.Instance.SetPlayerTeam(playerId, team);

                // Update visual indicators with better feedback
                if (blueTeamIndicator != null && redTeamIndicator != null)
                {
                    blueTeamIndicator.gameObject.SetActive(team == "Blue");
                    redTeamIndicator.gameObject.SetActive(team == "Red");
                    
                    // Add color feedback
                    if (team == "Blue")
                    {
                        blueTeamIndicator.color = new Color(0.2f, 0.4f, 1f, 1f); // Bright blue
                    }
                    else
                    {
                        redTeamIndicator.color = new Color(1f, 0.2f, 0.2f, 1f); // Bright red
                    }
                }

                // Force UI refresh
                LobbyManager.Instance.RefreshPlayerList();
                
                Debug.Log($"Selected team: {team} for 2v2 game - Need 4 players (2 per team) or minimum 2 for testing!");
            }
            else
            {
                Debug.LogError("LobbyManager instance is null!");
            }
        }
    }

    private void OnStartMatch()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.StartMatch();
            Debug.Log("Starting match...");
        }
        else
        {
            Debug.LogError("LobbyManager instance is null!");
        }
    }

    private void SetupScrollViews()
    {
        // Setup player list scroll view
        if (playerListContent)
        {
            // Set RectTransform anchors and sizing
            RectTransform rt = playerListContent.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var vlg = playerListContent.GetComponent<VerticalLayoutGroup>();
            if (!vlg) vlg = playerListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5f;
            vlg.padding = new RectOffset(10, 10, 10, 10); // Increased padding
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = playerListContent.GetComponent<ContentSizeFitter>();
            if (!csf) csf = playerListContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        // Setup chat content
        if (chatContent)
        {
            var vlg = chatContent.GetComponent<VerticalLayoutGroup>();
            if (!vlg) vlg = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var csf = chatContent.GetComponent<ContentSizeFitter>();
            if (!csf) csf = chatContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(chatInput.text)) return;

        string playerName = SettingsManager.Instance != null ? 
            SettingsManager.Instance.PlayerName : 
            "Player";

        bool isBlueTeam = false;
        if (AuthenticationService.Instance != null && LobbyManager.Instance != null)
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            isBlueTeam = LobbyManager.Instance.IsPlayerBlueTeam(playerId);
        }

        string coloredName = isBlueTeam ? 
            $"<color=#4080FF>{playerName}</color>" : 
            $"<color=#FF4040>{playerName}</color>";
            
        string message = $"{coloredName}: {chatInput.text}";
        
        // Send message through LobbyManager
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.AddChatMessage(message);
        }
        
        chatInput.text = string.Empty;
        chatInput.ActivateInputField();
    }

    public void ClearChat()
    {
        if (chatText != null)
        {
            chatText.text = string.Empty;
        }
    }

    public void AddChatMessage(string message)
    {
        if (chatText && !string.IsNullOrEmpty(message))
        {
            chatText.text += $"{message}\n";
            
            Canvas.ForceUpdateCanvases();
            if (chatScrollRect)
            {
                chatScrollRect.verticalNormalizedPosition = 0f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent as RectTransform);
            }
        }
    }

    private bool IsHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }

    private void CopyLobbyCode()
    {
        if (lobbyCodeText != null)
        {
            string code = lobbyCodeText.text.Replace("Lobby Code: ", "").Trim();
            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"Copied lobby code: {code} to clipboard");
        }
        else
        {
            Debug.LogError("Lobby code text component is missing!");
        }
    }

    private void OnReadySelect()
    {
        Debug.Log("Ready button pressed");
        
        // CRITICAL: Ensure Time.timeScale is normal
        if (Time.timeScale != 1)
        {
            Debug.LogWarning($"Time.timeScale was {Time.timeScale}, resetting to 1");
            Time.timeScale = 1;
        }
        
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            if (LobbyManager.Instance != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                LobbyManager.Instance.SetPlayerReady(playerId);
                
                // Update ready indicator with better visual feedback
                if (readyIndicator != null)
                {
                    bool isReady = LobbyManager.Instance.IsPlayerReady(playerId);
                    readyIndicator.color = isReady ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    
                    // Update ready button text
                    var buttonText = readyButton?.GetComponentInChildren<TMPro.TMP_Text>();
                    if (buttonText != null)
                    {
                        buttonText.text = isReady ? "READY!" : "READY";
                        buttonText.color = isReady ? Color.green : Color.white;
                    }
                }
                
                Debug.Log($"Player ready state: {LobbyManager.Instance.IsPlayerReady(playerId)}");
            }
        }
    }
    
    public void UpdateStartButton(bool canStart)
    {
        if (startMatchButton != null)
        {
            startMatchButton.interactable = IsHost() && canStart;
            
            // Better visual feedback for start button
            var buttonText = startMatchButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (buttonText != null)
            {
                if (canStart && IsHost())
                {
                    buttonText.text = "START MATCH (TESTING)";
                    buttonText.color = Color.green;
                }
                else if (!IsHost())
                {
                    buttonText.text = "WAITING FOR HOST...";
                    buttonText.color = Color.gray;
                }
                else
                {
                    buttonText.text = "PICK TEAM & READY UP";
                    buttonText.color = Color.yellow;
                }
            }
            
            // Update button colors
            ColorBlock colors = startMatchButton.colors;
            colors.normalColor = canStart ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.highlightedColor = canStart ? new Color(0.3f, 1f, 0.3f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
            startMatchButton.colors = colors;
        }
    }

    public void ForceInitialize()
    {
        Debug.Log("MenuScripts.LobbyPanelManager: ForceInitialize called");
        
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("MenuScripts.LobbyPanelManager: Set as singleton instance");
        }
        
        VerifyReferences();
        SetupScrollViews();
        SetupButtons();
        
        Debug.Log("MenuScripts.LobbyPanelManager: Force initialization completed");
    }

    public void InitializeManager()
    {
        ForceInitialize();
    }
}