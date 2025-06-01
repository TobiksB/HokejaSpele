using UnityEngine;
using Unity.Netcode;

public class PlayerTeamVisuals : NetworkBehaviour
{
    [SerializeField] private Material redTeamMaterial;
    [SerializeField] private Material blueTeamMaterial;
    [SerializeField] private Renderer[] teamColorRenderers;

    private NetworkVariable<bool> isBlueTeam = new NetworkVariable<bool>();
    private PlayerTeam playerTeam;

    private void Awake()
    {
        playerTeam = GetComponent<PlayerTeam>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        
        if (playerTeam != null)
        {
            isBlueTeam.Value = playerTeam.CurrentTeam == "Blue";
            UpdateTeamColor();
        }

        isBlueTeam.OnValueChanged += OnTeamChanged;
    }

    public override void OnNetworkDespawn()
    {
        isBlueTeam.OnValueChanged -= OnTeamChanged;
    }

    private void OnTeamChanged(bool oldValue, bool newValue)
    {
        UpdateTeamColor();
    }

    // FIXED: Add immediate team setting for server authority
    public void SetTeamImmediate(string team)
    {
        ApplyTeamVisuals(team);
        Debug.Log($"PlayerTeamVisuals: Applied {team} team visuals immediately");
    }

    // Networked team setter
    public void SetTeamNetworked(string team)
    {
        // FIXED: Check IsSpawned on this NetworkBehaviour, not NetworkManager
        if (NetworkManager.Singleton != null && this.IsSpawned)
        {
            if (IsServer)
            {
                ApplyTeamVisuals(team);
                SetTeamClientRpc(team);
            }
            else
            {
                SetTeamServerRpc(team);
            }
        }
        else
        {
            ApplyTeamVisuals(team);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetTeamServerRpc(string team)
    {
        // Apply on server first
        ApplyTeamVisuals(team);
        
        // Then sync to all clients
        SetTeamClientRpc(team);
    }

    [ClientRpc]
    private void SetTeamClientRpc(string team)
    {
        ApplyTeamVisuals(team);
    }

    private void ApplyTeamVisuals(string team)
    {
        Color teamColor = team.Equals("Blue", System.StringComparison.OrdinalIgnoreCase) ? Color.blue : Color.red;
        
        // FIXED: Get all renderers on this object and children
        var renderers = GetComponentsInChildren<Renderer>();
        
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                // FIXED: Set color directly on existing material instead of creating new one
                renderer.material.color = teamColor;
            }
        }
        
        Debug.Log($"PlayerTeamVisuals: Applied {team} color directly to {renderers.Length} renderers");
    }

    public void UpdateTeamColor(string team)
    {
        Material teamMaterial = team == "Blue" ? blueTeamMaterial : redTeamMaterial;
        foreach (var renderer in teamColorRenderers)
        {
            if (renderer != null)
            {
                renderer.material = teamMaterial;
            }
        }
    }

    // Keep the parameterless version for backward compatibility
    public void UpdateTeamColor()
    {
        if (teamColorRenderers == null || teamColorRenderers.Length == 0)
        {
            Debug.LogError($"[PlayerTeamVisuals] No renderers assigned for {gameObject.name}!");
            return;
        }

        Material teamMaterial = isBlueTeam.Value ? blueTeamMaterial : redTeamMaterial;
        if (teamMaterial == null)
        {
            Debug.LogError($"[PlayerTeamVisuals] Team material is null for {(isBlueTeam.Value ? "Blue" : "Red")} team!");
            return;
        }

        foreach (var renderer in teamColorRenderers)
        {
            if (renderer != null)
            {
                renderer.material = teamMaterial;
                Debug.Log($"[PlayerTeamVisuals] Applied {(isBlueTeam.Value ? "Blue" : "Red")} team material to {renderer.name}");
            }
        }
    }

    public string GetCurrentTeam()
    {
        return isBlueTeam.Value ? "Blue" : "Red";
    }

    // FIXED: Add SetTeamColorDirect method
    public void SetTeamColorDirect(string team)
    {
        Color teamColor = team.Equals("Blue", System.StringComparison.OrdinalIgnoreCase) ? Color.blue : Color.red;
        
        var renderers = GetComponentsInChildren<Renderer>();
        
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = teamColor;
            }
        }
        
        Debug.Log($"PlayerTeamVisuals: Applied {team} color directly to {renderers.Length} renderers");
    }

    // FIXED: Add missing SetBlueTeam method
    public void SetBlueTeam()
    {
        SetTeam("Blue");
    }

    // FIXED: Add missing SetRedTeam method
    public void SetRedTeam()
    {
        SetTeam("Red");
    }

    // FIXED: Remove duplicate SetTeam method - keep only one version
    // If there's an existing SetTeam method, update it to this implementation:
    public void SetTeam(string teamName)
    {
        if (teamName == "Blue")
        {
            ApplyBlueTeamColors();
        }
        else if (teamName == "Red")
        {
            ApplyRedTeamColors();
        }
        
        Debug.Log($"PlayerTeamVisuals: Set team to {teamName}");
    }

    // FIXED: Add team color application methods if they don't exist
    private void ApplyBlueTeamColors()
    {
        Color blueColor = Color.blue;
        ApplyColorToRenderers(blueColor);
    }

    private void ApplyRedTeamColors()
    {
        Color redColor = Color.red;
        ApplyColorToRenderers(redColor);
    }

    private void ApplyColorToRenderers(Color color)
    {
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = color;
            }
        }
    }
}
