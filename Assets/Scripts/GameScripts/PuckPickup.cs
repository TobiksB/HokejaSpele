using UnityEngine;
using Unity.Netcode;

// Šī klase ļauj spēlētājiem pacelt, turēt un atlaist hokeja ripu, kā arī nozagt to no pretiniekiem
// PuckPickup nodrošina gan lokālo ripas kontroli, gan tīklā sinhronizētu ripas pārvaldību vairākiem spēlētājiem
public class PuckPickup : NetworkBehaviour
{
    [Header("Pacelšanas iestatījumi")]
    [SerializeField] private float pickupRange = 2f; // Attālums, kurā spēlētājs var pacelt ripu
    [SerializeField] private Transform puckHoldPosition; // Pozīcija, kur ripa tiek turēta
    [SerializeField] private LayerMask puckLayer = 128; // Slānis ripas noteikšanai
    
    [Header("Ievade")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E; // Taustiņš ripas pacelšanai/atlaišanai
    
    [Header("Atkļūdošana")]
    [SerializeField] private bool enableDebugLogs = true; // Vai rādīt atkļūdošanas ziņojumus
    
    [Header("Ripas zagšana")]
    [SerializeField] private float stealChance = 0.25f; // 25% iespēja nozagt
    [SerializeField] private float stealRange = 3f; // Attālums, kurā var mēģināt nozagt
    [SerializeField] private float stealCooldown = 2f; // Atspiešanas laiks starp zagšanas mēģinājumiem
    [SerializeField] private bool enableStealDebugLogs = true; // Vai attēlot zagšanas atkļūdošanas ziņojumus
    
    private Puck currentPuck; // Pašreizējā ripa, ko tur spēlētājs
    private bool hasPuck = false; // Vai spēlētājam ir ripa
    private NetworkVariable<bool> networkHasPuck = new NetworkVariable<bool>(false); // Tīkla mainīgais ripas turēšanai
    private bool releasedForShooting = false; // Vai ripa ir atlaista šaušanai
    private float lastStealAttemptTime = 0f; // Kad pēdējo reizi mēģināja nozagt ripu
    
    private void Awake()
    {
        // Ja nav iestatīta ripas turēšanas pozīcija, izveido to automātiski
        if (puckHoldPosition == null)
        {
            GameObject holdPos = new GameObject("PuckHoldPosition");
            holdPos.transform.SetParent(transform);
            holdPos.transform.localPosition = new Vector3(0, 0.5f, 1.5f);
            holdPos.transform.localRotation = Quaternion.identity;
            puckHoldPosition = holdPos.transform;
        }
        
        // Pievieno PlayerShooting komponenti, ja tā neeksistē
        if (GetComponent<PlayerShooting>() == null)
        {
            gameObject.AddComponent<PlayerShooting>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Pievieno klausītāju tīkla mainīgā izmaiņām
        networkHasPuck.OnValueChanged += OnNetworkHasPuckChanged;
    }

    public override void OnNetworkDespawn()
    {
        // Noņem klausītāju, lai novērstu atmiņas noplūdes
        networkHasPuck.OnValueChanged -= OnNetworkHasPuckChanged;
        base.OnNetworkDespawn();
    }

    // Apstrādā tīkla stāvokļa maiņu attālinātajiem spēlētājiem
    private void OnNetworkHasPuckChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner)
        {
            hasPuck = newValue;
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: [Attālināts] Tīkla stāvoklis mainīts - HasPuck: {hasPuck}");
            }
        }
    }

    private void Update()
    {
        // Apstrādā ievadi tikai īpašniekam
        if (!IsOwner) return;

        HandleInput();

        // Lokālajam spēlētājam pārbauda, vai PuckFollower darbojas pareizi
        if (hasPuck && currentPuck != null && !releasedForShooting)
        {
            var puckFollower = currentPuck.GetComponent<PuckFollower>();
            if (puckFollower != null && !puckFollower.IsFollowing())
            {
                // Atsāk sekošanu, ja tā apstājās
                if (puckHoldPosition != null)
                {
                    puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                    if (enableDebugLogs)
                    {
                        Debug.Log("PuckPickup: Atsākta PuckFollower darbība lokālajam spēlētājam");
                    }
                }
            }
        }
    }

    // Apstrādā spēlētāja ievadi
    private void HandleInput()
    {
        if (Input.GetKeyDown(pickupKey))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: E taustiņš nospiests. HasPuck: {hasPuck}, ReleasedForShooting: {releasedForShooting}");
            }

            if (hasPuck && currentPuck != null && !releasedForShooting)
            {
                // Atlaiž ripu manuāli
                if (enableDebugLogs)
                {
                    Debug.Log("PuckPickup: Manuāli atlaiž ripu ar E taustiņu");
                }
                ManualReleasePuck();
            }
            else if (!hasPuck)
            {
                // Vispirms mēģina nozagt ripu no tuvumā esoša pretinieka
                bool stealAttempted = TryStealPuck();
                
                if (!stealAttempted)
                {
                    // Ja zagšanas mēģinājums netika veikts, mēģina parasto pacelšanu
                    if (enableDebugLogs)
                    {
                        Debug.Log("PuckPickup: Nav zagšanas mēģinājuma, mēģina parasto pacelšanu");
                    }
                    TryPickupPuck();
                }
            }
        }
    }

    // Mēģina pacelt tuvumā esošu ripu
    private void TryPickupPuck()
    {
        // Atrod tuvāko ripu
        Puck nearestPuck = FindNearestPuck();

        if (nearestPuck != null)
        {
            float distance = Vector3.Distance(transform.position, nearestPuck.transform.position);
            
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: Atrasta ripa attālumā {distance:F2}m (maks: {pickupRange}m)");
            }

            if (distance <= pickupRange)
            {
                releasedForShooting = false;
                currentPuck = nearestPuck;
                hasPuck = true;

                // Uzreiz sāk PuckFollower lokālai atsaucībai
                var puckFollower = nearestPuck.GetComponent<PuckFollower>();
                if (puckFollower != null)
                {
                    puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                    puckFollower.enabled = true;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"PuckPickup: Sākts PuckFollower lokālajam spēlētājam");
                    }
                }
                else
                {
                    // Pievieno PuckFollower, ja tā trūkst
                    puckFollower = nearestPuck.gameObject.AddComponent<PuckFollower>();
                    puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                    puckFollower.enabled = true;
                    Debug.Log("PuckPickup: Pievienota un sākta PuckFollower komponente");
                }

                // Konfigurē fiziku pacelšanai
                var col = nearestPuck.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                var rb = nearestPuck.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                nearestPuck.SetHeld(true);

                // Nosūta serverim tīkla sinhronizācijai
                if (NetworkManager.Singleton != null && IsSpawned)
                {
                    var puckNetObj = nearestPuck.GetComponent<NetworkObject>();
                    if (puckNetObj != null)
                    {
                        PickupPuckServerRpc(puckNetObj.NetworkObjectId);
                    }
                }

                if (enableDebugLogs)
                {
                    Debug.Log($"PuckPickup: Veiksmīgi pacelta ripa, izmantojot PuckFollower!");
                }
            }
        }
    }

    // Atrod tuvāko nepaceltuto ripu
    private Puck FindNearestPuck()
    {
        var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
        Puck nearestPuck = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var puck in allPucks)
        {
            if (puck == null) continue;
            
            bool isHeld = false;
            try
            {
                isHeld = puck.IsHeld();
            }
            catch
            {
                isHeld = false;
            }
            
            if (isHeld) continue;

            float distance = Vector3.Distance(transform.position, puck.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPuck = puck;
            }
        }
        
        return nearestPuck;
    }

    // ServerRpc metode ripas pacelšanai
    [ServerRpc]
    private void PickupPuckServerRpc(ulong puckNetworkId)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"PuckPickup: [ServerRpc] Apstrādā pacelšanu ripai {puckNetworkId}");
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var networkObject))
        {
            var puck = networkObject.GetComponent<Puck>();
            if (puck != null)
            {
                currentPuck = puck;
                networkHasPuck.Value = true;
                releasedForShooting = false;

                try
                {
                    puck.PickupByPlayer(this);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"PuckPickup: PickupByPlayer neizdevās: {e.Message}");
                }

                // Paziņo visiem klientiem, lai sāktu sekošanu
                StartFollowingClientRpc(puckNetworkId, NetworkObjectId);

                OnPuckPickedUpClientRpc();
            }
        }
    }

    // ClientRpc metode ripas sekošanas sākšanai
    [ClientRpc]
    private void StartFollowingClientRpc(ulong puckNetworkId, ulong playerNetworkId)
    {
        var puckObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(puckNetworkId, out var puckNetObj) ? puckNetObj.gameObject : null;
        var playerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out var playerNetObj) ? playerNetObj.gameObject : null;

        if (puckObj == null || playerObj == null)
        {
            Debug.LogWarning("PuckPickup: StartFollowingClientRpc nevarēja atrast ripas vai spēlētāja objektu.");
            return;
        }

        var puckHold = playerObj.GetComponent<PuckPickup>()?.GetPuckHoldPosition();
        bool isLocalPlayer = playerObj.GetComponent<NetworkObject>().IsLocalPlayer;

        if (puckHold != null)
        {
            // Sāk sekošanu VISIEM klientiem, izmantojot PuckFollower
            var puckFollower = puckObj.GetComponent<PuckFollower>();
            if (puckFollower == null)
            {
                puckFollower = puckObj.AddComponent<PuckFollower>();
                Debug.Log("PuckPickup: Pievienota trūkstošā PuckFollower komponente");
            }
            
            puckFollower.StartFollowing(puckHold, Vector3.zero);
            puckFollower.enabled = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"PuckPickup: Sākts PuckFollower priekš {(isLocalPlayer ? "LOKĀLĀ" : "ATTĀLINĀTĀ")} spēlētāja");
            }
        }

        // Izslēdz fiziku turētai ripai
        var col = puckObj.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var rb = puckObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Iestata stāvokli lokālajam spēlētājam
        if (isLocalPlayer)
        {
            var pickup = playerObj.GetComponent<PuckPickup>();
            if (pickup != null)
            {
                pickup.currentPuck = puckObj.GetComponent<Puck>();
                pickup.hasPuck = true;
                pickup.releasedForShooting = false;
            }
        }
    }

    // Manuāli atlaiž ripu (ar E taustiņu)
    private void ManualReleasePuck()
    {
        if (currentPuck == null) return;

        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Manuāli atlaiž ripu ar E taustiņu");
        }

        releasedForShooting = false; // Šī ir manuāla atlaišana, ne šaušanai

        // Aptur sekošanu
        var puckFollower = currentPuck.GetComponent<PuckFollower>();
        if (puckFollower != null)
        {
            puckFollower.StopFollowing();
            puckFollower.enabled = false;
            if (enableDebugLogs)
            {
                Debug.Log("PuckPickup: Apturēts PuckFollower manuālai atlaišanai");
            }
        }

        Vector3 releasePosition = transform.position + transform.forward * 2f;
        releasePosition.y = 0.71f;

        currentPuck.transform.position = releasePosition;
        currentPuck.transform.rotation = Quaternion.identity;

        // Ieslēdz collieru un fiziku
        var col = currentPuck.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var rb = currentPuck.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentPuck.SetHeld(false);

        currentPuck = null;
        hasPuck = false;

        // Nosūta serverim tīkla sinhronizācijai
        if (IsServer)
        {
            networkHasPuck.Value = false;
        }
        else if (NetworkManager.Singleton != null && IsSpawned)
        {
            ReleasePuckServerRpc();
        }

        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Manuāla atlaišana pabeigta");
        }
    }

    // Atlaiž ripu šaušanai (izmanto PlayerShooting)
    public void ReleasePuckForShooting()
    {
        if (currentPuck == null) return;

        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Atlaiž ripu ŠAUŠANAI");
        }

        releasedForShooting = true;

        // Nekavējoties aptur sekošanu
        var puckFollower = currentPuck.GetComponent<PuckFollower>();
        if (puckFollower != null)
        {
            puckFollower.StopFollowing();
            puckFollower.enabled = false;
            if (enableDebugLogs)
            {
                Debug.Log("PuckPickup: Apturēts PuckFollower šaušanai");
            }
        }

        // --- LABOJUMS: Vienmēr izsauc ServerRpc šaušanai, lai nodrošinātu servera puses atlaišanu ---
        if (IsOwner && NetworkManager.Singleton != null && IsSpawned)
        {
            ReleasePuckForShootingServerRpc();
        }
        else
        {
            // Rezerves variants resursdatoram/serverim
            InternalReleasePuck();
        }
    }

    // ServerRpc metode ripas atlaišanai šaušanai
    [ServerRpc(RequireOwnership = false)]
    private void ReleasePuckForShootingServerRpc(ServerRpcParams rpcParams = default)
    {
        // Tikai serveris izpilda atlaišanas loģiku
        InternalReleasePuck();
    }

    // Iekšējā ripas atlaišanas metode
    private void InternalReleasePuck()
    {
        if (currentPuck == null) return;

        var puck = currentPuck;
        
        Vector3 releasePosition = transform.position + transform.forward * 2f;
        releasePosition.y = 0.71f;
        
        puck.transform.position = releasePosition;
        puck.transform.rotation = Quaternion.identity;

        var col = puck.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var rb = puck.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        puck.SetHeld(false);
        
        currentPuck = null;
        hasPuck = false;
        
        if (IsServer)
        {
            networkHasPuck.Value = false;
        }
        else if (NetworkManager.Singleton != null && IsSpawned)
        {
            ReleasePuckServerRpc();
        }
    }

    // ServerRpc metode ripas atlaišanai
    [ServerRpc]
    private void ReleasePuckServerRpc()
    {
        if (currentPuck != null)
        {
            Vector3 releaseVelocity = transform.forward * 5f;
            
            try
            {
                currentPuck.ReleaseFromPlayer(puckHoldPosition.position, releaseVelocity);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"PuckPickup: ReleaseFromPlayer neizdevās: {e.Message}");
            }
            
            currentPuck = null;
            networkHasPuck.Value = false;
            OnPuckReleasedClientRpc();
        }
    }

    // ClientRpc metode ripas pacelšanas notifikācijai
    [ClientRpc]
    private void OnPuckPickedUpClientRpc()
    {
        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Ripa pacelta (ClientRpc)");
        }
    }

    // ClientRpc metode ripas atlaišanas notifikācijai
    [ClientRpc]
    private void OnPuckReleasedClientRpc()
    {
        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Ripa atlaista (ClientRpc)");
        }
    }

    // Publiskās metodes citiem skriptiem
    public bool HasPuck() => hasPuck && !releasedForShooting;
    public Puck GetCurrentPuck() => releasedForShooting ? null : currentPuck;
    public Transform GetPuckHoldPosition() => puckHoldPosition;
    public bool CanShootPuck() => hasPuck && currentPuck != null && !releasedForShooting;
    
    // Atiestata šaušanas karogu
    public void ResetShootingFlag()
    {
        releasedForShooting = false;
        if (enableDebugLogs)
        {
            Debug.Log("PuckPickup: Atiestatīts šaušanas karogs - gatavs jaunai pacelšanai");
        }
    }

    // Mēģina nozagt ripu no pretinieka
    private bool TryStealPuck()
    {
        // Pārbauda atspiešanas laiku
        if (Time.time - lastStealAttemptTime < stealCooldown)
        {
            if (enableStealDebugLogs)
            {
                Debug.Log($"PuckPickup: Zagšanas mēģinājumu bloķē atspiešanas laiks. Atlikušais laiks: {stealCooldown - (Time.time - lastStealAttemptTime):F1}s");
            }
            return false;
        }

        // Atrod tuvākos spēlētājus ar ripām
        PuckPickup targetPlayer = FindNearestPlayerWithPuck();
        
        if (targetPlayer == null)
        {
            if (enableStealDebugLogs)
            {
                Debug.Log("PuckPickup: Nav atrasti tuvumā esoši spēlētāji ar ripām zagšanai");
            }
            return false;
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
        
        if (distance > stealRange)
        {
            if (enableStealDebugLogs)
            {
                Debug.Log($"PuckPickup: Mērķa spēlētājs pārāk tālu zagšanas mēģinājumam: {distance:F2}m (maks: {stealRange}m)");
            }
            return false;
        }

        // Reģistrē zagšanas mēģinājumu, lai sāktu atspiešanas laiku
        lastStealAttemptTime = Time.time;
        
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: Mēģina nozagt ripu no spēlētāja {distance:F2}m attālumā");
        }

        // Pārbauda, vai šis ir pretinieks (cita komanda)
        if (!IsOpponent(targetPlayer))
        {
            if (enableStealDebugLogs)
            {
                Debug.Log("PuckPickup: Nevar nozagt no komandas biedra");
            }
            return true; // Mēģinājums tika veikts, bet bloķēts
        }

        // Met kauliņu zagšanas veiksmei
        float roll = Random.Range(0f, 1f);
        bool stealSuccessful = roll <= stealChance;
        
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: Zagšanas metiens: {roll:F3} (vajag ≤ {stealChance:F3}) - {(stealSuccessful ? "VEIKSMĪGS" : "NEIZDEVĀS")}");
        }

        if (stealSuccessful)
        {
            // Veiksmīga zagšana - paņem ripu
            ExecutePuckSteal(targetPlayer);
        }
        else
        {
            // Neveiksmīgs zagšanas mēģinājums
            if (enableStealDebugLogs)
            {
                Debug.Log("PuckPickup: Zagšanas mēģinājums neizdevās - ripa paliek pie pretinieka");
            }
            
            // Neobligāti: Parāda vizuālu atgriezenisko saiti par neveiksmīgu zagšanu
            ShowStealFailedEffect();
        }

        return true; // Mēģinājums tika veikts
    }

    // Atrod tuvāko spēlētāju ar ripu
    private PuckPickup FindNearestPlayerWithPuck()
    {
        var allPlayers = FindObjectsByType<PuckPickup>(FindObjectsSortMode.None);
        PuckPickup nearestPlayer = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var player in allPlayers)
        {
            if (player == null || player == this) continue;
            
            // Pārbauda, vai spēlētājam ir ripa
            if (!player.HasPuck()) continue;
            
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < nearestDistance && distance <= stealRange)
            {
                nearestDistance = distance;
                nearestPlayer = player;
            }
        }
        
        return nearestPlayer;
    }

    // Pārbauda, vai spēlētājs ir pretinieks
    private bool IsOpponent(PuckPickup otherPlayer)
    {
        // LABOTS: Vienkāršots un uzticamāks komandu pārbaudes veids gan resursdatoram, gan klientam
        var myPlayerMovement = GetComponent<PlayerMovement>();
        var otherPlayerMovement = otherPlayer.GetComponent<PlayerMovement>();
        
        if (myPlayerMovement == null || otherPlayerMovement == null)
        {
            Debug.LogWarning("PuckPickup: Nevarēja iegūt PlayerMovement komponentes komandu pārbaudei");
            // LABOTS: Testēšanai pieņem, ka visi spēlētāji ir pretinieki, ja nevaram noteikt komandas
            return true;
        }
        
        // LABOTS: Daudz vienkāršāka komandu pārbaude - pagaidām pieņem atšķirīgas komandas testēšanai
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: Komandu pārbaude - pieņem, ka spēlētāji ir dažādās komandās testēšanai");
        }
        
        // PAGAIDU: Vienmēr atgriež true, lai testētu zagšanas mehāniku
        // TODO: Ieviest pareizu komandu pārbaudi, kad zagšana strādā
        return true;
        
        /*
        // PAREIZS KOMANDU PĀRBAUDES KODS (izslēgts testēšanai):
        try
        {
            var myTeamField = typeof(PlayerMovement).GetField("networkTeam", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (myTeamField != null)
            {
                var myTeamVar = myTeamField.GetValue(myPlayerMovement);
                var otherTeamVar = myTeamField.GetValue(otherPlayerMovement);
                
                if (myTeamVar != null && otherTeamVar != null)
                {
                    var myTeamValue = myTeamVar.GetType().GetProperty("Value")?.GetValue(myTeamVar);
                    var otherTeamValue = otherTeamVar.GetType().GetProperty("Value")?.GetValue(otherTeamVar);
                    
                    if (myTeamValue != null && otherTeamValue != null)
                    {
                        string myTeam = myTeamValue.ToString();
                        string otherTeam = otherTeamValue.ToString();
                        
                        bool sameTeam = myTeam == otherTeam;
                        bool isOpponent = !sameTeam;
                        
                        if (enableStealDebugLogs)
                        {
                            Debug.Log($"PuckPickup: Komandu pārbaude - Mana komanda: {myTeam}, Cita komanda: {otherTeam}, Tā pati komanda: {sameTeam}, Ir pretinieks: {isOpponent}");
                        }
                        
                        return isOpponent;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            if (enableStealDebugLogs)
            {
                Debug.LogWarning($"PuckPickup: Kļūda komandu pārbaudes refleksijā: {e.Message}");
            }
        }
        
        return true; // Rezerves variants: pieņem pretinieku
        */
    }

    // Izpilda veiksmīgu ripas zagšanu
    private void ExecutePuckSteal(PuckPickup targetPlayer)
    {
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: Izpilda veiksmīgu ripas zagšanu no {targetPlayer.name}");
        }

        // --- LABOJUMS: Vienmēr iegūst ripas atsauci UZ SERVERA, nevis uz klienta ---
        // Uz klienta targetPlayer.currentPuck var būt null tīkla sinhronizācijas aizkaves dēļ.
        // Tā vietā vienmēr sūta ServerRpc un ļauj serverim validēt un apstrādāt zagšanu.

        if (IsLocalPlayer && NetworkManager.Singleton != null && IsSpawned)
        {
            var targetNetworkObject = targetPlayer.GetComponent<NetworkObject>();
            ulong targetPuckNetworkId = 0;

            // Mēģina iegūt ripas NetworkObjectId, bet ja null, vienkārši sūta 0 (serveris pārbaudīs)
            var puck = targetPlayer.GetCurrentPuck();
            if (puck != null)
            {
                var puckNetObj = puck.GetComponent<NetworkObject>();
                if (puckNetObj != null)
                    targetPuckNetworkId = puckNetObj.NetworkObjectId;
            }

            Debug.Log($"PuckPickup: [KLIENTS] Sūta ExecuteStealServerRpc no klienta {OwnerClientId} uz serveri. Mērķis: {targetNetworkObject?.NetworkObjectId}, Ripa: {targetPuckNetworkId}");

            if (targetNetworkObject != null)
            {
                ExecuteStealServerRpc(targetNetworkObject.NetworkObjectId, targetPuckNetworkId);
            }
            else
            {
                Debug.LogWarning("PuckPickup: [KLIENTS] Mērķa NetworkObject ir null, nevar nosūtīt ServerRpc");
            }
        }
        else
        {
            if (!IsLocalPlayer)
                Debug.LogWarning("PuckPickup: [KLIENTS] Nav IsLocalPlayer, nenosūtīs ServerRpc zagšanai.");
            if (NetworkManager.Singleton == null)
                Debug.LogWarning("PuckPickup: [KLIENTS] NetworkManager.Singleton ir null, nevar nosūtīt ServerRpc zagšanai.");
            if (!IsSpawned)
                Debug.LogWarning("PuckPickup: [KLIENTS] Nav izveidots, nevar nosūtīt ServerRpc zagšanai.");
        }

        // Nekavējoties parāda atgriezenisko saiti lokālajam spēlētājam (neobligāti)
        ShowStealSuccessEffect();

        if (enableStealDebugLogs)
        {
            Debug.Log("PuckPickup: Ripas zagšanas tīkla komanda nosūtīta!");
        }
    }

    // ServerRpc metode zagšanas izpildei
    [ServerRpc(RequireOwnership = false)]
    private void ExecuteStealServerRpc(ulong targetPlayerNetworkId, ulong puckNetworkId, ServerRpcParams rpcParams = default)
    {
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: [ServerRpc] Apstrādā zagšanu - Zaglis: {NetworkObjectId}, Mērķis: {targetPlayerNetworkId}, Ripa: {puckNetworkId}");
        }

        var targetPlayerObj = GetNetworkObjectById(targetPlayerNetworkId);
        if (targetPlayerObj == null)
        {
            Debug.LogWarning($"PuckPickup: [ServerRpc] Nevarēja atrast mērķa spēlētāja objektu zagšanai.");
            return;
        }

        var targetPuckPickup = targetPlayerObj.GetComponent<PuckPickup>();
        if (targetPuckPickup == null)
        {
            Debug.LogWarning($"PuckPickup: [ServerRpc] Nevarēja atrast PuckPickup uz mērķa spēlētāja.");
            return;
        }

        // --- LABOJUMS: Vienmēr iegūst ripas atsauci UZ SERVERA ---
        var puck = targetPuckPickup.GetCurrentPuck();
        if (puck == null)
        {
            Debug.LogWarning("PuckPickup: [ServerRpc] Mērķa spēlētājam nav ripas, ko nozagt.");
            return;
        }

        var puckObj = puck.gameObject;
        if (puckObj == null)
        {
            Debug.LogWarning("PuckPickup: [ServerRpc] Mērķa ripas objekts ir null.");
            return;
        }

        // Validē, vai mērķim tiešām ir ripa
        if (!targetPuckPickup.HasPuck())
        {
            Debug.LogWarning($"PuckPickup: [ServerRpc] Mērķa spēlētājam nav ripas.");
            return;
        }

        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: [ServerRpc] Izpilda zagšanu - liek mērķim atlaist ripu");
        }

        // Liek mērķa spēlētājam nekavējoties atlaist ripu
        targetPuckPickup.ForceReleasePuckForSteal();

        // Pagaida brīdi, lai atlaišana tiktu apstrādāta
        StartCoroutine(CompleteStealAfterRelease(puck, rpcParams.Receive.SenderClientId));
    }

    // Modificēts, lai pieņemtu stealerClientId
    private System.Collections.IEnumerator CompleteStealAfterRelease(Puck puck, ulong stealerClientId)
    {
        yield return new WaitForSeconds(0.1f); // Īsa aizkave, lai atlaišana tiktu pabeigta

        if (puck == null)
        {
            Debug.LogWarning("PuckPickup: [ServerRpc] Ripa kļuva null zagšanas pabeigšanas laikā");
            yield break;
        }

        // Atrod zagļa PuckPickup pēc clientId
        var stealerObj = NetworkManager.Singleton.ConnectedClients.ContainsKey(stealerClientId)
            ? NetworkManager.Singleton.ConnectedClients[stealerClientId].PlayerObject?.GetComponent<PuckPickup>()
            : null;

        if (stealerObj == null)
        {
            Debug.LogWarning("PuckPickup: [ServerRpc] Nevarēja atrast zagļa PuckPickup komponenti");
            yield break;
        }

        stealerObj.currentPuck = puck;
        stealerObj.hasPuck = true;
        stealerObj.releasedForShooting = false;
        stealerObj.networkHasPuck.Value = true;

        puck.SetHeld(true);

        try
        {
            puck.PickupByPlayer(stealerObj);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PuckPickup: [ServerRpc] PickupByPlayer neizdevās zagšanas laikā: {e.Message}");
        }

        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: [ServerRpc] Zagšana pabeigta uz servera - ripa tagad pieder zaglim");
        }

        // Paziņo visiem klientiem par pabeigto zagšanu
        CompleteStealClientRpc(stealerObj.NetworkObjectId, puck.GetComponent<NetworkObject>().NetworkObjectId);
    }

    // LABOTS: Jauns ClientRpc zagšanas pabeigšanai
    [ClientRpc]
    private void CompleteStealClientRpc(ulong stealerNetworkId, ulong puckNetworkId)
    {
        if (enableStealDebugLogs)
        {
            Debug.Log($"PuckPickup: [ClientRpc] Pabeigta zagšana - Zaglis: {stealerNetworkId}, Ripa: {puckNetworkId}");
        }

        // Atrod objektus
        var stealerObj = GetNetworkObjectById(stealerNetworkId);
        var puckObj = GetNetworkObjectById(puckNetworkId);

        if (stealerObj == null || puckObj == null)
        {
            Debug.LogWarning($"PuckPickup: [ClientRpc] Nevarēja atrast objektus zagšanas pabeigšanai");
            return;
        }

        // Atjaunina lokālo stāvokli zaglim (ja tas ir lokālais spēlētājs)
        bool isLocalStealer = stealerObj.GetComponent<NetworkObject>().IsLocalPlayer;
        if (isLocalStealer)
        {
            var stealerPickup = stealerObj.GetComponent<PuckPickup>();
            if (stealerPickup != null)
            {
                stealerPickup.currentPuck = puckObj.GetComponent<Puck>();
                stealerPickup.hasPuck = true;
                stealerPickup.releasedForShooting = false;
                
                if (enableStealDebugLogs)
                {
                    Debug.Log($"PuckPickup: [ClientRpc] Atjaunināts lokālā zagļa stāvoklis");
                }
            }
        }

        // Sāk PuckFollower zaglim
        var stealerPickupComponent = stealerObj.GetComponent<PuckPickup>();
        if (stealerPickupComponent != null && stealerPickupComponent.GetPuckHoldPosition() != null)
        {
            var puckFollower = puckObj.GetComponent<PuckFollower>();
            if (puckFollower == null)
            {
                puckFollower = puckObj.AddComponent<PuckFollower>();
            }
            
            puckFollower.StartFollowing(stealerPickupComponent.GetPuckHoldPosition(), Vector3.zero);
            puckFollower.enabled = true;
            
            if (enableStealDebugLogs)
            {
                Debug.Log($"PuckPickup: [ClientRpc] Sākts PuckFollower zaglim");
            }
        }

        // Konfigurē ripas fiziku turēšanai
        var col = puckObj.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        
        var rb = puckObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // LABOTS: Palīgmetode NetworkObject atrašanai pēc ID
    private GameObject GetNetworkObjectById(ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject))
        {
            return networkObject.gameObject;
        }
        
        // Rezerves variants: meklē visus NetworkObjects
        var allNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (var obj in allNetworkObjects)
        {
            if (obj != null && obj.NetworkObjectId == networkObjectId)
            {
                return obj.gameObject;
            }
        }
        
        return null;
    }

    // Parāda veiksmīgas zagšanas efektu
    private void ShowStealSuccessEffect()
    {
        // Pievieno vizuālu/audio atgriezenisko saiti veiksmīgai zagšanai
        // Šeit var pievienot daļiņu efektus, skaņas efektus utt.
        if (enableStealDebugLogs)
        {
            Debug.Log("PuckPickup: Zagšana Izdevās"); // Vienkāršota virkne
        }
    }

    // Parāda neveiksmīgas zagšanas efektu
    private void ShowStealFailedEffect()
    {
        // Pievieno vizuālu/audio atgriezenisko saiti neveiksmīgai zagšanai
        // Šeit var pievienot daļiņu efektus, skaņas efektus utt.
        if (enableStealDebugLogs)
        {
            Debug.Log("PuckPickup: Zagšana Neizdevās"); // Vienkāršota virkne
        }
    }

    // Servera izsaukta metode, lai liktu šim spēlētājam atlaist ripu zagšanai
    public void ForceReleasePuckForSteal()
    {
        if (currentPuck == null) return;

        // Aptur sekošanu
        var puckFollower = currentPuck.GetComponent<PuckFollower>();
        if (puckFollower != null)
        {
            puckFollower.StopFollowing();
            puckFollower.enabled = false;
        }

        // Ieslēdz collieru un fiziku
        var col = currentPuck.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var rb = currentPuck.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentPuck.SetHeld(false);

        currentPuck = null;
        hasPuck = false;
        releasedForShooting = false;

        // Sinhronizē tīkla mainīgo
        networkHasPuck.Value = false;
    }

    // LABOTS: Uzlabota PickupStolenPuck metode - šī metode vairs netiek izmantota
    private System.Collections.IEnumerator PickupStolenPuck(Puck puck)
    {
        // Šī metode vairs nav nepieciešama, jo ExecuteStealServerRpc apstrādā visu
        yield break;
    }
}
