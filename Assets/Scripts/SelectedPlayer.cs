using UnityEngine;

public class SelectedPlayer : MonoBehaviour
{
    private static SelectedPlayer instance;
    private static GameObject _playerPrefab;
    
    public static SelectedPlayer Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SelectedPlayer");
                instance = go.AddComponent<SelectedPlayer>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    public static GameObject playerPrefab
    {
        get 
        { 
            if (_playerPrefab == null)
            {
                // Try to load from PlayerPrefs if available
                string prefabPath = PlayerPrefs.GetString("SelectedPlayerPrefab", "");
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    _playerPrefab = Resources.Load<GameObject>(prefabPath);
                }
            }
            return _playerPrefab; 
        }
        set 
        { 
            _playerPrefab = value;
            if (_playerPrefab != null)
            {
                // Store the prefab path in PlayerPrefs
                PlayerPrefs.SetString("SelectedPlayerPrefab", _playerPrefab.name);
                PlayerPrefs.Save();
            }
            Debug.Log($"[SelectedPlayer] Player prefab set to: {(_playerPrefab != null ? _playerPrefab.name : "null")}");
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
}