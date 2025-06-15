using UnityEngine;
using Unity.Netcode;

// Šī klase pārvalda spēlētāja izturību (stamina) hokeja spēlē
// Tā kontrolē izturības izsīkšanu skriešanas laikā un tās atjaunošanu
public class PlayerStamina : NetworkBehaviour
{
    [Header("Izturības iestatījumi")]
    [SerializeField] private float maxStamina = 100f;  // Maksimālais izturības līmenis
    [SerializeField] private float staminaDrainRate = 15f; // Izturības iztērēšanas ātrums, ko izmanto UI attēlojumam un PlayerMovement
    [SerializeField] private float staminaRegenRate = 35f; // Izturības atjaunošanas ātrums sekundē
    [SerializeField] private float regenDelay = 0.8f; // Aizkave sekundēs pirms sākt atjaunot izturību
    [SerializeField] private float lowStaminaThreshold = 15f; // Slieksnis zema izturības līmeņa brīdinājumiem un UI efektiem

    // Lokālais mainīgais izturībai (nav nepieciešams NetworkVariable klientam)
    private float currentStamina = 100f; // Pašreizējais izturības līmenis
    private float lastDrainTime = 0f; // Laiks, kad pēdējo reizi tika iztērēta izturība
    private StaminaBar staminaBar; // Atsauce uz UI elementu, kas attēlo izturības līmeni
    private bool isRegenerating = false; // Norāda, vai pašlaik notiek izturības atjaunošana

    private void Start()
    {
        // Inicializē izturību uz maksimālo vērtību
        currentStamina = maxStamina;
        
        // Mēģina atrast izturības joslu (UI)
        staminaBar = FindFirstObjectByType<StaminaBar>();
        if (staminaBar == null)
        {
            // Mēģina atrast to kā PlayerUI bērnu
            var playerUI = GameObject.Find("PlayerUI");
            if (playerUI != null)
            {
                staminaBar = playerUI.GetComponentInChildren<StaminaBar>(true);
            }
        }
        
        // Atjauno izturības UI elementu
        UpdateStaminaUI();
    }

    private void Update()
    {
        // Atjauno izturību, ja ir atjaunošanas režīmā
        if (isRegenerating)
        {
            RegenerateStamina();
        }
        
        // Sāk izturības atjaunošanu pēc aizkaves
        if (!isRegenerating && Time.time - lastDrainTime > regenDelay)
        {
            isRegenerating = true;
        }
        
        // PIEVIENOTS: Izmanto zema izturības līmeņa slieksni brīdinājumiem
        // Parāda brīdinājumu, ja izturība ir zema, bet tikai reizi 60 kadros, lai izvairītos no pārāk biežiem ziņojumiem
        if (currentStamina < lowStaminaThreshold && Time.frameCount % 60 == 0)
        {
            Debug.LogWarning($"Zema izturība brīdinājums: {currentStamina:F1}/{maxStamina} (slieksnis: {lowStaminaThreshold})");
        }
    }

    // Pārbauda, vai spēlētājam ir pietiekami daudz izturības, lai skrietu
    public bool CanSprint()
    {
        // LABOTS: Izmanto staminaDrainRate aprēķinos, lai lauks būtu noderīgs
        // Pieprasa vismaz puspsekundes skrējiena vērtu izturību
        float minStaminaRequired = staminaDrainRate * 0.5f;
        return currentStamina > minStaminaRequired;
    }

    // Atgriež pašreizējo izturības līmeni
    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    // Pievieno trūkstošās metodes, kas nepieciešamas StaminaBar klasei
    // Atgriež maksimālo izturības līmeni
    public float GetMaxStamina()
    {
        return maxStamina;
    }
    
    // Atgriež izturības procentuālo vērtību (0-1)
    public float GetStaminaPercentage()
    {
        return currentStamina / maxStamina;
    }
    
    // Atgriež izturības iztērēšanas ātrumu
    public float GetStaminaDrainRate()
    {
        return staminaDrainRate;
    }
    
    // Atgriež zema izturības līmeņa slieksni
    public float GetLowStaminaThreshold()
    {
        return lowStaminaThreshold;
    }

    // Samazina izturības līmeni par norādīto daudzumu
    public void DrainStamina(float amount)
    {
        // Neturpina, ja izturība jau ir beigusies
        if (currentStamina <= 0) return;
        
        // Samazina izturību un nodrošina, ka tā nenoiet zem nulles
        currentStamina -= amount;
        currentStamina = Mathf.Max(0, currentStamina);
        lastDrainTime = Time.time;
        isRegenerating = false;
        
        // Atjauno izturības UI elementu
        UpdateStaminaUI();
        
        // PIEVIENOTS: Izmanto zema izturības līmeņa slieksni brīdinājumiem
        if (currentStamina < lowStaminaThreshold)
        {
            // Reģistrē tikai ik pa laikam, lai izvairītos no pārpildīšanas
            if (Time.frameCount % 60 == 0)
            {
                Debug.LogWarning($"Zema izturība: {currentStamina:F1}/{maxStamina}");
            }
        }
    }

    // Pievieno trūkstošo DrainStaminaForDash metodi, kas samazina izturību par lielāku daudzumu pēkšņam skrējienam
    public void DrainStaminaForDash()
    {
        // Dash izmaksā 1.5 reizes vairāk nekā parasts skrējiens
        float dashCost = staminaDrainRate * 1.5f; // Izmanto staminaDrainRate, lai padarītu to dinamisku
        DrainStamina(dashCost);
    }

    // Atjauno izturības līmeni laika gaitā
    public void RegenerateStamina()
    {
        // Neturpina, ja nav pagājis pietiekami daudz laika kopš pēdējās izturības iztērēšanas
        if (Time.time - lastDrainTime < regenDelay) return;
        // Neturpina, ja izturība jau ir maksimālā
        if (currentStamina >= maxStamina) return;
        
        // Aprēķina atjaunošanas daudzumu atkarībā no laika
        float regenAmount = staminaRegenRate * Time.deltaTime;
        currentStamina += regenAmount;
        currentStamina = Mathf.Min(maxStamina, currentStamina);
        
        // Atjauno izturības UI elementu
        UpdateStaminaUI();
    }
    
    // Pievieno trūkstošo UpdateStaminaUI metodi, kas atjauno izturības joslas attēlojumu
    private void UpdateStaminaUI()
    {
        // Atjaunina izturības joslu, ja tā eksistē
        if (staminaBar != null)
        {
            staminaBar.UpdateStamina(this);
        }
    }
}
