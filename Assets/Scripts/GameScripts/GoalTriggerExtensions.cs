using UnityEngine;

public static class GoalTriggerExtensions
{
    public static void ScoreGoal(this ScoreManager scoreManager, bool isBlueTeam)
    {
        if (scoreManager != null)
        {
            string teamName = isBlueTeam ? "Blue" : "Red";
            scoreManager.ScoreGoalServerRpc(teamName);
            Debug.Log($"GoalTriggerExtensions: Handled goal for {teamName} team");
        }
        else
        {
            Debug.LogError("GoalTriggerExtensions: ScoreManager is null!");
        }
    }
    
    public static void UpdateScore(this ScoreManager scoreManager, int score)
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateScoreDisplay();
            Debug.Log($"GoalTriggerExtensions: Updated score display with score {score}");
        }
        else
        {
            Debug.LogError("GoalTriggerExtensions: ScoreManager is null!");
        }
    }
}
