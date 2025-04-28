using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image teamIndicator;
    [SerializeField] private Image readyIndicator;

    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    public void SetPlayerInfo(string playerName, bool isBlueTeam, bool isReady)
    {
        playerNameText.text = playerName;
        
        // Update team indicator with vibrant colors
        if (teamIndicator != null)
        {
            teamIndicator.gameObject.SetActive(true);
            Color blueTeam = new Color(0.2f, 0.4f, 1f, 1f);  // Bright blue
            Color redTeam = new Color(1f, 0.2f, 0.2f, 1f);   // Bright red
            teamIndicator.color = isBlueTeam ? blueTeam : redTeam;
        }
        
        // Update ready indicator with glowing effect
        if (readyIndicator != null)
        {
            readyIndicator.gameObject.SetActive(true);
            Color readyColor = new Color(0.2f, 1f, 0.2f, 1f);   // Bright green
            Color notReadyColor = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Semi-transparent gray
            readyIndicator.color = isReady ? readyColor : notReadyColor;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        }
    }
}