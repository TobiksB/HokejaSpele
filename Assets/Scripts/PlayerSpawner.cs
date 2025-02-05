using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour
{
    private void Start()
    {
        // Wait for next frame to ensure scene is fully loaded
        StartCoroutine(SpawnWithDelay());
    }

    private System.Collections.IEnumerator SpawnWithDelay()
    {
        yield return new WaitForSeconds(0.1f); // Short delay to ensure scene is ready

        if (SelectedPlayer.playerPrefab != null)
        {
            GameObject player = Instantiate(SelectedPlayer.playerPrefab, transform.position, transform.rotation);
            Debug.Log($"Player {player.name} spawned successfully");
        }
        else
        {
            Debug.LogError("No player prefab selected! Did you start from the correct scene?");
            // Optionally return to main menu or previous scene
            // SceneManager.LoadScene("MainMenu");
        }
    }
}