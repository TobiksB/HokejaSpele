using UnityEngine;
using Unity.Netcode;

// Šī klase atbild par spēlētāju objektu radīšanu tīklotā spēlē
// Tā izmanto NetworkBehaviour, lai radītu spēlētāju objektus un kameras, kas tiem seko
public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab; // Spēlētāja prefab, kas tiks radīts
    [SerializeField] private GameObject thirdPersonCameraPrefab; // Trešās personas kameras prefab, kas sekos spēlētājam
    [SerializeField] private Camera defaultSceneCamera; // Noklusējuma ainas kamera, kas tiks atslēgta, kad spēlētāja kamera būs radīta

    public override void OnNetworkSpawn()
    {
        // Kad klienta savienojums ir izveidots (bet ne uz servera), pieprasa savu spēlētāju
        if (!IsServer && IsClient)
        {
            SpawnPlayerServerRpc(PlayerPrefs.GetString("PlayerTeam", "Red"));
        }
    }

    // Servera RPC metode, ko klients izsauc, lai pieprasītu spēlētāja radīšanu
    [ServerRpc(RequireOwnership = false)]
    private void SpawnPlayerServerRpc(string team)
    {
        // Nosaka sākuma pozīciju atkarībā no komandas - zilā komanda kreisajā pusē, sarkanā komanda labajā pusē
        Vector3 spawnPos = team == "Blue" ? new Vector3(-11.84f, 0.5f, 0f) : new Vector3(11.97f, 0.5f, 0f);
        
        // Rada spēlētāja objektu serverī
        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        
        // Piešķir spēlētāja objektu klientam, kas to pieprasīja
        netObj.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId);
        
        // Informē visus klientus, ka nepieciešams iestatīt kameru spēlētājam
        SetupPlayerCameraClientRpc(netObj.NetworkObjectId);
    }

    // Klienta RPC metode, ko serveris izsauc, lai iestatītu spēlētāja kameru
    [ClientRpc]
    private void SetupPlayerCameraClientRpc(ulong playerNetObjId)
    {
        // Atslēdz noklusējuma kameru, jo tagad izmantosim spēlētāja kameru
        if (defaultSceneCamera != null)
            defaultSceneCamera.gameObject.SetActive(false);

        // Atrod spēlētāja objektu pēc tā tīkla ID
        NetworkObject playerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerNetObjId];
        
        // Tikai īpašnieka klientā izveido kameru, kas seko spēlētājam
        if (playerObj != null && playerObj.IsOwner)
        {
            GameObject camera = Instantiate(thirdPersonCameraPrefab);
            var cameraFollow = camera.GetComponent<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(playerObj.transform);
            }
        }
    }
}
