using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChangeScript : MonoBehaviour
{
    public GameObject selectedPlayerPrefab;

    public void ChangeScene(string SpelesAina)
    {
        if (selectedPlayerPrefab == null)
        {
            Debug.LogError("Player prefab not assigned in SceneChangeScript!");
            return;
        }
        
        SelectedPlayer.playerPrefab = selectedPlayerPrefab;
        Debug.Log("Player prefab assigned successfully: " + selectedPlayerPrefab.name);
        SceneManager.LoadScene(SpelesAina);
    }
}
