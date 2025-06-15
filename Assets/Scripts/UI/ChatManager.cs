using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Services.Authentication; // Nepieciešams AuthenticationService izmantošanai


// Klase, kas pārvalda tērzēšanas funkcionalitāti lietotāja interfeisā.
// Ļauj spēlētājiem sūtīt ziņas un attēlo tās tērzēšanas logā.

public class ChatManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField chatInputField; // Teksta ievades lauks ziņu rakstīšanai
    [SerializeField] private TMP_Text chatContent;          // Teksta lauks, kur tiek attēlotas visas ziņas
    [SerializeField] private Button sendButton;             // Poga ziņas nosūtīšanai

    private List<string> chatMessages = new List<string>(); // Saraksts, kurā glabājas tērzēšanas vēsture
    private const int maxMessages = 50;                     // Maksimālais ziņu skaits vēsturē, lai ierobežotu atmiņas patēriņu

    
    // Tiek izsaukts, kad skripts tiek inicializēts.
    // Pievieno klausītāju nosūtīšanas pogai.

    private void Start()
    {
        sendButton.onClick.AddListener(SendMessage);
    }


    // Apstrādā ziņas nosūtīšanu, kad lietotājs nospiež sūtīšanas pogu.
    // Pārbauda, vai ievades lauks nav tukšs, pievieno ziņu vēsturei un notīra ievadi.

    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(chatInputField.text)) return; // Neļauj nosūtīt tukšas ziņas

        string playerId = AuthenticationService.Instance.PlayerId; // Iegūst spēlētāja ID no autentifikācijas servisa
        string message = $"[{System.DateTime.Now:HH:mm}] {playerId}: {chatInputField.text}"; // Formatē ziņu ar laiku un ID
        AddMessage(message);

        chatInputField.text = string.Empty; // Notīra ievades lauku pēc ziņas nosūtīšanas

    }

    //
    // Pievieno jaunu ziņu tērzēšanas vēsturei un atjaunina UI.
    // Ja sasniegts maksimālais ziņu skaits, dzēš vecākās ziņas.
    
    private void AddMessage(string message)
    {
        chatMessages.Add(message); // Pievieno jauno ziņu saraksta beigās
        
        // Ja sasniegts maksimālais ziņu limits, noņem vecāko ziņu
        if (chatMessages.Count > maxMessages)
        {
            chatMessages.RemoveAt(0);
        }

        // Atjaunina UI, apvienojot visas ziņas vienā teksta blokā
        chatContent.text = string.Join("\n", chatMessages);
    }
}
