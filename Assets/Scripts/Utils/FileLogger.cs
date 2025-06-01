using UnityEngine;
using System.IO;

public static class FileLogger
{
    private static string logFilePath;
    private static bool isInitialized = false;

    private static void Initialize()
    {
        if (isInitialized) return;

        string logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        logFilePath = Path.Combine(logDirectory, $"game_log_{timestamp}.txt");
        
        isInitialized = true;
        
        // Write initial header
        LogToFile("=== HOCKEY GAME LOG STARTED ===");
        LogToFile($"Unity Version: {Application.unityVersion}");
        LogToFile($"Platform: {Application.platform}");
        LogToFile($"Build: {Application.version}");
        LogToFile($"Timestamp: {System.DateTime.Now}");
        LogToFile("====================================");
    }

    public static void LogToFile(string message)
    {
        if (!isInitialized)
        {
            Initialize();
        }

        try
        {
            string logEntry = $"[{System.DateTime.Now:HH:mm:ss.fff}] {message}";
            File.AppendAllText(logFilePath, logEntry + "\n");
            
            // Also log to Unity console in editor
            #if UNITY_EDITOR
            Debug.Log($"[FILE] {message}");
            #endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to write to log file: {e.Message}");
        }
    }

    public static void LogError(string message)
    {
        LogToFile($"[ERROR] {message}");
        Debug.LogError(message);
    }

    public static void LogWarning(string message)
    {
        LogToFile($"[WARNING] {message}");
        Debug.LogWarning(message);
    }

    public static string GetLogFilePath()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        return logFilePath;
    }
}
