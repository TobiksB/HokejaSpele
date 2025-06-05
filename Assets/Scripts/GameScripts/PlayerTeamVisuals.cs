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
        // Subscribe to OnValueChanged on all clients, not just server
        isBlueTeam.OnValueChanged += OnTeamChanged;

        if (playerTeam != null)
        {
            // Only the server sets the value, but all clients will receive the change
            if (IsServer)
            {
                isBlueTeam.Value = playerTeam.CurrentTeam == "Blue";
                UpdateTeamColor();
            }
        }
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
        // Set the NetworkVariable so all clients get the correct value
        bool blue = team.Equals("Blue", System.StringComparison.OrdinalIgnoreCase);
        if (IsServer)
        {
            isBlueTeam.Value = blue;
            UpdateTeamColor();
        }
        else
        {
            // On clients, set the visuals directly (NetworkVariable will update from server)
            ApplyTeamVisuals(team);
        }
        Debug.Log($"PlayerTeamVisuals: SetTeamNetworked called, applied {team} color");
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
        // Assign the correct team material to ALL submeshes and ALL renderers, including SkinnedMeshRenderer
        Material teamMaterial = team.Equals("Blue", System.StringComparison.OrdinalIgnoreCase) ? blueTeamMaterial : redTeamMaterial;
        var renderers = GetComponentsInChildren<Renderer>(true);

        foreach (var renderer in renderers)
        {
            if (renderer != null && teamMaterial != null)
            {
                // Replace ALL materials with the team material (not just color)
                int matCount = renderer.materials.Length;
                Material[] mats = new Material[matCount];
                for (int i = 0; i < matCount; i++)
                {
                    mats[i] = teamMaterial;
                }
                renderer.materials = mats;
            }
        }
        Debug.Log($"PlayerTeamVisuals: Applied {team} material to all {renderers.Length} renderers and submeshes (full replace)");
    }

    public void UpdateTeamColor(string team)
    {
        Material teamMaterial = team == "Blue" ? blueTeamMaterial : redTeamMaterial;
        Renderer[] renderers = teamColorRenderers;
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
        foreach (var renderer in renderers)
        {
            if (renderer != null && teamMaterial != null)
            {
                int matCount = renderer.materials.Length;
                Material[] mats = new Material[matCount];
                for (int i = 0; i < matCount; i++)
                {
                    mats[i] = teamMaterial;
                }
                renderer.materials = mats;
            }
        }
    }

    public void UpdateTeamColor()
    {
        Renderer[] renderers = teamColorRenderers;
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[PlayerTeamVisuals] No teamColorRenderers assigned for {gameObject.name}, using all child renderers as fallback.");
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        Material teamMaterial = isBlueTeam.Value ? blueTeamMaterial : redTeamMaterial;
        if (teamMaterial == null)
        {
            Debug.LogWarning($"[PlayerTeamVisuals] Team material is null for {(isBlueTeam.Value ? "Blue" : "Red")} team! Using fallback color.");
            Color fallbackColor = isBlueTeam.Value ? Color.blue : Color.red;
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    Material[] mats = renderer.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i].color = fallbackColor;
                    }
                    renderer.materials = mats;
                }
            }
            return;
        }

        foreach (var renderer in renderers)
        {
            if (renderer != null)
            {
                int matCount = renderer.materials.Length;
                Material[] mats = new Material[matCount];
                for (int i = 0; i < matCount; i++)
                {
                    mats[i] = teamMaterial;
                }
                renderer.materials = mats;
                Debug.Log($"[PlayerTeamVisuals] Applied {(isBlueTeam.Value ? "Blue" : "Red")} team material to {renderer.name} (full replace)");
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
        // Assign the correct team material instead of changing color directly
        Material teamMaterial = team.Equals("Blue", System.StringComparison.OrdinalIgnoreCase) ? blueTeamMaterial : redTeamMaterial;
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer != null && teamMaterial != null)
            {
                renderer.material = teamMaterial;
            }
        }
        Debug.Log($"PlayerTeamVisuals: Applied {team} material directly to {renderers.Length} renderers");
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
        ApplyColorToRenderers(blueTeamMaterial);
    }

    private void ApplyRedTeamColors()
    {
        ApplyColorToRenderers(redTeamMaterial);
    }

    private void ApplyColorToRenderers(Material teamMaterial)
    {
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer != null && teamMaterial != null)
            {
                renderer.material = teamMaterial;
            }
        }
    }
}
