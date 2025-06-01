using UnityEngine;
using TMPro;
using System.Collections;

namespace HockeyGame.Game
{
    public class GameTimer : MonoBehaviour
    {
        [Header("Timer Settings")]
        [SerializeField] private float quarterDuration = 300f; // 5 minutes per quarter
        [SerializeField] private TMP_Text timerText;
        
        private float currentTime;
        private bool isRunning = false;
        private int currentQuarter = 1;
        
        public float CurrentTime => currentTime;
        public bool IsRunning => isRunning;
        public int CurrentQuarter => currentQuarter;
        
        private void Awake()
        {
            Debug.Log("GameTimer created");
            ResetTimer();
        }
        
        private void Update()
        {
            if (isRunning && currentTime > 0)
            {
                currentTime -= Time.deltaTime;
                if (currentTime <= 0)
                {
                    currentTime = 0;
                    OnQuarterEnd();
                }
                UpdateTimerDisplay();
            }
        }
        
        public void StartTimer()
        {
            isRunning = true;
            Debug.Log("Game timer started");
        }
        
        public void StopTimer()
        {
            isRunning = false;
            Debug.Log("Game timer stopped");
        }
        
        public void ResetTimer()
        {
            currentTime = quarterDuration;
            isRunning = false;
            UpdateTimerDisplay();
        }
        
        public void NextQuarter()
        {
            currentQuarter++;
            ResetTimer();
            Debug.Log($"Advanced to quarter {currentQuarter}");
        }
        
        private void OnQuarterEnd()
        {
            StopTimer();
            Debug.Log($"Quarter {currentQuarter} ended");
            
            var quarterManager = FindFirstObjectByType<QuarterManager>();
            if (quarterManager != null)
            {
                quarterManager.EndCurrentQuarter(); // Fix: Use correct method name
            }
        }

        private void OnTimerExpired()
        {
            if (HockeyGame.Game.QuarterManager.Instance != null)
            {
                // FIXED: Use the correct method name
                HockeyGame.Game.QuarterManager.Instance.EndCurrentQuarter();
                Debug.Log("Timer expired - ending current quarter");
            }
        }
        
        private void UpdateTimerDisplay()
        {
            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(currentTime / 60);
                int seconds = Mathf.FloorToInt(currentTime % 60);
                timerText.text = $"{minutes:00}:{seconds:00}";
            }
        }
        
        public void SetTimerText(TMP_Text text)
        {
            timerText = text;
            UpdateTimerDisplay();
        }

        public void SetQuarterDuration(float duration)
        {
            quarterDuration = duration;
            ResetTimer();
        }

        public void UpdateTimer(float timeInSeconds)
        {
            // FIXED: Format time as countdown (MM:SS)
            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
            
            string timeText = string.Format("{0:00}:{1:00}", minutes, seconds);
            
            if (timerText != null)
            {
                timerText.text = timeText;
                
                // ENHANCED: Color coding for urgency
                if (timeInSeconds <= 30f)
                {
                    timerText.color = Color.red; // Last 30 seconds - red
                }
                else if (timeInSeconds <= 60f)
                {
                    timerText.color = Color.yellow; // Last minute - yellow
                }
                else
                {
                    timerText.color = Color.white; // Normal - white
                }
            }
            
            // ENHANCED: Optional warning sounds/effects
            if (timeInSeconds <= 10f && timeInSeconds > 9f)
            {
                // Could add warning sound here
                Debug.Log("10 seconds remaining!");
            }
        }
    }
}
