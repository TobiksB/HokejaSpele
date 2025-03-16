using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [SerializeField] private bool isBlueGoal;
    private bool canScore = true;
    private float scoreCooldown = 1f;

    private void OnTriggerEnter(Collider other)
    {
        if (!canScore) return;
        
        if (other.CompareTag("Puck") || other.TryGetComponent<Puck>(out _))
        {
            Debug.Log($"Goal trigger entered by puck in {(isBlueGoal ? "Blue" : "Red")} goal!");
            
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ScoreGoalServerRpc(!isBlueGoal);
                StartCoroutine(ScoreCooldown());
            }
            else
            {
                Debug.LogError("ScoreManager instance not found!");
            }
        }
    }

    private System.Collections.IEnumerator ScoreCooldown()
    {
        canScore = false;
        yield return new WaitForSeconds(scoreCooldown);
        canScore = true;
    }
}
