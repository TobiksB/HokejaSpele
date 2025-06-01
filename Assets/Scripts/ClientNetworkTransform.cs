using Unity.Netcode.Components;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Netcode.Components
{

    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
  
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
