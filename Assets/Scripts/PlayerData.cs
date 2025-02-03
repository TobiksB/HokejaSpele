using UnityEngine;

public class PlayerData : MonoBehaviour
{
    private static PlayerData instance;
    public static PlayerData Instance
    {
        get { return instance; }
    }

    public Color playerColor;
    public GameObject playerPrefab;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("PlayerData initialized. Instance ID: " + GetInstanceID());
    }

    public void ValidateData()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerData: Player prefab is not assigned!");
        }
        Debug.Log($"PlayerData state - Color: {playerColor}, Prefab assigned: {playerPrefab != null}");
    }
}
