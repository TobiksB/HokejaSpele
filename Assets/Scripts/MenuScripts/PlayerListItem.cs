using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image teamIndicator;
    [SerializeField] private Image readyIndicator;

    public void SetPlayerInfo(string playerName, bool isBlueTeam, bool isReady)
    {
        playerNameText.text = playerName;
        teamIndicator.color = isBlueTeam ? Color.blue : Color.red;
        readyIndicator.color = isReady ? Color.green : Color.gray;
    }
}
