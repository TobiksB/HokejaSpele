using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HockeyGame.Game
{
    public class GameUI : MonoBehaviour
    {
        [Header("Score Display")]
        [SerializeField] private TMP_Text redScoreText;
        [SerializeField] private TMP_Text blueScoreText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text quarterText;
        
        private Canvas mainCanvas;
        
        private void Awake()
        {
            Debug.Log("GameUI initialized");
            CreateBasicUI();
        }
        
        private void CreateBasicUI()
        {
            // Get or create canvas
            mainCanvas = GetComponent<Canvas>();
            if (mainCanvas == null)
            {
                mainCanvas = gameObject.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                gameObject.AddComponent<GraphicRaycaster>();
            }
            
            // Create score texts with fallback to regular Text
            CreateScoreText("RedScoreText", "0", Color.red, new Vector2(-200, 450), out redScoreText);
            CreateScoreText("BlueScoreText", "0", Color.blue, new Vector2(200, 450), out blueScoreText);
            CreateScoreText("TimerText", "05:00", Color.white, new Vector2(0, 450), out timerText);
            CreateScoreText("QuarterText", "Q1", Color.yellow, new Vector2(0, 400), out quarterText);
        }
        
        private void CreateScoreText(string name, string text, Color color, Vector2 position, out TMP_Text textComponent)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(transform);
            
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = new Vector2(200, 50);
            
            // Try TextMeshPro first, fallback to regular Text
            try
            {
                textComponent = textObj.AddComponent<TextMeshProUGUI>();
                textComponent.text = text;
                textComponent.color = color;
                textComponent.fontSize = 36;
                textComponent.alignment = TextAlignmentOptions.Center;
            }
            catch
            {
                // Fallback to regular Text
                Text fallbackText = textObj.AddComponent<Text>();
                fallbackText.text = text;
                fallbackText.color = color;
                fallbackText.fontSize = 36;
                fallbackText.alignment = TextAnchor.MiddleCenter;
                fallbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // FIXED: Changed from LegacyRuntime.ttf
                
                textComponent = null;
            }
        }
        
        public void UpdateScore(int redScore, int blueScore)
        {
            if (redScoreText != null)
                redScoreText.text = redScore.ToString();
            if (blueScoreText != null)
                blueScoreText.text = blueScore.ToString();
        }
        
        public void UpdateTimer(string timeText)
        {
            if (timerText != null)
                timerText.text = timeText;
        }
        
        public void UpdateQuarter(int quarter)
        {
            if (quarterText != null)
                quarterText.text = $"Q{quarter}";
        }
    }
}
