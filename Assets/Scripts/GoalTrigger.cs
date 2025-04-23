using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [SerializeField] private bool isBlueGoal;
    private bool goalScored = false;
    private BoxCollider goalTrigger;

    private void Start()
    {
        // Set up goal trigger properly
        goalTrigger = GetComponent<BoxCollider>();
        if (goalTrigger != null)
        {
            goalTrigger.isTrigger = true;
            // Make trigger volume larger
            goalTrigger.size = new Vector3(4f, 3f, 2f);
            goalTrigger.center = Vector3.zero;
        }

        // Ensure correct layer setup
        gameObject.layer = LayerMask.NameToLayer("Goal");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (goalScored) return;

        Debug.Log($"Trigger entered by: {other.gameObject.name} on layer: {other.gameObject.layer}");

        // Use CompareTag for better performance and explicit tag check
        if (other.CompareTag("Puck"))
        {
            Debug.Log($"Goal scored by {(isBlueGoal ? "Blue" : "Red")} team!");
            if (ScoreManager.Instance != null)
            {
                goalScored = true;
                ScoreManager.Instance.ScoreGoalServerRpc(!isBlueGoal);
                Invoke("ResetGoalState", 2f);
            }
        }
    }

    private void ResetGoalState()
    {
        goalScored = false;
    }
}
