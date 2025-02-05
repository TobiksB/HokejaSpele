using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public TextMeshProUGUI team1ScoreText;
    public TextMeshProUGUI team2ScoreText;
    
    private int team1Score = 0;
    private int team2Score = 0;

    public void AddScoreTeam1()
    {
        team1Score++;
        UpdateScoreDisplay();
    }

    public void AddScoreTeam2()
    {
        team2Score++;
        UpdateScoreDisplay();
    }

    private void UpdateScoreDisplay()
    {
        team1ScoreText.text = team1Score.ToString();
        team2ScoreText.text = team2Score.ToString();
    }
}