using Unity.Netcode;
using TMPro;
using UnityEngine;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [SerializeField] private TMP_Text blueScoreText;
    [SerializeField] private TMP_Text redScoreText;
    
    private NetworkVariable<int> blueScore = new NetworkVariable<int>();
    private NetworkVariable<int> redScore = new NetworkVariable<int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        blueScore.OnValueChanged += (_, newValue) => UpdateScoreUI();
        redScore.OnValueChanged += (_, newValue) => UpdateScoreUI();
        UpdateScoreUI();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ScoreGoalServerRpc(bool isBlueTeam)
    {
        Debug.Log($"Goal scored for {(isBlueTeam ? "Blue" : "Red")} team!");
        
        if (isBlueTeam)
        {
            blueScore.Value++;
            Debug.Log($"Blue score: {blueScore.Value}");
        }
        else
        {
            redScore.Value++;
            Debug.Log($"Red score: {redScore.Value}");
        }

        // Immediately reset puck on server
        var puck = Object.FindAnyObjectByType<Puck>();
        if (puck != null)
        {
            puck.ResetPosition();
        }
        
        // Update all clients
        UpdateScoreClientRpc(blueScore.Value, redScore.Value);
    }

    [ClientRpc]
    private void UpdateScoreClientRpc(int blueScoreValue, int redScoreValue)
    {
        // Force UI update on all clients
        if (blueScoreText) blueScoreText.text = $"Blue: {blueScoreValue}";
        if (redScoreText) redScoreText.text = $"Red: {redScoreValue}";
    }

    private void UpdateScoreUI()
    {
        if (blueScoreText) blueScoreText.text = $"Blue: {blueScore.Value}";
        if (redScoreText) redScoreText.text = $"Red: {redScore.Value}";
        Debug.Log($"Score updated - Blue: {blueScore.Value}, Red: {redScore.Value}");
    }
}
