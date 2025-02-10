using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    public bool isTeam1Goal = true;
    private ScoreManager scoreManager;
    private BoxCollider triggerCollider;
    private bool goalScored = false;
    private float goalCooldown = 1f;
    private float goalTimer = 0f;

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

    private void Update()
    {
        if (goalScored)
        {
            goalTimer += Time.deltaTime;
            if (goalTimer >= goalCooldown)
            {
                goalScored = false;
                goalTimer = 0f;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleGoal(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleGoal(collision.collider);
    }

    private void HandleGoal(Collider other)
    {
        if (goalScored) return;

        Puck puck = other.GetComponent<Puck>();
        if (puck != null)
        {
            goalScored = true;
            Debug.Log($"Goal scored! Puck entered {(isTeam1Goal ? "Team 1's" : "Team 2's")} goal");
            if (scoreManager != null)
            {
                if (isTeam1Goal)
                {
                    scoreManager.AddScoreTeam2();
                }
                else
                {
                    scoreManager.AddScoreTeam1();
                }
                puck.ResetPosition();
            }
            else
            {
                Debug.LogError("ScoreManager not found!");
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