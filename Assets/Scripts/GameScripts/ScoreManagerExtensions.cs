using UnityEngine;

// FIXED: Extension methods to handle ScoreManager method signature mismatches
public static class ScoreManagerExtensions
{
    // Extension method to handle bool to string conversion for goals
    public static void ScoreGoal(this ScoreManager scoreManager, bool isBlueTeam)
    {
        if (scoreManager != null)
        {
            string teamName = isBlueTeam ? "Blue" : "Red";
            scoreManager.ScoreGoalServerRpc(teamName);
            Debug.Log($"ScoreManagerExtensions: Scored goal for {teamName} team");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager is null!");
        }
    }
    
    // Extension method to handle int parameter for UpdateScoreDisplay
    public static void UpdateScore(this ScoreManager scoreManager, int score)
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateScoreDisplay();
            Debug.Log($"ScoreManagerExtensions: Updated score display with value {score}");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager is null!");
        }
    }
    
    // Extension method to handle team name and score for UpdateScoreDisplay
    public static void UpdateTeamScore(this ScoreManager scoreManager, string teamName, int score)
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateScoreDisplay(teamName, score);
            Debug.Log($"ScoreManagerExtensions: Updated {teamName} team score to {score}");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager is null!");
        }
    }
    
    // Extension method to handle int parameter for UpdateScoreDisplay calls
    public static void UpdateScoreDisplaySafe(this ScoreManager scoreManager, int score)
    {
        if (scoreManager != null)
        {
            // Just call the parameterless version
            scoreManager.UpdateScoreDisplay();
            Debug.Log($"ScoreManagerExtensions: Updated score display (ignored score value {score})");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager is null!");
        }
    }
    
    // Extension method to handle the GameManager and QuarterManager calls
    public static void HandleScoreUpdate(this ScoreManager scoreManager, int totalScore)
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateScoreDisplay();
            Debug.Log($"ScoreManagerExtensions: Handled score update with total {totalScore}");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager is null!");
        }
    }
}
