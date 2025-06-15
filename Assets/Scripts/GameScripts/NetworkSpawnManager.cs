using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

// Šī klase pārvalda spēlētāju komandu piešķiršanu un tīkla spawning procesu
// NetworkSpawnManager atbild par spēlētāju piederības noteikšanu komandām un
// sadarbojas ar GameNetworkManager, lai nodrošinātu pareizu spēlētāju spawning procesu
public class NetworkSpawnManager : NetworkBehaviour
{
    // Singleton instance, lai piekļūtu šai klasei no citām klasēm
    public static NetworkSpawnManager Instance { get; private set; }

    
    // Vārdnīca, kas glabā katra klienta komandu (Red vai Blue)
    private Dictionary<ulong, string> playerTeams = new Dictionary<ulong, string>();

    private void Awake()
    {
        // Standarta Singleton ieviešana - nodrošina, ka pastāv tikai viena šīs klases instance
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Saglabā objektu starp scēnu ielādēm
            Debug.Log("NetworkSpawnManager: Instance izveidota (PIEZĪME: Spawning tagad tiek apstrādāts ar GameNetworkManager's ConnectionApprovalCheck)");
        }
        else if (Instance != this)
        {
            // Iznīcina dublikātu, ja tāds eksistē
            Destroy(gameObject);
        }
    }

    // Atgriež spēlētāja komandu pēc klienta ID, vai piešķir jaunu komandu, ja tā vēl nav noteikta
    // Komandas tiek sadalītas pēc pāra/nepāra klienta ID, lai nodrošinātu līdzsvarotu sadalījumu
    public string GetPlayerTeam(ulong clientId)
    {
        // Mēģina atrast jau piešķirtu komandu
        if (playerTeams.TryGetValue(clientId, out string team))
        {
            return team;
        }

        // Piešķir komandu, balstoties uz klienta ID - pāra ID iet sarkanā komandā, nepāra - zilā
        team = (clientId % 2 == 0) ? "Red" : "Blue";
        playerTeams[clientId] = team;
        Debug.Log($"NetworkSpawnManager: Piešķirta {team} komanda klientam {clientId}");
        
        return team;
    }

    // PIEZĪME: Šī klase agrāk bija atbildīga par spēlētāju spawning procesu,
    // bet tagad šo funkcionalitāti ir pārņēmis GameNetworkManager,
    // izmantojot ConnectionApprovalCheck metodi.
    // NetworkSpawnManager tagad galvenokārt atbild par komandu piešķiršanu.
}