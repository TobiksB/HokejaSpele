using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HockeyGame.Network
{
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }

        protected override void Awake()
        {
            base.Awake();
            
            // Configure transform synchronization
            InLocalSpace = false;
            Interpolate = true;
            
            // Configure position and rotation sync
            SyncPositionX = true;
            SyncPositionY = true;
            SyncPositionZ = true;
            
            SyncRotAngleX = false;  // Only sync Y rotation for hockey
            SyncRotAngleY = true;
            SyncRotAngleZ = false;
            
            // Fine-tune sync settings
            PositionThreshold = 0.001f;
            RotAngleThreshold = 1.0f;
            
            UseHalfFloatPrecision = false;
            SlerpPosition = false;
            
            Debug.Log("ClientNetworkTransform configured for hockey player movement");
        }
    }
}
