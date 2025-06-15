using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Šī klase nodrošina atkļūdošanas konsoli, kas var tikt parādīta spēles izpildes laikā,
// ļaujot redzēt žurnāla ziņojumus bez Unity redaktora
public class DebugConsole : MonoBehaviour
{
    [SerializeField] private GameObject consolePanel; // Konsoles UI panelis
    [SerializeField] private TMP_Text logText; // UI teksta lauks žurnāla ziņojumu attēlošanai
    [SerializeField] private ScrollRect scrollRect; // Ritināmā zona teksta attēlošanai
    [SerializeField] private Button openLogFolderButton; // Poga žurnāla mapes atvēršanai
    [SerializeField] private TMP_Text logPathText; // Teksta lauks žurnāla faila ceļa parādīšanai
    [SerializeField] private KeyCode toggleKey = KeyCode.F1; // Taustiņš konsoles pārslēgšanai (noklusējums: F1)
    
    private Queue<string> logQueue = new Queue<string>(); // Rinda žurnāla ziņojumu glabāšanai
    private const int maxLogCount = 100; // Maksimālais glabājamo ziņojumu skaits
    private bool isVisible = false; // Vai konsole pašlaik ir redzama
    private BuildLogger buildLogger; // Atsauce uz BuildLogger klasi, kas raksta žurnāla ziņojumus failā

    private void Awake()
    {
        // Iespējo žurnāla ierakstus gatavajās versijās
        Debug.unityLogger.logEnabled = true;
        Application.logMessageReceived += HandleLog; // Pievieno žurnāla notikumu apstrādātāju
        
        if (consolePanel) consolePanel.SetActive(false); // Sākotnēji paslēpj konsoles paneli
        
        // Atrod BuildLogger instanci
        buildLogger = FindFirstObjectByType<BuildLogger>();
        
        // Iestata žurnāla mapes atvēršanas pogu
        if (openLogFolderButton)
        {
            openLogFolderButton.onClick.AddListener(OpenLogFolder);
        }
        
        DontDestroyOnLoad(gameObject); // Saglabā konsoli starp ainu ielādēm
    }

    private void Start()
    {
        // Atjaunina žurnāla ceļa attēlojumu
        UpdateLogPathDisplay();
    }

    private void Update()
    {
        // Pārbauda, vai lietotājs nospieda pārslēgšanas taustiņu
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleConsole();
        }
    }

    // Apstrādā Unity žurnāla ziņojumus un saglabā tos konsolē
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string coloredLog = "";
        switch (type)
        {
            case LogType.Error:
                coloredLog = $"<color=red>[KĻŪDA]</color> {logString}";
                break;
            case LogType.Warning:
                coloredLog = $"<color=yellow>[BRĪDINĀJUMS]</color> {logString}";
                break;
            case LogType.Log:
                coloredLog = $"<color=white>[ŽURNĀLS]</color> {logString}";
                break;
            default:
                coloredLog = logString;
                break;
        }

        // Pievieno laika zīmogu ziņojumam un saglabā to rindā
        logQueue.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {coloredLog}");
        
        // Ierobežo žurnāla izmēru, noņemot vecākos ierakstus
        if (logQueue.Count > maxLogCount)
        {
            logQueue.Dequeue();
        }

        // Atjaunina konsoles UI attēlojumu
        UpdateLogDisplay();
    }

    // Atjaunina konsoles teksta attēlojumu ar visiem saglabātajiem ziņojumiem
    private void UpdateLogDisplay()
    {
        if (logText != null)
        {
            logText.text = string.Join("\n", logQueue.ToArray());
            
            // Automātiski ritina uz leju, lai redzētu jaunākos ziņojumus
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    // Atjaunina žurnāla faila ceļa attēlojumu
    private void UpdateLogPathDisplay()
    {
        if (logPathText != null && buildLogger != null)
        {
            string logPath = buildLogger.GetLogFilePath();
            if (!string.IsNullOrEmpty(logPath))
            {
                logPathText.text = $"Žurnāla fails: {logPath}";
            }
            else
            {
                logPathText.text = "Žurnāla fails: Nav pieejams redaktorā";
            }
        }
    }

    // Atver žurnāla mapi failu pārlūkā
    private void OpenLogFolder()
    {
        if (buildLogger != null)
        {
            buildLogger.OpenLogDirectory();
        }
        else
        {
            Debug.LogWarning("BuildLogger nav atrasts - nevar atvērt žurnāla mapi");
        }
    }

    // Pārslēdz konsoles redzamību
    public void ToggleConsole()
    {
        isVisible = !isVisible;
        if (consolePanel) consolePanel.SetActive(isVisible);
        
        if (isVisible)
        {
            UpdateLogPathDisplay(); // Atjaunina žurnāla ceļa attēlojumu, kad konsole tiek atvērta
        }
    }

    // Notīra visus žurnāla ziņojumus no konsoles
    public void ClearLogs()
    {
        logQueue.Clear();
        UpdateLogDisplay();
    }

    // Noņem žurnāla notikumu apstrādātāju, kad konsole tiek iznīcināta
    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }
}
