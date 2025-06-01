using UnityEngine;
using Unity.Netcode;

public enum TeamColor { Red, Blue }

public class PlayerTeam : NetworkBehaviour
{
    [SerializeField] private TeamColor teamColor = TeamColor.Red;
    private NetworkVariable<int> networkTeamColor = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isBlueTeam = new NetworkVariable<bool>();
    private PlayerTeamVisuals visuals;

    // Add this property or field at the top of your class
    public string Team { get; private set; }
    public string CurrentTeam => isBlueTeam.Value ? "Blue" : "Red";

    private void Awake()
    {
        visuals = GetComponent<PlayerTeamVisuals>();
    }

    public override void OnNetworkSpawn()
    {
        networkTeamColor.OnValueChanged += OnTeamColorChanged;
        
        if (!IsServer) return;
        
        // Determine team from stored lobby data
        TeamColor assignedTeam = GetTeamFromStoredDataAdvanced();
        networkTeamColor.Value = (int)assignedTeam;
        teamColor = assignedTeam;
        Debug.Log($"PlayerTeam: Server assigned team {assignedTeam} to {gameObject.name}");
    }

    public override void OnNetworkDespawn()
    {
        networkTeamColor.OnValueChanged -= OnTeamColorChanged;
        if (IsServer)
        {
            isBlueTeam.OnValueChanged -= OnTeamChanged;
        }
    }

    private TeamColor GetTeamFromStoredDataAdvanced()
    {
        // FIXED: Use direct authId mapping instead of non-existent GetPlayerTeam method
        ulong ownerClientId = OwnerClientId;
        
        // Get authId for this client
        string authId = "";
        try
        {
            if (ownerClientId == NetworkManager.Singleton.LocalClientId)
            {
                authId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            }
            else if (NetworkSpawnManager.Instance != null)
            {
                // Try to get from NetworkSpawnManager's mapping
                var clientToAuthMapping = GetClientToAuthMapping();
                if (clientToAuthMapping != null && clientToAuthMapping.ContainsKey(ownerClientId))
                {
                    authId = clientToAuthMapping[ownerClientId];
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PlayerTeam: Error getting authId: {e.Message}");
        }

        if (!string.IsNullOrEmpty(authId))
        {
            // Try to get team from LobbyManager mapping
            var teamMapping = LobbyManager.Instance?.GetAuthIdToTeamMapping();
            if (teamMapping != null && teamMapping.ContainsKey(authId))
            {
                string team = teamMapping[authId];
                TeamColor result = team.ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                Debug.Log($"PlayerTeam: Got team {result} from lobby mapping for authId {authId}");
                return result;
            }
        }

        // Fallback to stored PlayerPrefs data with duplicate name handling
        string allPlayerTeams = PlayerPrefs.GetString("AllPlayerTeams", "");
        if (string.IsNullOrEmpty(allPlayerTeams))
        {
            Debug.LogWarning("PlayerTeam: No stored team data found, defaulting based on clientId");
            return ownerClientId == 0 ? TeamColor.Red : TeamColor.Blue;
        }

        // FIXED: Handle duplicate names by using clientId order as fallback
        string[] playerEntries = allPlayerTeams.Split('|');
        
        // Try to match by unique player identifier (authId or unique name)
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
                    
                    // FIXED: Try exact match first, then partial match for duplicate names
                    if (name == playerIdentifier || (playerIdentifier.Contains(name) && name.Length > 3))
                    {
                        TeamColor result = team.ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                        Debug.Log($"PlayerTeam: Found team assignment - {name} -> {result}");
                        return result;
                    }
                }
            }
        }

        // FIXED: If no match found, use clientId-based assignment from stored data order
        if (playerEntries.Length >= 2)
        {
            if (ownerClientId == 0) // Host gets first team
            {
                string[] parts = playerEntries[0].Split(':');
                if (parts.Length == 2)
                {
                    TeamColor result = parts[1].ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                    Debug.Log($"PlayerTeam: Host gets first stored team: {result}");
                    return result;
                }
            }
            else if (ownerClientId == 1 && playerEntries.Length > 1) // First client gets second team
            {
                string[] parts = playerEntries[1].Split(':');
                if (parts.Length == 2)
                {
                    TeamColor result = parts[1].ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                    Debug.Log($"PlayerTeam: Client 1 gets second stored team: {result}");
                    return result;
                }
            }
            else
            {
                // For additional clients, alternate teams
                int entryIndex = (int)ownerClientId % playerEntries.Length;
                string[] parts = playerEntries[entryIndex].Split(':');
                if (parts.Length == 2)
                {
                    TeamColor result = parts[1].ToLower() == "blue" ? TeamColor.Blue : TeamColor.Red;
                    Debug.Log($"PlayerTeam: Client {ownerClientId} gets team from entry {entryIndex}: {result}");
                    return result;
                }
            }
        }

        Debug.LogWarning($"PlayerTeam: No team found for player, defaulting based on clientId {ownerClientId}");
        return ownerClientId % 2 == 0 ? TeamColor.Red : TeamColor.Blue;
    }

    // FIXED: Add method to get client-to-auth mapping safely
    private System.Collections.Generic.Dictionary<ulong, string> GetClientToAuthMapping()
    {
        if (NetworkSpawnManager.Instance == null) return null;
        
        // Use reflection to access private field safely
        var field = typeof(NetworkSpawnManager).GetField("clientIdToAuthId", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            return field.GetValue(NetworkSpawnManager.Instance) as System.Collections.Generic.Dictionary<ulong, string>;
        }
        
        return null;
    }

    // FIXED: Create unique player identifier to handle duplicate names
    private string GetUniquePlayerIdentifier()
    {
        string baseIdentifier = "";
        
        // Try to get from SettingsManager first
        if (SettingsManager.Instance != null)
        {
            baseIdentifier = SettingsManager.Instance.PlayerName;
        }

        // Try to get from authentication service
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

        // Fallback: use object name
        if (string.IsNullOrEmpty(baseIdentifier))
        {
            baseIdentifier = gameObject.name.Replace("(Clone)", "").Replace("Player", "");
        }

        // FIXED: Make identifier unique by appending clientId if name might be duplicate
        if (!string.IsNullOrEmpty(baseIdentifier))
        {
            // Check if this is likely a duplicate name (short or common names)
            if (baseIdentifier.Length <= 3 || 
                baseIdentifier.ToLower().Contains("player") || 
                baseIdentifier.ToLower().Contains("user"))
            {
                baseIdentifier += $"_{OwnerClientId}";
                Debug.Log($"PlayerTeam: Created unique identifier for potential duplicate: {baseIdentifier}");
            }
        }

        return baseIdentifier;
    }

    private void OnTeamColorChanged(int oldValue, int newValue)
    {
        teamColor = (TeamColor)newValue;
        UpdateVisuals();
        Debug.Log($"PlayerTeam: Network team color changed to {teamColor} on {gameObject.name}");
    }

    private void OnTeamChanged(bool oldValue, bool newValue)
    {
        if (visuals != null)
        {
            visuals.UpdateTeamColor(newValue ? "Blue" : "Red");
        }
    }

    public void SetTeamColor(TeamColor color)
    {
        teamColor = color;
        Debug.Log($"PlayerTeam: Set team color to {color} on {gameObject.name}");
        
        // Update network variable if we're the server
        if (IsServer)
        {
            networkTeamColor.Value = (int)color;
        }
        
        UpdateVisuals();
    }

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

    [ServerRpc(RequireOwnership = false)]
    private void SetTeamServerRpc(bool isBlue)
    {
        isBlueTeam.Value = isBlue;
    }

    public string GetTeam()
    {
        return isBlueTeam.Value ? "Blue" : "Red";
    }

    public bool IsBlueTeam()
    {
        return teamColor == TeamColor.Blue;
    }

    public TeamColor GetTeamColor()
    {
        return teamColor;
    }

    public void UpdateVisuals()
    {
        var visuals = GetComponent<PlayerTeamVisuals>();
        if (visuals != null)
        {
            visuals.UpdateTeamColor(teamColor.ToString());
        }
    }
}
