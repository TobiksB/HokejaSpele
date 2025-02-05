using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    public bool isTeam1Goal = true;
    private ScoreManager scoreManager;
    private BoxCollider triggerCollider;

    private void Start()
    {
        scoreManager = FindObjectOfType<ScoreManager>();
        triggerCollider = GetComponent<BoxCollider>();
        
        if (!triggerCollider)
        {
            Debug.LogError("BoxCollider component missing on GoalTrigger!");
            enabled = false;
            return;
        }

        // Ensure the collider is set as trigger
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Puck"))
        {
            Debug.Log($"Goal scored! Ball entered {(isTeam1Goal ? "Team 1's" : "Team 2's")} goal");
            if (isTeam1Goal)
            {
                scoreManager.AddScoreTeam2(); // Team 2 scores in Team 1's goal
            }
            else
            {
                scoreManager.AddScoreTeam1(); // Team 1 scores in Team 2's goal
            }
        }
    }

    // Visualize the trigger zone in the editor
    private void OnDrawGizmos()
    {
        Gizmos.color = isTeam1Goal ? Color.blue : Color.red;
        if (triggerCollider != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(triggerCollider.center, triggerCollider.size);
        }
    }
}