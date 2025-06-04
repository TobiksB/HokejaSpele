using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // ADDED: For Take method

namespace HockeyGame.UI
{
    public class LobbyChatManager : MonoBehaviour
    {
        public static LobbyChatManager Instance { get; private set; }
        
        [Header("Chat Settings")]
        [SerializeField] private int maxMessages = 10;
        [SerializeField] private float pollInterval = 15f;
        [SerializeField] private float sendCooldown = 3f;
        
        // Local cache for quick access
        private List<string> localMessages = new List<string>();
        
        // ADDED: Client message queue for host processing
        private Queue<string> pendingClientMessages = new Queue<string>();
        private float lastClientMessageTime = 0f;
        
        // Event to notify UI when chat is updated
        public event Action<List<string>> OnChatUpdated;
        
        // Chat polling and rate limiting
        private float pollTimer;
        private int lastMessageCount = 0;
        private float lastSendTime = 0f;
        private bool isUpdatingLobby = false;
        private string lastMessageHash = "";
        
        // ADDED: Coordinate with LobbyManager to avoid duplicate API calls
        private bool useSharedPolling = true;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("LobbyChatManager: Instance created using Lobby Service");
            }
            else
            {
                Debug.LogWarning("LobbyChatManager: Duplicate instance destroyed");
                Destroy(gameObject);
            }
        }
        
        private void Update()
        {
            // FIXED: Only poll if LobbyManager isn't already polling
            if (useSharedPolling && LobbyManager.Instance != null)
            {
                // Let LobbyManager handle all polling, we'll get updates via callback
                return;
            }
            
            // Fallback polling only if LobbyManager is unavailable
            if (LobbyManager.Instance != null && LobbyManager.Instance.GetCurrentLobby() != null)
            {
                pollTimer -= Time.deltaTime;
                if (pollTimer <= 0f && !isUpdatingLobby)
                {
                    pollTimer = pollInterval;
                    _ = PollLobbyForChatUpdates();
                }
            }
        }
        
        // ADDED: Method for LobbyManager to provide chat updates
        public void OnLobbyDataUpdated(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            if (lobby?.Data == null) return;
            
            try
            {
                // Extract current chat messages
                var newMessages = ExtractChatMessagesFromLobby(lobby);
                
                // If we're the host, also check for pending client messages
                if (LobbyManager.Instance != null && LobbyManager.Instance.IsLobbyHost())
                {
                    _ = ProcessPendingClientMessagesAsync(lobby);
                }
                
                string newMessageHash = GetMessageHash(newMessages);
                
                if (newMessageHash != lastMessageHash)
                {
                    localMessages = newMessages;
                    lastMessageCount = newMessages.Count;
                    lastMessageHash = newMessageHash;
                    
                    Debug.Log($"LobbyChatManager: Updated chat from LobbyManager with {newMessages.Count} messages");
                    
                    // Remove any "sending..." indicators from local messages
                    CleanupPendingMessages();
                    
                    // Notify UI
                    OnChatUpdated?.Invoke(new List<string>(localMessages));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"LobbyChatManager: Error processing lobby data: {e.Message}");
            }
        }
        
        // Poll lobby data for chat messages with better error handling
        private async Task PollLobbyForChatUpdates()
        {
            if (LobbyManager.Instance == null || isUpdatingLobby) return;
            
            var currentLobby = LobbyManager.Instance.GetCurrentLobby();
            if (currentLobby == null) return;
            
            try
            {
                // FIXED: Direct call to LobbyService instead of reflection to avoid async issues
                var lobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                
                if (lobby?.Data != null)
                {
                    // Extract chat messages from lobby data
                    var newMessages = ExtractChatMessagesFromLobby(lobby);
                    
                    // IMPROVED: Better change detection using hash
                    string newMessageHash = GetMessageHash(newMessages);
                    bool messagesChanged = newMessageHash != lastMessageHash;
                    
                    // Update local cache if messages changed
                    if (messagesChanged)
                    {
                        localMessages = newMessages;
                        lastMessageCount = newMessages.Count;
                        lastMessageHash = newMessageHash;
                        
                        Debug.Log($"LobbyChatManager: Updated chat with {newMessages.Count} messages (hash changed)");
                        Debug.Log($"LobbyChatManager: Latest messages: [{string.Join(", ", newMessages)}]");
                        
                        // Notify UI immediately
                        OnChatUpdated?.Invoke(new List<string>(localMessages));
                    }
                }
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Rate limit") || e.Message.Contains("429"))
                {
                    Debug.LogWarning($"LobbyChatManager: Rate limit hit, backing off polling for 30s");
                    pollTimer = 30f; // Longer backoff
                }
                else
                {
                    Debug.LogError($"LobbyChatManager: Error polling lobby for chat: {e.Message}");
                }
            }
        }
        
        // ADDED: Helper method to generate hash for better change detection
        private string GetMessageHash(List<string> messages)
        {
            if (messages == null || messages.Count == 0) return "";
            return string.Join("|", messages).GetHashCode().ToString();
        }
        
        // Extract chat messages from lobby data
        private List<string> ExtractChatMessagesFromLobby(Lobby lobby)
        {
            var messages = new List<string>();
            
            if (lobby.Data == null) return messages;
            
            // Chat messages are stored with keys like "Chat_0", "Chat_1", etc.
            for (int i = 0; i < maxMessages; i++)
            {
                string chatKey = $"Chat_{i}";
                if (lobby.Data.ContainsKey(chatKey))
                {
                    string message = lobby.Data[chatKey].Value;
                    if (!string.IsNullOrEmpty(message))
                    {
                        messages.Add(message);
                    }
                }
            }
            
            return messages;
        }
        
        // Check if two message lists match
        private bool MessagesMatch(List<string> list1, List<string> list2)
        {
            if (list1.Count != list2.Count) return false;
            
            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i] != list2[i]) return false;
            }
            
            return true;
        }
        
        // COMPLETELY REWRITTEN: Client message sending through host relay
        public async void SendChat(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
                
            // Rate limiting
            if (Time.time - lastSendTime < sendCooldown)
            {
                Debug.LogWarning($"LobbyChatManager: Send rate limited, wait {sendCooldown - (Time.time - lastSendTime):F1}s");
                return;
            }
            
            if (isUpdatingLobby)
            {
                Debug.LogWarning("LobbyChatManager: Already updating lobby, ignoring send request");
                return;
            }
                
            Debug.Log($"LobbyChatManager: Sending chat message: {message}");
            
            if (LobbyManager.Instance == null)
            {
                Debug.LogError("LobbyChatManager: LobbyManager instance is null!");
                return;
            }
            
            var currentLobby = LobbyManager.Instance.GetCurrentLobby();
            if (currentLobby == null)
            {
                Debug.LogError("LobbyChatManager: No current lobby!");
                return;
            }
            
            lastSendTime = Time.time;
            
            // Check if we're the host or client
            bool isHost = LobbyManager.Instance.IsLobbyHost();
            
            if (isHost)
            {
                // Host can update lobby directly
                await SendMessageAsHost(message);
            }
            else
            {
                // Client must use player data to signal message to host
                await SendMessageAsClient(message);
            }
        }
        
        // ADDED: Host message sending (direct lobby update)
        private async Task SendMessageAsHost(string message)
        {
            Debug.Log($"LobbyChatManager: Host sending message directly: {message}");
            
            isUpdatingLobby = true;
            
            try
            {
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                var lobby = await GetLobbyWithRateLimit(currentLobby.Id);
                if (lobby == null)
                {
                    Debug.LogError("LobbyChatManager: Failed to get lobby data");
                    return;
                }
                
                var currentMessages = ExtractChatMessagesFromLobby(lobby);
                currentMessages.Add(message);
                
                // Process any pending client messages too
                await ProcessPendingClientMessages(lobby, currentMessages);
                
                // Trim messages
                while (currentMessages.Count > maxMessages)
                {
                    currentMessages.RemoveAt(0);
                }
                
                // Update lobby with all messages
                var lobbyData = new Dictionary<string, DataObject>();
                
                // Preserve existing data
                if (lobby.Data != null)
                {
                    foreach (var kvp in lobby.Data)
                    {
                        if (!kvp.Key.StartsWith("Chat_"))
                        {
                            lobbyData[kvp.Key] = kvp.Value;
                        }
                    }
                }
                
                // Add chat messages
                for (int i = 0; i < currentMessages.Count && i < maxMessages; i++)
                {
                    string chatKey = $"Chat_{i}";
                    lobbyData[chatKey] = new DataObject(DataObject.VisibilityOptions.Member, currentMessages[i]);
                }
                
                bool updateSuccess = await UpdateLobbyWithRateLimit(currentLobby.Id, lobbyData);
                
                if (updateSuccess)
                {
                    Debug.Log($"LobbyChatManager: Host successfully sent message");
                    localMessages = new List<string>(currentMessages);
                    lastMessageHash = GetMessageHash(localMessages);
                    OnChatUpdated?.Invoke(new List<string>(localMessages));
                }
                else
                {
                    Debug.LogError("LobbyChatManager: Host failed to update lobby");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Host error sending message: {e.Message}");
            }
            finally
            {
                isUpdatingLobby = false;
            }
        }
        
        // ADDED: Client message sending (via player data)
        private async Task SendMessageAsClient(string message)
        {
            Debug.Log($"LobbyChatManager: Client sending message via player data: {message}");
            
            try
            {
                string playerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                
                // Encode message with timestamp to make it unique
                string encodedMessage = $"{timestamp}:{message}";
                
                // Update player data with pending message
                var playerData = new Dictionary<string, PlayerDataObject>();
                playerData["PendingChatMessage"] = new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member, 
                    encodedMessage
                );
                
                // FIXED: Use correct UpdatePlayerOptions from Unity.Services.Lobbies namespace
                var updateOptions = new UpdatePlayerOptions
                {
                    Data = playerData
                };
                
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                await Unity.Services.Lobbies.LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id, playerId, updateOptions);
                
                Debug.Log($"LobbyChatManager: Client successfully signaled message to host");
                
                // Add to local pending queue for UI feedback
                pendingClientMessages.Enqueue(message);
                lastClientMessageTime = Time.time;
                
                // Show message locally with "sending..." indicator
                var tempMessages = new List<string>(localMessages);
                tempMessages.Add($"{message} (sending...)");
                OnChatUpdated?.Invoke(tempMessages);
                
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Client error signaling message: {e.Message}");
            }
        }
        
        // ADDED: Host processes pending client messages
        private async Task ProcessPendingClientMessages(Unity.Services.Lobbies.Models.Lobby lobby, List<string> currentMessages)
        {
            if (lobby.Players == null) return;
            
            foreach (var player in lobby.Players)
            {
                if (player.Data != null && player.Data.ContainsKey("PendingChatMessage"))
                {
                    try
                    {
                        string encodedMessage = player.Data["PendingChatMessage"].Value;
                        if (!string.IsNullOrEmpty(encodedMessage))
                        {
                            // Parse timestamp and message
                            var parts = encodedMessage.Split(':', 2);
                            if (parts.Length >= 2)
                            {
                                string timestamp = parts[0];
                                string message = parts[1];
                                
                                // Check if we've already processed this message
                                string messageId = $"{player.Id}:{timestamp}";
                                if (!HasProcessedMessage(messageId))
                                {
                                    Debug.Log($"LobbyChatManager: Host processing client message: {message}");
                                    currentMessages.Add(message);
                                    MarkMessageAsProcessed(messageId);
                                    
                                    // Clear the client's pending message
                                    await ClearClientPendingMessage(player.Id);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"LobbyChatManager: Error processing client message from {player.Id}: {e.Message}");
                    }
                }
            }
        }
        
        // ADDED: Track processed messages to avoid duplicates
        private HashSet<string> processedMessageIds = new HashSet<string>();
        
        private bool HasProcessedMessage(string messageId)
        {
            return processedMessageIds.Contains(messageId);
        }
        
        private void MarkMessageAsProcessed(string messageId)
        {
            processedMessageIds.Add(messageId);
            
            // Keep only recent message IDs to prevent memory bloat
            if (processedMessageIds.Count > 100)
            {
                // FIXED: Convert to list first, then use Take
                var oldestIds = processedMessageIds.ToList().Take(50).ToArray();
                foreach (var id in oldestIds)
                {
                    processedMessageIds.Remove(id);
                }
            }
        }
        
        // ADDED: Clear client's pending message after processing
        private async Task ClearClientPendingMessage(string playerId)
        {
            try
            {
                var playerData = new Dictionary<string, PlayerDataObject>();
                playerData["PendingChatMessage"] = new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member, 
                    "" // Clear the message
                );
                
                // FIXED: Use correct UpdatePlayerOptions from Unity.Services.Lobbies namespace
                var updateOptions = new UpdatePlayerOptions
                {
                    Data = playerData
                };
                
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                await Unity.Services.Lobbies.LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id, playerId, updateOptions);
                
                Debug.Log($"LobbyChatManager: Cleared pending message for player {playerId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Error clearing pending message for {playerId}: {e.Message}");
            }
        }

        // ADDED: Async wrapper for client message processing
        private async Task ProcessPendingClientMessagesAsync(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            if (!LobbyManager.Instance.IsLobbyHost() || isUpdatingLobby) return;
            
            try
            {
                var currentMessages = ExtractChatMessagesFromLobby(lobby);
                bool hasNewMessages = false;
                
                foreach (var player in lobby.Players)
                {
                    if (player.Data != null && player.Data.ContainsKey("PendingChatMessage"))
                    {
                        string encodedMessage = player.Data["PendingChatMessage"].Value;
                        if (!string.IsNullOrEmpty(encodedMessage))
                        {
                            var parts = encodedMessage.Split(':', 2);
                            if (parts.Length >= 2)
                            {
                                string timestamp = parts[0];
                                string message = parts[1];
                                string messageId = $"{player.Id}:{timestamp}";
                                
                                if (!HasProcessedMessage(messageId))
                                {
                                    Debug.Log($"LobbyChatManager: Host auto-processing client message: {message}");
                                    currentMessages.Add(message);
                                    MarkMessageAsProcessed(messageId);
                                    hasNewMessages = true;
                                    
                                    // Clear client's pending message
                                    _ = ClearClientPendingMessage(player.Id);
                                }
                            }
                        }
                    }
                }
                
                if (hasNewMessages)
                {
                    // Update lobby with new messages
                    await UpdateChatMessages(currentMessages);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Error in auto-processing client messages: {e.Message}");
            }
        }
        
        // ADDED: Helper to update chat messages in lobby
        private async Task UpdateChatMessages(List<string> messages)
        {
            if (isUpdatingLobby) return;
            
            isUpdatingLobby = true;
            
            try
            {
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                var lobby = await GetLobbyWithRateLimit(currentLobby.Id);
                if (lobby == null) return;
                
                // Trim messages
                while (messages.Count > maxMessages)
                {
                    messages.RemoveAt(0);
                }
                
                var lobbyData = new Dictionary<string, DataObject>();
                
                // Preserve existing data
                if (lobby.Data != null)
                {
                    foreach (var kvp in lobby.Data)
                    {
                        if (!kvp.Key.StartsWith("Chat_"))
                        {
                            lobbyData[kvp.Key] = kvp.Value;
                        }
                    }
                }
                
                // Add chat messages
                for (int i = 0; i < messages.Count && i < maxMessages; i++)
                {
                    string chatKey = $"Chat_{i}";
                    lobbyData[chatKey] = new DataObject(DataObject.VisibilityOptions.Member, messages[i]);
                }
                
                bool success = await UpdateLobbyWithRateLimit(currentLobby.Id, lobbyData);
                
                if (success)
                {
                    localMessages = new List<string>(messages);
                    lastMessageHash = GetMessageHash(localMessages);
                    OnChatUpdated?.Invoke(new List<string>(localMessages));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Error updating chat messages: {e.Message}");
            }
            finally
            {
                isUpdatingLobby = false;
            }
        }
        
        // ADDED: Clean up pending message indicators
        private void CleanupPendingMessages()
        {
            // Remove "sending..." indicators that are now complete
            if (pendingClientMessages.Count > 0 && Time.time - lastClientMessageTime > 5f)
            {
                pendingClientMessages.Clear();
            }
        }

        // ADDED: Rate-limited lobby get using LobbyManager's API with better retries
        private async Task<Unity.Services.Lobbies.Models.Lobby> GetLobbyWithRateLimit(string lobbyId)
        {
            int maxRetries = 3;
            float baseDelay = 1f;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Try to use LobbyManager's rate-limited API first
                    var lobbyApiMethod = typeof(LobbyManager).GetMethod("LobbyApiWithBackoff", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (lobbyApiMethod != null && LobbyManager.Instance != null)
                    {
                        var task = lobbyApiMethod.MakeGenericMethod(typeof(Unity.Services.Lobbies.Models.Lobby))
                            .Invoke(LobbyManager.Instance, new object[] { 
                                new System.Func<Task<Unity.Services.Lobbies.Models.Lobby>>(() => LobbyService.Instance.GetLobbyAsync(lobbyId)), 
                                "LobbyChatManager.GetLobbyWithRateLimit" 
                            }) as Task<Unity.Services.Lobbies.Models.Lobby>;
                        
                        return await task;
                    }
                    else
                    {
                        // Fallback to direct call
                        return await LobbyService.Instance.GetLobbyAsync(lobbyId);
                    }
                }
                catch (System.Exception e)
                {
                    if (e.Message.Contains("Rate limit") || e.Message.Contains("429"))
                    {
                        if (attempt < maxRetries - 1)
                        {
                            float delay = baseDelay * (attempt + 1);
                            Debug.LogWarning($"LobbyChatManager: Rate limit on get lobby attempt {attempt + 1}, waiting {delay}s");
                            await Task.Delay((int)(delay * 1000));
                            continue;
                        }
                    }
                    
                    Debug.LogWarning($"LobbyChatManager: Failed to get lobby on attempt {attempt + 1}: {e.Message}");
                    
                    if (attempt == maxRetries - 1)
                    {
                        throw;
                    }
                }
            }
            
            return null;
        }
        
        // ADDED: Rate-limited lobby update using LobbyManager's API with better retries
        private async Task<bool> UpdateLobbyWithRateLimit(string lobbyId, Dictionary<string, DataObject> lobbyData)
        {
            int maxRetries = 3;
            float baseDelay = 2f;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var updateOptions = new UpdateLobbyOptions { Data = lobbyData };
                    
                    // Try to use LobbyManager's rate-limited API first
                    var lobbyApiMethod = typeof(LobbyManager).GetMethod("LobbyApiWithBackoff", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (lobbyApiMethod != null && LobbyManager.Instance != null)
                    {
                        var task = lobbyApiMethod.MakeGenericMethod(typeof(Unity.Services.Lobbies.Models.Lobby))
                            .Invoke(LobbyManager.Instance, new object[] { 
                                new System.Func<Task<Unity.Services.Lobbies.Models.Lobby>>(() => LobbyService.Instance.UpdateLobbyAsync(lobbyId, updateOptions)), 
                                "LobbyChatManager.UpdateLobbyWithRateLimit" 
                            }) as Task<Unity.Services.Lobbies.Models.Lobby>;
                        
                        await task;
                        Debug.Log($"LobbyChatManager: Successfully updated lobby via LobbyManager API on attempt {attempt + 1}");
                        return true;
                    }
                    else
                    {
                        // Fallback to direct call
                        await LobbyService.Instance.UpdateLobbyAsync(lobbyId, updateOptions);
                        Debug.Log($"LobbyChatManager: Successfully updated lobby via direct API on attempt {attempt + 1}");
                        return true;
                    }
                }
                catch (System.Exception e)
                {
                    if (e.Message.Contains("Rate limit") || e.Message.Contains("429"))
                    {
                        if (attempt < maxRetries - 1)
                        {
                            float delay = baseDelay * (attempt + 1);
                            Debug.LogWarning($"LobbyChatManager: Rate limit on update attempt {attempt + 1}, waiting {delay}s");
                            await Task.Delay((int)(delay * 1000));
                            continue;
                        }
                        else
                        {
                            Debug.LogError($"LobbyChatManager: Rate limit exceeded after {maxRetries} attempts");
                            return false;
                        }
                    }
                    else if (e.Message.Contains("Forbidden") || e.Message.Contains("403"))
                    {
                        Debug.LogError($"LobbyChatManager: Permission denied - client may not have lobby update permissions");
                        return false;
                    }
                    else
                    {
                        Debug.LogWarning($"LobbyChatManager: Update failed on attempt {attempt + 1}: {e.Message}");
                        
                        if (attempt == maxRetries - 1)
                        {
                            Debug.LogError($"LobbyChatManager: All update attempts failed");
                            return false;
                        }
                    }
                }
            }
            
            return false;
        }
        
        // ADDED: Missing GetAllMessages method for UI integration
        public List<string> GetAllMessages()
        {
            return new List<string>(localMessages);
        }
    }
}
