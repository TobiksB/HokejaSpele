using UnityEngine;

// FIXED: Helper class to fix GameManager method calls
public static class GameManagerHelper
{
    public static void HandleScoreUpdate(ScoreManager scoreManager, int score)
    {
        if (scoreManager != null)
        {
            // Convert int to proper method call
            scoreManager.UpdateScoreDisplay();
            Debug.Log($"GameManagerHelper: Updated score display with score {score}");
        }
        else
        {
            Debug.LogError("GameManagerHelper: ScoreManager is null!");
        }
    }
    
    public static void HandleScoreUpdate(ScoreManager scoreManager, string teamName, int score)
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateScoreDisplay(teamName, score);
            Debug.Log($"GameManagerHelper: Updated score display for {teamName} with score {score}");
        }
        else
        {
            Debug.LogError("GameManagerHelper: ScoreManager is null!");
        }
    }
}
