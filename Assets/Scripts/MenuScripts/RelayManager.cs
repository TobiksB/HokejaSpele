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
    // Šī klase nodrošina Unity Relay servisa komunikāciju hokeja spēlē
    // RelayManager ir atbildīgs par savienojumu izveidi starp spēlētājiem, izmantojot Unity Relay servisus
    // Tas ļauj spēlētājiem izveidot un pievienoties tīkla spēlēm bez sarežģītas tīkla konfigurācijas
    public class RelayManager : MonoBehaviour
    {
        // Singltona ieviešana, lai nodrošinātu, ka pastāv tikai viena RelayManager instance
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

        // UnityTransport komponente, kas tiek izmantota tīkla komunikācijai
        private UnityTransport transport;
        // Rāda, vai RelayManager ir inicializēts
        private bool isInitialized;

        private void Awake()
        {
            // Singltona pārbaude - pārliecinamies, ka pastāv tikai viena instance
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                
                // LABOTS: Aizkavējam tīkla pārvaldnieka iestatīšanu, lai izvairītos no nulles atsaucēm
                StartCoroutine(DelayedInitialization());
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // Aizkavētā inicializācija, lai pārliecinātos, ka NetworkManager ir pareizi iestatīts
        private System.Collections.IEnumerator DelayedInitialization()
        {
            // Uzgaidīt kadru, lai nodrošinātu, ka NetworkManager ir pareizi iestatīts
            yield return null;
            
            // Atrod NetworkManager instanci un iespējo ainu pārvaldību
            var networkManager = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
            if (networkManager != null)
            {
                networkManager.NetworkConfig.EnableSceneManagement = true;
                Debug.Log("RelayManager: Iespējota ainu pārvaldība NetworkManager");
            }
            
            InitializeTransport();
        }

        // Inicializē UnityTransport komponenti, kas ir nepieciešama tīkla savienojumiem
        private void InitializeTransport()
        {
            // Mēģina atrast UnityTransport komponenti
            transport = UnityEngine.Object.FindFirstObjectByType<UnityTransport>();
            if (transport == null)
            {
                var networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                if (networkManager != null)
                {
                    transport = networkManager.GetComponent<UnityTransport>();
                }
            }

            // Ja neatrod UnityTransport, izvada kļūdu
            if (transport == null)
            {
                Debug.LogError("RelayManager: Nav atrasts UnityTransport ainā!");
                return;
            }

            isInitialized = true;
            Debug.Log("RelayManager: Veiksmīgi inicializēts");
        }

        // Inicializē Unity Relay servisu, ieskaitot autentifikāciju
        public async Task InitializeRelay()
        {
            try
            {
                // Ja nav inicializēts, inicializējam transportu
                if (!isInitialized)
                {
                    InitializeTransport();
                }

                // Inicializēt Unity pakalpojumus tikai tad, ja tie vēl nav inicializēti
                if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
                {
                    Debug.Log("RelayManager: Inicializējam Unity servisu...");
                    await UnityServices.InitializeAsync();
                }

                // Pierakstāmies anonīmi, ja vēl neesam ielogojušies
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("RelayManager: Ielogojoties anonīmi...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                
                // Pārbaudām vai ir atrasti nepieciešamie komponenti
                var networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                var transport = UnityEngine.Object.FindFirstObjectByType<UnityTransport>();
                
                if (networkManager == null || transport == null)
                {
                    Debug.LogError("RelayManager: Ainā nav atrasti nepieciešamie komponenti!");
                    return;
                }

                Debug.Log("RelayManager: Relay serviss veiksmīgi inicializēts");
            }
            catch (Exception e)
            {
                Debug.LogError($"RelayManager: Neizdevās inicializēt relay: {e.Message}");
            }
        }

        // Izveido Relay pieslēgumu resursdatoram (host)
        // Modificēts: Tikai konfigurē transportu, nesāk resursdatoru/klientu
        public async Task<string> CreateRelay(int maxConnections, int maxPlayers)
        {
            // Pārliecinamies, ka RelayManager ir inicializēts
            if (!isInitialized)
            {
                await InitializeRelay();
            }

            try
            {
                // Izveido Relay piešķīrumu norādītajam savienojumu skaitam
                Debug.Log($"RelayManager: Veidojam Relay piešķīrumu {maxPlayers} spēlētājiem, {maxConnections} savienojumiem");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                // Konfigurē transportu - ļauj GameNetworkManager pārvaldīt resursdatora sākšanu
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                Debug.Log($"RelayManager: Relay konfigurēts resursdatoram ar pievienošanās kodu: {joinCode}");
                Debug.Log($"RelayManager: Serveris: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
                
                return joinCode;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RelayManager: Neizdevās izveidot relay: {e}");
                return null;
            }
        }

        // Pievienojas esošam Relay pieslēgumam kā klients
        // Modificēts: Tikai konfigurē transportu, nesāk klientu
        public async Task<bool> JoinRelay(string joinCode)
        {
            if (!isInitialized)
            {
                Debug.LogError("RelayManager: Nav inicializēts!");
                return false;
            }

            try
            {
                // Pievienojas relay piešķīrumam, izmantojot pievienošanās kodu
                Debug.Log($"RelayManager: Pievienojas Relay ar kodu: {joinCode}");
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                // Konfigurējam transportu klientam - ļaujam GameNetworkManager pārvaldīt klienta sākšanu
                transport.SetClientRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData,
                    allocation.HostConnectionData
                );

                Debug.Log($"RelayManager: Relay konfigurēts klientam");
                Debug.Log($"RelayManager: Serveris: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RelayManager: Neizdevās pievienoties relay: {e}");
                return false;
            }
        }

        // Metode, lai pārbaudītu transporta konfigurācijas statusu
        public bool IsTransportConfigured()
        {
            return transport != null && isInitialized;
        }

        // Metode, lai atiestatītu transporta konfigurāciju
        public void ResetTransport()
        {
            if (transport != null)
            {
                Debug.Log("RelayManager: Atiestata transporta konfigurācija");
                // Transports tiks pārkonfigurēts, kad būs nepieciešams
            }
        }
    }
}
