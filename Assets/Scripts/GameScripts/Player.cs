using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

// Šī klase reprezentē spēlētāju hokeja spēlē un glabā spēlētāja pamatinformāciju tīklā
// Klase nodrošina spēlētāja vārda, komandas un gatavības statusa sinhronizēšanu starp visiem klientiem
public class Player : NetworkBehaviour
{
    // Tīkla mainīgais spēlētāja vārda glabāšanai (maks. 32 baiti)
    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>();
    
    // Tīkla mainīgais, kas norāda, vai spēlētājs pieder zilajai komandai (true = zilā, false = sarkanā)
    private NetworkVariable<bool> isBlueTeam = new NetworkVariable<bool>();
    
    // Tīkla mainīgais, kas norāda, vai spēlētājs ir gatavs spēles sākšanai
    private NetworkVariable<bool> isReady = new NetworkVariable<bool>();

    // Inicializē spēlētāja pamatinformāciju - izsaucama tikai no servera
    public void Initialize(string name, bool blueTeam)
    {
        // Pārbauda, vai šis kods tiek izpildīts uz servera
        if (IsServer)
        {
            // Uzstāda spēlētāja vārdu, komandu un sākuma gatavības statusu
            playerName.Value = new FixedString32Bytes(name);
            isBlueTeam.Value = blueTeam;
            isReady.Value = false;
        }
    }

    // Atgriež spēlētāja vārdu kā parastu virkni
    public string GetPlayerName() => playerName.Value.ToString();
    
    // Atgriež true, ja spēlētājs pieder zilajai komandai
    public bool IsBlueTeam() => isBlueTeam.Value;
    
    // Atgriež true, ja spēlētājs ir gatavs spēlei
    public bool IsReady() => isReady.Value;
}
