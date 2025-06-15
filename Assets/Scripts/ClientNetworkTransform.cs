using Unity.Netcode.Components;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// Tiek izmantots, lai sinhronizētu transformāciju ar klienta puses izmaiņām. Tas ietver arī resursdatora klienta izmaiņas.
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        /// Tiek izmantots, lai noteiktu, kurš var rakstīt šajā transformācijā. Tikai īpašnieka klients.
        /// Tas uzspiež stāvokli serverim. Tas nozīmē uzticēšanos klientiem. Pārliecinieties, ka neviena drošībai jutīga funkcija neizmanto šo transformāciju.
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
