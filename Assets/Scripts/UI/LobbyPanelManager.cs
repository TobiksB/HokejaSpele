using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Netcode; // Pievienots tīklojumam
using System.Linq; // Pievienots FirstOrDefault izmantošanai
using Unity.Collections; // Pievienots FixedString512Bytes izmantošanai

namespace HockeyGame.UI
{
    // Padaram LobbyPanelManager par NetworkBehaviour, lai tas varētu uzturēt NetworkList un sinhronizēt tīklā
    public class LobbyPanelManager : NetworkBehaviour
    {
        //  Padarām statisko Instance īpašību izturīgāku
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
                        // Mēģinām atrast jebkuru lobija paneli, kam piesaistīties
                        var lobbyPanel = FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                            .FirstOrDefault(go => go.name.ToLower().Contains("lobby") && 
                                            go.name.ToLower().Contains("panel"));
                        if (lobbyPanel != null)
                        {
                            Debug.Log($"Izveidojam LobbyPanelManager uz esošā paneļa: {lobbyPanel.name}");
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

        [Header("Paneļu atsauces")]
        [SerializeField] private RectTransform mainPanel;

        [Header("Lobija kods")]
        [SerializeField] private TMP_Text lobbyCodeText;
        [SerializeField] private Button copyCodeButton;

        [Header("Spēlētāju saraksts")]
        [SerializeField] private ScrollRect playerListScrollRect;
        [SerializeField] private RectTransform playerListContent;
        [SerializeField] private PlayerListItem playerListItemPrefab; // Noņemts UI prefikss

        [Header("Komandas izvēle")]
        [SerializeField] private Button blueTeamButton;
        [SerializeField] private Button redTeamButton;
        [SerializeField] private Image blueTeamIndicator;
        [SerializeField] private Image redTeamIndicator;

        [Header("Tērzēšana")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private RectTransform chatContent;
        [SerializeField] private TMP_InputField chatInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_Text chatText;

        [Header("Spēles kontrole")]
        [SerializeField] private Button startMatchButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Image readyIndicator;

  

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Pierakstāmies uz izmaiņām (visi klienti, ieskaitot resursdatoru)
            // Noņemam visu manuālo NetworkList izveidi no Awake
            VerifyReferences();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
        }

        private void Awake()
        {
            Debug.Log($"UI.LobbyPanelManager.Awake uz {gameObject.name}, jau ir instance: {_instance != null}");
            if (_instance == null)
            {
                _instance = this;
                VerifyReferences();
                Debug.Log($"UI.LobbyPanelManager iestatīts kā Instance, gameObject: {gameObject.name}, aktīvs: {gameObject.activeInHierarchy}");
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"Atrastas vairākas UI.LobbyPanelManager instances. Iznīcinām dublējumu ({gameObject.name}).");
                Destroy(gameObject);
                return;
            }

            // Noņemam visu manuālo NetworkList izveidi no Awake
            VerifyReferences();
        }
        
        private void OnEnable()
        {
            Debug.Log($"UI.LobbyPanelManager.OnEnable uz {gameObject.name}");
            
            // Nodrošinām, ka šī ir aktīvā instance, kad tā ir iespējota
            if (_instance != this)
            {
                _instance = this;
                Debug.Log($"UI.LobbyPanelManager.OnEnable pārpiešķir instanci uz {gameObject.name}");
            }
            
            SetupScrollViews();
            SetupButtons();
            
            //  Neatjauninām uzreiz - gaidām, līdz lobija izveide ir pabeigta
            // Atsvaidzinām tikai tad, ja LobbyManager jau ir spēlētāji
            if (LobbyManager.Instance != null)
            {
                // Pārbaudām, vai lobija pārvaldniekam ir spēlētāju dati pirms atsvaidzināšanas
                var hasPlayers = LobbyManager.Instance.GetType().GetField("playerNames", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hasPlayers != null)
                {
                    var playerNamesDict = hasPlayers.GetValue(LobbyManager.Instance) as Dictionary<string, string>;
                    if (playerNamesDict != null && playerNamesDict.Count > 0)
                    {
                        Debug.Log($"UI.LobbyPanelManager.OnEnable: Atrasti {playerNamesDict.Count} esoši spēlētāji, notiek atsvaidzināšana...");
                        LobbyManager.Instance.RefreshPlayerList();
                    }
                    else
                    {
                        Debug.Log("UI.LobbyPanelManager.OnEnable: Nav atrasti esoši spēlētāji, izlaižam atsvaidzināšanu");
                    }
                }
            }

            // Pierakstāmies uz tērzēšanas atjauninājumiem
            if (HockeyGame.UI.LobbyChatManager.Instance != null)
            {
                HockeyGame.UI.LobbyChatManager.Instance.OnChatUpdated -= OnChatUpdated;
                HockeyGame.UI.LobbyChatManager.Instance.OnChatUpdated += OnChatUpdated;
                // Sākotnējā sinhronizācija
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
                Debug.LogWarning("UI.LobbyPanelManager: Trūkst atsauču spēlētāju sarakstam - mēģinām tās atrast");
                VerifyReferences();
            }
            
            if (!playerListContent)
            {
                Debug.LogError("UI.LobbyPanelManager: playerListContent joprojām ir null pēc verifikācijas!");
                // Mēģinām atrast jebkuru ScrollRect, ko izmantot
                var scrollRect = GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null && scrollRect.content != null)
                {
                    playerListContent = scrollRect.content;
                    Debug.Log("UI.LobbyPanelManager: Atrasta saturs bērnu ScrollRect");
                }
                else
                {
                    return; // Nevar turpināt bez satura
                }
            }
            
            if (!playerListItemPrefab)
            {
                Debug.LogError("UI.LobbyPanelManager: playerListItemPrefab joprojām ir null pēc verifikācijas!");
                // Mēģinām atrast PlayerListItem projekta resursos
                playerListItemPrefab = Resources.Load<PlayerListItem>("Prefabs/UI/PlayerListItem");
                
                if (!playerListItemPrefab)
                {
                    // Mēģinām izveidot pamata vienu
                    GameObject tempObj = new GameObject("TempPlayerListItem");
                    playerListItemPrefab = tempObj.AddComponent<PlayerListItem>();
                    Debug.Log("UI.LobbyPanelManager: Izveidots pagaidu PlayerListItem");
                }
            }

            try
            {
                // Notīrām esošos vienumus
                foreach (Transform child in playerListContent)
                {
                    Destroy(child.gameObject);
                }

                if (players == null || players.Count == 0)
                {
                    Debug.LogWarning("UI.LobbyPanelManager: Nav spēlētāju, ko attēlot");
                    return;
                }

                // Pievienojam spēlētāju vienumus
                foreach (var player in players)
                {
                    Debug.Log($"UI.LobbyPanelManager: Izveidojam vienumu {player.PlayerName}");
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
                            Debug.Log($"UI.LobbyPanelManager: ✓ Veiksmīgi izveidots vienums {player.PlayerName}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"UI.LobbyPanelManager: ✗ Kļūda, izveidojot vienumu: {e.Message}");
                    }
                }

                // Spiediena izkārtojuma atjaunināšana
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContent);

                Debug.Log($"UI.LobbyPanelManager: ✓ Veiksmīgi atjaunināts spēlētāju saraksts ar {players.Count} vienumiem");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Kļūda UI.LobbyPanelManager.UpdatePlayerList: {e.Message}");
                Debug.LogError($"Kļūdas steka izsekošana: {e.StackTrace}");
            }
        }

        private void VerifyReferences()
        {
            if (playerListContent == null)
            {
                Debug.LogError("UI.LobbyPanelManager: playerListContent trūkst!");
                playerListContent = GetComponentInChildren<ScrollRect>()?.content as RectTransform;
            }

            if (playerListItemPrefab == null)
            {
                Debug.LogError("UI.LobbyPanelManager: playerListItemPrefab trūkst!");
                // Meklējam PlayerListItem ainā vai resursos
                var foundPrefab = Object.FindFirstObjectByType<PlayerListItem>();
                if (foundPrefab != null)
                {
                    playerListItemPrefab = foundPrefab;
                    Debug.Log("UI.LobbyPanelManager: Atrasts PlayerListItem ainā.");
                }
                else
                {
                    playerListItemPrefab = Resources.Load<PlayerListItem>("Prefabs/UI/PlayerListItem");
                    if (playerListItemPrefab != null)
                        Debug.Log("UI.LobbyPanelManager: Ielādēts PlayerListItem no Resursiem.");
                }
            }

            Debug.Log($"UI.LobbyPanelManager: Atsauces - Saturs: {playerListContent != null}, Prefabs: {playerListItemPrefab != null}");
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
                lobbyCodeText.text = $"Lobija kods: {code}";
                Debug.Log($"Iestatīts lobija kods uz: {code}");
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

                    // Atjauninām vizuālos rādītājus ar labāku atsauksmi
                    if (blueTeamIndicator != null && redTeamIndicator != null)
                    {
                        blueTeamIndicator.gameObject.SetActive(team == "Blue");
                        redTeamIndicator.gameObject.SetActive(team == "Red");
                        
                        // Pievienojam krāsas atsauksmi
                        if (team == "Blue")
                        {
                            blueTeamIndicator.color = new Color(0.2f, 0.4f, 1f, 1f); // Spilgti zila
                        }
                        else
                        {
                            redTeamIndicator.color = new Color(1f, 0.2f, 0.2f, 1f); // Spilgti sarkana
                        }
                    }

                    // Spiediena UI atsvaidzināšana
                    LobbyManager.Instance.RefreshPlayerList();
                    
                    Debug.Log($"Izvēlēta komanda: {team} 2v2 spēlei - nepieciešami 4 spēlētāji (2 uz katru komandu) vai vismaz 2 testēšanai!");
                }
                else
                {
                    Debug.LogError("LobbyManager instance ir null!");
                }
            }
        }

        private void OnStartMatch()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.StartMatch();
                Debug.Log("Sākam spēli...");
            }
            else
            {
                Debug.LogError("LobbyManager instance ir null!");
            }
        }

        private void SetupScrollViews()
        {
            // Iestatām spēlētāju saraksta ritināšanas skatu
            if (playerListContent)
            {
                // Iestatām RectTransform enkura un izmēra iestatījumus
                RectTransform rt = playerListContent.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                var vlg = playerListContent.GetComponent<VerticalLayoutGroup>();
                if (!vlg) vlg = playerListContent.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 5f;
                vlg.padding = new RectOffset(10, 10, 10, 10); // Palielināta iekšējā atstarpe
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

            // Iestatām tērzēšanas saturu
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

        // --- Tīklota tērzēšanas sūtīšana caur LobbyChatManager ---
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

        // --- UI atjaunināšanas atsaukums tērzēšanai ---
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
                    
                    // Atjauninām gatavības rādītāju ar labāku vizuālo atsauksmi
                    if (readyIndicator != null)
                    {
                        bool isReady = LobbyManager.Instance.IsPlayerReady(playerId);
                        readyIndicator.color = isReady ? new Color(0.2f, 1f, 0.2f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                        
                        // Atjauninām gatavības pogas tekstu, ja tai ir teksta komponents - FIXED: Noņemam atzīmi
                        var buttonText = readyButton?.GetComponentInChildren<TMPro.TMP_Text>();
                        if (buttonText != null)
                        {
                            buttonText.text = isReady ? "GATAVS!" : "GATAVS";
                            buttonText.color = isReady ? Color.green : Color.white;
                        }
                    }
                    
                    Debug.Log($"Spēlētāja gatavības stāvoklis: {LobbyManager.Instance.IsPlayerReady(playerId)} - 2v2 nepieciešami 4 spēlētāji (2 uz katru komandu) vai vismaz 2 testēšanai!");
                }
            }
        }

        public void UpdateStartButton(bool canStart)
        {
            if (startMatchButton != null)
            {
                startMatchButton.interactable = IsHost() && canStart;
                
                // Labāka vizuālā atsauksme sākšanas pogai
                var buttonText = startMatchButton.GetComponentInChildren<TMPro.TMP_Text>();
                if (buttonText != null)
                {
                    if (canStart && IsHost())
                    {
                        buttonText.text = "SĀKT SPĒLI (TESTĒŠANA)";
                        buttonText.color = Color.green;
                    }
                    else if (!IsHost())
                    {
                        buttonText.text = "GAIDĪT RESURSDATORU...";
                        buttonText.color = Color.gray;
                    }
                    else
                    {
                        buttonText.text = "IZVĒLIES KOMANDU UN GATAVOJIES!";
                        buttonText.color = Color.yellow;
                    }
                }
                
                // Atjauninām pogas krāsas
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
                    Debug.Log("Atrasta esošais LobbyPanelManager");
                }
                else
                {
                    Debug.LogError("Ainā nav atrasts nevienas LobbyPanelManager!");
                }
            }
        }

        private void ValidateUIComponents()
        {
            if (!lobbyCodeText)
                Debug.LogError("Lobija koda teksts trūkst!");
            if (!playerListContent)
                Debug.LogError("Spēlētāju saraksta saturs trūkst!");
            if (!playerListScrollRect)
                Debug.LogError("Spēlētāju saraksta ritināšanas taisnstūris trūkst!");
            if (!chatContent)
                Debug.LogError("Tērzēšanas saturs trūkst!");
            if (!chatScrollRect)
                Debug.LogError("Tērzēšanas ritināšanas taisnstūris trūkst!");
            if (!chatInput)
                Debug.LogError("Tērzēšanas ievades lauks trūkst!");
            if (!blueTeamButton)
                Debug.LogError("Zilās komandas poga trūkst!");
            if (!redTeamButton)
                Debug.LogError("Sarkanās komandas poga trūkst!");
            if (!startMatchButton)
                Debug.LogError("Sākt spēles poga trūkst!");
            if (!readyButton)
                Debug.LogError("Gatavības poga trūkst!");
        }

        // Pievienojam šo metodi, lai novērstu trūkstošu 'ForceInitialize' kļūdas
        public void ForceInitialize()
        {
            Debug.Log("LobbyPanelManager.ForceInitialize izsaukta");
            // Pēc izvēles atkārtoti izpildām inicializācijas loģiku, ja nepieciešams
            VerifyReferences();
            SetupScrollViews();
            SetupButtons();
        }
    }
}
