using UnityEngine;
using UnityEngine.UI;

namespace HockeyGame.Game
{
    public class TrainingResetUI : MonoBehaviour
    {
        [SerializeField] private Button resetButton;
        
        private TrainingModeManager trainingManager;
        
        private void Start()
        {
            trainingManager = FindObjectOfType<TrainingModeManager>();
            
            if (resetButton == null)
            {
                // Create a button if one wasn't assigned
                CreateResetButton();
            }
            
            // Set up button click event
            if (resetButton != null)
            {
                resetButton.onClick.AddListener(ResetTraining);
            }
        }
        
        private void CreateResetButton()
        {
            // Create Canvas if needed
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("TrainingUI");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            // Create button
            GameObject buttonObj = new GameObject("ResetButton");
            buttonObj.transform.SetParent(canvas.transform, false);
            
            // Set up button component
            resetButton = buttonObj.AddComponent<Button>();
            
            // Add image
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.8f);
            
            // Add text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            Text text = textObj.AddComponent<Text>();
            text.text = "Reset";
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            
            // Position button
            RectTransform rt = buttonObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(100, 40);
            rt.anchoredPosition = new Vector2(10, 10);
            
            // Position text
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            textRT.anchoredPosition = Vector2.zero;
        }
        
        private void ResetTraining()
        {
            if (trainingManager != null)
            {
                trainingManager.ResetPlayerAndPuck();
            }
        }
    }
}
