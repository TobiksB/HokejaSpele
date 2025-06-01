using UnityEngine;
using System.IO;
using System;

public class BuildLogger : MonoBehaviour
{
    private StreamWriter logWriter;
    private string logFilePath;

    private void Awake()
    {
        // Only enable in builds, not in editor
        #if !UNITY_EDITOR
        InitializeLogger();
        #endif
        
        DontDestroyOnLoad(gameObject);
    }

    private void InitializeLogger()
    {
        try
        {
            // Create log directory in AppData/Local
            string appName = Application.productName.Replace(" ", "");
            string companyName = Application.companyName.Replace(" ", "");
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                companyName,
                appName,
                "Logs"
            );
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(appDataPath);
            
            // Create log file with timestamp
            string logFileName = $"game_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            logFilePath = Path.Combine(appDataPath, logFileName);
            
            logWriter = new StreamWriter(logFilePath, true);
            logWriter.AutoFlush = true;
            
            // Subscribe to Unity's log events
            Application.logMessageReceived += HandleLog;
            
            Debug.Log($"BuildLogger initialized. Log file: {logFilePath}");
            LogToFile($"=== Game Log Started at {DateTime.Now} ===");
            LogToFile($"Application version: {Application.version}");
            LogToFile($"Unity version: {Application.unityVersion}");
            LogToFile($"Platform: {Application.platform}");
            LogToFile($"Data path: {Application.persistentDataPath}");
            LogToFile($"Log path: {logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize BuildLogger: {e.Message}");
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] [{type}] {logString}";
        
        if (type == LogType.Error || type == LogType.Exception)
        {
            logEntry += $"\nStack Trace: {stackTrace}";
        }
        
        LogToFile(logEntry);
    }

    private void LogToFile(string message)
    {
        try
        {
            logWriter?.WriteLine(message);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to write to log file: {e.Message}");
        }
    }

    // Public method to get the current log file path
    public string GetLogFilePath()
    {
        return logFilePath;
    }

    // Public method to open the log directory in explorer
    public void OpenLogDirectory()
    {
        try
        {
            string logDirectory = Path.GetDirectoryName(logFilePath);
            if (Directory.Exists(logDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", logDirectory);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to open log directory: {e.Message}");
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        LogToFile($"Application paused: {pauseStatus}");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        LogToFile($"Application focus: {hasFocus}");
    }

    private void OnDestroy()
    {
        LogToFile("=== Game Log Ended ===");
        Application.logMessageReceived -= HandleLog;
        logWriter?.Close();
    }
}
