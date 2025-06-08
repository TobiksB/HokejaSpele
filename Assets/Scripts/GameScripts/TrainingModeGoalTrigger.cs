using UnityEngine;

namespace HockeyGame.Game
{
    public class TrainingModeGoalTrigger : MonoBehaviour
    {
        [Header("Goal Configuration")]
        [SerializeField] private bool isBlueTeamGoal = false; // true if this is Blue team's goal
        [SerializeField] private string goalName = "Goal"; // For debugging

        [Header("Effects")]
        [SerializeField] private ParticleSystem goalEffect;
        [SerializeField] private AudioSource goalSound;

        // Event that gets fired when a goal is scored
        public System.Action<string> OnGoalScored;

        private bool goalCooldown = false;
        private float cooldownTime = 2f;

        private void Awake()
        {
            // Ensure this has a trigger collider
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            else
            {
                Debug.LogWarning($"TrainingModeGoalTrigger: No collider found on {gameObject.name}! Please add a collider.");
            }

            // Set tag to "Goal"
            if (!gameObject.CompareTag("Goal"))
            {
                gameObject.tag = "Goal";
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (goalCooldown) return;

            if (other.CompareTag("Puck"))
            {
                string scoringTeam = isBlueTeamGoal ? "Red" : "Blue";
                Debug.Log($"GOAL! {scoringTeam} team scored in {goalName}!");

                // Play effects
                if (goalEffect != null) goalEffect.Play();
                if (goalSound != null) goalSound.Play();

                // Invoke event
                OnGoalScored?.Invoke(scoringTeam);

                // Reset player and puck
                StartCoroutine(ResetAfterGoal(other.gameObject));

                // Set cooldown
                goalCooldown = true;
                Invoke(nameof(ResetCooldown), cooldownTime);
            }
        }

        private System.Collections.IEnumerator ResetAfterGoal(GameObject puck)
        {
            // Wait a moment before resetting
            yield return new WaitForSeconds(1.5f);

            // 1. Reset player position
            var player = FindObjectOfType<TrainingPlayerMovement>();
            if (player != null)
            {
                // Reset to initial position if available
                if (player.initialPosition != Vector3.zero)
                {
                    player.transform.position = player.initialPosition;
                    player.transform.rotation = player.initialRotation;

                    // Reset player physics
                    var playerRb = player.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        playerRb.linearVelocity = Vector3.zero;
                        playerRb.angularVelocity = Vector3.zero;
                        playerRb.position = player.initialPosition;
                        playerRb.rotation = player.initialRotation;
                    }

                    Debug.Log($"TrainingModeGoalTrigger: Reset player to initial position {player.initialPosition}");
                }
                else
                {
                    Debug.LogWarning("TrainingModeGoalTrigger: Player's initial position not set!");
                }
            }
            else
            {
                Debug.LogWarning("TrainingModeGoalTrigger: Player not found for reset!");
            }

            // 2. Reset puck position
            if (puck != null)
            {
                // If player was holding the puck, force release
                var puckPickup = FindObjectOfType<TrainingPuckPickup>();
                if (puckPickup != null && puckPickup.HasPuck())
                {
                    try
                    {
                        puckPickup.GetType().GetMethod("ManualReleasePuck", 
                            System.Reflection.BindingFlags.NonPublic | 
                            System.Reflection.BindingFlags.Instance)?.Invoke(puckPickup, null);
                        
                        Debug.Log("TrainingModeGoalTrigger: Released puck from player");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"TrainingModeGoalTrigger: Error releasing puck: {e.Message}");
                    }
                }

                // Stop any puck follower
                var puckFollower = puck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }

                // Reset puck position to center
                Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
                puck.transform.position = centerPos;
                puck.transform.rotation = Quaternion.identity;

                // Reset puck physics
                var puckRb = puck.GetComponent<Rigidbody>();
                if (puckRb != null)
                {
                    puckRb.isKinematic = false;
                    puckRb.useGravity = true;
                    puckRb.linearVelocity = Vector3.zero;
                    puckRb.angularVelocity = Vector3.zero;
                    puckRb.position = centerPos;
                }

                // Reset puck state
                var puckComponent = puck.GetComponent<Puck>();
                if (puckComponent != null)
                {
                    puckComponent.SetHeld(false);
                }

                Debug.Log("TrainingModeGoalTrigger: Reset puck to center position");
            }
            else
            {
                Debug.LogWarning("TrainingModeGoalTrigger: Puck became null during reset!");
                
                // Try finding puck in the scene
                var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
                if (allPucks.Length > 0)
                {
                    var foundPuck = allPucks[0].gameObject;
                    Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
                    foundPuck.transform.position = centerPos;
                    
                    var puckRb = foundPuck.GetComponent<Rigidbody>();
                    if (puckRb != null)
                    {
                        puckRb.linearVelocity = Vector3.zero;
                        puckRb.angularVelocity = Vector3.zero;
                    }
                    
                    Debug.Log("TrainingModeGoalTrigger: Reset alternative puck to center");
                }
            }

            yield return new WaitForSeconds(0.5f);

            // Make player automatically pick up puck after reset
            // This makes it easy for the player to continue training
            var pickup = FindObjectOfType<TrainingPuckPickup>();
            if (pickup != null)
            {
                try
                {
                    pickup.GetType().GetMethod("TryPickupPuck", 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.Instance)?.Invoke(pickup, null);
                    
                    Debug.Log("TrainingModeGoalTrigger: Auto-picked up puck after reset");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"TrainingModeGoalTrigger: Error auto-picking up puck: {e.Message}");
                }
            }
        }

        private void ResetCooldown()
        {
            goalCooldown = false;
        }
    }
}
