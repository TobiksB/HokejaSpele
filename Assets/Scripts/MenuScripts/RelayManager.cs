using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System;
using System.Threading.Tasks;

namespace HockeyGame.Network
{
    public class RelayManager : MonoBehaviour
    {
        private static RelayManager _instance;
        public static RelayManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<RelayManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("RelayManager");
                        _instance = go.AddComponent<RelayManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private UnityTransport transport;
        private bool isInitialized;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                
                // FIXED: Delay network manager setup to avoid null references
                StartCoroutine(DelayedInitialization());
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private System.Collections.IEnumerator DelayedInitialization()
        {
            // Wait a frame to ensure NetworkManager is properly set up
            yield return null;
            
            var networkManager = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
            if (networkManager != null)
            {
                networkManager.NetworkConfig.EnableSceneManagement = true;
                Debug.Log("RelayManager: Enabled scene management on NetworkManager");
            }
            
            InitializeTransport();
        }

        private void InitializeTransport()
        {
            transport = UnityEngine.Object.FindFirstObjectByType<UnityTransport>();
            if (transport == null)
            {
                var networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                if (networkManager != null)
                {
                    transport = networkManager.GetComponent<UnityTransport>();
                }
            }

            if (transport == null)
            {
                Debug.LogError("RelayManager: No UnityTransport found in scene!");
                return;
            }

            isInitialized = true;
            Debug.Log("RelayManager: Initialized successfully");
        }

        public async Task InitializeRelay()
        {
            try
            {
                if (!isInitialized)
                {
                    InitializeTransport();
                }

                // Only initialize if not already initialized
                if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
                {
                    Debug.Log("RelayManager: Initializing Unity Services...");
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("RelayManager: Signing in anonymously...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                
                var networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                var transport = UnityEngine.Object.FindFirstObjectByType<UnityTransport>();
                
                if (networkManager == null || transport == null)
                {
                    Debug.LogError("RelayManager: Required components not found in scene!");
                    return;
                }

                Debug.Log("RelayManager: Relay services initialized successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"RelayManager: Failed to initialize relay: {e.Message}");
            }
        }

        // Modified: Only configure transport, don't start host/client
        public async Task<string> CreateRelay(int maxConnections, int maxPlayers)
        {
            if (!isInitialized)
            {
                await InitializeRelay();
            }

            try
            {
                Debug.Log($"RelayManager: Creating Relay allocation for {maxPlayers} players, {maxConnections} connections");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                // Only configure transport - let GameNetworkManager handle starting host
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                Debug.Log($"RelayManager: Relay configured for host with join code: {joinCode}");
                Debug.Log($"RelayManager: Server: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
                
                return joinCode;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RelayManager: Failed to create relay: {e}");
                return null;
            }
        }

        // Modified: Only configure transport, don't start client
        public async Task<bool> JoinRelay(string joinCode)
        {
            if (!isInitialized)
            {
                Debug.LogError("RelayManager: Not initialized!");
                return false;
            }

            try
            {
                Debug.Log($"RelayManager: Joining Relay with code: {joinCode}");
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                // Only configure transport - let GameNetworkManager handle starting client
                transport.SetClientRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData,
                    allocation.HostConnectionData
                );

                Debug.Log($"RelayManager: Relay configured for client");
                Debug.Log($"RelayManager: Server: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RelayManager: Failed to join relay: {e}");
                return false;
            }
        }

        // Add method to get current transport status
        public bool IsTransportConfigured()
        {
            return transport != null && isInitialized;
        }

        // Add method to reset transport configuration
        public void ResetTransport()
        {
            if (transport != null)
            {
                Debug.Log("RelayManager: Resetting transport configuration");
                // Transport will be reconfigured when needed
            }
        }
    }
}
