using UnityEngine;
using System.IO;
using System;

// Šī klase nodrošina spēles žurnalēšanu failā, lai palīdzētu atkļūdot problēmas gatavajās (build) versijās
// Tā saglabā visus Debug.Log, LogWarning un LogError ziņojumus failā, kas atrodas lietotāja AppData mapē
public class BuildLogger : MonoBehaviour
{
    private StreamWriter logWriter; // Rakstītājs žurnāla failam
    private string logFilePath; // Žurnāla faila ceļš

    private void Awake()
    {
        // Iespējot tikai gatavajās versijās, nevis redaktorā
        #if !UNITY_EDITOR
        InitializeLogger();
        #endif
        
        // Saglabā objektu starp ainu ielādēm
        DontDestroyOnLoad(gameObject);
    }

    // Inicializē žurnalēšanas sistēmu, izveidojot failu un abonējot ziņojumu notikumus
    private void InitializeLogger()
    {
        try
        {
            // Izveido žurnāla direktoriju AppData/Local mapē
            string appName = Application.productName.Replace(" ", "");
            string companyName = Application.companyName.Replace(" ", "");
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                companyName,
                appName,
                "Logs"
            );
            
            // Izveido direktoriju, ja tā neeksistē
            Directory.CreateDirectory(appDataPath);
            
            // Izveido žurnāla failu ar laika zīmogu
            string logFileName = $"game_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            logFilePath = Path.Combine(appDataPath, logFileName);
            
            // Inicializē faila rakstītāju ar automātisko iztukšošanu
            logWriter = new StreamWriter(logFilePath, true);
            logWriter.AutoFlush = true;
            
            // Abonē Unity žurnāla notikumus
            Application.logMessageReceived += HandleLog;
            
            // Reģistrē sākotnējo informāciju par spēli un sistēmu
            Debug.Log($"BuildLogger inicializēts. Žurnāla fails: {logFilePath}");
            LogToFile($"=== Spēles žurnāls sākts {DateTime.Now} ===");
            LogToFile($"Lietotnes versija: {Application.version}");
            LogToFile($"Unity versija: {Application.unityVersion}");
            LogToFile($"Platforma: {Application.platform}");
            LogToFile($"Datu ceļš: {Application.persistentDataPath}");
            LogToFile($"Žurnāla ceļš: {logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Neizdevās inicializēt BuildLogger: {e.Message}");
        }
    }

    // Apstrādā Unity žurnāla ziņojumus un saglabā tos failā
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Pievieno laika zīmogu katram ziņojumam
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] [{type}] {logString}";
        
        // Kļūdām un izņēmumiem pievieno pilno izsaukumu ķēdi
        if (type == LogType.Error || type == LogType.Exception)
        {
            logEntry += $"\nIzsaukumu ķēde: {stackTrace}";
        }
        
        // Saglabā ziņojumu failā
        LogToFile(logEntry);
    }

    // Ieraksta ziņojumu žurnāla failā
    private void LogToFile(string message)
    {
        try
        {
            logWriter?.WriteLine(message);
        }
        catch (Exception e)
        {
            Debug.LogError($"Neizdevās ierakstīt žurnāla failā: {e.Message}");
        }
    }

    // Publiska metode, lai iegūtu pašreizējā žurnāla faila ceļu
    public string GetLogFilePath()
    {
        return logFilePath;
    }

    // Publiska metode, lai atvērtu žurnāla direktoriju pārlūkā
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
            Debug.LogError($"Neizdevās atvērt žurnāla direktoriju: {e.Message}");
        }
    }

    // Reģistrē, kad lietotne tiek pauzēta (piem., minimizēta vai paslēpta)
    private void OnApplicationPause(bool pauseStatus)
    {
        LogToFile($"Lietotne pauzēta: {pauseStatus}");
    }

    // Reģistrē, kad lietotne iegūst vai zaudē fokusu
    private void OnApplicationFocus(bool hasFocus)
    {
        LogToFile($"Lietotnes fokuss: {hasFocus}");
    }

    // Noslēdz žurnālu, kad objekts tiek iznīcināts
    private void OnDestroy()
    {
        LogToFile("=== Spēles žurnāls beidzies ===");
        Application.logMessageReceived -= HandleLog;
        logWriter?.Close();
    }
}
