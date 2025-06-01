using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public class Player : NetworkBehaviour
{
    // Use FixedString instead of string for network synchronization
    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>();
    private NetworkVariable<bool> isBlueTeam = new NetworkVariable<bool>();
    private NetworkVariable<bool> isReady = new NetworkVariable<bool>();

    public void Initialize(string name, bool blueTeam)
    {
        if (IsServer)
        {
            playerName.Value = new FixedString32Bytes(name);
            isBlueTeam.Value = blueTeam;
            isReady.Value = false;
        }
    }

    public string GetPlayerName() => playerName.Value.ToString();
    public bool IsBlueTeam() => isBlueTeam.Value;
    public bool IsReady() => isReady.Value;
}
