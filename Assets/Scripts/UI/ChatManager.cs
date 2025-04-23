using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication; // Add this for AuthenticationService

public class ChatManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private TMP_Text chatContent;
    [SerializeField] private Button sendButton;

    private List<string> chatMessages = new List<string>();
    private const int maxMessages = 50;

    private void Start()
    {
        sendButton.onClick.AddListener(SendMessage);
    }

    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(chatInputField.text)) return;

        string playerId = AuthenticationService.Instance.PlayerId; // Ensure AuthenticationService is initialized
        string message = $"[{System.DateTime.Now:HH:mm}] {playerId}: {chatInputField.text}";
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
}
