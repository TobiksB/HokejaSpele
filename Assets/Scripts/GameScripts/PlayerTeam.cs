using UnityEngine;
using Unity.Netcode;

// Uzskaitījums, kas definē iespējamās komandas krāsas
public enum TeamColor { Red, Blue }

// Šī klase pārvalda spēlētāja komandas piederību un sinhronizē to starp visiem tīkla klientiem
// PlayerTeam atbild par spēlētāja komandas noteikšanu, saglabāšanu un vizuālo attēlošanu
public class PlayerTeam : NetworkBehaviour
{
    [SerializeField] private TeamColor teamColor = TeamColor.Red; // Noklusējuma komanda ir sarkanā
    private NetworkVariable<int> networkTeamColor = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); // Tīklā sinhronizētā komanda (0=Red, 1=Blue)
    private NetworkVariable<bool> isBlueTeam = new NetworkVariable<bool>(); // Alternatīvs veids kā uzglabāt komandu - true=zilā, false=sarkanā
    private PlayerTeamVisuals visuals; // Atsauce uz komponenti, kas atbild par komandas vizuālo attēlojumu

    // Īpašības komandas piekļuvei
    public string Team { get; private set; } // Komandas nosaukums (nav izmantots?)
    public string CurrentTeam => isBlueTeam.Value ? "Blue" : "Red"; // Atgriež pašreizējo komandu kā tekstu

    private void Awake()
    {
        // Iegūst atsauci uz vizuālo komponenti
        visuals = GetComponent<PlayerTeamVisuals>();
    }

    public override void OnNetworkSpawn()
    {
        // Pievieno klausītāju tīkla mainīgā izmaiņām
        networkTeamColor.OnValueChanged += OnTeamColorChanged;
        
        if (!IsServer) return; // Tālāk turpina tikai serveris
        
        // Nosaka komandu no saglabātajiem priekštelpas datiem
        TeamColor assignedTeam = GetTeamFromStoredDataAdvanced();
        networkTeamColor.Value = (int)assignedTeam;
        teamColor = assignedTeam;
        Debug.Log($"PlayerTeam: Serveris piešķīra komandu {assignedTeam} objektam {gameObject.name}");
    }

    public override void OnNetworkDespawn()
    {
        // Noņem klausītājus, lai izvairītos no atmiņas noplūdēm
        networkTeamColor.OnValueChanged -= OnTeamColorChanged;
        if (IsServer)
        {
            isBlueTeam.OnValueChanged -= OnTeamChanged;
        }
    }

    // Sarežģīta funkcija komandas noteikšanai no dažādiem avotiem
    private TeamColor GetTeamFromStoredDataAdvanced()
    {
        ulong ownerClientId = OwnerClientId;
        
        // Iegūst autentifikācijas ID šim klientam
        string authId = "";
        try
        {
            if (ownerClientId == NetworkManager.Singleton.LocalClientId)
            {
                authId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            }
            else if (NetworkSpawnManager.Instance != null)
            {
                // Mēģina iegūt no NetworkSpawnManager kartējuma
                var clientToAuthMapping = GetClientToAuthMapping();
                if (clientToAuthMapping != null && clientToAuthMapping.ContainsKey(ownerClientId))
                {
                    authId = clientToAuthMapping[ownerClientId];
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PlayerTeam: Kļūda iegūstot authId: {e.Message}");
        }

        if (!string.IsNullOrEmpty(authId))
        {
            // Mēģina iegūt komandu no LobbyManager kartējuma
            var teamMapping = LobbyManager.Instance?.GetAuthIdToTeamMapping();
            if (teamMapping != null && teamMapping.ContainsKey(authId))
            {
                string team = teamMapping[authId];
                TeamColor result = team.ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                Debug.Log($"PlayerTeam: Ieguva komandu {result} no priekštelpas kartējuma priekš authId {authId}");
                return result;
            }
        }

        // Rezerves variants - izmanto saglabātos PlayerPrefs datus ar dublikātu vārdu apstrādi
        string allPlayerTeams = PlayerPrefs.GetString("AllPlayerTeams", "");
        if (string.IsNullOrEmpty(allPlayerTeams))
        {
            Debug.LogWarning("PlayerTeam: Nav atrasti saglabāti komandu dati, izmanto noklusējumu pēc klienta ID");
            return ownerClientId == 0 ? TeamColor.Red : TeamColor.Blue;
        }

        string[] playerEntries = allPlayerTeams.Split('|');
        
        // Mēģina atrast pēc unikāla spēlētāja identifikatora (authId vai unikāls vārds)
        string playerIdentifier = GetUniquePlayerIdentifier();
        if (!string.IsNullOrEmpty(playerIdentifier))
        {
            foreach (string entry in playerEntries)
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2)
                {
                    string name = parts[0];
                    string team = parts[1];
                    
                    // LABOTS: Vispirms mēģina precīzu sakritību, tad daļēju sakritību dublikātu vārdiem
                    if (name == playerIdentifier || (playerIdentifier.Contains(name) && name.Length > 3))
                    {
                        TeamColor result = team.ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                        Debug.Log($"PlayerTeam: Atrasta komandas piešķire - {name} -> {result}");
                        return result;
                    }
                }
            }
        }

        if (playerEntries.Length >= 2)
        {
            if (ownerClientId == 0) // Resursdatora īpašnieks saņem pirmo komandu
            {
                string[] parts = playerEntries[0].Split(':');
                if (parts.Length == 2)
                {
                    TeamColor result = parts[1].ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                    Debug.Log($"PlayerTeam: Resursdatora īpašnieks saņem pirmo saglabāto komandu: {result}");
                    return result;
                }
            }
            else if (ownerClientId == 1 && playerEntries.Length > 1) // Pirmais klients saņem otro komandu
            {
                string[] parts = playerEntries[1].Split(':');
                if (parts.Length == 2)
                {
                    TeamColor result = parts[1].ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                    Debug.Log($"PlayerTeam: Klients 1 saņem otro saglabāto komandu: {result}");
                    return result;
                }
            }
            else
            {
                // Papildus klientiem, pamīšus komandas
                int entryIndex = (int)ownerClientId % playerEntries.Length;
                string[] parts = playerEntries[entryIndex].Split(':');
                if (parts.Length == 2)
                {
                    TeamColor result = parts[1].ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                    Debug.Log($"PlayerTeam: Klients {ownerClientId} saņem komandu no ieraksta {entryIndex}: {result}");
                    return result;
                }
            }
        }

        Debug.LogWarning($"PlayerTeam: Komanda netika atrasta spēlētājam, izmanto noklusējumu pēc klienta ID {ownerClientId}");
        return ownerClientId % 2 == 0 ? TeamColor.Red : TeamColor.Blue;
    }

    // Palīgfunkcija, kas iegūst klientu-uz-autentifikāciju kartējumu no NetworkSpawnManager
    private System.Collections.Generic.Dictionary<ulong, string> GetClientToAuthMapping()
    {
        if (NetworkSpawnManager.Instance == null) return null;
        
        // Izmanto refleksiju, lai droši piekļūtu privātam laukam
        var field = typeof(NetworkSpawnManager).GetField("clientIdToAuthId", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            return field.GetValue(NetworkSpawnManager.Instance) as System.Collections.Generic.Dictionary<ulong, string>;
        }
        
        return null;
    }

    // Iegūst unikālu spēlētāja identifikatoru dažādos veidos
    private string GetUniquePlayerIdentifier()
    {
        string baseIdentifier = "";
        
        // Vispirms mēģina iegūt no SettingsManager
        if (SettingsManager.Instance != null)
        {
            baseIdentifier = SettingsManager.Instance.PlayerName;
        }

        // Mēģina iegūt no autentifikācijas servisa
        if (string.IsNullOrEmpty(baseIdentifier))
        {
            try
            {
                if (Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
                {
                    baseIdentifier = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                }
            }
            catch { }
        }

        // Rezerves variants: izmanto objekta nosaukumu
        if (string.IsNullOrEmpty(baseIdentifier))
        {
            baseIdentifier = gameObject.name.Replace("(Clone)", "").Replace("Player", "");
        }

        if (!string.IsNullOrEmpty(baseIdentifier))
        {
            // Pārbauda, vai šis varētu būt dublikāta vārds (īsi vai izplatīti vārdi)
            if (baseIdentifier.Length <= 3 || 
                baseIdentifier.ToLower().Contains("player") || 
                baseIdentifier.ToLower().Contains("user"))
            {
                baseIdentifier += $"_{OwnerClientId}";
                Debug.Log($"PlayerTeam: Izveidots unikāls identifikators potenciālam dublikātam: {baseIdentifier}");
            }
        }

        return baseIdentifier;
    }

    // Apstrādā tīkla krāsas izmaiņas
    private void OnTeamColorChanged(int oldValue, int newValue)
    {
        teamColor = (TeamColor)newValue;
        UpdateVisuals();
        Debug.Log($"PlayerTeam: Tīkla komandas krāsa mainīta uz {teamColor} objektam {gameObject.name}");
    }

    // Apstrādā komandas izmaiņas (zilā vai ne)
    private void OnTeamChanged(bool oldValue, bool newValue)
    {
        if (visuals != null)
        {
            visuals.UpdateTeamColor(newValue ? "Blue" : "Red");
        }
    }

    // Iestata spēlētāja komandas krāsu
    public void SetTeamColor(TeamColor color)
    {
        teamColor = color;
        Debug.Log($"PlayerTeam: Iestatīta komandas krāsa uz {color} objektam {gameObject.name}");
        
        // Atjaunina tīkla mainīgo, ja mēs esam serveris
        if (IsServer)
        {
            networkTeamColor.Value = (int)color;
        }
        
        UpdateVisuals();
    }

    // Iestata spēlētāja komandu pēc nosaukuma
    public void SetTeam(string team)
    {
        if (IsServer)
        {
            isBlueTeam.Value = team == "Blue";
            if (visuals != null)
            {
                visuals.UpdateTeamColor(team);
            }
        }
        else
        {
            SetTeamServerRpc(team == "Blue");
        }
    }

    // Servera RPC metode komandas iestatīšanai no klienta
    [ServerRpc(RequireOwnership = false)]
    private void SetTeamServerRpc(bool isBlue)
    {
        isBlueTeam.Value = isBlue;
    }

    // Atgriež pašreizējo komandu kā tekstu
    public string GetTeam()
    {
        return isBlueTeam.Value ? "Blue" : "Red";
    }

    // Pārbauda, vai spēlētājs ir zilajā komandā
    public bool IsBlueTeam()
    {
        return teamColor == TeamColor.Blue;
    }

    // Atgriež pašreizējo komandas krāsu
    public TeamColor GetTeamColor()
    {
        return teamColor;
    }

    // Atjaunina spēlētāja vizuālo izskatu atbilstoši komandai
    public void UpdateVisuals()
    {
        var visuals = GetComponent<PlayerTeamVisuals>();
        if (visuals != null)
        {
            visuals.UpdateTeamColor(teamColor.ToString());
        }
    }
}
