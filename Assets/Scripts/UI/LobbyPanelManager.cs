using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication; // Ensure this is included
using Unity.Netcode; // Ensure this is included

public class LobbyPanelManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Button blueTeamButton;
    [SerializeField] private Button redTeamButton;
    [SerializeField] private Button startMatchButton;

    [Header("Chat Elements")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private TMP_Text chatContent;
    [SerializeField] private Button sendButton;

    private List<string> chatMessages = new List<string>();
    private const int maxMessages = 50;

    private void Start()
    {
        // Set up button listeners
        blueTeamButton.onClick.AddListener(() => SelectTeam("Blue"));
        redTeamButton.onClick.AddListener(() => SelectTeam("Red"));
        startMatchButton.onClick.AddListener(StartMatch);
        sendButton.onClick.AddListener(SendMessage);

        // Disable Start Match button for non-hosts
        if (!IsHost())
        {
            startMatchButton.interactable = false;
        }
    }

    public void SetLobbyCode(string code)
    {
        lobbyCodeText.text = $"Lobby Code: {code}";
    }

    public void UpdatePlayerList(List<LobbyPlayerData> players)
    {
        // Clear existing player list
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        // Add players to the list
        foreach (var player in players)
        {
            GameObject item = Instantiate(playerListItemPrefab, playerListContent);
            var listItem = item.GetComponent<PlayerListItem>();
            listItem.SetPlayerInfo(player.PlayerName, player.IsBlueTeam, player.IsReady);
        }
    }

    private void SelectTeam(string team)
    {
        // Ensure AuthenticationService is initialized
        if (AuthenticationService.Instance.IsSignedIn)
        {
            LobbyManager.Instance.SetPlayerTeam(AuthenticationService.Instance.PlayerId, team);
        }
        else
        {
            Debug.LogError("AuthenticationService is not signed in.");
        }
    }

    private void StartMatch()
    {
        // Notify the server to start the match
        LobbyManager.Instance.StartMatch();
    }

    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(chatInputField.text)) return;

        string message = $"[{System.DateTime.Now:HH:mm}] {AuthenticationService.Instance.PlayerId}: {chatInputField.text}";
        AddMessage(message);

        chatInputField.text = string.Empty;

        // TODO: Sync chat messages across the network
    }

    private void AddMessage(string message)
    {
        chatMessages.Add(message);
        if (chatMessages.Count > maxMessages)
        {
            chatMessages.RemoveAt(0);
        }

        chatContent.text = string.Join("\n", chatMessages);
    }

    private bool IsHost()
    {
        // Ensure NetworkManager is properly referenced
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }
}
