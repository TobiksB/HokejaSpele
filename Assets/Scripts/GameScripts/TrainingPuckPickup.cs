using UnityEngine;

namespace HockeyGame.Game
{
    // Klase, kas ļauj spēlētājam pacelt, turēt un atlaist ripu treniņa režīmā
    // Šī ir vienkāršota versija, kas darbojas bez tīkla koda
    public class TrainingPuckPickup : MonoBehaviour
    {
        [SerializeField] private float pickupRange = 2f; // Attālums, kurā spēlētājs var pacelt ripu
        [SerializeField] private Transform puckHoldPosition; // Pozīcija, kur ripa tiek turēta
        
        private Puck currentPuck; // Pašreizējā ripa, ko tur spēlētājs
        private bool hasPuck = false; // Vai spēlētājam ir ripa
        private bool releasedForShooting = false; // Vai ripa ir atlaista šaušanai
        
        private void Awake()
        {
            // Ja nav iestatīta ripas turēšanas pozīcija, izveido to automātiski
            if (puckHoldPosition == null)
            {
                GameObject holdPos = new GameObject("PuckHoldPosition");
                holdPos.transform.SetParent(transform);
                holdPos.transform.localPosition = new Vector3(0, 0.5f, 1.5f);
                holdPos.transform.localRotation = Quaternion.identity;
                puckHoldPosition = holdPos.transform;
            }
        }
        
        private void Update()
        {
            // Apstrādā ievadi ripas pacelšanai
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (hasPuck && currentPuck != null && !releasedForShooting)
                {
                    // Atlaiž ripu
                    ManualReleasePuck();
                }
                else if (!hasPuck)
                {
                    // Mēģina pacelt ripu
                    TryPickupPuck();
                }
            }
            
            // Nodrošina, ka ripa seko pareizi
            if (hasPuck && currentPuck != null && !releasedForShooting)
            {
                var puckFollower = currentPuck.GetComponent<PuckFollower>();
                if (puckFollower != null && !puckFollower.IsFollowing())
                {
                    puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                }
            }
        }
        
        // Mēģina pacelt tuvumā esošu ripu
        public void TryPickupPuck()
        {
            // Atrod tuvāko ripu
            Puck nearestPuck = FindNearestPuck();
            
            if (nearestPuck != null)
            {
                float distance = Vector3.Distance(transform.position, nearestPuck.transform.position);
                
                if (distance <= pickupRange)
                {
                    releasedForShooting = false;
                    currentPuck = nearestPuck;
                    hasPuck = true;
                    
                    // Sāk PuckFollower, lai ripa sekotu turēšanas pozīcijai
                    var puckFollower = nearestPuck.GetComponent<PuckFollower>();
                    if (puckFollower != null)
                    {
                        puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                        puckFollower.enabled = true;
                    }
                    else
                    {
                        puckFollower = nearestPuck.gameObject.AddComponent<PuckFollower>();
                        puckFollower.StartFollowing(puckHoldPosition, Vector3.zero);
                        puckFollower.enabled = true;
                    }
                    
                    // Konfigurē fiziku pacelšanai
                    var col = nearestPuck.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                    
                    var rb = nearestPuck.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    
                    nearestPuck.SetHeld(true);
                }
            }
        }
        
        // Atrod tuvāko nepaceltuto ripu
        private Puck FindNearestPuck()
        {
            var allPucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            Puck nearestPuck = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var puck in allPucks)
            {
                if (puck == null) continue;
                
                bool isHeld = false;
                try
                {
                    isHeld = puck.IsHeld();
                }
                catch
                {
                    isHeld = false;
                }
                
                if (isHeld) continue;
                
                float distance = Vector3.Distance(transform.position, puck.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPuck = puck;
                }
            }
            
            return nearestPuck;
        }
        
        // Manuāli atlaiž ripu (ar E taustiņu)
        private void ManualReleasePuck()
        {
            if (currentPuck == null) return;
            
            releasedForShooting = false;
            
            // Aptur sekošanu
            var puckFollower = currentPuck.GetComponent<PuckFollower>();
            if (puckFollower != null)
            {
                puckFollower.StopFollowing();
                puckFollower.enabled = false;
            }
            
            // Nosaka atlaišanas pozīciju spēlētāja priekšā
            Vector3 releasePosition = transform.position + transform.forward * 2f;
            releasePosition.y = 0.71f;
            
            currentPuck.transform.position = releasePosition;
            currentPuck.transform.rotation = Quaternion.identity;
            
            // Ieslēdz sadursmes detektoru un fiziku
            var col = currentPuck.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            
            var rb = currentPuck.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            currentPuck.SetHeld(false);
            
            currentPuck = null;
            hasPuck = false;
        }
        
        // Atlaiž ripu šaušanai (izsauc TrainingPlayerShooting)
        public void ReleasePuckForShooting()
        {
            if (currentPuck == null) return;
            
            releasedForShooting = true;
            
            // Nekavējoties aptur sekošanu
            var puckFollower = currentPuck.GetComponent<PuckFollower>();
            if (puckFollower != null)
            {
                puckFollower.StopFollowing();
                puckFollower.enabled = false;
            }
            
            // Nosaka atlaišanas pozīciju spēlētāja priekšā
            Vector3 releasePosition = transform.position + transform.forward * 2f;
            releasePosition.y = 0.71f;
            
            currentPuck.transform.position = releasePosition;
            currentPuck.transform.rotation = Quaternion.identity;
            
            // Ieslēdz sadursmes detektoru un fiziku
            var col = currentPuck.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            
            var rb = currentPuck.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            currentPuck.SetHeld(false);
            
            currentPuck = null;
            hasPuck = false;
        }
        
        // Publiskās metodes stāvokļa pārbaudei
        public bool HasPuck() => hasPuck && !releasedForShooting;
        public Puck GetCurrentPuck() => releasedForShooting ? null : currentPuck;
        public Transform GetPuckHoldPosition() => puckHoldPosition;
        public bool CanShootPuck() => hasPuck && currentPuck != null && !releasedForShooting;
        
        // Atiestata šaušanas karogu
        public void ResetShootingFlag()
        {
            releasedForShooting = false;
        }
    }
}
