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
        // Abonē OnValueChanged visiem klientiem, ne tikai serverim
        isBlueTeam.OnValueChanged += OnTeamChanged;

        if (playerTeam != null)
        {
            // Tikai serveris iestata vērtību, bet visi klienti saņems izmaiņas
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

    //  Pievienot tūlītēju komandas iestatīšanu servera autoritātei
    public void SetTeamImmediate(string team)
    {
        ApplyTeamVisuals(team);
        Debug.Log($"PlayerTeamVisuals: Applied {team} team visuals immediately");
    }

    // Tīklotā komandu iestatīšana
    public void SetTeamNetworked(string team)
    {
        // Iestata NetworkVariable, lai visi klienti saņemtu pareizo vērtību
        bool blue = team.Equals("Blue", System.StringComparison.OrdinalIgnoreCase);
        if (IsServer)
        {
            isBlueTeam.Value = blue;
            UpdateTeamColor();
        }
        else
        {
            // Klientiem iestata vizuālos elementus tieši (NetworkVariable atjaunosies no servera)
            ApplyTeamVisuals(team);
        }
        Debug.Log($"PlayerTeamVisuals: SetTeamNetworked called, applied {team} color");
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetTeamServerRpc(string team)
    {
        // Vispirms piemēro serverī
        ApplyTeamVisuals(team);
        
        // Tad sinhronizē ar visiem klientiem
        SetTeamClientRpc(team);
    }

    [ClientRpc]
    private void SetTeamClientRpc(string team)
    {
        ApplyTeamVisuals(team);
    }

    private void ApplyTeamVisuals(string team)
    {
        // Piešķir pareizo komandas materiālu VISIEM apakšrežģiem un VISIEM renderētājiem, ieskaitot SkinnedMeshRenderer
        Material teamMaterial = team.Equals("Blue", System.StringComparison.OrdinalIgnoreCase) ? blueTeamMaterial : redTeamMaterial;
        var renderers = GetComponentsInChildren<Renderer>(true);

        foreach (var renderer in renderers)
        {
            if (renderer != null && teamMaterial != null)
            {
                // Aizstāj VISUS materiālus ar komandas materiālu (ne tikai krāsu)
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
            Debug.LogWarning($"[PlayerTeamVisuals] Nav piešķirti teamColorRenderers objektam {gameObject.name}, izmantojam visus bērnu renderētājus kā rezerves variantu.");
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        Material teamMaterial = isBlueTeam.Value ? blueTeamMaterial : redTeamMaterial;
        if (teamMaterial == null)
        {
            Debug.LogWarning($"[PlayerTeamVisuals] Komandas materiāls ir null priekš {(isBlueTeam.Value ? "Blue" : "Red")} komandas! Izmantojam rezerves krāsu.");
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
                Debug.Log($"[PlayerTeamVisuals] Piemērots {(isBlueTeam.Value ? "Blue" : "Red")} komandas materiāls objektam {renderer.name} (pilna aizstāšana)");
            }
        }
    }

    public string GetCurrentTeam()
    {
        return isBlueTeam.Value ? "Blue" : "Red";
    }

    //  Pievienot SetTeamColorDirect metodi
    public void SetTeamColorDirect(string team)
    {
        // Piešķir pareizo komandas materiālu tā vietā, lai mainītu krāsu tieši
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

    //  Pievienot trūkstošo SetBlueTeam metodi
    public void SetBlueTeam()
    {
        SetTeam("Blue");
    }

    //  Pievienot trūkstošo SetRedTeam metodi
    public void SetRedTeam()
    {
        SetTeam("Red");
    }

    // Izņemt dublikāta SetTeam metodi - paturēt tikai vienu versiju
    // Ja jau eksistē SetTeam metode, atjaunināt to uz šo implementāciju:
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

    // Pievienot komandas krāsu piemērošanas metodes, ja tās neeksistē
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
