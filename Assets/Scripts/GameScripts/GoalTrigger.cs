using UnityEngine;
using Unity.Netcode;

namespace HockeyGame.Game
{
    public class GoalTrigger : NetworkBehaviour
    {
        [Header("Goal Configuration")]
        [SerializeField] private bool isBlueTeamGoal = false; // true if this is Blue team's goal (Red scores here)
        [SerializeField] private string goalName = "Goal"; // For debugging
        
        [Header("Effects")]
        [SerializeField] private ParticleSystem goalEffect;
        [SerializeField] private AudioSource goalSound;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private bool goalScored = false;
        private float goalCooldown = 2f;
        private float lastGoalTime = 0f;

        private void Awake()
        {
            // Ensure this has a trigger collider - don't create if already exists
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"GoalTrigger {goalName}: Found existing collider of type {collider.GetType().Name}");
                    if (collider is BoxCollider boxCollider)
                    {
                        Debug.Log($"  Box size: {boxCollider.size}, Center: {boxCollider.center}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"GoalTrigger {goalName}: No collider found! Please assign a collider in the scene.");
            }
            
            // Ensure proper tag and layer
            if (!gameObject.CompareTag("Goal"))
            {
                gameObject.tag = "Goal";
                if (enableDebugLogs)
                {
                    Debug.Log($"GoalTrigger {goalName}: Set tag to 'Goal'");
                }
            }
            
            // Set goal layer if exists
            int goalLayer = LayerMask.NameToLayer("Goal");
            if (goalLayer != -1)
            {
                gameObject.layer = goalLayer;
                if (enableDebugLogs)
                {
                    Debug.Log($"GoalTrigger {goalName}: Set layer to 'Goal'");
                }
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"GoalTrigger {goalName}: Initialized. IsBlueGoal: {isBlueTeamGoal}");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Only process on server
            if (!IsServer) return;
            
            // Prevent multiple goals in quick succession
            if (goalScored || (Time.time - lastGoalTime) < goalCooldown) return;

            if (enableDebugLogs)
            {
                Debug.Log($"GoalTrigger {goalName}: Trigger entered by {other.gameObject.name} (Tag: {other.tag}, Layer: {other.gameObject.layer})");
            }

            // Check if it's the puck
            if (other.CompareTag("Puck"))
            {
                ProcessGoal(other.gameObject);
            }
        }

        private void ProcessGoal(GameObject puck)
        {
            if (goalScored) return;
            
            goalScored = true;
            lastGoalTime = Time.time;
            
            // Determine which team scored
            // If puck enters Blue team's goal, Red team scores (and vice versa)
            bool redTeamScored = isBlueTeamGoal;
            string scoringTeam = redTeamScored ? "Red" : "Blue";
            string goalTeam = isBlueTeamGoal ? "Blue" : "Red";
            
            if (enableDebugLogs)
            {
                Debug.Log($"GOAL SCORED! {scoringTeam} team scored in {goalTeam} team's goal!");
            }

            // Play effects on all clients
            PlayGoalEffectsClientRpc(scoringTeam);
            
            // Update score through ScoreManager
            UpdateScore(redTeamScored);

            // ADDED: Reset all players to spawn points
            ResetAllPlayersToSpawnPoints();

            // Reset puck position after delay
            StartCoroutine(ResetPuckAfterGoal(puck));
            
            // Reset goal state after cooldown
            Invoke(nameof(ResetGoalState), goalCooldown);
        }

        private void UpdateScore(bool redTeamScored)
        {
            if (ScoreManager.Instance != null)
            {
                string teamName = redTeamScored ? "Red" : "Blue";
                
                Debug.Log($"🎯 GoalTrigger: Calling ScoreManager to add point for {teamName} team");
                
                if (redTeamScored)
                {
                    ScoreManager.Instance.AddRedScore();
                    Debug.Log($"🔴 GoalTrigger: Red team scored! New score should be: {ScoreManager.Instance.GetRedScore()}");
                }
                else
                {
                    ScoreManager.Instance.AddBlueScore();
                    Debug.Log($"🔵 GoalTrigger: Blue team scored! New score should be: {ScoreManager.Instance.GetBlueScore()}");
                }
                
                // FIXED: Force UI update after scoring
                ScoreManager.Instance.UpdateScoreDisplay();
                
                Debug.Log($"🏒 GoalTrigger: Score update complete - Red: {ScoreManager.Instance.GetRedScore()}, Blue: {ScoreManager.Instance.GetBlueScore()}");
            }
            else
            {
                Debug.LogError("🚨 GoalTrigger: ScoreManager.Instance is null! Cannot update score.");
            }
        }

        [ClientRpc]
        private void PlayGoalEffectsClientRpc(string scoringTeam)
        {
            // Play particle effect
            if (goalEffect != null)
            {
                goalEffect.Play();
            }
            
            // Play sound effect
            if (goalSound != null)
            {
                goalSound.Play();
            }
            
            // Log goal on all clients
            Debug.Log($"🏒 GOAL! {scoringTeam} team scored!");
            
            // You can add more effects here like screen shake, UI animations, etc.
        }

        private System.Collections.IEnumerator ResetPuckAfterGoal(GameObject puck)
        {
            yield return new WaitForSeconds(1.5f);
            
            // Reset puck to center position
            var puckComponent = puck.GetComponent<Puck>();
            if (puckComponent != null && puckComponent.IsServer)
            {
                puckComponent.ResetToCenter();
                if (enableDebugLogs)
                {
                    Debug.Log("GoalTrigger: Reset puck via Puck.ResetToCenter()");
                }
            }
            else
            {
                // Fallback: direct position reset
                puck.transform.position = new Vector3(0, 0.71f, 0);
                var puckRigidbody = puck.GetComponent<Rigidbody>();
                if (puckRigidbody != null)
                {
                    puckRigidbody.linearVelocity = Vector3.zero;
                    puckRigidbody.angularVelocity = Vector3.zero;
                }
                if (enableDebugLogs)
                {
                    Debug.Log("GoalTrigger: Reset puck via direct transform");
                }
            }
        }

        // ADDED: Reset all players to their spawn points after a goal
        private void ResetAllPlayersToSpawnPoints()
        {
            if (!IsServer) return;

            var gameNetMgr = FindFirstObjectByType<GameNetworkManager>();
            if (gameNetMgr == null)
            {
                Debug.LogWarning("GoalTrigger: GameNetworkManager not found, cannot reset player positions.");
                return;
            }

            var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                ulong clientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? 0;
                // Try to get team as string
                string team = "Red";
                if (player.GetType().GetProperty("Team") != null)
                {
                    team = player.GetType().GetProperty("Team").GetValue(player).ToString();
                }
                else if (player.GetType().GetField("team") != null)
                {
                    team = player.GetType().GetField("team").GetValue(player).ToString();
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
                            team = enumValue.ToString();
                        }
                    }
                }

                // Get spawn position from GameNetworkManager
                Vector3 spawnPos = gameNetMgr.GetType().GetMethod("GetSpawnPositionFromInspector", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(gameNetMgr, new object[] { clientId, team }) as Vector3? ?? player.transform.position;

                Quaternion spawnRot = team == "Blue"
                    ? Quaternion.Euler(0, 90, 0)
                    : Quaternion.Euler(0, -90, 0);

                // Teleport player to spawn
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

            Debug.Log("GoalTrigger: All players reset to their spawn points after goal.");
        }

        // ADDED: Reset goal state so another goal can be scored after cooldown
        private void ResetGoalState()
        {
            goalScored = false;
            if (enableDebugLogs)
            {
                Debug.Log("GoalTrigger: Goal state reset, ready for next goal.");
            }
        }
    }
}
