using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject thirdPersonCameraPrefab;
    [SerializeField] private Camera defaultSceneCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsServer && IsClient)
        {
            SpawnPlayerServerRpc(PlayerPrefs.GetString("PlayerTeam", "Red"));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnPlayerServerRpc(string team)
    {
        Vector3 spawnPos = team == "Blue" ? new Vector3(-11.84f, 0.5f, 0f) : new Vector3(11.97f, 0.5f, 0f);
        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId);
        
        SetupPlayerCameraClientRpc(netObj.NetworkObjectId);
    }

    [ClientRpc]
    private void SetupPlayerCameraClientRpc(ulong playerNetObjId)
    {
        if (defaultSceneCamera != null)
            defaultSceneCamera.gameObject.SetActive(false);

        NetworkObject playerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerNetObjId];
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
