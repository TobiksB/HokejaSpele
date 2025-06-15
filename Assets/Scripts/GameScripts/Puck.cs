using UnityEngine;
using Unity.Netcode;
using HockeyGame.Game; // Pievienota namespace priekš GoalTrigger

// Šī klase pārvalda hokeja ripas uzvedību, tās fizikas īpašības un mijiedarbību ar spēlētājiem
// Ripa var būt brīvi kustoša vai turēta spēlētāju rokās, un tiek sinhronizēta starp visiem tīkla klientiem
public class Puck : NetworkBehaviour
{
    [Header("Ripas iestatījumi")]
    [SerializeField] private float friction = 0.95f; // Berzes koeficients, kas palēnina ripu
    [SerializeField] private float minVelocity = 0.1f; // Minimālais ātrums, zem kura ripa apstājas
    [SerializeField] private bool enableDebugLogs = true; // Vai atļaut atkļūdošanas ziņojumus
    
    private Rigidbody puckRigidbody; // Ripas fiziskais ķermenis
    private PuckPickup currentHolder; // Spēlētājs, kas šobrīd tur ripu
    private bool isHeld = false; // Vai ripa ir turēta kāda spēlētāja rokās
    // Tīkla mainīgais, lai sinhronizētu turēšanas stāvokli
    private NetworkVariable<bool> networkIsHeld = new NetworkVariable<bool>(false);
    
    private void Awake()
    {
        puckRigidbody = GetComponent<Rigidbody>();
        if (puckRigidbody == null)
        {
            puckRigidbody = gameObject.AddComponent<Rigidbody>();
        }
        // Nodrošina pareizu ripas fiziku
        puckRigidbody.mass = 0.16f; // Standarta hokeja ripas masa
        puckRigidbody.linearDamping = 0.5f; // Lineārā kustības slāpēšana
        puckRigidbody.angularDamping = 0.5f; // Rotācijas slāpēšana
        
        // Nodrošina pareizu tagu un slāni
        if (!CompareTag("Puck"))
        {
            tag = "Puck";
        }
        
        if (gameObject.layer != 7) // 7. slānis ripām
        {
            gameObject.layer = 7;
        }
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: Inicializēta ar masu {puckRigidbody.mass}kg {gameObject.layer}. slānī");
        }
    }
    
    private void Update()
    {
        // Sinhronizē lokālo stāvokli ar tīkla stāvokli
        bool networkState = networkIsHeld.Value;
        if (isHeld != networkState)
        {
            isHeld = networkState;
            if (enableDebugLogs)
            {
                Debug.Log($"Puck: Sinhronizēts turēšanas stāvoklis ar tīklu: {isHeld}");
            }
        }
    }
    
    private void FixedUpdate()
    {
        //  Piemēro fiziku tikai kad ripa NAV kinematic un NAV turēta
        if (puckRigidbody == null || puckRigidbody.isKinematic || isHeld)
        {
            return;
        }
        
        // Piemēro berzi, lai ripa ar laiku palēninātos
        if (puckRigidbody.linearVelocity.magnitude > minVelocity)
        {
            puckRigidbody.linearVelocity *= friction;
        }
        else
        {
            // Aptur ļoti lēnu kustību
            puckRigidbody.linearVelocity = Vector3.zero;
        }
        
        // Piemēro rotācijas berzi
        if (puckRigidbody.angularVelocity.magnitude > 0.1f)
        {
            puckRigidbody.angularVelocity *= friction;
        }
        else
        {
            puckRigidbody.angularVelocity = Vector3.zero;
        }
        
        // Notur ripu uz ledus līmeņa
        Vector3 pos = transform.position;
        if (pos.y != 0.71f)
        {
            pos.y = 0.71f;
            transform.position = pos;
        }
    }
    
    // Pārbauda, vai ripa ir turēta
    public bool IsHeld()
    {
        return isHeld;
    }
    
    // Metode, kas tiek izsaukta, kad spēlētājs paņem ripu
    public void PickupByPlayer(PuckPickup pickup)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: PickupByPlayer izsaukta no {pickup.name}");
        }
        
        currentHolder = pickup;
        isHeld = true;
        
        // Atjaunina tīkla stāvokli, ja esam serveris
        if (IsServer)
        {
            networkIsHeld.Value = true;
        }
        
        // Pozicionē ripu spēlētāja priekšā uzreiz
        Vector3 holdWorldPosition = pickup.transform.position + pickup.transform.forward * 1.5f + Vector3.up * 0.5f;
        transform.position = holdWorldPosition;
        transform.rotation = pickup.transform.rotation;
        
        //  Piestiprina ripu turēšanas pozīcijai, saglabājot pasaules pozīciju
        Transform holdPosition = pickup.GetPuckHoldPosition();
        if (holdPosition != null)
        {
            // Piestiprina ar pasaules pozīcijas saglabāšanu
            transform.SetParent(holdPosition, true);
            
            if (enableDebugLogs)
            {
                Debug.Log($"Puck: Piestiprināta turēšanas pozīcijai pasaules koordinātēs: {transform.position}");
            }
        }
        else
        {
            // Rezerves variants: piestiprina tieši spēlētājam
            transform.SetParent(pickup.transform, true);
            
            if (enableDebugLogs)
            {
                Debug.Log($"Puck: Piestiprināta tieši spēlētājam pasaules koordinātēs: {transform.position}");
            }
        }
        
        // Konfigurē fizikas īpašības paņemšanai
        if (puckRigidbody != null)
        {
            puckRigidbody.isKinematic = true; // Izslēdz fizikas ietekmi uz ripu
            puckRigidbody.useGravity = false; // Izslēdz gravitācijas ietekmi
        }
        
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false; // Izslēdz sadursmes detektēšanu
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: Veiksmīgi paņemta gala pozīcijā {transform.position}");
        }
    }

    // Metode ripas atlaišanai no spēlētāja
    public void ReleaseFromPlayer(Vector3 position, Vector3 velocity)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: ReleaseFromPlayer izsaukta pozīcijā {position} ar ātrumu {velocity}");
        }
        
        currentHolder = null;
        isHeld = false;
        
        // Atjaunina tīkla stāvokli, ja esam serveris
        if (IsServer)
        {
            networkIsHeld.Value = false;
        }
        
        //  Vispirms atvieno no vecāka, tad iestata pozīciju
        transform.SetParent(null);
        
        //  Nodrošina pareizu atlaišanas pozīciju (spēlētāja priekšā, nevis (0,0,0))
        if (position == Vector3.zero)
        {
            // Avārijas rezerves variants, ja pozīcija ir nulle
            position = new Vector3(0f, 0.71f, 0f);
            Debug.LogWarning("Puck: Atlaišanas pozīcija bija nulle, izmanto centru kā rezerves variantu");
        }
        
        transform.position = position;
        transform.rotation = Quaternion.identity;
        
        // Konfigurē ripu atlaišanai
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true; // Ieslēdz sadursmes detektēšanu
        }
        
        if (puckRigidbody != null)
        {
            puckRigidbody.isKinematic = false; // Ieslēdz fizikas ietekmi
            puckRigidbody.useGravity = true; // Ieslēdz gravitāciju
            puckRigidbody.linearVelocity = velocity; // Piešķir ripaai ātrumu
            puckRigidbody.angularVelocity = Vector3.zero; // Noņem rotācijas ātrumu
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: Veiksmīgi atlaista pozīcijā {transform.position} ar ātrumu {velocity}");
        }
    }
    
    //  Metode, lai piespiedu kārtā notīrītu turēšanas stāvokli (šaušanas sistēmai)
    public void SetHeld(bool held)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: SetHeld izsaukta ar vērtību: {held}");
        }
        
        isHeld = held;
        
        // Atjaunina tīkla stāvokli, ja esam serveris
        if (IsServer)
        {
            networkIsHeld.Value = held;
        }
        
        if (!held)
        {
            currentHolder = null;
            
            // Ieslēdz fiziku tikai ja ripa nav kinematic citu iemeslu dēļ
            if (puckRigidbody != null && puckRigidbody.isKinematic)
            {
                puckRigidbody.isKinematic = false;
                puckRigidbody.useGravity = true;
            }
            
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.enabled)
            {
                collider.enabled = true;
            }
            
            // Atvieno tikai ja ripa ir piestiprināta turēšanas pozīcijai
            if (transform.parent != null && 
                (transform.parent.name.Contains("Hold") || transform.parent.name.Contains("Puck")))
            {
                transform.SetParent(null);
            }
        }
    }
    
    //  Metode, lai pareizi piemērotu šaušanas spēku
    public void ApplyShootForce(Vector3 force)
    {
        if (puckRigidbody == null || puckRigidbody.isKinematic)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"Puck: Nevar piemērot šaušanas spēku - rigidbody ir null vai kinematic");
            }
            return;
        }
        
        puckRigidbody.AddForce(force, ForceMode.VelocityChange);
        
        if (enableDebugLogs)
        {
            Debug.Log($"Puck: Piemērots šaušanas spēks {force}, rezultāta ātrums: {puckRigidbody.linearVelocity}");
        }
    }
    
    //  Atiestata ripu uz centru (pēc vārtu guvuma)
    public void ResetToCenter()
    {
        if (enableDebugLogs)
        {
            Debug.Log("Puck: Notiek atiestatīšana uz centru");
        }
        
        // Notīra turēšanas stāvokli
        SetHeld(false);
        
        // Pozicionē centrā
        Vector3 centerPos = new Vector3(0f, 0.71f, 0f);
        transform.position = centerPos;
        transform.rotation = Quaternion.identity;
        
        // Aptur visu kustību
        if (puckRigidbody != null)
        {
            puckRigidbody.linearVelocity = Vector3.zero;
            puckRigidbody.angularVelocity = Vector3.zero;
        }
    }
    
    // Apstrādā sadursmes ar citiem objektiem
    private void OnCollisionEnter(Collision collision)
    {
        // Apstrādā sadursmes ar sienām, spēlētājiem utt.
        if (enableDebugLogs && !isHeld)
        {
            Debug.Log($"Puck: Sadūrās ar {collision.gameObject.name}");
        }
    }
    
    // Apstrādā iekļūšanu trigeru zonās
    private void OnTriggerEnter(Collider other)
    {
        // Apstrādā vārtu detektēšanu
        if (other.CompareTag("Goal"))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Puck: Iegāja vārtu trigerī {other.name}");
            }
            
            // Noņemta atsauce uz neeksistējošo Goal klasi
            // Izmanto tikai GoalTrigger, kas eksistē HockeyGame.Game namespace
            var goalTrigger = other.GetComponent<GoalTrigger>();
            if (goalTrigger != null)
            {
                // GoalTrigger automātiski apstrādās vārtu loģiku
                if (enableDebugLogs)
                {
                    Debug.Log($"Puck: Atrasta GoalTrigger komponente uz {other.name}");
                }
            }
            else
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"Puck: Vārtu objektam {other.name} ir Goal tags, bet nav GoalTrigger komponentes!");
                }
            }
        }
    }
}
