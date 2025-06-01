using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HockeyGame.Game
{
    public class GameOverPanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text gameOverText;
        [SerializeField] private TMP_Text finalScoreText;
        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        private void Awake()
        {
            if (panel == null)
            {
                CreateBasicPanel();
            }

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void CreateBasicPanel()
        {
            // Create main panel
            panel = new GameObject("GameOverPanel");
            panel.transform.SetParent(transform);

            Canvas canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = panel.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Background
            Image background = panel.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.8f);

            // Game Over text
            CreateText("GameOverText", "GAME OVER", Color.white, new Vector2(0, 200), 48, out gameOverText);

            // Final Score text
            CreateText("FinalScoreText", "Final Score: 0 - 0", Color.yellow, new Vector2(0, 100), 36, out finalScoreText);

            // Winner text
            CreateText("WinnerText", "", Color.green, new Vector2(0, 0), 42, out winnerText);

            Debug.Log("Created basic GameOverPanel");
        }

        private void CreateText(string name, string text, Color color, Vector2 position, int fontSize, out TMP_Text textComponent)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(panel.transform);

            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = new Vector2(800, 100);

            // Try to add TextMeshPro component
            try
            {
                textComponent = textObj.AddComponent<TextMeshProUGUI>();
            }
            catch
            {
                // Fallback to regular Text if TMP fails
                Text fallbackText = textObj.AddComponent<Text>();
                fallbackText.text = text;
                fallbackText.color = color;
                fallbackText.fontSize = fontSize;
                fallbackText.alignment = TextAnchor.MiddleCenter;
                fallbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                textComponent = null;
                return;
            }

            if (textComponent != null)
            {
                textComponent.text = text;
                textComponent.color = color;
                textComponent.fontSize = fontSize;
                textComponent.alignment = TextAlignmentOptions.Center;
            }
        }

        public void ShowGameOver(int redScore, int blueScore)
        {
            if (panel != null)
            {
                panel.SetActive(true);
                
                if (finalScoreText != null)
                {
                    finalScoreText.text = $"Final Score: {redScore} - {blueScore}";
                }
                
                if (winnerText != null)
                {
                    if (redScore > blueScore)
                    {
                        winnerText.text = "RED TEAM WINS!";
                        winnerText.color = Color.red;
                    }
                    else if (blueScore > redScore)
                    {
                        winnerText.text = "BLUE TEAM WINS!";
                        winnerText.color = Color.blue;
                    }
                    else
                    {
                        winnerText.text = "IT'S A TIE!";
                        winnerText.color = Color.yellow;
                    }
                }
            }
            
            Debug.Log($"Game Over: Red {redScore} - Blue {blueScore}");
        }

        public void HideGameOver()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }
    }
}
