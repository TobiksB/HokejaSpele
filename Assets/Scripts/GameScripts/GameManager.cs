using UnityEngine;
using Unity.Netcode;

namespace HockeyGame.Game
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }
        
        [Header("UI References - TextMeshPro (Ieteicams)")]
        [SerializeField] private TMPro.TextMeshProUGUI redScoreTMP;
        [SerializeField] private TMPro.TextMeshProUGUI blueScoreTMP;
        
        [Header("UI References - Legacy Text (Rezerves variants)")]
        [SerializeField] private UnityEngine.UI.Text redScoreText;
        [SerializeField] private UnityEngine.UI.Text blueScoreText;
        
        [Header("Spēles norādes")]
        [SerializeField] private ScoreManager scoreManager;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("GameManager: Instance izveidota");
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // Atrast ScoreManager, ja nav piešķirts
            if (scoreManager == null)
            {
                scoreManager = FindFirstObjectByType<ScoreManager>();
            }
            
            // Abonēt rezultātu izmaiņas UI atjauninājumiem
            if (scoreManager != null)
            {
                scoreManager.OnScoreChanged += UpdateScoreUI;
                Debug.Log("GameManager: Abonēti ScoreManager notikumi");
            }
            
            // Automātiski atrast rezultātu tekstus, ja nav piešķirti (prioritāte TMP)
            if (redScoreTMP == null || blueScoreTMP == null)
            {
                FindScoreUIElements();
            }
            
            Debug.Log("GameManager: Tīkla instances izveidotas");
        }
        
        public override void OnNetworkDespawn()
        {
            // Atrakstīties no rezultātu izmaiņām
            if (scoreManager != null)
            {
                scoreManager.OnScoreChanged -= UpdateScoreUI;
            }
            
            base.OnNetworkDespawn();
        }
        
        private void FindScoreUIElements()
        {
            Debug.Log("GameManager: Automātiski meklē rezultātu UI elementus (prioritāte TextMeshPro)...");
            
            // PRIORITĀTE: Vispirms atrast TextMeshPro komponentes
            var allTMPs = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var tmp in allTMPs)
            {
                string tmpName = tmp.name.ToLower();
                
                if (redScoreTMP == null && tmpName.Contains("red") && tmpName.Contains("score"))
                {
                    redScoreTMP = tmp;
                    Debug.Log($"GameManager: Atrasts sarkanās komandas rezultāta TMP: {tmp.name}");
                }
                else if (blueScoreTMP == null && tmpName.Contains("blue") && tmpName.Contains("score"))
                {
                    blueScoreTMP = tmp;
                    Debug.Log($"GameManager: Atrasts zilās komandas rezultāta TMP: {tmp.name}");
                }
            }
            
            // REZERVES VARIANTS: Atrast UI Text komponentes tikai ja TMP nav atrasts
            if (redScoreText == null || blueScoreText == null)
            {
                var allTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
                foreach (var text in allTexts)
                {
                    string textName = text.name.ToLower();
                    
                    if (redScoreText == null && textName.Contains("red") && textName.Contains("score"))
                    {
                        redScoreText = text;
                        Debug.Log($"GameManager: Atrasts sarkanās komandas rezultāta teksts (rezerves variants): {text.name}");
                    }
                    else if (blueScoreText == null && textName.Contains("blue") && textName.Contains("score"))
                    {
                        blueScoreText = text;
                        Debug.Log($"GameManager: Atrasts zilās komandas rezultāta teksts (rezerves variants): {text.name}");
                    }
                }
            }
        }
        
        private void UpdateScoreUI(int redScore, int blueScore)
        {
            Debug.Log($"GameManager: Atjaunina rezultātu UI - Sarkanā: {redScore}, Zilā: {blueScore}");
            
            // PRIORITĀTE: Vispirms atjaunināt TextMeshPro komponentes
            if (redScoreTMP != null)
            {
                redScoreTMP.text = redScore.ToString();
                Debug.Log($"GameManager: Atjaunināts sarkanās komandas rezultāta TMP uz {redScore}");
            }
            
            if (blueScoreTMP != null)
            {
                blueScoreTMP.text = blueScore.ToString();
                Debug.Log($"GameManager: Atjaunināts zilās komandas rezultāta TMP uz {blueScore}");
            }
            
            // REZERVES VARIANTS: Atjaunināt UI Text komponentes, ja TMP nav pieejams
            if (redScoreTMP == null && redScoreText != null)
            {
                redScoreText.text = redScore.ToString();
                Debug.Log($"GameManager: Atjaunināts sarkanās komandas rezultāta teksts (rezerves variants) uz {redScore}");
            }
            
            if (blueScoreTMP == null && blueScoreText != null)
            {
                blueScoreText.text = blueScore.ToString();
                Debug.Log($"GameManager: Atjaunināts zilās komandas rezultāta teksts (rezerves variants) uz {blueScore}");
            }
            
            // Ja nav piešķirtu UI elementu, mēģināt atrast tos automātiski
            if (redScoreTMP == null && blueScoreTMP == null && redScoreText == null && blueScoreText == null)
            {
                FindAndUpdateScoreUI(redScore, blueScore);
            }
        }
        
        private void FindAndUpdateScoreUI(int redScore, int blueScore)
        {
            // PRIORITĀTE: Vispirms mēģināt atrast un atjaunināt TMP tekstus
            var allTMPs = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            bool foundRedTMP = false;
            bool foundBlueTMP = false;
            
            foreach (var tmp in allTMPs)
            {
                string tmpName = tmp.name.ToLower();
                
                if (!foundRedTMP && tmpName.Contains("red") && tmpName.Contains("score"))
                {
                    tmp.text = redScore.ToString();
                    Debug.Log($"GameManager: Atrasts un atjaunināts sarkanās komandas rezultāta TMP: {tmp.name}");
                    foundRedTMP = true;
                }
                else if (!foundBlueTMP && tmpName.Contains("blue") && tmpName.Contains("score"))
                {
                    tmp.text = blueScore.ToString();
                    Debug.Log($"GameManager: Atrasts un atjaunināts zilās komandas rezultāta TMP: {tmp.name}");
                    foundBlueTMP = true;
                }
            }
            
            // REZERVES VARIANTS: Mēģināt parasto UI Text tikai ja TMP nav atrasts
            if (!foundRedTMP || !foundBlueTMP)
            {
                var allTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
                foreach (var text in allTexts)
                {
                    string textName = text.name.ToLower();
                    
                    if (!foundRedTMP && textName.Contains("red") && textName.Contains("score"))
                    {
                        text.text = redScore.ToString();
                        Debug.Log($"GameManager: Atrasts un atjaunināts sarkanās komandas rezultāta teksts (rezerves variants): {text.name}");
                    }
                    else if (!foundBlueTMP && textName.Contains("blue") && textName.Contains("score"))
                    {
                        text.text = blueScore.ToString();
                        Debug.Log($"GameManager: Atrasts un atjaunināts zilās komandas rezultāta teksts (rezerves variants): {text.name}");
                    }
                }
            }
        }
        
        public void OnGameEnd()
        {
            Debug.Log("GameManager: Spēle beigusies");
            // Šeit apstrādāt spēles beigu loģiku
        }
    }
}
