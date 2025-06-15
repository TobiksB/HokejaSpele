using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HockeyGame.Game
{
    public class TrainingHUD : MonoBehaviour
    {
        [SerializeField] private Button resetButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private TextMeshProUGUI instructionsText;
        
        private TrainingModeManager trainingManager;
        
        private void Awake()
        {
            // Atrast training manager, ja tas nav piešķirts
            trainingManager = FindObjectOfType<TrainingModeManager>();
            
            
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnResetButtonClicked);
            }
            
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
            }
            
        
            if (instructionsText != null)
            {
                instructionsText.text = "Treniņa režīms\n\nW/S - Kustība\nA/D - Rotācija\nShift - Sprints\nSpace - Ātrā apstāšanās\nE - Pacelt ripu\nPeles poga - Mest (turēt, lai uzlādētu)\nESC - Pauzes izvēlne";
            }
        }
        
        private void OnResetButtonClicked()
        {
            if (trainingManager != null)
            {
                trainingManager.ResetTraining();
            }
            else
            {
                Debug.LogWarning("TrainingHUD: TrainingModeManager not found!");
            }
        }
        
        private void OnMainMenuButtonClicked()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }
}
