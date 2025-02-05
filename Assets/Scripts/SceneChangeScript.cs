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
        
        // Register callback for scene loading
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(SpelesAina);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Verify prefab is still assigned after scene load
        Debug.Log($"Scene {scene.name} loaded with player prefab: {(SelectedPlayer.playerPrefab != null ? SelectedPlayer.playerPrefab.name : "null")}");
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
