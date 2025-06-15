using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Nepieciešams Take metodei

namespace HockeyGame.UI
{
   
    /// Pārvalda tērzēšanas funkcionalitāti lobija lietotāja saskarnē, izmantojot Unity Lobby servisus.
    /// Klase nodrošina ziņu sūtīšanu, saņemšanu un sinhronizāciju starp spēlētājiem.

    public class LobbyChatManager : MonoBehaviour
    {
        public static LobbyChatManager Instance { get; private set; }
        
        [Header("Tērzēšanas iestatījumi")]
        [SerializeField] private int maxMessages = 10;           // Maksimālais saglabāto ziņu skaits
        [SerializeField] private float pollInterval = 15f;       // Intervāls starp tērzēšanas atjaunināšanu (sekundes)
        [SerializeField] private float sendCooldown = 3f;        // Minimālais laiks starp ziņu sūtīšanu (pret spamu)
        
        // Lokāls kešs ātrai piekļuvei
        private List<string> localMessages = new List<string>();  // Saraksts ar visām ziņām
        
        // Klienta ziņu rinda resursdatora apstrādei
        private Queue<string> pendingClientMessages = new Queue<string>();  // Rinda ar gaidošajām klienta ziņām
        private float lastClientMessageTime = 0f;                          // Pēdējās ziņas sūtīšanas laiks
        
        // Notikums, lai paziņotu UI, kad tērzēšana ir atjaunināta
        public event Action<List<string>> OnChatUpdated;         // Izsauc, kad tērzēšanas saturs mainās
        
        // Tērzēšanas aptaujāšana un ātruma ierobežošana
        private float pollTimer;                                 // Taimeris aptaujas intervālam
        private int lastMessageCount = 0;                        // Pēdējais zināmais ziņu skaits
        private float lastSendTime = 0f;                         // Pēdējās ziņas sūtīšanas laiks
        private bool isUpdatingLobby = false;                    // Vai pašlaik notiek lobija atjaunināšana
        private string lastMessageHash = "";                     // Kontrolsumma izmaiņu noteikšanai
        
        // Koordinācija ar LobbyManager, lai izvairītos no dubultām API izsaukšanām
        private bool useSharedPolling = true;                    // Vai izmantot LobbyManager aptauju
        
      
        /// Unity dzīves cikla metode - izsaukta inicializācijas laikā

        private void Awake()
        {
            // Singltona pārvaldība - nodrošina, ka pastāv tikai viena instance
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("LobbyChatManager: Instance izveidota, izmantojot Lobby Service");
            }
            else
            {
                Debug.LogWarning("LobbyChatManager: Dublikāta instance iznīcināta");
                Destroy(gameObject);
            }
        }

        /// Unity dzīves cikla metode - izsaukta katrā kadrā

        private void Update()
        {
            // Izmantot tikai LobbyManager aptaujāšanu, ja tas ir pieejams
            if (useSharedPolling && LobbyManager.Instance != null)
            {
                // Ļauj LobbyManager apstrādāt visu aptaujāšanu, mēs saņemsim atjauninājumus caur atsaukumu
                return;
            }
            
            // Atkāpšanās aptaujāšana tikai tad, ja LobbyManager nav pieejams
            if (LobbyManager.Instance != null && LobbyManager.Instance.GetCurrentLobby() != null)
            {
                pollTimer -= Time.deltaTime;
                if (pollTimer <= 0f && !isUpdatingLobby)
                {
                    pollTimer = pollInterval;
                    _ = PollLobbyForChatUpdates();  // Asinhrons izsaukums bez gaidīšanas
                }
            }
        }
        

        // Metode, lai LobbyManager varētu sniegt tērzēšanas atjauninājumus
      
        public void OnLobbyDataUpdated(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            if (lobby?.Data == null) return;
            
            try
            {
                // Iegūt pašreizējās tērzēšanas ziņas
                var newMessages = ExtractChatMessagesFromLobby(lobby);
                
                // Ja esam resursdators, pārbaudīt gaidošās klienta ziņas
                if (LobbyManager.Instance != null && LobbyManager.Instance.IsLobbyHost())
                {
                    _ = ProcessPendingClientMessagesAsync(lobby);
                }
                
                string newMessageHash = GetMessageHash(newMessages);
                
                // Atjaunināt lokālo kešu un paziņot UI, ja ziņas ir mainījušās
                if (newMessageHash != lastMessageHash)
                {
                    localMessages = newMessages;
                    lastMessageCount = newMessages.Count;
                    lastMessageHash = newMessageHash;
                    
                    Debug.Log($"LobbyChatManager: Atjaunināts tērzēšanas saraksts no LobbyManager ar {newMessages.Count} ziņām");
                    
                    // Notīrīt jebkurus "sending..." indikatorus no lokālajām ziņām
                    CleanupPendingMessages();
                    
                    // Paziņot UI
                    OnChatUpdated?.Invoke(new List<string>(localMessages));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"LobbyChatManager: Kļūda apstrādājot lobija datus: {e.Message}");
            }
        }
        
 
        /// Aptaujā lobija datus tērzēšanas ziņām ar labāku kļūdu apstrādi

        private async Task PollLobbyForChatUpdates()
        {
            if (LobbyManager.Instance == null || isUpdatingLobby) return;
            
            var currentLobby = LobbyManager.Instance.GetCurrentLobby();
            if (currentLobby == null) return;
            
            try
            {
                // Tiešs izsaukums uz LobbyService, lai izvairītos no asinhrono problēmām
                var lobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                
                if (lobby?.Data != null)
                {
                    // Iegūt tērzēšanas ziņas no lobija datiem
                    var newMessages = ExtractChatMessagesFromLobby(lobby);
                    
                    // Labāka izmaiņu noteikšana, izmantojot kontrolsummu
                    string newMessageHash = GetMessageHash(newMessages);
                    bool messagesChanged = newMessageHash != lastMessageHash;
                    
                    // Atjaunināt lokālo kešu, ja ziņas ir mainījušās
                    if (messagesChanged)
                    {
                        localMessages = newMessages;
                        lastMessageCount = newMessages.Count;
                        lastMessageHash = newMessageHash;
                        
                        Debug.Log($"LobbyChatManager: Atjaunināta tērzēšana ar {newMessages.Count} ziņām (kontrolsumma mainījusies)");
                        Debug.Log($"LobbyChatManager: Jaunākās ziņas: [{string.Join(", ", newMessages)}]");
                        
                        // Nekavējoties paziņot UI
                        OnChatUpdated?.Invoke(new List<string>(localMessages));
                    }
                }
            }
            catch (Exception e)
            {
                // Ātruma ierobežojuma apstrāde
                if (e.Message.Contains("Rate limit") || e.Message.Contains("429"))
                {
                    Debug.LogWarning($"LobbyChatManager: Sasniegts ātruma ierobežojums, atpakaļspiešana aptaujāšanai uz 30s");
                    pollTimer = 30f; // Ilgāka atpakaļspiešana
                }
                else
                {
                    Debug.LogError($"LobbyChatManager: Kļūda aptaujājot lobiju tērzēšanai: {e.Message}");
                }
            }
        }
        

        /// Palīgmetode, lai ģenerētu kontrolsummu labākai izmaiņu noteikšanai
   
        private string GetMessageHash(List<string> messages)
        {
            if (messages == null || messages.Count == 0) return "";
            return string.Join("|", messages).GetHashCode().ToString();
        }
        

        /// Iegūst tērzēšanas ziņas no lobija datiem

        private List<string> ExtractChatMessagesFromLobby(Lobby lobby)
        {
            var messages = new List<string>();
            
            if (lobby.Data == null) return messages;
            
            // Tērzēšanas ziņas ir saglabātas ar atslēgām "Chat_0", "Chat_1", utt.
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
        
   
        /// /// Pārbauda, vai divi ziņu saraksti sakrīt
    
        private bool MessagesMatch(List<string> list1, List<string> list2)
        {
            if (list1.Count != list2.Count) return false;
            
            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i] != list2[i]) return false;
            }
            
            return true;
        }
        
    
        /// Klienta ziņu sūtīšana caur resursdatora starpniecību
 
        public async void SendChat(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
                
            // Ātruma ierobežošana
            if (Time.time - lastSendTime < sendCooldown)
            {
                Debug.LogWarning($"LobbyChatManager: Sūtīšanas ātrums ierobežots, gaidiet {sendCooldown - (Time.time - lastSendTime):F1}s");
                return;
            }
            
            if (isUpdatingLobby)
            {
                Debug.LogWarning("LobbyChatManager: Jau notiek lobija atjaunināšana, ignorējam sūtīšanas pieprasījumu");
                return;
            }
                
            Debug.Log($"LobbyChatManager: Sūtam tērzēšanas ziņu: {message}");
            
            if (LobbyManager.Instance == null)
            {
                Debug.LogError("LobbyChatManager: LobbyManager instance ir null!");
                return;
            }
            
            var currentLobby = LobbyManager.Instance.GetCurrentLobby();
            if (currentLobby == null)
            {
                Debug.LogError("LobbyChatManager: Nav pašreizējā lobija!");
                return;
            }
            
            lastSendTime = Time.time;
            
            // Pārbaudīt, vai esam resursdators vai klients
            bool isHost = LobbyManager.Instance.IsLobbyHost();
            
            if (isHost)
            {
                // Resursdators var atjaunināt lobiju tieši
                await SendMessageAsHost(message);
            }
            else
            {
                // Klientam jāizmanto spēlētāja dati, lai signalizētu ziņu resursdatoram
                await SendMessageAsClient(message);
            }
        }
        

        /// Resursdatora ziņu sūtīšana (tiešs lobija atjauninājums)

        private async Task SendMessageAsHost(string message)
        {
            Debug.Log($"LobbyChatManager: Resursdators sūta ziņu tieši: {message}");
            
            isUpdatingLobby = true;
            
            try
            {
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                var lobby = await GetLobbyWithRateLimit(currentLobby.Id);
                if (lobby == null)
                {
                    Debug.LogError("LobbyChatManager: Neizdevās iegūt lobija datus");
                    return;
                }
                
                var currentMessages = ExtractChatMessagesFromLobby(lobby);
                currentMessages.Add(message);
                
                // Apstrādāt arī gaidošās klienta ziņas
                await ProcessPendingClientMessages(lobby, currentMessages);
                
                // Apgriezt ziņas, ja pārsniegts limits
                while (currentMessages.Count > maxMessages)
                {
                    currentMessages.RemoveAt(0);
                }
                
                // Atjaunināt lobiju ar visām ziņām
                var lobbyData = new Dictionary<string, DataObject>();
                
                // Saglabāt esošos datus
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
                
                // Pievienot tērzēšanas ziņas
                for (int i = 0; i < currentMessages.Count && i < maxMessages; i++)
                {
                    string chatKey = $"Chat_{i}";
                    lobbyData[chatKey] = new DataObject(DataObject.VisibilityOptions.Member, currentMessages[i]);
                }
                
                bool updateSuccess = await UpdateLobbyWithRateLimit(currentLobby.Id, lobbyData);
                
                if (updateSuccess)
                {
                    Debug.Log($"LobbyChatManager: Resursdators veiksmīgi nosūtījis ziņu");
                    localMessages = new List<string>(currentMessages);
                    lastMessageHash = GetMessageHash(localMessages);
                    OnChatUpdated?.Invoke(new List<string>(localMessages));
                }
                else
                {
                    Debug.LogError("LobbyChatManager: Resursdatoram neizdevās atjaunināt lobiju");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Resursdatora kļūda ziņas sūtīšanā: {e.Message}");
            }
            finally
            {
                isUpdatingLobby = false;
            }
        }
        

        /// Klienta ziņu sūtīšana (caur spēlētāja datiem)

        private async Task SendMessageAsClient(string message)
        {
            Debug.Log($"LobbyChatManager: Klients sūta ziņu caur spēlētāja datiem: {message}");
            
            try
            {
                string playerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                
                // Kodēt ziņu ar laika zīmogu, lai padarītu to unikālu
                string encodedMessage = $"{timestamp}:{message}";
                
                // Atjaunināt spēlētāja datus ar gaidošo ziņu
                var playerData = new Dictionary<string, PlayerDataObject>();
                playerData["PendingChatMessage"] = new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member, 
                    encodedMessage
                );
                
                // Izmantot pareizo UpdatePlayerOptions no Unity.Services.Lobbies namespace
                var updateOptions = new UpdatePlayerOptions
                {
                    Data = playerData
                };
                
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                await Unity.Services.Lobbies.LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id, playerId, updateOptions);
                
                Debug.Log($"LobbyChatManager: Klients veiksmīgi signalizējis ziņu resursdatoram");
                
                // Pievienot lokālajai gaidošajai rindai UI atgriezeniskajai saitei
                pendingClientMessages.Enqueue(message);
                lastClientMessageTime = Time.time;
                
                // Parādīt ziņu lokāli ar "sending..." indikatoru
                var tempMessages = new List<string>(localMessages);
                tempMessages.Add($"{message} (sūta...)");
                OnChatUpdated?.Invoke(tempMessages);
                
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Klienta kļūda signalizējot ziņu: {e.Message}");
            }
        }
        

        /// Resursdators apstrādā gaidošās klienta ziņas
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
                            // Parsēt laika zīmogu un ziņu
                            var parts = encodedMessage.Split(':', 2);
                            if (parts.Length >= 2)
                            {
                                string timestamp = parts[0];
                                string message = parts[1];
                                
                                // Pārbaudīt, vai esam jau apstrādājuši šo ziņu
                                string messageId = $"{player.Id}:{timestamp}";
                                if (!HasProcessedMessage(messageId))
                                {
                                    Debug.Log($"LobbyChatManager: Resursdators apstrādā klienta ziņu: {message}");
                                    currentMessages.Add(message);
                                    MarkMessageAsProcessed(messageId);
                                    
                                    // Notīrīt klienta gaidošo ziņu
                                    await ClearClientPendingMessage(player.Id);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"LobbyChatManager: Kļūda apstrādājot klienta ziņu no {player.Id}: {e.Message}");
                    }
                }
            }
        }
        
        // Apstrādāto ziņu izsekošana, lai izvairītos no dublikātiem
        private HashSet<string> processedMessageIds = new HashSet<string>();
        

        /// Pārbauda, vai ziņa jau ir apstrādāta

        private bool HasProcessedMessage(string messageId)
        {
            return processedMessageIds.Contains(messageId);
        }
        

        /// Atzīmē ziņu kā apstrādātu

        private void MarkMessageAsProcessed(string messageId)
        {
            processedMessageIds.Add(messageId);
            
            // Saglabāt tikai jaunākās ziņu ID, lai novērstu atmiņas pārpildīšanos
            if (processedMessageIds.Count > 100)
            {
                // Vispirms pārveidot sarakstā, tad izmantot Take
                var oldestIds = processedMessageIds.ToList().Take(50).ToArray();
                foreach (var id in oldestIds)
                {
                    processedMessageIds.Remove(id);
                }
            }
        }
        

        /// Notīra klienta gaidošo ziņu pēc apstrādes

        private async Task ClearClientPendingMessage(string playerId)
        {
            try
            {
                var playerData = new Dictionary<string, PlayerDataObject>();
                playerData["PendingChatMessage"] = new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member, 
                    "" // Notīrīt ziņu
                );
                
                // Izmantot pareizo UpdatePlayerOptions no Unity.Services.Lobbies namespace
                var updateOptions = new UpdatePlayerOptions
                {
                    Data = playerData
                };
                
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                await Unity.Services.Lobbies.LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id, playerId, updateOptions);
                
                Debug.Log($"LobbyChatManager: Notīrīta gaidošā ziņa spēlētājam {playerId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Kļūda notīrot gaidošo ziņu spēlētājam {playerId}: {e.Message}");
            }
        }

   
        /// Asinhrona iesaiņotājs klienta ziņu apstrādei
   
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
                                    Debug.Log($"LobbyChatManager: Resursdators automātiski apstrādā klienta ziņu: {message}");
                                    currentMessages.Add(message);
                                    MarkMessageAsProcessed(messageId);
                                    hasNewMessages = true;
                                    
                                    // Notīrīt klienta gaidošo ziņu
                                    _ = ClearClientPendingMessage(player.Id);
                                }
                            }
                        }
                    }
                }
                
                if (hasNewMessages)
                {
                    // Atjaunināt lobiju ar jaunām ziņām
                    await UpdateChatMessages(currentMessages);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbyChatManager: Kļūda automātiskajā klienta ziņu apstrādē: {e.Message}");
            }
        }
        

        /// Palīgs tērzēšanas ziņu atjaunināšanai lobijā

        private async Task UpdateChatMessages(List<string> messages)
        {
            if (isUpdatingLobby) return;
            
            isUpdatingLobby = true;
            
            try
            {
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                var lobby = await GetLobbyWithRateLimit(currentLobby.Id);
                if (lobby == null) return;
                
                // Apgriezt ziņas, ja pārsniegts limits
                while (messages.Count > maxMessages)
                {
                    messages.RemoveAt(0);
                }
                
                var lobbyData = new Dictionary<string, DataObject>();
                
                // Saglabāt esošos datus
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
                
                // Pievienot tērzēšanas ziņas
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
                Debug.LogError($"LobbyChatManager: Kļūda atjauninot tērzēšanas ziņas: {e.Message}");
            }
            finally
            {
                isUpdatingLobby = false;
            }
        }
        
        /// Notīra gaidošo ziņu indikatorus
        private void CleanupPendingMessages()
        {
            // Notīrīt "sending..." indikatorus, kas tagad ir pabeigti
            if (pendingClientMessages.Count > 0 && Time.time - lastClientMessageTime > 5f)
            {
                pendingClientMessages.Clear();
            }
        }

        /// Iegūst lobiju ar ātruma ierobežojuma pārvaldību, izmantojot LobbyManager API
        private async Task<Unity.Services.Lobbies.Models.Lobby> GetLobbyWithRateLimit(string lobbyId)
        {
            int maxRetries = 3;
            float baseDelay = 1f;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Mēģināt izmantot LobbyManager ātruma ierobežoto API vispirms
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
                        // Rezerves variants - tiešs izsaukums
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
                            Debug.LogWarning($"LobbyChatManager: Ātruma ierobežojums lobija iegūšanas mēģinājumā {attempt + 1}, gaidām {delay}s");
                            await Task.Delay((int)(delay * 1000));
                            continue;
                        }
                    }
                    
                    Debug.LogWarning($"LobbyChatManager: Neizdevās iegūt lobiju mēģinājumā {attempt + 1}: {e.Message}");
                    
                    if (attempt == maxRetries - 1)
                    {
                        throw;
                    }
                }
            }
            
            return null;
        }
        
        /// Atjaunina lobiju ar ātruma ierobežojuma pārvaldību, izmantojot LobbyManager API
        private async Task<bool> UpdateLobbyWithRateLimit(string lobbyId, Dictionary<string, DataObject> lobbyData)
        {
            int maxRetries = 3;
            float baseDelay = 2f;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var updateOptions = new UpdateLobbyOptions { Data = lobbyData };
                    
                    // Mēģināt izmantot LobbyManager ātruma ierobežoto API vispirms
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
                        Debug.Log($"LobbyChatManager: Veiksmīgi atjaunināts lobijs caur LobbyManager API mēģinājumā {attempt + 1}");
                        return true;
                    }
                    else
                    {
                        // Rezerves variants - tiešs izsaukums
                        await LobbyService.Instance.UpdateLobbyAsync(lobbyId, updateOptions);
                        Debug.Log($"LobbyChatManager: Veiksmīgi atjaunināts lobijs caur tiešo API mēģinājumā {attempt + 1}");
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
                            Debug.LogWarning($"LobbyChatManager: Ātruma ierobežojums atjaunināšanas mēģinājumā {attempt + 1}, gaidām {delay}s");
                            await Task.Delay((int)(delay * 1000));
                            continue;
                        }
                        else
                        {
                            Debug.LogError($"LobbyChatManager: Ātruma ierobežojums pārsniegts pēc {maxRetries} mēģinājumiem");
                            return false;
                        }
                    }
                    else if (e.Message.Contains("Forbidden") || e.Message.Contains("403"))
                    {
                        Debug.LogError($"LobbyChatManager: Piekļuve liegta - klientam var nebūt lobija atjaunināšanas atļaujas");
                        return false;
                    }
                    else
                    {
                        Debug.LogWarning($"LobbyChatManager: Atjaunināšana neizdevās mēģinājumā {attempt + 1}: {e.Message}");
                        
                        if (attempt == maxRetries - 1)
                        {
                            Debug.LogError($"LobbyChatManager: Visi atjaunināšanas mēģinājumi neizdevās");
                            return false;
                        }
                    }
                }
            }
            
            return false;
        }
        
        /// Iegūst visas ziņas UI integrācijai
        public List<string> GetAllMessages()
        {
            return new List<string>(localMessages);
        }
    }
}
