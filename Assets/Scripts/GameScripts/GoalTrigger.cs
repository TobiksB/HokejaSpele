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
            if (!IsServer) return;
            if (goalScored || (Time.time - lastGoalTime) < goalCooldown) return;

            if (enableDebugLogs)
            {
                Debug.Log($"GoalTrigger: Object entered goal: {other.name}");
            }

            if (other.CompareTag("Puck"))
            {
                HandleGoalScored(other.gameObject);
            }
        }

        private void HandleGoalScored(GameObject puck)
        {
            if (!IsServer) return;

            goalScored = true;
            lastGoalTime = Time.time;

            string scoringTeam = isBlueTeamGoal ? "Red" : "Blue";
            Debug.Log($"GoalTrigger: GOAL! {scoringTeam} team scored in {goalName}!");

            // ADD: Update score
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
                Debug.LogWarning("GoalTrigger: ScoreManager.Instance is null, cannot update score!");
            }

            // Play effects on all clients
            PlayGoalEffectsClientRpc();

            // Reset all players to their correct team spawn positions
            RespawnAllPlayersAfterGoal();

            // Reset puck using the working coroutine
            StartCoroutine(ResetPuckAfterGoal(puck, scoringTeam));

            // Reset goal cooldown after a delay
            StartCoroutine(ResetGoalCooldown());
        }

        [ClientRpc]
        private void PlayGoalEffectsClientRpc()
        {
            if (goalEffect != null) goalEffect.Play();
            if (goalSound != null) goalSound.Play();
        }

        private void RespawnAllPlayersAfterGoal()
        {
            var gameNetMgr = GameNetworkManager.Instance;
            if (gameNetMgr == null)
            {
                Debug.LogError("GoalTrigger: GameNetworkManager.Instance is null!");
                return;
            }

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                ulong clientId = client.ClientId;
                string team = gameNetMgr.GetTeamForClient(clientId);
                Vector3 spawnPos = gameNetMgr.GetSpawnPositionFromInspector(clientId, team);

                var playerObj = client.PlayerObject != null ? client.PlayerObject.gameObject : null;
                if (playerObj == null)
                {
                    Debug.LogWarning($"GoalTrigger: No player object for client {clientId}");
                    continue;
                }

                var rb = playerObj.GetComponent<Rigidbody>();
                playerObj.transform.position = spawnPos;
                playerObj.transform.rotation = team == "Blue"
                    ? Quaternion.Euler(0, 90, 0)
                    : Quaternion.Euler(0, -90, 0);

                if (rb != null)
                {
                    rb.position = spawnPos;
                    rb.rotation = playerObj.transform.rotation;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Debug.Log($"GoalTrigger: Reset player {clientId} ({team} team) to {spawnPos}");
            }
        }

        private System.Collections.IEnumerator ResetPuckAfterGoal(GameObject puck, string goalType)
        {
            yield return new WaitForSeconds(1.5f);
            
            if (puck == null)
            {
                Debug.LogWarning("GoalTrigger: Puck became null during reset, finding puck in scene");
                var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
                if (allPucks.Length > 0)
                {
                    puck = allPucks[0].gameObject;
                }
            }
            
            if (puck != null)
            {
                var puckComponent = puck.GetComponent<Puck>();
                
                // CRITICAL: Ensure puck is completely free before reset
                if (puckComponent != null)
                {
                    // Force clear all held states
                    puckComponent.SetHeld(false);
                    
                    // Clear from any player still holding it
                    var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
                    foreach (var player in allPlayers)
                    {
                        if (player.GetCurrentPuck() == puckComponent)
                        {
                            player.ForceReleasePuckForSteal();
                            if (enableDebugLogs)
                            {
                                Debug.Log($"GoalTrigger: Cleared remaining reference from {player.name}");
                            }
                        }
                    }
                }
                
                // Stop any PuckFollower components
                var puckFollower = puck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }
                
                // FIXED: Proper puck reset to center
                Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
                puck.transform.SetParent(null);
                puck.transform.position = centerPos;
                puck.transform.rotation = Quaternion.identity;
                
                var rb = puck.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = centerPos; // Force rigidbody position too
                }
                
                var col = puck.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = true;
                }
                
                // ADDED: Use Puck's ResetToCenter method if available (for network sync)
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
                                Debug.Log("GoalTrigger: Used Puck.ResetToCenter() method for network sync");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"GoalTrigger: Failed to use Puck.ResetToCenter(): {e.Message}");
                    }
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log($"GoalTrigger: Puck reset to center after {goalType} goal at position {centerPos}");
                }
            }
            else
            {
                Debug.LogError("GoalTrigger: Could not find puck to reset!");
            }
        }

        private System.Collections.IEnumerator ResetGoalCooldown()
        {
            yield return new WaitForSeconds(goalCooldown);
            goalScored = false;
        }
    }
}
