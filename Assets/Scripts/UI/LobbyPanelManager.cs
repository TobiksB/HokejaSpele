using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Netcode;

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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("LobbyPanelManager initialized with references - " +
                $"Content: {(playerListContent != null ? "Valid" : "Missing")}, " +
                $"Prefab: {(playerListItemPrefab != null ? "Valid" : "Missing")}");
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Multiple LobbyPanelManager instances found. Destroying duplicate.");
            Destroy(gameObject);
        }

        VerifyReferences();
    }

    private void VerifyReferences()
    {
        if (playerListContent == null)
        {
            Debug.LogError("playerListContent reference is missing! Please assign in Inspector.");
            // Try to find it in children if not assigned
            playerListContent = GetComponentInChildren<ScrollRect>()?.content as RectTransform;
            if (playerListContent != null)
                Debug.Log("Found playerListContent in children.");
        }

        if (playerListItemPrefab == null)
        {
            Debug.LogError("playerListItemPrefab reference is missing! Please assign in Inspector.");
            // Try to load from Resources if available
            playerListItemPrefab = Resources.Load<PlayerListItem>("Prefabs/UI/PlayerListItem");
            if (playerListItemPrefab != null)
                Debug.Log("Loaded playerListItemPrefab from Resources.");
        }
    }

    private void OnEnable()
    {
        SetupScrollViews();
        SetupButtons();
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
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            if (LobbyManager.Instance != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                LobbyManager.Instance.SetPlayerTeam(playerId, team);

                // Update visual indicators
                if (blueTeamIndicator != null && redTeamIndicator != null)
                {
                    blueTeamIndicator.gameObject.SetActive(team == "Blue");
                    redTeamIndicator.gameObject.SetActive(team == "Red");
                }

                // Force UI refresh
                LobbyManager.Instance.RefreshPlayerList();
                
                Debug.Log($"Selected team: {team}");
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

    public void UpdatePlayerList(List<LobbyPlayerData> players)
    {
        if (!playerListContent || !playerListItemPrefab)
        {
            Debug.LogError($"Cannot update player list - missing references");
            return;
        }

        // Clear existing items
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        // Add player items
        foreach (var player in players)
        {
            var item = Instantiate(playerListItemPrefab, playerListContent);
            if (item)
            {
                // Set RectTransform settings for the item
                RectTransform itemRT = item.GetComponent<RectTransform>();
                itemRT.anchorMin = new Vector2(0, 0);
                itemRT.anchorMax = new Vector2(1, 0);
                itemRT.sizeDelta = new Vector2(0, 50); // Fixed height
                
                Debug.Log($"Creating player item: {player.PlayerName}");
                item.SetPlayerInfo(player.PlayerName, player.IsBlueTeam, player.IsReady);
            }
        }

        // Force layout update
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContent);

        if (playerListScrollRect)
        {
            playerListScrollRect.normalizedPosition = Vector2.one;
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
        
        // Send message through LobbyManager instead of adding directly
        LobbyManager.Instance.SendChatMessage(message);
        
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
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            if (LobbyManager.Instance != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                LobbyManager.Instance.SetPlayerReady(playerId);
                
                // Update ready indicator
                if (readyIndicator != null)
                {
                    bool isReady = LobbyManager.Instance.IsPlayerReady(playerId);
                    readyIndicator.color = isReady ? Color.green : Color.gray;
                }
            }
        }
    }

    public void UpdateStartButton(bool canStart)
    {
        if (startMatchButton != null)
        {
            startMatchButton.interactable = IsHost() && canStart;
            Color buttonColor = canStart ? Color.green : Color.gray;
            ColorBlock colors = startMatchButton.colors;
            colors.normalColor = buttonColor;
            startMatchButton.colors = colors;
        }
    }
}
