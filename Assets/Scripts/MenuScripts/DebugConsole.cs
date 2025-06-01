using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DebugConsole : MonoBehaviour
{
    [SerializeField] private GameObject consolePanel;
    [SerializeField] private TMP_Text logText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Button openLogFolderButton;
    [SerializeField] private TMP_Text logPathText;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    
    private Queue<string> logQueue = new Queue<string>();
    private const int maxLogCount = 100;
    private bool isVisible = false;
    private BuildLogger buildLogger;

    private void Awake()
    {
        // Enable logging in builds
        Debug.unityLogger.logEnabled = true;
        Application.logMessageReceived += HandleLog;
        
        if (consolePanel) consolePanel.SetActive(false);
        
        // Find BuildLogger
        buildLogger = FindFirstObjectByType<BuildLogger>();
        
        // Setup log folder button
        if (openLogFolderButton)
        {
            openLogFolderButton.onClick.AddListener(OpenLogFolder);
        }
        
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Update log path display
        UpdateLogPathDisplay();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleConsole();
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string coloredLog = "";
        switch (type)
        {
            case LogType.Error:
                coloredLog = $"<color=red>[ERROR]</color> {logString}";
                break;
            case LogType.Warning:
                coloredLog = $"<color=yellow>[WARNING]</color> {logString}";
                break;
            case LogType.Log:
                coloredLog = $"<color=white>[LOG]</color> {logString}";
                break;
            default:
                coloredLog = logString;
                break;
        }

        logQueue.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {coloredLog}");
        
        if (logQueue.Count > maxLogCount)
        {
            logQueue.Dequeue();
        }

        UpdateLogDisplay();
    }

    private void UpdateLogDisplay()
    {
        if (logText != null)
        {
            logText.text = string.Join("\n", logQueue.ToArray());
            
            // Auto-scroll to bottom
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    private void UpdateLogPathDisplay()
    {
        if (logPathText != null && buildLogger != null)
        {
            string logPath = buildLogger.GetLogFilePath();
            if (!string.IsNullOrEmpty(logPath))
            {
                logPathText.text = $"Log file: {logPath}";
            }
            else
            {
                logPathText.text = "Log file: Not available in editor";
            }
        }
    }

    private void OpenLogFolder()
    {
        if (buildLogger != null)
        {
            buildLogger.OpenLogDirectory();
        }
        else
        {
            Debug.LogWarning("BuildLogger not found - cannot open log folder");
        }
    }

    public void ToggleConsole()
    {
        isVisible = !isVisible;
        if (consolePanel) consolePanel.SetActive(isVisible);
        
        if (isVisible)
        {
            UpdateLogPathDisplay();
        }
    }

    public void ClearLogs()
    {
        logQueue.Clear();
        UpdateLogDisplay();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }
}
