using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HockeyGame.Network
{
    // Šī klase ļauj klientam kontrolēt objekta transformāciju, nevis serverim
    // This class enables client-side authority over object transforms instead of server authority,
    // allowing player movement to be controlled locally by clients while still syncing to other clients
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        // Pārraksta noklusējuma iestatījumu, lai transformācija būtu klienta autoritāte, nevis servera
        // Overrides the default setting to make transform authority client-side instead of server-side
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }

        protected override void Awake()
        {
            base.Awake();
            
            // Konfigurē transformācijas sinhronizāciju
            // Configure transform synchronization settings
            InLocalSpace = false;  // Izmanto pasaules koordinātu sistēmu, nevis lokālo
            Interpolate = true;    // Iespējo interpolāciju, lai kustība būtu vienmērīgāka
            
            // Konfigurē pozīcijas un rotācijas sinhronizāciju
            // Configure which position axes to synchronize
            SyncPositionX = true;  // Sinhronizē X pozīciju
            SyncPositionY = true;  // Sinhronizē Y pozīciju
            SyncPositionZ = true;  // Sinhronizē Z pozīciju
            
            // Hokejā svarīga ir tikai Y rotācija (ap vertikālo asi)
            // For hockey, only Y rotation is important (around vertical axis)
            SyncRotAngleX = false; // Nesinhronizē X rotāciju hokejā
            SyncRotAngleY = true;  // Sinhronizē Y rotāciju (spēlētāja pagriezienu laukumā)
            SyncRotAngleZ = false; // Nesinhronizē Z rotāciju hokejā
            
            // Precīzi noregulē sinhronizācijas iestatījumus
            // Fine-tune synchronization thresholds and precision settings
            PositionThreshold = 0.001f;  // Minimālā pozīcijas izmaiņa, lai sinhronizētu (metros)
            RotAngleThreshold = 1.0f;    // Minimālā rotācijas izmaiņa, lai sinhronizētu (grādos)
            
            // Izmanto pilnu precizitāti un tiešu pozīcijas atjaunināšanu
            // Use full precision and direct position updates instead of slerp
            UseHalfFloatPrecision = false;  // Izmanto pilnu float precizitāti, nevis pusprecizitāti
            SlerpPosition = false;          // Neizmanto sfērisko lineāro interpolāciju pozīcijai
            
            Debug.Log("ClientNetworkTransform konfigurēts hokeja spēlētāja kustībai");
        }
    }
}
