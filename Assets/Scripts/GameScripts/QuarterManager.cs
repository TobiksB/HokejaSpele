using Unity.Netcode;
using UnityEngine;
using TMPro; // Pievieno TMPro namespace

namespace HockeyGame.Game
{
    public class QuarterManager : NetworkBehaviour
    {
        public static QuarterManager Instance { get; private set; }
        
        [Header("Spēles iestatījumi")]
        [SerializeField] private float quarterDuration = 20f; // TESTAM: 30 sekundes ceturtdaļā
        [SerializeField] private int totalQuarters = 3;
        
        [Header("UI atsauces")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private QuarterDisplayUI quarterDisplayUI;
        [SerializeField] private QuarterTransitionPanel quarterTransitionPanel;
        [SerializeField] private GameOverPanel gameOverPanel; // Pievieno šo inspektora atsauci

        private NetworkVariable<int> currentQuarter = new NetworkVariable<int>(1);
        private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(20f); // TESTAM: 30 sekundes sākotnēji
        private bool isGameActive = true;

        private void Awake()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            // KRITISKS: Atļaut tikai spēles ainās
            if (currentScene == "MainMenu")
            {
                Debug.LogError($"KRITISKA APTURĒŠANA: QuarterManager MainMenu ainā - IZNĪCINA NEKAVĒJOTIES");
                Destroy(gameObject);
                return;
            }
            
            if (!IsGameScene(currentScene))
            {
                Debug.LogError($"QuarterManager: Nav spēles ainā ({currentScene}) - iznīcina");
                Destroy(gameObject);
                return;
            }

            if (Instance == null)
            {
                Instance = this;
                Debug.Log($"QuarterManager inicializēts ainā: {currentScene}");
                
                // UZLABOTS: Meklēt atkarības tikai ja esam faktiskā spēles ainā ar UI
                if (currentScene != "TrainingMode") // Treniņa režīmam var nebūt pilna UI
                {
                    if (quarterTransitionPanel == null)
                    {
                        quarterTransitionPanel = FindFirstObjectByType<QuarterTransitionPanel>();
                        if (quarterTransitionPanel == null)
                        {
                            Debug.LogWarning("QuarterTransitionPanel nav atrasts, izveido jaunu.");
                            var go = new GameObject("QuarterTransitionPanel");
                            quarterTransitionPanel = go.AddComponent<QuarterTransitionPanel>();
                        }
                    }
                }
                
                // Atrast QuarterDisplayUI ainā
                if (quarterDisplayUI == null)
                {
                    quarterDisplayUI = FindFirstObjectByType<QuarterDisplayUI>();
                }

                // --- Parādīt 1. ceturtdaļu sākumā ---
                if (quarterDisplayUI != null)
                {
                    quarterDisplayUI.SetQuarter(1);
                }

                //  Sākt taimeri nekavējoties
                Debug.Log("QuarterManager: Sāk spēles taimeri automātiski");
                isGameActive = true;
                timeRemaining.Value = quarterDuration;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        //  Pievienota trūkstošā IsGameScene metode
        private bool IsGameScene(string sceneName)
        {
            return sceneName == "TrainingMode" || 
                   sceneName == "GameScene2v2" || 
                   sceneName == "GameScene4v4";
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                timeRemaining.Value = quarterDuration;
                currentQuarter.Value = 1;
                StartQuarter();
            }

            // Abonē izmaiņas UI atjauninājumiem
            timeRemaining.OnValueChanged += OnTimeChanged;
            currentQuarter.OnValueChanged += OnQuarterChanged;

            // Nodrošina, ka UI ir pareizs aktivizējoties
            UpdateQuarterUI();
        }

        public override void OnNetworkDespawn()
        {
            timeRemaining.OnValueChanged -= OnTimeChanged;
            currentQuarter.OnValueChanged -= OnQuarterChanged;
        }

        private void Update()
        {
            //  Atskaites taimeris - laiks iet uz leju
            if (IsServer && isGameActive && timeRemaining.Value > 0)
            {
                timeRemaining.Value -= Time.deltaTime;
                
                if (timeRemaining.Value <= 0)
                {
                    timeRemaining.Value = 0;
                    EndQuarter();
                }
            }
        }

        private void StartQuarter()
        {
            if (!IsServer) return;

            Debug.Log($"Sākas {currentQuarter.Value}. ceturtdaļa");
            isGameActive = true;
            timeRemaining.Value = quarterDuration;

            //  Nodrošināt, ka ceturtdaļas rādītājs tiek atjaunināts katras ceturtdaļas sākumā ---
            UpdateQuarterUI();

            // Atjaunot UI
            UpdateTimerUI();

            //  Vienmēr atiestatīt ripu ceturtdaļas sākumā, īpaši 2. un 3. ceturtdaļā ---
            StartCoroutine(BruteForceResetPuck());
        }

        //  Pilnīgi uzticama ripas atiestate ar skaidru atkļūdošanu ---
        private System.Collections.IEnumerator BruteForceResetPuck() 
        {
            Debug.LogWarning($"QuarterManager: DRASTISKA ripas atiestate Q{currentQuarter.Value} sākumā");
            
            // Gaidīt, lai fizikas sistēma stabilizētos
            yield return new WaitForSeconds(1.0f);
            
            // Atrast visas ripas
            var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (allPucks.Length == 0) {
                Debug.LogError("QuarterManager: Nav atrasta ripa, ko atiestatīt!");
                yield break;
            }
            
            GameObject puck = allPucks[0].gameObject;
            Debug.Log($"QuarterManager: Atrasta ripa {puck.name} pozīcijā {puck.transform.position}");
            
            // Piespiest atlaist ripu no visiem spēlētājiem
            var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
            foreach (var player in allPlayers) {
                if (player.HasPuck()) {
                    Debug.Log($"QuarterManager: Piespiež spēlētāju {player.name} atlaist ripu");
                    player.ForceReleasePuckForSteal();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            
            // Notīrīt visus PuckFollower
            var puckFollower = puck.GetComponent<PuckFollower>();
            if (puckFollower != null) {
                Debug.Log("QuarterManager: Aptur PuckFollower");
                puckFollower.StopFollowing();
                puckFollower.enabled = false;
            }
            
            // Teleportēt uz centru
            Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
            Debug.Log($"QuarterManager: Teleportē ripu no {puck.transform.position} uz {centerPos}");
            
            puck.transform.position = centerPos;
            puck.transform.rotation = Quaternion.identity;
            
            // Atiestatīt fiziku un rigidbody
            var rb = puck.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = centerPos;
                rb.rotation = Quaternion.identity;
                Debug.Log($"QuarterManager: Atiestatīts ripas rigidbody uz {centerPos}");
            }
            
            // Nodrošināt, ka sadursmes detektors ir ieslēgts
            var col = puck.GetComponent<Collider>();
            if (col != null) {
                col.enabled = true;
                Debug.Log("QuarterManager: Ieslēgts ripas sadursmes detektors");
            }
            
            // Atiestatīt turēšanas stāvokli
            var puckComponent = puck.GetComponent<Puck>();
            if (puckComponent != null) {
                puckComponent.SetHeld(false);
                Debug.Log("QuarterManager: Iestatīts ripas turēšanas stāvoklis uz false");
            }
            
            // Paziņot visiem klientiem par ripas pozīcijas atiestatīšanu
            if (puckComponent != null && IsServer) {
                var puckNetObj = puck.GetComponent<NetworkObject>();
                if (puckNetObj != null) {
                    SetPuckPositionClientRpc(puckNetObj.NetworkObjectId, centerPos);
                    Debug.Log($"QuarterManager: Nosūtīts SetPuckPositionClientRpc klientiem ripai {puckNetObj.NetworkObjectId}");
                }
            }
            
            Debug.LogWarning($"QuarterManager: RIPAS ATIESTATĪŠANA PABEIGTA - Tagad pozīcijā {puck.transform.position}");
        }

        [ClientRpc]
        private void SetPuckPositionClientRpc(ulong puckNetworkId, Vector3 position)
        {
            Debug.Log($"QuarterManager: KLIENTS saņēma SetPuckPositionClientRpc ripai {puckNetworkId}");
            
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var netObj))
            {
                var puck = netObj.gameObject;
                Debug.Log($"QuarterManager: KLIENTS atrada ripu {puck.name}, iestata pozīciju uz {position}");
                
                // Iestatīt pozīciju
                puck.transform.position = position;
                puck.transform.rotation = Quaternion.identity;
                
                // Atiestatīt fiziku
                var rb = puck.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = position;
                    rb.rotation = Quaternion.identity;
                }
                
                // Pārtraukt sekošanu
                var puckFollower = puck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }
                
                // Atiestatīt turēšanas stāvokli
                var puckComponent = puck.GetComponent<Puck>();
                if (puckComponent != null)
                {
                    puckComponent.SetHeld(false);
                }
                
                Debug.LogWarning($"QuarterManager: KLIENTA ripas atiestatīšana pabeigta pozīcijā {position}");
            }
            else
            {
                Debug.LogError($"QuarterManager: KLIENTS nevarēja atrast ripu ar NetworkObjectId {puckNetworkId}");
            }
        }
        
        private void EndQuarter()
        {
            if (!IsServer) return;

            isGameActive = false;

            // Atiestatīt visus spēlētājus uz sākumpozīcijām ceturtdaļas beigās
            ResetAllPlayersToSpawnPoints();

            //  Izmantot to pašu loģiku kā GoalTrigger, lai atiestatītu ripu ---
            ResetPuckToCenterGoalTriggerStyle();

            if (currentQuarter.Value >= totalQuarters)
            {
                ShowGameOverPanelClientRpc();
            }
            else
            {
                StartCoroutine(TransitionToNextQuarter());
            }
        }

        //  Izmantot tieši to pašu loģiku kā GoalTrigger ripas atiestatīšanai ---
        private void ResetPuckToCenterGoalTriggerStyle()
        {
            StartCoroutine(ResetPuckCoroutineGoalStyle());
        }

        private System.Collections.IEnumerator ResetPuckCoroutineGoalStyle()
        {
            yield return new WaitForSeconds(1.5f); // Pagaidīt spēlētāju atiestatīšanu un tīkla sinhronizāciju

            //  Vairākas stratēģijas ripas atrašanai
            GameObject puckObject = FindPuckByAllMeans();
            
            if (puckObject == null)
            {
                Debug.LogError("QuarterManager: Nav atrasta ripa pēc pilnīgas meklēšanas. Nevar atiestatīt!");
                yield break;
            }
            
            Debug.Log($"QuarterManager: Atrasta ripa: {puckObject.name} pozīcijā {puckObject.transform.position}");
            
            var puckComponent = puckObject.GetComponent<Puck>();

            // Nodrošināt, ka ripa ir pilnīgi brīva pirms atiestatīšanas
            if (puckComponent != null)
            {
                puckComponent.SetHeld(false);

                // Notīrīt no jebkura spēlētāja, kas joprojām to tur
                var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
                foreach (var player in allPlayers)
                {
                    if (player.GetCurrentPuck() == puckComponent)
                    {
                        player.ForceReleasePuckForSteal();
                        Debug.Log($"QuarterManager: Notīrīta atlikušā atsauce no {player.name}");
                    }
                }
            }

            // Apturēt visas PuckFollower komponentes
            var puckFollower = puckObject.GetComponent<PuckFollower>();
            if (puckFollower != null)
            {
                puckFollower.StopFollowing();
                puckFollower.enabled = false;
            }

            // Pareiza ripas atiestate uz centru (tāpat kā pēc vārtiem)
            Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
            puckObject.transform.SetParent(null);
            puckObject.transform.position = centerPos;
            puckObject.transform.rotation = Quaternion.identity;

            var rb = puckObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = centerPos;
            }

            var col = puckObject.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }

            // Izmantot Puck.ResetToCenter metodi, ja tā ir pieejama (tīkla sinhronizācijai)
            if (puckComponent != null && puckComponent.IsServer)
            {
                try
                {
                    var resetMethod = puckComponent.GetType().GetMethod("ResetToCenter");
                    if (resetMethod != null)
                    {
                        resetMethod.Invoke(puckComponent, null);
                        Debug.Log("QuarterManager: Izmantota Puck.ResetToCenter() metode tīkla sinhronizācijai");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"QuarterManager: Neizdevās izmantot Puck.ResetToCenter(): {e.Message}");
                }
            }

            Debug.Log($"QuarterManager: Ripa atiestatīta uz centru pēc ceturtdaļas pozīcijā {centerPos}");
        }
        
        // Jauna metode ripas atrašanai, izmantojot vairākas meklēšanas stratēģijas
        private GameObject FindPuckByAllMeans()
        {
            Debug.Log("QuarterManager: Sāk vispusīgu ripas meklēšanu, izmantojot vairākas metodes...");
            
            // 1. metode: FindObjectsByType<Puck>
            var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (allPucks != null && allPucks.Length > 0)
            {
                Debug.Log($"QuarterManager: Atrastas {allPucks.Length} ripas, izmantojot FindObjectsByType<Puck>");
                return allPucks[0].gameObject;
            }
            
            // 2. metode: FindGameObjectsWithTag
            var taggedObjects = GameObject.FindGameObjectsWithTag("Puck");
            if (taggedObjects != null && taggedObjects.Length > 0)
            {
                Debug.Log($"QuarterManager: Atrasti {taggedObjects.Length} objekti ar tagu 'Puck'");
                return taggedObjects[0];
            }
            
            // 3. metode: Atrast objektus ripas slānī
            var allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.layer == LayerMask.NameToLayer("Puck"))
                {
                    Debug.Log($"QuarterManager: Atrasts objekts {obj.name} 'Puck' slānī");
                    return obj;
                }
            }
            
            // 4. metode: Atrast pēc nosaukuma satur
            foreach (var obj in allObjects)
            {
                if (obj.name.ToLower().Contains("puck"))
                {
                    Debug.Log($"QuarterManager: Atrasts objekts ar 'puck' nosaukumā: {obj.name}");
                    return obj;
                }
            }
            
            // 5. metode: Pēdējā iespēja - izveidot jaunu ripu
            Debug.LogWarning("QuarterManager: Nav atrasta ripa! Izveido jaunu ripu centra pozīcijā.");
            var newPuck = new GameObject("ĀrkārtasRipa");
            newPuck.transform.position = new Vector3(0f, 0.71f, 0f);
            newPuck.transform.rotation = Quaternion.identity;
            newPuck.tag = "Puck";
            newPuck.layer = LayerMask.NameToLayer("Puck");
            var rb = newPuck.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            newPuck.AddComponent<SphereCollider>().radius = 0.5f;
            
            // Pievienot vienkāršu Puck komponenti
            if (newPuck.GetComponent<Puck>() == null)
            {
                newPuck.AddComponent<Puck>();
            }
            
            return newPuck;
        }

        // --- Parādīt GameOverPanel uz VISIEM klientiem ---
        [ClientRpc]
        private void ShowGameOverPanelClientRpc()
        {
            Debug.Log("QuarterManager: [ClientRpc] ShowGameOverPanelClientRpc izsaukts klientā");
            
            // Piespiest tūlītēju izpildi galvenajā pavedienā
            StartCoroutine(ShowGameOverPanelCoroutine());
        }

        private System.Collections.IEnumerator ShowGameOverPanelCoroutine()
        {
            yield return null; // Gaidīt vienu kadru
            
            Debug.Log("QuarterManager: [Coroutine] Sākas GameOver paneļa meklēšana un attēlošana");
            
            // Iegūt rezultātus no visiem iespējamiem avotiem
            int redScore = 0, blueScore = 0;
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                redScore = scoreManager.GetRedScore();
                blueScore = scoreManager.GetBlueScore();
                Debug.Log($"QuarterManager: Iegūti rezultāti no ScoreManager - Sarkanā: {redScore}, Zilā: {blueScore}");
            }

            // Mēģināt vairākus veidus, kā atrast GameOverPanel
            GameOverPanel panelToUse = gameOverPanel;
            
            if (panelToUse == null)
            {
                panelToUse = FindFirstObjectByType<GameOverPanel>();
                Debug.Log($"QuarterManager: FindFirstObjectByType rezultāts: {(panelToUse != null ? panelToUse.name : "null")}");
            }
            
            if (panelToUse == null)
            {
                var panelGO = GameObject.Find("GameOverPanel");
                if (panelGO != null)
                {
                    panelToUse = panelGO.GetComponent<GameOverPanel>();
                    Debug.Log($"QuarterManager: Atrasts ar GameObject.Find: {panelGO.name}");
                }
            }
            
            if (panelToUse == null)
            {
                // Meklēt visos canvas
                var allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var canvas in allCanvases)
                {
                    var panel = canvas.GetComponentInChildren<GameOverPanel>(true);
                    if (panel != null)
                    {
                        panelToUse = panel;
                        Debug.Log($"QuarterManager: Atrasts GameOverPanel kanvā: {canvas.name}");
                        break;
                    }
                }
            }

            if (panelToUse != null)
            {
                Debug.Log($"QuarterManager: Atrasts GameOverPanel: {panelToUse.name}, aktivizē...");
                
                // Vispirms piespiedu kārtā aktivizēt paneļa spēles objektu
                panelToUse.gameObject.SetActive(true);
                
                // Gaidīt kadru aktivizācijai
                yield return null;
                
                // Tagad parādīt spēles beigu ekrānu
                panelToUse.ShowGameOver(redScore, blueScore);
                
                Debug.Log($"QuarterManager: GameOverPanel aktivizēts un ShowGameOver izsaukts!");
            }
            else
            {
                Debug.LogError("QuarterManager: Nevarēja atrast GameOverPanel nekur! Izveido rezerves variantu...");
                CreateFallbackGameOverUI(redScore, blueScore);
            }
        }

        // --- Izveidot vienkāršu rezerves UI, ja GameOverPanel trūkst ---
        private void CreateFallbackGameOverUI(int redScore, int blueScore)
        {
            var tempPanel = new GameObject("FallbackGameOverPanel");
            var canvas = tempPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            var canvasScaler = tempPanel.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            
            tempPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            var background = new GameObject("Background").AddComponent<UnityEngine.UI.Image>();
            background.transform.SetParent(tempPanel.transform);
            background.color = new Color(0, 0, 0, 0.8f);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            var text = new GameObject("GameOverText");
            text.transform.SetParent(tempPanel.transform);
            var textComponent = text.AddComponent<TMPro.TextMeshProUGUI>();
            
            string winner = "Neizšķirts!";
            if (redScore > blueScore) winner = "Sarkanā komanda uzvar!";
            else if (blueScore > redScore) winner = "Zilā komanda uzvar!";
            
            textComponent.text = $"{winner}\nSarkanā: {redScore} - Zilā: {blueScore}\n\nSpēle beigusies";
            textComponent.fontSize = 48;
            textComponent.color = Color.white;
            textComponent.alignment = TMPro.TextAlignmentOptions.Center;
            
            var rectTransform = text.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            
            Debug.Log("QuarterManager: Veiksmīgi izveidots rezerves spēles beigu UI");
        }

        //  Atiestatīt visus spēlētājus uz to sākumpozīcijām (tāpat kā pēc vārtiem) ---
        private void ResetAllPlayersToSpawnPoints()
        {
            var gameNetMgr = FindFirstObjectByType<GameNetworkManager>();
            if (gameNetMgr == null)
            {
                Debug.LogWarning("QuarterManager: GameNetworkManager nav atrasts, nevar atiestatīt spēlētāju pozīcijas.");
                return;
            }

            var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
            if (players == null || players.Length == 0)
            {
                Debug.LogWarning("QuarterManager: Nav atrasti PlayerMovement objekti, ko atiestatīt pozīcijās.");
                return;
            }

            foreach (var player in players)
            {
                if (player == null)
                {
                    Debug.LogWarning("QuarterManager: Atrasts null PlayerMovement spēlētāju masīvā.");
                    continue;
                }

                var netObj = player.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogWarning($"QuarterManager: Spēlētājam {player.name} nav NetworkObject.");
                    continue;
                }

                ulong clientId = netObj.OwnerClientId;
                // Mēģināt iegūt komandu kā tekstu
                string team = "Red";
                try
                {
                    if (player.GetType().GetProperty("Team") != null)
                    {
                        team = player.GetType().GetProperty("Team").GetValue(player)?.ToString() ?? "Red";
                    }
                    else if (player.GetType().GetField("team") != null)
                    {
                        team = player.GetType().GetField("team").GetValue(player)?.ToString() ?? "Red";
                    }
                    else if (player.GetType().GetField("networkTeam", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null)
                    {
                        var networkTeamVar = player.GetType().GetField("networkTeam", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(player);
                        if (networkTeamVar != null)
                        {
                            var valueProp = networkTeamVar.GetType().GetProperty("Value");
                            if (valueProp != null)
                            {
                                var enumValue = valueProp.GetValue(networkTeamVar);
                                team = enumValue?.ToString() ?? "Red";
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"QuarterManager: Kļūda nosakot komandu spēlētājam {player.name}: {e.Message}");
                }

                Vector3 spawnPos = player.transform.position;
                try
                {
                    var method = gameNetMgr.GetType().GetMethod("GetSpawnPositionFromInspector", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        var result = method.Invoke(gameNetMgr, new object[] { clientId, team });
                        if (result is Vector3 pos)
                        {
                            spawnPos = pos;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"QuarterManager: Kļūda iegūstot sākuma pozīciju spēlētājam {player.name}: {e.Message}");
                }

                Quaternion spawnRot = team == "Blue"
                    ? Quaternion.Euler(0, 90, 0)
                    : Quaternion.Euler(0, -90, 0);

                // Teleportēt spēlētāju uz sākuma pozīciju
                player.transform.position = spawnPos;
                player.transform.rotation = spawnRot;
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = spawnPos;
                    rb.rotation = spawnRot;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            Debug.Log("QuarterManager: Visi spēlētāji atiestatīti uz sākuma pozīcijām ceturtdaļas beigās.");
        }

        private System.Collections.IEnumerator TransitionToNextQuarter()
        {
            // Parādīt pārejas paneli
            if (quarterTransitionPanel != null)
            {
                quarterTransitionPanel.ShowTransition(currentQuarter.Value + 1);
            }

            yield return new WaitForSeconds(3f); // 3 sekunžu pāreja

            if (IsServer)
            {
                //  Izmantot ServerRpc, lai atjauninātu ceturtdaļu un taimeri visiem klientiem ---
                SetNextQuarterServerRpc();
            }

            // Paslēpt pārejas paneli
            if (quarterTransitionPanel != null)
            {
                quarterTransitionPanel.HideTransition();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetNextQuarterServerRpc()
        {
            currentQuarter.Value++;
            timeRemaining.Value = quarterDuration;
            isGameActive = true;
            UpdateQuarterUI();
            UpdateTimerUI();
        }

        private void EndGame()
        {
            Debug.Log("Spēle beigusies - visas ceturtdaļas pabeigtas");
            isGameActive = false;

            //  Parādīt spēles beigu paneli un uzvarētāju ---
            ShowEndGamePanel();

            //  Sākt koroutīnu, lai atgrieztos galvenajā izvēlnē un izslēgtu tīklošanu ---
            StartCoroutine(EndGameSequence());
        }

        //  Parādīt spēles beigu paneli un uzvarētāju ---
        private void ShowEndGamePanel()
        {
            int redScore = 0, blueScore = 0;
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                redScore = scoreManager.GetRedScore();
                blueScore = scoreManager.GetBlueScore();
            }

            string winner = "Neizšķirts!";
            if (redScore > blueScore) winner = "Sarkanā komanda uzvar!";
            else if (blueScore > redScore) winner = "Zilā komanda uzvar!";

            // Mēģināt atrast GameOverPanel vai līdzīgu UI
            var gameOverPanel = FindFirstObjectByType<GameOverPanel>();
            if (gameOverPanel != null)
            {
                // Parādīt tikai uzvarētāja tekstu un pogas
                gameOverPanel.ShowWinner(winner);
            }
            else
            {
                // Rezerves variants: žurnalēt konsolē
                Debug.Log($"SPĒLE BEIGUSIES! {winner}");
            }
        }

        //  Koroutīna, lai atgrieztos galvenajā izvēlnē un izslēgtu tīklošanu ---
        private System.Collections.IEnumerator EndGameSequence()
        {
            // Gaidīt 5 sekundes, lai parādītu paneli
            yield return new WaitForSeconds(5f);

            // Izslēgt resursdatoru/klientu un atgriezties galvenajā izvēlnē
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }

            // Gaidīt brīdi, lai nodrošinātu izslēšanu pirms ainas ielādes
            yield return new WaitForSeconds(0.5f);

            //  Vienmēr ielādēt MainMenu gan resursdatoram, gan klientam ---
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");

            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }
        }

        private void OnTimeChanged(float previousValue, float newValue)
        {
            UpdateTimerUI();
        }

        private void OnQuarterChanged(int previousValue, int newValue)
        {
            //  Atiestatīt taimeri un aktivizēt spēli visiem klientiem, kad mainās ceturtdaļa ---
            timeRemaining.Value = quarterDuration;
            isGameActive = true;
            UpdateQuarterUI();
            UpdateTimerUI();
        }

        private void UpdateTimerUI()
        {
            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(timeRemaining.Value / 60f);
                int seconds = Mathf.FloorToInt(timeRemaining.Value % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
            }
            
            // Atjaunināt arī ScoreManager, ja tas eksistē
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                //  Izsaukt UpdateScoreDisplay bez parametriem, nevis ar int rezultātiem
                scoreManager.UpdateScoreDisplay();
            }
        }

        private void UpdateQuarterUI()
        {
            // Atjaunināt ceturtdaļas rādītāju UI
            Debug.Log($"Pašreizējā ceturtdaļa: {currentQuarter.Value}/{totalQuarters}");
            if (quarterDisplayUI == null)
            {
                quarterDisplayUI = FindFirstObjectByType<QuarterDisplayUI>();
            }
            if (quarterDisplayUI != null)
            {
                quarterDisplayUI.SetQuarter(currentQuarter.Value);
            }
        }

        // Publiskās metodes ārējai pieejai
        public int GetCurrentQuarter() => currentQuarter.Value;
        public float GetTimeRemaining() => timeRemaining.Value;
        public bool IsGameActive() => isGameActive;
        public float GetQuarterDuration() => quarterDuration;

        [ServerRpc(RequireOwnership = false)]
        public void PauseGameServerRpc()
        {
            isGameActive = false;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ResumeGameServerRpc()
        {
            isGameActive = true;
        }

        //  Pievienot trūkstošo EndCurrentQuarter metodi
        public void EndCurrentQuarter()
        {
            if (!IsServer) return;
            
            Debug.Log($"Manuāli beigt pašreizējo ceturtdaļu {currentQuarter.Value}");
            EndQuarter();
        }

        private void OnQuarterEnd()
        {
            // Apstrādāt ceturtdaļas beigu loģiku
            var scoreManager = FindFirstObjectByType<ScoreManager>();
            if (scoreManager != null)
            {
                //  Izsaukt UpdateScoreDisplay bez parametriem, nevis ar int
                scoreManager.UpdateScoreDisplay();
                Debug.Log($"QuarterManager: Ceturtdaļa {currentQuarter} beigusies, rezultātu attēlojums atjaunināts");
            }
            
        }
    }
}
