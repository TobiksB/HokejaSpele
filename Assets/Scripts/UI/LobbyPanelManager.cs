using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Netcode; // Add this for networking
using System.Linq; // Add this for FirstOrDefault
using Unity.Collections; // Add this for FixedString512Bytes

namespace HockeyGame.UI
{
    // Make LobbyPanelManager a NetworkBehaviour so it can own a NetworkList and sync across network
    public class LobbyPanelManager : NetworkBehaviour
    {
        // IMPROVED: Make static Instance property more resilient
        private static LobbyPanelManager _instance;
        public static LobbyPanelManager Instance 
        { 
            get 
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<LobbyPanelManager>();
                    if (_instance == null)
                    {
                        // Try to find any lobby panel to attach to
                        var lobbyPanel = FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                            .FirstOrDefault(go => go.name.ToLower().Contains("lobby") && 
                                            go.name.ToLower().Contains("panel"));
                        if (lobbyPanel != null)
                        {
                            Debug.Log($"Creating LobbyPanelManager on existing panel: {lobbyPanel.name}");
                            _instance = lobbyPanel.AddComponent<LobbyPanelManager>();
                            _instance.ForceInitialize();
                        }
                    }
                }
                return _instance;
            }
            set
            {
                _instance = value;
            }
        }

        [Header("Panel References")]
        [SerializeField] private RectTransform mainPanel;

        [Header("Lobby Code")]
        [SerializeField] private TMP_Text lobbyCodeText;
        [SerializeField] private Button copyCodeButton;

        [Header("Player List")]
        [SerializeField] private ScrollRect playerListScrollRect;
        [SerializeField] private RectTransform playerListContent;
        [SerializeField] private PlayerListItem playerListItemPrefab; // Remove UI.prefix

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

        // --- REMOVE: NetworkList for chat messages ---
        // private NetworkList<FixedString512Bytes> networkChatMessages;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Subscribe to changes (all clients, including host)
            // Remove all manual NetworkList creation from Awake
            VerifyReferences();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
        }

        private void Awake()
        {
            Debug.Log($"UI.LobbyPanelManager.Awake on {gameObject.name}, already have instance: {_instance != null}");
            if (_instance == null)
            {
                _instance = this;
                VerifyReferences();
                Debug.Log($"UI.LobbyPanelManager set as Instance, gameObject: {gameObject.name}, active: {gameObject.activeInHierarchy}");
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"Multiple UI.LobbyPanelManager instances found. Destroying duplicate ({gameObject.name}).");
                Destroy(gameObject);
                return;
            }

            // Remove all manual NetworkList creation from Awake
            VerifyReferences();
        }
        
        private void OnEnable()
        {
            Debug.Log($"UI.LobbyPanelManager.OnEnable on {gameObject.name}");
            
            // Ensure this is the active instance when enabled
            if (_instance != this)
            {
                _instance = this;
                Debug.Log($"UI.LobbyPanelManager.OnEnable reassigned instance to {gameObject.name}");
            }
            
            SetupScrollViews();
            SetupButtons();
            
            // FIXED: Don't immediately refresh - wait for lobby creation to complete
            // Only refresh if LobbyManager already has players
            if (LobbyManager.Instance != null)
            {
                // Check if lobby manager actually has player data before refreshing
                var hasPlayers = LobbyManager.Instance.GetType().GetField("playerNames", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hasPlayers != null)
                {
                    var playerNamesDict = hasPlayers.GetValue(LobbyManager.Instance) as Dictionary<string, string>;
                    if (playerNamesDict != null && playerNamesDict.Count > 0)
                    {
                        Debug.Log($"UI.LobbyPanelManager.OnEnable: Found {playerNamesDict.Count} existing players, refreshing...");
                        LobbyManager.Instance.RefreshPlayerList();
                    }
                    else
                    {
                        Debug.Log("UI.LobbyPanelManager.OnEnable: No existing players found, skipping refresh");
                    }
                }
            }

            // Subscribe to chat updates
            if (HockeyGame.UI.LobbyChatManager.Instance != null)
            {
                HockeyGame.UI.LobbyChatManager.Instance.OnChatUpdated -= OnChatUpdated;
                HockeyGame.UI.LobbyChatManager.Instance.OnChatUpdated += OnChatUpdated;
                // Initial sync
                OnChatUpdated(HockeyGame.UI.LobbyChatManager.Instance.GetAllMessages());
            }
        }
        
        private void OnDisable()
        {
            if (HockeyGame.UI.LobbyChatManager.Instance != null)
            {
                HockeyGame.UI.LobbyChatManager.Instance.OnChatUpdated -= OnChatUpdated;
            }
        }

        public void OnDestroy()
        {
            if (HockeyGame.UI.LobbyChatManager.Instance != null)
            {
                HockeyGame.UI.LobbyChatManager.Instance.OnChatUpdated -= OnChatUpdated;
            }
        }

        public void UpdatePlayerList(List<LobbyPlayerData> players)
        {
            Debug.Log($"UI.LobbyPanelManager: UpdatePlayerList called with {players?.Count ?? 0} players");
            
            // Try to find references if they're missing
            if (!playerListContent || !playerListItemPrefab)
            {
                Debug.LogWarning("UI.LobbyPanelManager: Missing references for player list - attempting to find them");
                VerifyReferences();
            }
            
            if (!playerListContent)
            {
                Debug.LogError("UI.LobbyPanelManager: playerListContent is still null after verification!");
                // Try to find any ScrollRect to use
                var scrollRect = GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null && scrollRect.content != null)
                {
                    playerListContent = scrollRect.content;
                    Debug.Log("UI.LobbyPanelManager: Found content in child ScrollRect");
                }
                else
                {
                    return; // Cannot proceed without content
                }
            }
            
            if (!playerListItemPrefab)
            {
                Debug.LogError("UI.LobbyPanelManager: playerListItemPrefab is still null after verification!");
                // Try to find PlayerListItem in project resources
                playerListItemPrefab = Resources.Load<PlayerListItem>("Prefabs/UI/PlayerListItem");
                
                if (!playerListItemPrefab)
                {
                    // Try creating a basic one
                    GameObject tempObj = new GameObject("TempPlayerListItem");
                    playerListItemPrefab = tempObj.AddComponent<PlayerListItem>();
                    Debug.Log("UI.LobbyPanelManager: Created temporary PlayerListItem");
                }
            }

            try
            {
                // Clear existing items
                foreach (Transform child in playerListContent)
                {
                    Destroy(child.gameObject);
                }

                if (players == null || players.Count == 0)
                {
                    Debug.LogWarning("UI.LobbyPanelManager: No players to display");
                    return;
                }

                // Add player items
                foreach (var player in players)
                {
                    Debug.Log($"UI.LobbyPanelManager: Creating item for {player.PlayerName}");
                    try
                    {
                        var item = Instantiate(playerListItemPrefab, playerListContent);
                        if (item)
                        {
                            RectTransform itemRT = item.GetComponent<RectTransform>();
                            itemRT.anchorMin = new Vector2(0, 0);
                            itemRT.anchorMax = new Vector2(1, 0);
                            itemRT.sizeDelta = new Vector2(0, 50);
                            
                            item.SetPlayerInfo(player.PlayerName, player.IsBlueTeam, player.IsReady);
                            Debug.Log($"UI.LobbyPanelManager: ✓ Successfully created item for {player.PlayerName}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"UI.LobbyPanelManager: ✗ Error creating item: {e.Message}");
                    }
                }

                // Force layout update
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContent);

                Debug.Log($"UI.LobbyPanelManager: ✓ Successfully updated player list with {players.Count} items");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in UI.LobbyPanelManager.UpdatePlayerList: {e.Message}");
                Debug.LogError($"Stack trace: {e.StackTrace}");
            }
        }

        private void VerifyReferences()
        {
            if (playerListContent == null)
            {
                Debug.LogError("UI.LobbyPanelManager: playerListContent is missing!");
                playerListContent = GetComponentInChildren<ScrollRect>()?.content as RectTransform;
            }

            if (playerListItemPrefab == null)
            {
                Debug.LogError("UI.LobbyPanelManager: playerListItemPrefab is missing!");
                // Look for the PlayerListItem in the scene or resources
                var foundPrefab = Object.FindFirstObjectByType<PlayerListItem>();
                if (foundPrefab != null)
                {
                    playerListItemPrefab = foundPrefab;
                    Debug.Log("UI.LobbyPanelManager: Found PlayerListItem in scene.");
                }
                else
                {
                    playerListItemPrefab = Resources.Load<PlayerListItem>("Prefabs/UI/PlayerListItem");
                    if (playerListItemPrefab != null)
                        Debug.Log("UI.LobbyPanelManager: Loaded PlayerListItem from Resources.");
                }
            }

            Debug.Log($"UI.LobbyPanelManager: References - Content: {playerListContent != null}, Prefab: {playerListItemPrefab != null}");
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
        }

        public void SetLobbyCode(string code)
        {
            if (lobbyCodeText != null)
            {
                lobbyCodeText.text = $"Lobby Code: {code}";
                Debug.Log($"Set lobby code to: {code}");
            }
        }

        private void OnTeamSelect(string team)
        {
            if (!ValidateManagerState()) return;

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

        // --- Networked chat send via LobbyChatManager ---
        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(chatInput.text)) return;

            string playerName = SettingsManager.Instance != null ? 
                SettingsManager.Instance.PlayerName : 
                "Player";

            bool isBlueTeam = false;
            if (Unity.Services.Authentication.AuthenticationService.Instance != null && LobbyManager.Instance != null)
            {
                string playerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                isBlueTeam = LobbyManager.Instance.IsPlayerBlueTeam(playerId);
            }

            string coloredName = isBlueTeam ? 
                $"<color=#4080FF>{playerName}</color>" : 
                $"<color=#FF4040>{playerName}</color>";
            string message = $"{coloredName}: {chatInput.text}";

            // --- FIXED: Only send to networked chat manager, remove local UI update ---
            if (HockeyGame.UI.LobbyChatManager.Instance != null)
            {
                HockeyGame.UI.LobbyChatManager.Instance.SendChat(message);
                Debug.Log($"LobbyPanelManager: Sent message to LobbyChatManager: {message}");
            }
            else
            {
                Debug.LogError("LobbyPanelManager: LobbyChatManager.Instance is null!");
            }

            chatInput.text = string.Empty;
            chatInput.ActivateInputField();
        }

        // --- UI update callback for chat ---
        private void OnChatUpdated(List<string> messages)
        {
            Debug.Log($"LobbyPanelManager: OnChatUpdated called with {messages?.Count ?? 0} messages");
            
            if (chatText != null && messages != null)
            {
                chatText.text = "";
                foreach (var msg in messages)
                {
                    chatText.text += msg + "\n";
                }
                Canvas.ForceUpdateCanvases();
                if (chatScrollRect)
                {
                    chatScrollRect.verticalNormalizedPosition = 0f;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent as RectTransform);
                }
                
                Debug.Log($"LobbyPanelManager: Updated chat UI with {messages.Count} messages");
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
            if (!ValidateManagerState()) return;

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
                        readyIndicator.color = isReady ? new Color(0.2f, 1f, 0.2f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                        
                        // Update ready button text if it has a text component - FIXED: Remove checkmark
                        var buttonText = readyButton?.GetComponentInChildren<TMPro.TMP_Text>();
                        if (buttonText != null)
                        {
                            buttonText.text = isReady ? "READY!" : "READY";
                            buttonText.color = isReady ? Color.green : Color.white;
                        }
                    }
                    
                    Debug.Log($"Player ready state: {LobbyManager.Instance.IsPlayerReady(playerId)} - 2v2 needs 4 players (2 per team) or minimum 2 for testing!");
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

        private bool ValidateManagerState()
        {
            if (Instance == null)
            {
                Debug.LogError("UI.LobbyPanelManager: Instance is null!");
                return false;
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("UI.LobbyPanelManager: GameObject is not active!");
                return false;
            }

            return true;
        }

        public static void EnsureInstance()
        {
            if (Instance == null)
            {
                var existingManager = Object.FindFirstObjectByType<LobbyPanelManager>();
                if (existingManager != null)
                {
                    Instance = existingManager;
                    Debug.Log("Found existing LobbyPanelManager");
                }
                else
                {
                    Debug.LogError("No LobbyPanelManager found in scene!");
                }
            }
        }

        private void ValidateUIComponents()
        {
            if (!lobbyCodeText)
                Debug.LogError("Lobby code text is missing!");
            if (!playerListContent)
                Debug.LogError("Player list content is missing!");
            if (!playerListScrollRect)
                Debug.LogError("Player list scroll rect is missing!");
            if (!chatContent)
                Debug.LogError("Chat content is missing!");
            if (!chatScrollRect)
                Debug.LogError("Chat scroll rect is missing!");
            if (!chatInput)
                Debug.LogError("Chat input is missing!");
            if (!blueTeamButton)
                Debug.LogError("Blue team button is missing!");
            if (!redTeamButton)
                Debug.LogError("Red team button is missing!");
            if (!startMatchButton)
                Debug.LogError("Start match button is missing!");
            if (!readyButton)
                Debug.LogError("Ready button is missing!");
        }

        // Add this method to fix missing 'ForceInitialize' errors
        public void ForceInitialize()
        {
            Debug.Log("LobbyPanelManager.ForceInitialize called");
            // Optionally, re-run initialization logic if needed
            VerifyReferences();
            SetupScrollViews();
            SetupButtons();
        }
    }
}
