using UnityEngine;

public static class GoalTriggerHelper
{
    public static void FixScoreGoalCall(ScoreManager scoreManager, bool isRedTeam)
    {
        if (scoreManager != null)
        {
            string teamName = isRedTeam ? "Red" : "Blue";
            scoreManager.ScoreGoalServerRpc(teamName);
        }
    }
}
