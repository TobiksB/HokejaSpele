using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace HockeyGame.Game
{
    public class QuarterTransitionPanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text quarterText;
        [SerializeField] private TMP_Text transitionText;
        [SerializeField] private float displayDuration = 3f;

        private void Awake()
        {
            CreateBasicPanel();
            gameObject.SetActive(false);
        }

        private void CreateBasicPanel()
        {
            // Create main panel
            panel = new GameObject("QuarterTransitionPanel");
            panel.transform.SetParent(transform);
            
            Canvas canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            
            // Background
            Image background = panel.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.9f);
            
            // Quarter text
            GameObject textObj = new GameObject("QuarterText");
            textObj.transform.SetParent(panel.transform);
            
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(600, 200);
            
            try
            {
                quarterText = textObj.AddComponent<TextMeshProUGUI>();
                quarterText.text = "QUARTER 1";
                quarterText.color = Color.white;
                quarterText.fontSize = 48;
                quarterText.alignment = TextAlignmentOptions.Center;
            }
            catch
            {
                // Fallback to regular Text
                Text fallbackText = textObj.AddComponent<Text>();
                fallbackText.text = "QUARTER 1";
                fallbackText.color = Color.white;
                fallbackText.fontSize = 48;
                fallbackText.alignment = TextAnchor.MiddleCenter;
                fallbackText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // FIXED: Changed from LegacyRuntime.ttf
            }
            
            panel.SetActive(false);
        }
        
        public void ShowQuarterTransition(int quarter)
        {
            if (panel != null)
            {
                panel.SetActive(true);
                
                if (quarterText != null)
                {
                    quarterText.text = $"QUARTER {quarter}";
                }
                
                // Auto-hide after 2 seconds
                Invoke(nameof(HidePanel), displayDuration);
            }
        }
        
        private void HidePanel()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void ShowTransition(int nextQuarter)
        {
            if (panel != null)
            {
                panel.SetActive(true);
                
                if (quarterText != null)
                {
                    quarterText.text = $"Quarter {nextQuarter}";
                }
                
                if (transitionText != null)
                {
                    transitionText.text = "Next Quarter Starting...";
                }
                
                Debug.Log($"Showing transition to Quarter {nextQuarter}");
            }
        }

        // FIXED: Add missing HideTransition method
        public void HideTransition()
        {
            if (panel != null)
            {
                panel.SetActive(false);
                Debug.Log("Hiding quarter transition panel");
            }
        }
    }
}
