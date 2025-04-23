using Unity.Netcode;
using UnityEngine;

namespace HockeyGame.Game
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }
        [SerializeField] private GameObject puckPrefab;
        [SerializeField] private Transform puckSpawnPoint;
        private GameObject currentPuck;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                var existingPuck = Object.FindFirstObjectByType<Puck>();
                if (existingPuck == null)
                {
                    SpawnPuck();
                }
                else
                {
                    currentPuck = existingPuck.gameObject;
                }
            }
        }

        private void SpawnPuck()
        {
            if (!IsServer) return;
            
            Puck[] existingPucks = Object.FindObjectsByType<Puck>(FindObjectsSortMode.None);
            foreach (var puck in existingPucks)
            {
                if (puck.NetworkObject != null)
                {
                    puck.NetworkObject.Despawn();
                }
                Destroy(puck.gameObject);
            }

            Vector3 spawnPos = puckSpawnPoint != null ? puckSpawnPoint.position : Vector3.zero;
            currentPuck = Instantiate(puckPrefab, spawnPos, Quaternion.identity);
            
            var networkObject = currentPuck.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
            }
        }
    }
}
