using UnityEngine;

namespace HockeyGame.Game
{
    public class GoalTrigger : MonoBehaviour
    {
        [SerializeField] private bool isBlueGoal;
        private bool goalScored = false;
        private BoxCollider goalTrigger;

        private void Start()
        {
            goalTrigger = GetComponent<BoxCollider>();
            if (goalTrigger != null)
            {
                goalTrigger.isTrigger = true;
                goalTrigger.size = new Vector3(4f, 3f, 2f);
                goalTrigger.center = Vector3.zero;
            }

            gameObject.layer = LayerMask.NameToLayer("Goal");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (goalScored) return;

            Debug.Log($"Trigger entered by: {other.gameObject.name} on layer: {other.gameObject.layer}");

            if (other.CompareTag("Puck"))
            {
                Debug.Log($"Goal scored by {(isBlueGoal ? "Blue" : "Red")} team!");
                if (ScoreManager.Instance != null)
                {
                    goalScored = true;
                    ScoreManager.Instance.ScoreGoalServerRpc(!isBlueGoal);
                    Invoke(nameof(ResetGoalState), 2f);
                }
            }
        }

        private void ResetGoalState()
        {
            goalScored = false;
        }
    }
}
