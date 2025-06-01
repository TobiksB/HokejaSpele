using UnityEngine;
using Unity.Netcode;

namespace HockeyGame.Game
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }
        
        [Header("UI References - TextMeshPro (Preferred)")]
        [SerializeField] private TMPro.TextMeshProUGUI redScoreTMP;
        [SerializeField] private TMPro.TextMeshProUGUI blueScoreTMP;
        
        [Header("UI References - Legacy Text (Fallback)")]
        [SerializeField] private UnityEngine.UI.Text redScoreText;
        [SerializeField] private UnityEngine.UI.Text blueScoreText;
        
        [Header("Game References")]
        [SerializeField] private ScoreManager scoreManager;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("GameManager: Instance created");
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // Find ScoreManager if not assigned
            if (scoreManager == null)
            {
                scoreManager = FindFirstObjectByType<ScoreManager>();
            }
            
            // Subscribe to score changes for UI updates
            if (scoreManager != null)
            {
                scoreManager.OnScoreChanged += UpdateScoreUI;
                Debug.Log("GameManager: Subscribed to ScoreManager events");
            }
            
            // Auto-find score texts if not assigned (prioritize TMP)
            if (redScoreTMP == null || blueScoreTMP == null)
            {
                FindScoreUIElements();
            }
            
            Debug.Log("GameManager: Network spawned");
        }
        
        public override void OnNetworkDespawn()
        {
            // Unsubscribe from score changes
            if (scoreManager != null)
            {
                scoreManager.OnScoreChanged -= UpdateScoreUI;
            }
            
            base.OnNetworkDespawn();
        }
        
        private void FindScoreUIElements()
        {
            Debug.Log("GameManager: Auto-finding score UI elements (prioritizing TextMeshPro)...");
            
            // PRIORITY: Find TextMeshPro components first
            var allTMPs = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var tmp in allTMPs)
            {
                string tmpName = tmp.name.ToLower();
                
                if (redScoreTMP == null && tmpName.Contains("red") && tmpName.Contains("score"))
                {
                    redScoreTMP = tmp;
                    Debug.Log($"GameManager: Found red score TMP: {tmp.name}");
                }
                else if (blueScoreTMP == null && tmpName.Contains("blue") && tmpName.Contains("score"))
                {
                    blueScoreTMP = tmp;
                    Debug.Log($"GameManager: Found blue score TMP: {tmp.name}");
                }
            }
            
            // FALLBACK: Find UI Text components only if TMP not found
            if (redScoreText == null || blueScoreText == null)
            {
                var allTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
                foreach (var text in allTexts)
                {
                    string textName = text.name.ToLower();
                    
                    if (redScoreText == null && textName.Contains("red") && textName.Contains("score"))
                    {
                        redScoreText = text;
                        Debug.Log($"GameManager: Found red score text (fallback): {text.name}");
                    }
                    else if (blueScoreText == null && textName.Contains("blue") && textName.Contains("score"))
                    {
                        blueScoreText = text;
                        Debug.Log($"GameManager: Found blue score text (fallback): {text.name}");
                    }
                }
            }
        }
        
        private void UpdateScoreUI(int redScore, int blueScore)
        {
            Debug.Log($"GameManager: Updating score UI - Red: {redScore}, Blue: {blueScore}");
            
            // PRIORITY: Update TextMeshPro components first
            if (redScoreTMP != null)
            {
                redScoreTMP.text = redScore.ToString();
                Debug.Log($"GameManager: Updated red score TMP to {redScore}");
            }
            
            if (blueScoreTMP != null)
            {
                blueScoreTMP.text = blueScore.ToString();
                Debug.Log($"GameManager: Updated blue score TMP to {blueScore}");
            }
            
            // FALLBACK: Update UI Text components if TMP not available
            if (redScoreTMP == null && redScoreText != null)
            {
                redScoreText.text = redScore.ToString();
                Debug.Log($"GameManager: Updated red score text (fallback) to {redScore}");
            }
            
            if (blueScoreTMP == null && blueScoreText != null)
            {
                blueScoreText.text = blueScore.ToString();
                Debug.Log($"GameManager: Updated blue score text (fallback) to {blueScore}");
            }
            
            // If no assigned UI elements, try to find them automatically
            if (redScoreTMP == null && blueScoreTMP == null && redScoreText == null && blueScoreText == null)
            {
                FindAndUpdateScoreUI(redScore, blueScore);
            }
        }
        
        private void FindAndUpdateScoreUI(int redScore, int blueScore)
        {
            // PRIORITY: Try to find and update TMP texts first
            var allTMPs = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            bool foundRedTMP = false;
            bool foundBlueTMP = false;
            
            foreach (var tmp in allTMPs)
            {
                string tmpName = tmp.name.ToLower();
                
                if (!foundRedTMP && tmpName.Contains("red") && tmpName.Contains("score"))
                {
                    tmp.text = redScore.ToString();
                    Debug.Log($"GameManager: Found and updated red score TMP: {tmp.name}");
                    foundRedTMP = true;
                }
                else if (!foundBlueTMP && tmpName.Contains("blue") && tmpName.Contains("score"))
                {
                    tmp.text = blueScore.ToString();
                    Debug.Log($"GameManager: Found and updated blue score TMP: {tmp.name}");
                    foundBlueTMP = true;
                }
            }
            
            // FALLBACK: Try regular UI Text only if TMP not found
            if (!foundRedTMP || !foundBlueTMP)
            {
                var allTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
                foreach (var text in allTexts)
                {
                    string textName = text.name.ToLower();
                    
                    if (!foundRedTMP && textName.Contains("red") && textName.Contains("score"))
                    {
                        text.text = redScore.ToString();
                        Debug.Log($"GameManager: Found and updated red score text (fallback): {text.name}");
                    }
                    else if (!foundBlueTMP && textName.Contains("blue") && textName.Contains("score"))
                    {
                        text.text = blueScore.ToString();
                        Debug.Log($"GameManager: Found and updated blue score text (fallback): {text.name}");
                    }
                }
            }
        }
        
        public void OnGameEnd()
        {
            Debug.Log("GameManager: Game ended");
            // Handle game end logic here
        }
    }
}
