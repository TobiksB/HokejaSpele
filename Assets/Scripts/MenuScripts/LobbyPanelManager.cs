using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Netcode;


public class LobbyPanelManager : MonoBehaviour
{
    public static LobbyPanelManager Instance { get; private set; }

    [Header("Paneļu atsauces")]
    [SerializeField] private RectTransform mainPanel;

    [Header("Lobija kods")]
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Button copyCodeButton;

    [Header("Spēlētāju saraksts")]
    [SerializeField] private ScrollRect playerListScrollRect;
    [SerializeField] private RectTransform playerListContent;
    [SerializeField] private PlayerListItem playerListItemPrefab;

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

    [Header("Atkļūdošana")]
    [SerializeField] private Button debugStartButton;

    private void Awake()
    {
        Debug.Log($"MenuScripts.LobbyPanelManager.Awake uz {gameObject.name}");
        if (Instance == null)
        {
            Instance = this;
            VerifyReferences();
            Debug.Log($"MenuScripts.LobbyPanelManager iestatīts kā Instance");
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"Atrastas vairākas MenuScripts.LobbyPanelManager instances. Iznīcinu dublējumu.");
            Destroy(gameObject);
            return;
        }
    }

    private void VerifyReferences()
    {
        if (playerListContent == null)
        {
            Debug.LogError("MenuScripts.LobbyPanelManager: playerListContent atsauce nav atrasta!");
            playerListContent = GetComponentInChildren<ScrollRect>()?.content as RectTransform;
            if (playerListContent != null)
                Debug.Log("MenuScripts.LobbyPanelManager: Atrasts playerListContent bērnu elementos.");
        }

        if (playerListItemPrefab == null)
        {
            Debug.LogError("MenuScripts.LobbyPanelManager: playerListItemPrefab atsauce nav atrasta!");
            // Vispirms meklēt UI versiju tieši ainā
            var uiPrefab = Object.FindFirstObjectByType<PlayerListItem>();
            if (uiPrefab != null)
            {
                playerListItemPrefab = uiPrefab;
                Debug.Log("MenuScripts.LobbyPanelManager: Atrasts PlayerListItem ainā.");
            }
            else
            {
                // Mēģiniet ielādēt no Resources
                playerListItemPrefab = Resources.Load<PlayerListItem>("Prefabs/UI/PlayerListItem");
                if (playerListItemPrefab != null)
                    Debug.Log("MenuScripts.LobbyPanelManager: Ielādēts PlayerListItem no Resources.");
            }
        }

        Debug.Log($"MenuScripts.LobbyPanelManager: Atsauces pārbaudītas - Content: {playerListContent != null}, Prefab: {playerListItemPrefab != null}");
    }

    private void OnEnable()
    {
        Debug.Log($"MenuScripts.LobbyPanelManager.OnEnable uz {gameObject.name}");
        
        // Vienmēr iestatīt šo instanci kā aktīvo, kad tā ir iespējota
        Instance = this;
        
        SetupScrollViews();
        SetupButtons();
        
        // Paziņot jebkuram kodam, kas gaida aktīvu lobija paneli
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RefreshPlayerList();
        }
    }

    private void OnDisable()
    {
        Debug.Log($"MenuScripts.LobbyPanelManager.OnDisable uz {gameObject.name}");
        
        // Ja šī instance tiek atspējota un tā ir pašreizējā Instance,
        // meklēt citu aktīvu instanci, lai to pārņemtu
        if (Instance == this)
        {
            var otherManagers = FindObjectsByType<LobbyPanelManager>(FindObjectsSortMode.None);
            foreach (var manager in otherManagers)
            {
                if (manager != this && manager.gameObject.activeInHierarchy)
                {
                    Instance = manager;
                    Debug.Log($"MenuScripts.LobbyPanelManager Instance pārslēgts uz {manager.gameObject.name}");
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
        Debug.Log($"MenuScripts.LobbyPanelManager: UpdatePlayerList izsaukta ar {players?.Count ?? 0} spēlētājiem");
        
        if (!playerListContent)
        {
            Debug.LogError("MenuScripts.LobbyPanelManager: playerListContent ir null!");
            VerifyReferences();
            if (!playerListContent) return;
        }
        
        if (!playerListItemPrefab)
        {
            Debug.LogError("MenuScripts.LobbyPanelManager: playerListItemPrefab ir null!");
            VerifyReferences();
            if (!playerListItemPrefab) return;
        }

        // Notīrīt esošos elementus
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("MenuScripts.LobbyPanelManager: Nav spēlētāju, ko attēlot sarakstā");
            return;
        }

        // Pievienot spēlētāju elementus
        foreach (var player in players)
        {
            Debug.Log($"MenuScripts.LobbyPanelManager: Izveidoju UI elementu spēlētājam: {player.PlayerName}");
            try
            {
                var item = Instantiate(playerListItemPrefab, playerListContent);
                if (item)
                {
                    // Iestatīt RectTransform iestatījumus elementam
                    RectTransform itemRT = item.GetComponent<RectTransform>();
                    itemRT.anchorMin = new Vector2(0, 0);
                    itemRT.anchorMax = new Vector2(1, 0);
                    itemRT.sizeDelta = new Vector2(0, 50); // Fiksēts augstums
                    
                    item.SetPlayerInfo(player.PlayerName, player.IsBlueTeam, player.IsReady);
                    Debug.Log($"MenuScripts.LobbyPanelManager: ✓ Veiksmīgi izveidots elements spēlētājam {player.PlayerName}");
                }
                else
                {
                    Debug.LogError("MenuScripts.LobbyPanelManager: ✗ Neizdevās instancēt spēlētāja saraksta elementu");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MenuScripts.LobbyPanelManager: ✗ Kļūda veidojot spēlētāja elementu: {e.Message}");
            }
        }

        // Piespiest izkārtojuma atjaunināšanu
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContent);

        Debug.Log($"MenuScripts.LobbyPanelManager: ✓ Spēlētāju saraksts veiksmīgi atjaunināts ar {players.Count} elementiem");
    }

    public void SetLobbyCode(string code)
    {
        Debug.Log($"SetLobbyCode izsaukta ar: {code}");
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = $"Lobija kods: {code}";
            Debug.Log($"Lobija koda UI atjaunināts uz: {lobbyCodeText.text}");
        }
        else
        {
            Debug.LogError("lobbyCodeText ir null! Nevar attēlot lobija kodu.");
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

                // Atjaunināt vizuālos indikatorus ar labāku atgriezenisko saiti
                if (blueTeamIndicator != null && redTeamIndicator != null)
                {
                    blueTeamIndicator.gameObject.SetActive(team == "Blue");
                    redTeamIndicator.gameObject.SetActive(team == "Red");
                    
                    // Pievienot krāsu atgriezenisko saiti
                    if (team == "Blue")
                    {
                        blueTeamIndicator.color = new Color(0.2f, 0.4f, 1f, 1f); // Spilgti zila
                    }
                    else
                    {
                        redTeamIndicator.color = new Color(1f, 0.2f, 0.2f, 1f); // Spilgti sarkana
                    }
                }

                // Piespiest UI atsvaidzināšanu
                LobbyManager.Instance.RefreshPlayerList();
                
                Debug.Log($"Izvēlēta komanda: {team} 2v2 spēlei - Nepieciešami 4 spēlētāji (2 katrā komandā) vai minimums 2 testēšanai!");
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
        // Iestatīt spēlētāju saraksta ritināšanas skatu
        if (playerListContent)
        {
            // Iestatīt RectTransform enkurus un izmērus
            RectTransform rt = playerListContent.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var vlg = playerListContent.GetComponent<VerticalLayoutGroup>();
            if (!vlg) vlg = playerListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5f;
            vlg.padding = new RectOffset(10, 10, 10, 10); // Palielināta apmale
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

        // Iestatīt tērzēšanas saturu
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
        
        // Nosūtīt ziņu caur LobbyManager
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
            string code = lobbyCodeText.text.Replace("Lobija kods: ", "").Trim();
            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"Nokopēts lobija kods: {code} uz starpliktuvi");
        }
        else
        {
            Debug.LogError("Lobija koda teksta komponente nav atrasta!");
        }
    }

    private void OnReadySelect()
    {
        Debug.Log("Gatavības poga nospiesta");
        
        // KRITISKI: Pārliecinieties, ka Time.timeScale ir normāls
        if (Time.timeScale != 1)
        {
            Debug.LogWarning($"Time.timeScale bija {Time.timeScale}, atiestatot uz 1");
            Time.timeScale = 1;
        }
        
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            if (LobbyManager.Instance != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                LobbyManager.Instance.SetPlayerReady(playerId);
                
                // Atjaunināt gatavības indikatoru ar labāku vizuālo atgriezenisko saiti
                if (readyIndicator != null)
                {
                    bool isReady = LobbyManager.Instance.IsPlayerReady(playerId);
                    readyIndicator.color = isReady ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    
                    // Atjaunināt gatavības pogas tekstu
                    var buttonText = readyButton?.GetComponentInChildren<TMPro.TMP_Text>();
                    if (buttonText != null)
                    {
                        buttonText.text = isReady ? "GATAVS!" : "GATAVS";
                        buttonText.color = isReady ? Color.green : Color.white;
                    }
                }
                
                Debug.Log($"Spēlētāja gatavības statuss: {LobbyManager.Instance.IsPlayerReady(playerId)}");
            }
        }
    }
    
    public void UpdateStartButton(bool canStart)
    {
        if (startMatchButton != null)
        {
            startMatchButton.interactable = IsHost() && canStart;
            
            // Labāka vizuāla atgriezeniskā saite sākšanas pogai
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
                    buttonText.text = "GAIDA HOSTU...";
                    buttonText.color = Color.gray;
                }
                else
                {
                    buttonText.text = "IZVĒLIES KOMANDU UN ESI GATAVS";
                    buttonText.color = Color.yellow;
                }
            }
            
            // Atjaunināt pogas krāsas
            ColorBlock colors = startMatchButton.colors;
            colors.normalColor = canStart ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.highlightedColor = canStart ? new Color(0.3f, 1f, 0.3f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
            startMatchButton.colors = colors;
        }
    }

    public void ForceInitialize()
    {
        Debug.Log("MenuScripts.LobbyPanelManager: ForceInitialize izsaukts");
        
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("MenuScripts.LobbyPanelManager: Iestatīts kā singletona instance");
        }
        
        VerifyReferences();
        SetupScrollViews();
        SetupButtons();
        
        Debug.Log("MenuScripts.LobbyPanelManager: Piespiedu inicializācija pabeigta");
    }

    public void InitializeManager()
    {
        ForceInitialize();
    }
}