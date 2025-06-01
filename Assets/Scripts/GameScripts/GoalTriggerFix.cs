using UnityEngine;

// FIXED: Helper class to fix GoalTrigger calls
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
