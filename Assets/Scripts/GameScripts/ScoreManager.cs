using UnityEngine;
using Unity.Netcode;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }
    
    [Header("Score Settings")]
    [SerializeField] private int maxScore = 5;
    
    private NetworkVariable<int> redScore = new NetworkVariable<int>(0);
    private NetworkVariable<int> blueScore = new NetworkVariable<int>(0);
    
    public event System.Action<int, int> OnScoreChanged;
    public event System.Action<string> OnGameEnd;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("ScoreManager: Instance created");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        redScore.OnValueChanged += OnRedScoreChanged;
        blueScore.OnValueChanged += OnBlueScoreChanged;
        
        Debug.Log("ScoreManager: Network spawned");
    }

    public override void OnNetworkDespawn()
    {
        redScore.OnValueChanged -= OnRedScoreChanged;
        blueScore.OnValueChanged -= OnBlueScoreChanged;
        
        base.OnNetworkDespawn();
    }

    private void OnRedScoreChanged(int oldValue, int newValue)
    {
        Debug.Log($"🏒 RED TEAM SCORED! New score: Red {newValue} - Blue {blueScore.Value}");
        
        // FIXED: Force immediate UI update
        UpdateScoreDisplayImmediate(newValue, blueScore.Value);
        
        if (newValue >= maxScore)
        {
            Debug.Log($"🏆 GAME OVER! Red team wins {newValue}-{blueScore.Value}!");
            OnGameEnd?.Invoke("Red");
        }
    }

    private void OnBlueScoreChanged(int oldValue, int newValue)
    {
        Debug.Log($"🏒 BLUE TEAM SCORED! New score: Red {redScore.Value} - Blue {newValue}");
        
        // FIXED: Force immediate UI update
        UpdateScoreDisplayImmediate(redScore.Value, newValue);
        
        if (newValue >= maxScore)
        {
            Debug.Log($"🏆 GAME OVER! Blue team wins {newValue}-{redScore.Value}!");
            OnGameEnd?.Invoke("Blue");
        }
    }

    // FIXED: Add immediate UI update method
    private void UpdateScoreDisplayImmediate(int redScore, int blueScore)
    {
        Debug.Log($"📊 SCORE UPDATE: Red {redScore} - Blue {blueScore}");
        
        // Trigger score changed event to update UI immediately
        OnScoreChanged?.Invoke(redScore, blueScore);
        
        // FIXED: Try to find UIManager without specific namespace reference
        var uiManagers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var uiManager in uiManagers)
        {
            if (uiManager.GetType().Name == "UIManager")
            {
                try
                {
                    // Use reflection to call UpdateScore if it exists
                    var updateScoreMethod = uiManager.GetType().GetMethod("UpdateScore");
                    if (updateScoreMethod != null)
                    {
                        updateScoreMethod.Invoke(uiManager, new object[] { redScore, blueScore });
                        Debug.Log("📱 ScoreManager: Updated UI via UIManager.UpdateScore()");
                        break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"ScoreManager: Could not update UIManager: {e.Message}");
                }
            }
        }

        // PRIORITY: Update TextMeshPro texts first (preferred)
        var tmpTexts = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
        bool redScoreUpdated = false;
        bool blueScoreUpdated = false;
        
        foreach (var tmpText in tmpTexts)
        {
            string textName = tmpText.name.ToLower();
            
            // Update Red team TMP score text
            if (textName.Contains("red") && textName.Contains("score"))
            {
                tmpText.text = redScore.ToString();
                Debug.Log($"📱 ScoreManager: Updated RED TMP score text {tmpText.name} to {redScore}");
                redScoreUpdated = true;
            }
            // Update Blue team TMP score text
            else if (textName.Contains("blue") && textName.Contains("score"))
            {
                tmpText.text = blueScore.ToString();
                Debug.Log($"📱 ScoreManager: Updated BLUE TMP score text {tmpText.name} to {blueScore}");
                blueScoreUpdated = true;
            }
            // Fallback: combined TMP score text
            else if (textName.Contains("score") && !textName.Contains("red") && !textName.Contains("blue"))
            {
                tmpText.text = $"Red {redScore} - Blue {blueScore}";
                Debug.Log($"📱 ScoreManager: Updated combined TMP score text {tmpText.name}");
            }
        }

        // FALLBACK: Only use regular UI Text if TMP texts not found
        if (!redScoreUpdated || !blueScoreUpdated)
        {
            var scoreTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
            
            foreach (var scoreText in scoreTexts)
            {
                string textName = scoreText.name.ToLower();
                
                // Update Red team score text (only if TMP not found)
                if (!redScoreUpdated && textName.Contains("red") && textName.Contains("score"))
                {
                    scoreText.text = redScore.ToString();
                    Debug.Log($"📱 ScoreManager: Updated RED UI Text score {scoreText.name} to {redScore} (fallback)");
                    redScoreUpdated = true;
                }
                // Update Blue team score text (only if TMP not found)
                else if (!blueScoreUpdated && textName.Contains("blue") && textName.Contains("score"))
                {
                    scoreText.text = blueScore.ToString();
                    Debug.Log($"📱 ScoreManager: Updated BLUE UI Text score {scoreText.name} to {blueScore} (fallback)");
                    blueScoreUpdated = true;
                }
                // Fallback: combined score text
                else if (textName.Contains("score") && !textName.Contains("red") && !textName.Contains("blue"))
                {
                    scoreText.text = $"Red {redScore} - Blue {blueScore}";
                    Debug.Log($"📱 ScoreManager: Updated combined UI Text score {scoreText.name} (fallback)");
                }
            }
        }
        
        // ADDED: Log if team scores were successfully updated
        if (redScoreUpdated && blueScoreUpdated)
        {
            Debug.Log($"✅ ScoreManager: Successfully updated both RED and BLUE team score displays");
        }
        else
        {
            if (!redScoreUpdated)
                Debug.LogWarning($"⚠️ ScoreManager: RED team score text not found (looking for 'red' + 'score' in name)");
            if (!blueScoreUpdated)
                Debug.LogWarning($"⚠️ ScoreManager: BLUE team score text not found (looking for 'blue' + 'score' in name)");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ScoreGoalServerRpc(string teamName)
    {
        if (IsServer)
        {
            Debug.Log($"🎯 GOAL ATTEMPT: {teamName} team trying to score!");
            
            if (teamName.ToLower() == "red")
            {
                int oldScore = redScore.Value;
                redScore.Value++;
                Debug.Log($"🔴 RED TEAM GOAL! Score changed from {oldScore} to {redScore.Value}");
            }
            else if (teamName.ToLower() == "blue")
            {
                int oldScore = blueScore.Value;
                blueScore.Value++;
                Debug.Log($"🔵 BLUE TEAM GOAL! Score changed from {oldScore} to {blueScore.Value}");
            }
            
            Debug.Log($"🏒 FINAL SCORE UPDATE: Red {redScore.Value} - Blue {blueScore.Value}");
            
            // FIXED: Force immediate score display update on server
            UpdateScoreDisplayImmediate(redScore.Value, blueScore.Value);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddRedScoreServerRpc()
    {
        if (IsServer)
        {
            redScore.Value++;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddBlueScoreServerRpc()
    {
        if (IsServer)
        {
            blueScore.Value++;
        }
    }

    public void AddRedScore()
    {
        if (IsServer)
        {
            redScore.Value++;
        }
        else
        {
            AddRedScoreServerRpc();
        }
    }

    public void AddBlueScore()
    {
        if (IsServer)
        {
            blueScore.Value++;
        }
        else
        {
            AddBlueScoreServerRpc();
        }
    }

    public int GetRedScore()
    {
        return redScore.Value;
    }

    public int GetBlueScore()
    {
        return blueScore.Value;
    }

    // FIXED: Add overload that accepts team name and score (for compatibility)
    public void UpdateScoreDisplay(string teamName, int score)
    {
        Debug.Log($"ScoreManager: UpdateScoreDisplay called for {teamName} with score {score}");
        
        // Update the appropriate team score
        if (IsServer)
        {
            if (teamName.ToLower() == "red")
            {
                redScore.Value = score;
            }
            else if (teamName.ToLower() == "blue")
            {
                blueScore.Value = score;
            }
        }
        
        // Trigger UI update
        UpdateScoreDisplay();
    }

    public void ScoreGoal(bool isBlueTeam)
    {
        string teamName = isBlueTeam ? "Blue" : "Red";
        ScoreGoalServerRpc(teamName);
    }

    public void UpdateScoreDisplay()
    {
        // Trigger score changed event to update UI
        OnScoreChanged?.Invoke(redScore.Value, blueScore.Value);
        Debug.Log($"ScoreManager: Score display updated - Red {redScore.Value} - Blue {blueScore.Value}");
    }

    // FIXED: Remove the conflicting UpdateScoreDisplay(int) method that's causing issues
    // Keep only the specific named methods to avoid signature conflicts

    // FIXED: Remove duplicate UpdateScoreDisplay methods and replace with properly named methods
    public void UpdateScoreDisplayWithTeam(string teamName, int score)
    {
        Debug.Log($"ScoreManager: UpdateScoreDisplayWithTeam called for {teamName} with score {score}");
        
        // Update the appropriate team score
        if (IsServer)
        {
            if (teamName.ToLower() == "red")
            {
                redScore.Value = score;
            }
            else if (teamName.ToLower() == "blue")
            {
                blueScore.Value = score;
            }
        }
        
        // Trigger UI update
        UpdateScoreDisplay();
    }

    // FIXED: Rename to avoid duplicate signature
    public void UpdateScoreDisplayWithTotal(int totalScore)
    {
        Debug.Log($"ScoreManager: UpdateScoreDisplayWithTotal called with total score {totalScore}");
        // Just trigger the regular UI update
        UpdateScoreDisplay();
    }

    public void ResetScores()
    {
        if (IsServer)
        {
            redScore.Value = 0;
            blueScore.Value = 0;
            Debug.Log("ScoreManager: Scores reset");
        }
    }
}
