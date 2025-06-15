using UnityEngine;

// Paplašinājuma metodes, kas apstrādā ScoreManager metožu parakstu neatbilstības
public static class ScoreManagerExtensions
{
    // Paplašinājuma metode bool uz teksta pārveidošanai vārtiem
    public static void ScoreGoal(this ScoreManager scoreManager, bool isBlueTeam)
    {
        if (scoreManager != null)
        {
            string teamName = isBlueTeam ? "Blue" : "Red";
            scoreManager.ScoreGoalServerRpc(teamName);
            Debug.Log($"ScoreManagerExtensions: Iegūti vārti {teamName} komandai");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager ir null!");
        }
    }
    
    // Paplašinājuma metode int parametram UpdateScoreDisplay izsaukumam
    public static void UpdateScore(this ScoreManager scoreManager, int score)
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateScoreDisplay();
            Debug.Log($"ScoreManagerExtensions: Atjaunināts rezultāta attēlojums ar vērtību {score}");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager ir null!");
        }
    }
    
    // Paplašinājuma metode komandas nosaukumam un rezultātam UpdateScoreDisplay izsaukumam
    public static void UpdateTeamScore(this ScoreManager scoreManager, string teamName, int score)
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateScoreDisplay(teamName, score);
            Debug.Log($"ScoreManagerExtensions: Atjaunināts {teamName} komandas rezultāts uz {score}");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager ir null!");
        }
    }
    
    // Paplašinājuma metode int parametram UpdateScoreDisplay izsaukumiem
    public static void UpdateScoreDisplaySafe(this ScoreManager scoreManager, int score)
    {
        if (scoreManager != null)
        {
            // Vienkārši izsauc versiju bez parametriem
            scoreManager.UpdateScoreDisplay();
            Debug.Log($"ScoreManagerExtensions: Atjaunināts rezultāta attēlojums (ignorēta rezultāta vērtība {score})");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager ir null!");
        }
    }
    
    // Paplašinājuma metode GameManager un QuarterManager izsaukumiem
    public static void HandleScoreUpdate(this ScoreManager scoreManager, int totalScore)
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateScoreDisplay();
            Debug.Log($"ScoreManagerExtensions: Apstrādāts rezultāta atjauninājums ar kopējo vērtību {totalScore}");
        }
        else
        {
            Debug.LogError("ScoreManagerExtensions: ScoreManager ir null!");
        }
    }
}
