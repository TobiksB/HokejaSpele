using Unity.Netcode.Components;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// Used for syncing a transform with client side changes. This includes host client changes
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        /// <summary>
        /// Used to determine who can write to this transform. Owner client only.
        /// This imposes state to the server. This is putting trust on your clients. Make sure no security-sensitive features use this transform.
        /// </summary>
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
