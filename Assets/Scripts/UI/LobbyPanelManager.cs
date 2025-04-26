using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Netcode;

[DisallowMultipleComponent]
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
    [SerializeField] private float playerListSpacing = 5f;
    [SerializeField] private RectOffset playerListPadding;

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
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Image readyIndicator;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Initialize RectOffset here
        playerListPadding = new RectOffset(5, 5, 5, 5);
        
        SetupLayouts();

        // Set up button listeners
        if (copyCodeButton) copyCodeButton.onClick.AddListener(CopyLobbyCode);
        if (blueTeamButton) blueTeamButton.onClick.AddListener(() => SelectTeam("Blue"));
        if (redTeamButton) redTeamButton.onClick.AddListener(() => SelectTeam("Red"));
        if (startGameButton) startGameButton.onClick.AddListener(StartMatch);
        if (sendButton) sendButton.onClick.AddListener(SendMessage);

        // Disable Start Match button for non-hosts
        if (!IsHost())
        {
            startGameButton.interactable = false;
        }
    }

    private void SetupLayouts()
    {
        if (playerListContent != null)
        {
            var vlg = playerListContent.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = playerListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = playerListSpacing;
            vlg.padding = playerListPadding;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
        }

        if (chatContent != null)
        {
            var vlg = chatContent.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
        }
    }

    public void SetLobbyCode(string code)
    {
        Debug.Log($"Setting lobby code: {code}");
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = $"Lobby Code: {code}";
        }
        else
        {
            Debug.LogError("lobbyCodeText is not assigned in the Inspector.");
        }
    }

    private void CopyLobbyCode()
    {
        if (lobbyCodeText != null && !string.IsNullOrEmpty(lobbyCodeText.text))
        {
            string code = lobbyCodeText.text.Replace("Lobby Code: ", "").Trim();
            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"Copied lobby code: {code}");
        }
    }

    public void UpdatePlayerList(List<LobbyPlayerData> players)
    {
        Debug.Log($"Updating player list with {players.Count} players");

        // Clear existing player list
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        // Add players to the list
        foreach (var player in players)
        {
            Debug.Log($"Adding player to list: {player.PlayerName}");
            PlayerListItem item = Instantiate(playerListItemPrefab, playerListContent);
            item.SetPlayerInfo(player.PlayerName, player.IsBlueTeam, player.IsReady);
        }

        // Force layout refresh
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContent);
        Canvas.ForceUpdateCanvases();
        if (playerListScrollRect != null)
        {
            playerListScrollRect.normalizedPosition = Vector2.one;
        }
    }

    private void SelectTeam(string team)
    {
        // Ensure AuthenticationService is initialized
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            var lobbyManager = FindFirstObjectByType<LobbyManager>();
            if (lobbyManager != null)
            {
                lobbyManager.SetPlayerTeam(AuthenticationService.Instance.PlayerId, team);
            }
            else
            {
                Debug.LogError("LobbyManager not found in the scene.");
            }
        }
        else
        {
            Debug.LogError("AuthenticationService is not signed in.");
        }
    }

    private void StartMatch()
    {
        var lobbyManager = FindFirstObjectByType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.StartMatch();
        }
        else
        {
            Debug.LogError("LobbyManager not found in the scene.");
        }
    }

    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(chatInput.text)) return;

        string message = $"[{System.DateTime.Now:HH:mm}] {AuthenticationService.Instance.PlayerId}: {chatInput.text}";
        AddMessage(message);

        chatInput.text = string.Empty;
        chatInput.ActivateInputField();
    }

    private void AddMessage(string message)
    {
        if (chatText != null)
        {
            chatText.text += message + "\n";

            // Scroll to bottom
            Canvas.ForceUpdateCanvases();
            if (chatScrollRect != null)
            {
                chatScrollRect.normalizedPosition = Vector2.zero;
            }
        }
    }

    private bool IsHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }
}
