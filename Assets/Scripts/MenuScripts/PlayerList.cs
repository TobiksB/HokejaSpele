using UnityEngine;
using System.Collections.Generic;

public class PlayerList : MonoBehaviour
{
    [SerializeField] private Transform contentPanel;
    [SerializeField] private PlayerListItem playerItemPrefab;

    private List<PlayerListItem> activeItems = new List<PlayerListItem>();

    public void UpdatePlayerList(List<LobbyPlayerData> players)
    {
        // Notīrīt esošos elementus
        ClearList();

        // Izveidot jaunus elementus katram spēlētājam
        foreach (var player in players)
        {
            PlayerListItem item = Instantiate(playerItemPrefab, contentPanel);
            item.SetPlayerInfo(player.PlayerName, player.IsBlueTeam, player.IsReady);
            activeItems.Add(item);
        }
    }

    private void ClearList()
    {
        foreach (var item in activeItems)
        {
            Destroy(item.gameObject);
        }
        activeItems.Clear();
    }
}
