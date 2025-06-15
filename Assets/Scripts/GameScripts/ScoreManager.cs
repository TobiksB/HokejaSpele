using UnityEngine;
using Unity.Netcode;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }
    
    [Header("Rezultātu iestatījumi")]
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
            Debug.Log("ScoreManager: Instance izveidota");
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
        
        Debug.Log("ScoreManager: Tīklā izvietots");
    }

    public override void OnNetworkDespawn()
    {
        redScore.OnValueChanged -= OnRedScoreChanged;
        blueScore.OnValueChanged -= OnBlueScoreChanged;
        
        base.OnNetworkDespawn();
    }

    private void OnRedScoreChanged(int oldValue, int newValue)
    {        
        Debug.Log($"SARKANĀ KOMANDA GUVA VĀRTUS! Jauns rezultāts: Sarkanā {newValue} - Zilā {blueScore.Value}");
        
        //  Uzspiest tūlītēju UI atjaunināšanu
        UpdateScoreDisplayImmediate(newValue, blueScore.Value);
        
        if (newValue >= maxScore)
        {
            Debug.Log($"SPĒLE BEIGUSIES! Sarkanā komanda uzvar {newValue}-{blueScore.Value}!");
            OnGameEnd?.Invoke("Red");
        }
    }

    private void OnBlueScoreChanged(int oldValue, int newValue)
    {        
        Debug.Log($"ZILĀ KOMANDA GUVA VĀRTUS! Jauns rezultāts: Sarkanā {redScore.Value} - Zilā {newValue}");
        
        //  Uzspiest tūlītēju UI atjaunināšanu
        UpdateScoreDisplayImmediate(redScore.Value, newValue);
        
        if (newValue >= maxScore)
        {
            Debug.Log($"SPĒLE BEIGUSIES! Zilā komanda uzvar {newValue}-{redScore.Value}!");
            OnGameEnd?.Invoke("Blue");
        }
    }

    //  Pievienota tūlītēja UI atjaunināšanas metode
    private void UpdateScoreDisplayImmediate(int redScore, int blueScore)
    {
        Debug.Log($"REZULTĀTA ATJAUNINĀŠANA: Sarkanā {redScore} - Zilā {blueScore}");
        
        // Izraisīt rezultāta maiņas notikumu, lai atjauninātu UI
        OnScoreChanged?.Invoke(redScore, blueScore);
        
        //  Mēģināt atrast UIManager bez specifiskas namespace atsauces
        var uiManagers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var uiManager in uiManagers)
        {
            if (uiManager.GetType().Name == "UIManager")
            {
                try
                {
                    // Izmanto refleksiju, lai izsauktu UpdateScore, ja tā pastāv
                    var updateScoreMethod = uiManager.GetType().GetMethod("UpdateScore");
                    if (updateScoreMethod != null)
                    {
                        updateScoreMethod.Invoke(uiManager, new object[] { redScore, blueScore });
                        Debug.Log("ScoreManager: Atjaunināts UI caur UIManager.UpdateScore()");
                        break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"ScoreManager: Nevarēja atjaunināt UIManager: {e.Message}");
                }
            }
        }

        //  Vispirms atjaunināt TextMeshPro tekstus (ieteicams)
        var tmpTexts = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
        bool redScoreUpdated = false;
        bool blueScoreUpdated = false;
        
        foreach (var tmpText in tmpTexts)
        {
            string textName = tmpText.name.ToLower();
            
            // Atjauno Sarkanās komandas TMP rezultāta tekstu
            if (textName.Contains("red") && textName.Contains("score"))
            {
                tmpText.text = redScore.ToString();
                Debug.Log($"ScoreManager: Atjaunināts SARKANĀS komandas TMP rezultāta teksts {tmpText.name} uz {redScore}");
                redScoreUpdated = true;
            }
            // Atjauno Zilās komandas rezultāta tekstu (tikai ja TMP nav atrasts)
            else if (textName.Contains("blue") && textName.Contains("score"))
            {
                tmpText.text = blueScore.ToString();
                Debug.Log($"ScoreManager: Atjaunināts ZILĀS komandas TMP rezultāta teksts {tmpText.name} uz {blueScore}");
                blueScoreUpdated = true;
            }
            // Rezerves variants: apvienotais rezultāta teksts
            else if (textName.Contains("score") && !textName.Contains("red") && !textName.Contains("blue"))
            {
                tmpText.text = $"Red {redScore} - Blue {blueScore}";
                Debug.Log($"ScoreManager: Atjaunināts apvienotais TMP rezultāta teksts {tmpText.name}");
            }
        }

        //  Izmantot parasto UI Text tikai ja TMP teksti nav atrasti
        if (!redScoreUpdated || !blueScoreUpdated)
        {
            var scoreTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
            
            foreach (var scoreText in scoreTexts)
            {
                string textName = scoreText.name.ToLower();
                
                // Atjaunināt Sarkanās komandas rezultāta tekstu (tikai ja TMP nav atrasts)
                if (!redScoreUpdated && textName.Contains("red") && textName.Contains("score"))
                {
                    scoreText.text = redScore.ToString();
                    Debug.Log($"ScoreManager: Atjaunināts SARKANĀS komandas UI Text rezultāts {scoreText.name} uz {redScore} (rezerves variants)");
                    redScoreUpdated = true;
                }
                // Atjaunināt Zilās komandas rezultāta tekstu (tikai ja TMP nav atrasts)
                else if (!blueScoreUpdated && textName.Contains("blue") && textName.Contains("score"))
                {
                    scoreText.text = blueScore.ToString();
                    Debug.Log($"ScoreManager: Atjaunināts ZILĀS komandas UI Text rezultāts {scoreText.name} uz {blueScore} (rezerves variants)");
                    blueScoreUpdated = true;
                }
                // Rezerves variants: apvienotais rezultāta teksts
                else if (textName.Contains("score") && !textName.Contains("red") && !textName.Contains("blue"))
                {
                    scoreText.text = $"Red {redScore} - Blue {blueScore}";
                    Debug.Log($"ScoreManager: Atjaunināts apvienotais UI Text rezultāts {scoreText.name} (rezerves variants)");
                }
            }
        }
        
        //  Žurnalēt, ja komandas rezultāti veiksmīgi atjaunināti
        if (redScoreUpdated && blueScoreUpdated)
        {
            Debug.Log($"ScoreManager: Veiksmīgi atjaunināti gan SARKANĀS, gan ZILĀS komandas rezultātu rādījumi");
        }
        else
        {
            if (!redScoreUpdated)
                Debug.LogWarning($"ScoreManager: SARKANĀS komandas rezultāta teksts nav atrasts (meklē 'red' + 'score' nosaukumā)");
            if (!blueScoreUpdated)
                Debug.LogWarning($"ScoreManager: ZILĀS komandas rezultāta teksts nav atrasts (meklē 'blue' + 'score' nosaukumā)");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ScoreGoalServerRpc(string teamName)
    {
        if (IsServer)
        {
            Debug.Log($"VĀRTU MĒĢINĀJUMS: {teamName} komanda mēģina gūt vārtus!");
            
            if (teamName.ToLower() == "red")
            {
                int oldScore = redScore.Value;
                redScore.Value++;
                Debug.Log($"SARKANĀS KOMANDAS VĀRTI! Rezultāts mainījies no {oldScore} uz {redScore.Value}");
            }
            else if (teamName.ToLower() == "blue")
            {
                int oldScore = blueScore.Value;
                blueScore.Value++;
                Debug.Log($"ZILĀS KOMANDAS VĀRTI! Rezultāts mainījies no {oldScore} uz {blueScore.Value}");
            }
            
            Debug.Log($"GALĪGAIS REZULTĀS: Sarkanā {redScore.Value} - Zilā {blueScore.Value}");
            
            // LABOTS: Piespiest tūlītēju rezultāta attēlojuma atjaunināšanu serverī
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

    // : Pievienots pārslodzes variants, kas pieņem komandas nosaukumu un rezultātu (saderībai)
    public void UpdateScoreDisplay(string teamName, int score)
    {
        Debug.Log($"ScoreManager: UpdateScoreDisplay izsaukts komandai {teamName} ar rezultātu {score}");
        
        // Atjaunināt atbilstošās komandas rezultātu
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
        
        // Izraisīt UI atjaunināšanu
        UpdateScoreDisplay();
    }

    public void ScoreGoal(bool isBlueTeam)
    {
        string teamName = isBlueTeam ? "Blue" : "Red";
        ScoreGoalServerRpc(teamName);
    }

    public void UpdateScoreDisplay()
    {
        // Izraisīt rezultāta izmaiņas notikumu, lai atjauninātu UI
        OnScoreChanged?.Invoke(redScore.Value, blueScore.Value);
        Debug.Log($"ScoreManager: Rezultāta rādījums atjaunināts - Sarkanā {redScore.Value} - Zilā {blueScore.Value}");
    }

    // : Noņemta konfliktējošā UpdateScoreDisplay(int) metode, kas rada problēmas
    // Saglabātas tikai konkrēti nosauktās metodes, lai izvairītos no parakstu konfliktiem

    // : Noņemtas dublētās UpdateScoreDisplay metodes un aizstātas ar pareizi nosauktām metodēm
    public void UpdateScoreDisplayWithTeam(string teamName, int score)
    {
        Debug.Log($"ScoreManager: UpdateScoreDisplayWithTeam izsaukts komandai {teamName} ar rezultātu {score}");
        
        // Atjaunināt atbilstošās komandas rezultātu
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
        
        // Izraisīt UI atjaunināšanu
        UpdateScoreDisplay();
    }

    // : Pārsaukts, lai izvairītos no dublētiem parakstiem
    public void UpdateScoreDisplayWithTotal(int totalScore)
    {
        Debug.Log($"ScoreManager: UpdateScoreDisplayWithTotal izsaukts ar kopējo rezultātu {totalScore}");
        // Vienkārši izraisīt parasto UI atjaunināšanu
        UpdateScoreDisplay();
    }

    public void ResetScores()
    {
        if (IsServer)
        {
            redScore.Value = 0;
            blueScore.Value = 0;
            Debug.Log("ScoreManager: Rezultāti atiestatīti");
        }
    }
}
