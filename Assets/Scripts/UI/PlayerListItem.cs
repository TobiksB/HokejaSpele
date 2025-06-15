using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// Šī klase atbild par viena spēlētāja informācijas attēlošanu lobija spēlētāju sarakstā.
/// Tā parāda spēlētāja vārdu, komandas piederību (ar krāsu indikatoru) un gatavības statusu.
/// Katrs PlayerListItem objekts tiek izveidots dinamiski LobbyPanelManager klasē,
/// lai attēlotu visus spēlētājus, kas pievienojušies lobijam.
public class PlayerListItem : MonoBehaviour
{
    [Header("UI Komponentes")]
    [SerializeField] private Image backgroundImage;    // Elementa fona attēls
    [SerializeField] private TMP_Text playerNameText;  // Teksta lauks spēlētāja vārdam
    [SerializeField] private Image teamIndicator;      // Krāsas indikators, kas parāda komandas piederību
    [SerializeField] private Image readyIndicator;     // Krāsas indikators, kas parāda gatavības statusu

    /// Inicializācijas funkcija, kas tiek izsaukta, kad objekts tiek izveidots.
    /// Pārbauda un atrod backgroundImage komponenti, ja tā nav piešķirta caur inspektoru.
    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    /// Galvenā funkcija, kas iestata un atjaunina spēlētāja informāciju UI elementā.
    public void SetPlayerInfo(string playerName, bool isBlueTeam, bool isReady)
    {
        playerNameText.text = playerName;
        
        if (teamIndicator != null)
        {
            teamIndicator.gameObject.SetActive(true);
            Color blueTeam = new Color(0.2f, 0.4f, 1f, 1f);  // Spilgti zila krāsa
            Color redTeam = new Color(1f, 0.2f, 0.2f, 1f);   // Spilgti sarkana krāsa
            teamIndicator.color = isBlueTeam ? blueTeam : redTeam;
        }
        
        if (readyIndicator != null)
        {
            readyIndicator.gameObject.SetActive(true);
            Color readyColor = new Color(0.2f, 1f, 0.2f, 1f);   // Spilgti zaļa krāsa gatavam statusam
            Color notReadyColor = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Daļēji caurspīdīga pelēka krāsa negatavam statusam
            readyIndicator.color = isReady ? readyColor : notReadyColor;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Tumši pelēks, daļēji caurspīdīgs fons
        }
    }
}