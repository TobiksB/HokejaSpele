using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    void Start()
    {
        if (SelectedPlayer.playerPrefab != null)
        {
            Instantiate(SelectedPlayer.playerPrefab, transform.position, transform.rotation);
        }
        else
        {
            Debug.LogError("No player prefab selected!");
        }
    }
}