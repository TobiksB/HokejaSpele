using UnityEngine;
using Unity.Netcode;

namespace HockeyGame.Game
{
    // Klase, kas atbild par vārtu trigeršanas zonu un punktu skaitīšanu
    public class GoalTrigger : NetworkBehaviour
    {        [Header("Vārtu konfigurācija")]
        [SerializeField] private bool isBlueTeamGoal = false; // true, ja šie ir Zilās komandas vārti (Sarkanā komanda gūs punktus šeit)
        [SerializeField] private string goalName = "Goal"; // Atkļūdošanai

        [Header("Efekti")]
        [SerializeField] private ParticleSystem goalEffect; // Daļiņu efekts, kas tiks atskaņots, kad tiek gūti vārti
        [SerializeField] private AudioSource goalSound; // Skaņa, kas tiks atskaņota, kad tiek gūti vārti

        [Header("Atkļūdošana")]
        [SerializeField] private bool enableDebugLogs = true; // Iespējot papildu žurnāla ziņojumus

        private bool goalScored = false; // Norāda, vai vārti tikko gūti
        private float goalCooldown = 2f; // Laiks sekundēs starp pieļaujamiem vārtiem
        private float lastGoalTime = 0f; // Pēdējo vārtu laiks        
        
        private void Awake()
        {
            // Nodrošina, ka šim ir trigera sadursmes detektors - neveido, ja jau eksistē
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;

                if (enableDebugLogs)
                {
                    Debug.Log($"GoalTrigger {goalName}: Atrasts esošs sadursmes detektors ar tipu {collider.GetType().Name}");
                    if (collider is BoxCollider boxCollider)
                    {
                        Debug.Log($"  Kastes izmērs: {boxCollider.size}, Centrs: {boxCollider.center}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"GoalTrigger {goalName}: Nav atrasts sadursmes detektors! Lūdzu, piešķiriet sadursmes detektoru skatā.");
            }

            // Nodrošina pareizu tagu un slāni
            if (!gameObject.CompareTag("Goal"))
            {
                gameObject.tag = "Goal";
                if (enableDebugLogs)
                {
                    Debug.Log($"GoalTrigger {goalName}: Iestatīts tags uz 'Goal'");
                }
            }

            // Iestata vārtu slāni, ja tāds eksistē
            int goalLayer = LayerMask.NameToLayer("Goal");
            if (goalLayer != -1)
            {
                gameObject.layer = goalLayer;
                if (enableDebugLogs)
                {
                    Debug.Log($"GoalTrigger {goalName}: Iestatīts slānis uz 'Goal'");
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"GoalTrigger {goalName}: Inicializēts. VaiZilieVārti: {isBlueTeamGoal}");
            }
        }

        // Izsaucas, kad kaut kas ienāk vārtu trigera zonā
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return; // Darbojas tikai uz servera
            if (goalScored || (Time.time - lastGoalTime) < goalCooldown) return; // Ignorē, ja nesen gūti vārti

            if (enableDebugLogs)
            {
                Debug.Log($"GoalTrigger: Objekts iegājis vārtos: {other.name}");
            }

            if (other.CompareTag("Puck"))
            {
                HandleGoalScored(other.gameObject); // Apstrādā vārtu gūšanu, ja tas ir ripa
            }
        }

        // Apstrādā vārtu gūšanu
        private void HandleGoalScored(GameObject puck)
        {
            if (!IsServer) return;

            goalScored = true;
            lastGoalTime = Time.time;

            // Nosaka, kura komanda guva vārtus
            string scoringTeam = isBlueTeamGoal ? "Red" : "Blue";
            Debug.Log($"GoalTrigger: VĀRTI! {scoringTeam} komanda guva punktus vārtos {goalName}!");

            // PIEVIENOTS: Atjaunina rezultātu
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                if (scoringTeam == "Red")
                    scoreManager.AddRedScore();
                else
                    scoreManager.AddBlueScore();
            }
            else
            {
                Debug.LogWarning("GoalTrigger: ScoreManager.Instance ir null, nevar atjaunināt rezultātu!");
            }

            // Atskaņo efektus visiem klientiem
            PlayGoalEffectsClientRpc();

            // Atjauno visu spēlētāju pozīcijas atbilstoši viņu komandām
            RespawnAllPlayersAfterGoal();

            // Atjauno ripas pozīciju, izmantojot strādājošo koroutīnu
            StartCoroutine(ResetPuckAfterGoal(puck, scoringTeam));

            // Atjauno vārtu dzesēšanas laiku pēc aizkaves
            StartCoroutine(ResetGoalCooldown());
        }

        // Klienta RPC metode, kas atskaņo efektus visiem klientiem
        [ClientRpc]
        private void PlayGoalEffectsClientRpc()
        {
            if (goalEffect != null) goalEffect.Play();
            if (goalSound != null) goalSound.Play();
        }

        // Atjauno visu spēlētāju pozīcijas pēc vārtiem
        private void RespawnAllPlayersAfterGoal()
        {
            var gameNetMgr = GameNetworkManager.Instance;
            if (gameNetMgr == null)
            {
                Debug.LogError("GoalTrigger: GameNetworkManager.Instance ir null!");
                return;
            }

            // Atjauno katru pievienoto spēlētāju
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                ulong clientId = client.ClientId;
                string team = gameNetMgr.GetTeamForClient(clientId);
                Vector3 spawnPos = gameNetMgr.GetSpawnPositionFromInspector(clientId, team);

                var playerObj = client.PlayerObject != null ? client.PlayerObject.gameObject : null;
                if (playerObj == null)
                {
                    Debug.LogWarning($"GoalTrigger: Nav spēlētāja objekta klientam {clientId}");
                    continue;
                }

                // Atjauno spēlētāja pozīciju un rotāciju
                var rb = playerObj.GetComponent<Rigidbody>();
                playerObj.transform.position = spawnPos;
                playerObj.transform.rotation = team == "Blue"
                    ? Quaternion.Euler(0, 90, 0)
                    : Quaternion.Euler(0, -90, 0);

                // Atjauno arī fizisko ķermeni, ja tas pastāv
                if (rb != null)
                {
                    rb.position = spawnPos;
                    rb.rotation = playerObj.transform.rotation;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Debug.Log($"GoalTrigger: Atjaunots spēlētājs {clientId} ({team} komanda) uz {spawnPos}");
            }
        }

        // Koroutīna, kas atjauno ripas pozīciju pēc vārtiem
        private System.Collections.IEnumerator ResetPuckAfterGoal(GameObject puck, string goalType)
        {
            // Nogaida nelielu laiku, lai atskaņotu efektus
            yield return new WaitForSeconds(1.5f);
            
            // Ja ripa kaut kā kļuvusi null, mēģina atrast to skatā
            if (puck == null)
            {
                Debug.LogWarning("GoalTrigger: Ripa kļuva null atiestatīšanas laikā, meklē ripu skatā");
                var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
                if (allPucks.Length > 0)
                {
                    puck = allPucks[0].gameObject;
                }
            }
            
            if (puck != null)
            {
                var puckComponent = puck.GetComponent<Puck>();
                
                // KRITISKS: Nodrošina, ka ripa ir pilnīgi brīva pirms atiestatīšanas
                if (puckComponent != null)
                {
                    // Piespiedu kārtā notīra visus turēšanas stāvokļus
                    puckComponent.SetHeld(false);
                    
                    // Notīra no jebkura spēlētāja, kas joprojām tur ripu
                    var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
                    foreach (var player in allPlayers)
                    {
                        if (player.GetCurrentPuck() == puckComponent)
                        {
                            player.ForceReleasePuckForSteal();
                            if (enableDebugLogs)
                            {
                                Debug.Log($"GoalTrigger: Notīrīta atlikušā atsauce no {player.name}");
                            }
                        }
                    }
                }
                
                // Aptur jebkādas PuckFollower komponentes
                var puckFollower = puck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }
                
                // LABOTS: Pareiza ripas atiestatīšana uz centru
                Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
                puck.transform.SetParent(null);
                puck.transform.position = centerPos;
                puck.transform.rotation = Quaternion.identity;
                
                // Atjauno ripas fizikas īpašības
                var rb = puck.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = centerPos; // Piespiedu kārtā iestata arī fizikas pozīciju
                }
                
                // Iespējo sadursmes detektoru
                var col = puck.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = true;
                }
                
                // PIEVIENOTS: Izmanto Puck.ResetToCenter metodi, ja tā ir pieejama (tīkla sinhronizācijai)
                if (puckComponent != null && puckComponent.IsServer)
                {
                    try
                    {
                        var resetMethod = puckComponent.GetType().GetMethod("ResetToCenter");
                        if (resetMethod != null)
                        {
                            resetMethod.Invoke(puckComponent, null);
                            if (enableDebugLogs)
                            {
                                Debug.Log("GoalTrigger: Izmantota Puck.ResetToCenter() metode tīkla sinhronizācijai");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"GoalTrigger: Neizdevās izmantot Puck.ResetToCenter(): {e.Message}");
                    }
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log($"GoalTrigger: Ripa atiestatīta uz centru pēc {goalType} vārtiem pozīcijā {centerPos}");
                }
            }
            else
            {
                Debug.LogError("GoalTrigger: Nevarēja atrast ripu, ko atiestatīt!");
            }
        }

        // Koroutīna, kas atjauno vārtu dzesēšanas laiku
        private System.Collections.IEnumerator ResetGoalCooldown()
        {
            yield return new WaitForSeconds(goalCooldown);
            goalScored = false; // Atkal atļauj skaitīt vārtus
        }
    }
}
