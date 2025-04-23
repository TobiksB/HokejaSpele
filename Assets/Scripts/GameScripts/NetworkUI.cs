using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text joinCodeDisplay; // Add this field

    private void Awake()
    {
        // Find NetworkManager in DontDestroyOnLoad scene
        NetworkManager[] managers = Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
        if (managers.Length == 0)
        {
            Debug.LogError("NetworkManager not found. Please add NetworkManager to scene.");
            return;
        }

        // Setup network transport if it exists
        var transport = managers[0].GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = "127.0.0.1";
            transport.ConnectionData.Port = 7777;
        }

        // Ensure UI is visible in all instances
        Canvas canvas = GetComponent<Canvas>();
        if (canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Ensure it's on top
        }

        // Position buttons for each instance
        RectTransform rect = GetComponent<RectTransform>();
        if (rect)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
        }
    }

    private void Start()
    {
        // Find NetworkManager in case it wasn't available in Awake
        if (NetworkManager.Singleton == null)
        {
            NetworkManager[] managers = Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            if (managers.Length == 0) return;
        }

        if (NetworkManager.Singleton == null) return;

        hostButton.onClick.AddListener(async () => {
            string joinCode = await RelayManager.Instance.CreateRelay();
            if (string.IsNullOrEmpty(joinCode))
            {
                UpdateStatus("Failed to create relay");
                return;
            }

            if (NetworkManager.Singleton.StartHost())
            {
                UpdateStatus($"Connected as Host");
                HideButtons();
                ShowJoinCode(joinCode);
            }
        });

        clientButton.onClick.AddListener(async () => {
            string joinCode = joinCodeInput.text.ToUpper();
            if (string.IsNullOrEmpty(joinCode))
            {
                UpdateStatus("Please enter a join code");
                return;
            }

            bool joined = await RelayManager.Instance.JoinRelay(joinCode);
            if (!joined)
            {
                UpdateStatus("Failed to join relay");
                return;
            }

            if (NetworkManager.Singleton.StartClient())
            {
                UpdateStatus("Joined as client");
                HideButtons();
            }
        });

        NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
        {
            UpdateStatus($"Connected as {(NetworkManager.Singleton.IsHost ? "Host" : "Client")}");
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += (id) =>
        {
            UpdateStatus("Disconnected");
            ShowButtons();
        };
    }

    private void HideButtons()
    {
        if (hostButton) hostButton.gameObject.SetActive(false);
        if (clientButton) clientButton.gameObject.SetActive(false);
        if (joinCodeInput) joinCodeInput.gameObject.SetActive(false);
    }

    private void ShowButtons()
    {
        if (hostButton) hostButton.gameObject.SetActive(true);
        if (clientButton) clientButton.gameObject.SetActive(true);
    }

    private void UpdateStatus(string status)
    {
        if (statusText)
        {
            statusText.text = status;
            Debug.Log($"Network Status: {status}"); // Additional debug info
        }
    }

    private void ShowJoinCode(string joinCode)
    {
        if (joinCodeDisplay != null)
        {
            joinCodeDisplay.gameObject.SetActive(true);
            joinCodeDisplay.text = $"Join Code: {joinCode}";
        }
    }
}
