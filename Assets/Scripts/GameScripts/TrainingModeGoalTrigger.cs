using UnityEngine;

namespace HockeyGame.Game
{
    public class TrainingModeGoalTrigger : MonoBehaviour
    {        
        [Header("Vārtu konfigurācija")]
        [SerializeField] private bool isBlueTeamGoal = false; // true, ja šie ir Zilās komandas vārti
        [SerializeField] private string goalName = "Goal"; // Atkļūdošanai

        [Header("Efekti")]
        [SerializeField] private ParticleSystem goalEffect;
        [SerializeField] private AudioSource goalSound;
        // Notikums, kas tiek izsaukts, kad tiek gūti vārti
        public System.Action<string> OnGoalScored;

        private bool goalCooldown = false;
        private float cooldownTime = 2f;

        private void Awake()
        {
            // Nodrošina, ka šim ir trigera sadursmes detektors
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            else
            {
                Debug.LogWarning($"TrainingModeGoalTrigger: Nav atrasts sadursmes detektors objektam {gameObject.name}! Lūdzu, pievienojiet sadursmes detektoru.");
            }

            // Iestata tagu uz "Goal"
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
                Debug.Log($"VĀRTI! {scoringTeam} komanda guva vārtus {goalName}!");

                // Atskaņo efektus
                if (goalEffect != null) goalEffect.Play();
                if (goalSound != null) goalSound.Play();

                // Izsauc notikumu
                OnGoalScored?.Invoke(scoringTeam);

                // Atiestata spēlētāju un ripu
                StartCoroutine(ResetAfterGoal(other.gameObject));

                // Iestata dzesēšanas laiku
                goalCooldown = true;
                Invoke(nameof(ResetCooldown), cooldownTime);
            }
        }

        private System.Collections.IEnumerator ResetAfterGoal(GameObject puck)
        {
            // Pagaida brīdi pirms atiestatīšanas
            yield return new WaitForSeconds(1.5f);

            // 1. Atiestata spēlētāja pozīciju
            var player = FindObjectOfType<TrainingPlayerMovement>();
            if (player != null)
            {
                // Atiestata uz sākotnējo pozīciju, ja tā ir pieejama
                if (player.initialPosition != Vector3.zero)
                {
                    player.transform.position = player.initialPosition;
                    player.transform.rotation = player.initialRotation;

                    // Atiestata spēlētāja fiziku
                    var playerRb = player.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        playerRb.linearVelocity = Vector3.zero;
                        playerRb.angularVelocity = Vector3.zero;
                        playerRb.position = player.initialPosition;
                        playerRb.rotation = player.initialRotation;
                    }

                    Debug.Log($"TrainingModeGoalTrigger: Atiestatīts spēlētājs uz sākotnējo pozīciju {player.initialPosition}");
                }
                else
                {
                    Debug.LogWarning("TrainingModeGoalTrigger: Spēlētāja sākotnējā pozīcija nav iestatīta!");
                }
            }
            else
            {
                Debug.LogWarning("TrainingModeGoalTrigger: Spēlētājs nav atrasts atiestatīšanai!");
            }

            // 2. Atiestata ripas pozīciju
            if (puck != null)
            {
                // Ja spēlētājs turēja ripu, piespiedu kārtā atbrīvo
                var puckPickup = FindObjectOfType<TrainingPuckPickup>();
                if (puckPickup != null && puckPickup.HasPuck())
                {
                    try
                    {
                        puckPickup.GetType().GetMethod("ManualReleasePuck", 
                            System.Reflection.BindingFlags.NonPublic | 
                            System.Reflection.BindingFlags.Instance)?.Invoke(puckPickup, null);
                        
                        Debug.Log("TrainingModeGoalTrigger: Atbrīvota ripa no spēlētāja");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"TrainingModeGoalTrigger: Kļūda atbrīvojot ripu: {e.Message}");
                    }
                }

                // Aptur jebkuru ripas sekotāju
                var puckFollower = puck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StopFollowing();
                    puckFollower.enabled = false;
                }

                // Atiestata ripas pozīciju uz centru
                Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
                puck.transform.position = centerPos;
                puck.transform.rotation = Quaternion.identity;

                // Atiestata ripas fiziku
                var puckRb = puck.GetComponent<Rigidbody>();
                if (puckRb != null)
                {
                    puckRb.isKinematic = false;
                    puckRb.useGravity = true;
                    puckRb.linearVelocity = Vector3.zero;
                    puckRb.angularVelocity = Vector3.zero;
                    puckRb.position = centerPos;
                }

                // Atiestata ripas stāvokli
                var puckComponent = puck.GetComponent<Puck>();
                if (puckComponent != null)
                {
                    puckComponent.SetHeld(false);
                }

                Debug.Log("TrainingModeGoalTrigger: Atiestatīta ripa uz centra pozīciju");
            }
            else
            {
                Debug.LogWarning("TrainingModeGoalTrigger: Ripa kļuva null atiestatīšanas laikā!");
                
                // Mēģina atrast ripu skatā
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
                    
                    Debug.Log("TrainingModeGoalTrigger: Atiestatīta alternatīvā ripa uz centru");
                }
            }

            yield return new WaitForSeconds(0.5f);

            // Liek spēlētājam automātiski pacelt ripu pēc atiestatīšanas
            // Tas atvieglo spēlētājam turpināt treniņu
            var pickup = FindObjectOfType<TrainingPuckPickup>();
            if (pickup != null)
            {
                try
                {
                    pickup.GetType().GetMethod("TryPickupPuck", 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.Instance)?.Invoke(pickup, null);
                    
                    Debug.Log("TrainingModeGoalTrigger: Automātiski pacelta ripa pēc atiestatīšanas");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"TrainingModeGoalTrigger: Kļūda automātiski paceļot ripu: {e.Message}");
                }
            }
        }

        private void ResetCooldown()
        {
            goalCooldown = false;
        }
    }
}
