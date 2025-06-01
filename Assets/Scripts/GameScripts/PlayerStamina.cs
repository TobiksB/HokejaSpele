using UnityEngine;
using Unity.Netcode;

public class PlayerStamina : NetworkBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainRate = 15f; // Used for UI display and by PlayerMovement
    [SerializeField] private float staminaRegenRate = 35f;
    [SerializeField] private float regenDelay = 0.8f;
    [SerializeField] private float lowStaminaThreshold = 15f; // Used for UI effects and stamina warnings

    // Local variable for stamina (no NetworkVariable needed for client)
    private float currentStamina = 100f;
    private float lastDrainTime = 0f;
    private StaminaBar staminaBar;
    private bool isRegenerating = false; // Add the missing field

    private void Start()
    {
        currentStamina = maxStamina;
        
        // Try to find stamina bar
        staminaBar = FindFirstObjectByType<StaminaBar>();
        if (staminaBar == null)
        {
            // Try to find it as a child of PlayerUI
            var playerUI = GameObject.Find("PlayerUI");
            if (playerUI != null)
            {
                staminaBar = playerUI.GetComponentInChildren<StaminaBar>(true);
            }
        }
        
        UpdateStaminaUI();
    }

    private void Update()
    {
        if (isRegenerating)
        {
            RegenerateStamina();
        }
        
        // Start regenerating after delay
        if (!isRegenerating && Time.time - lastDrainTime > regenDelay)
        {
            isRegenerating = true;
        }
        
        // ADDED: Use lowStaminaThreshold for warnings
        if (currentStamina < lowStaminaThreshold && Time.frameCount % 60 == 0)
        {
            Debug.LogWarning($"Low stamina warning: {currentStamina:F1}/{maxStamina} (threshold: {lowStaminaThreshold})");
        }
    }

    public bool CanSprint()
    {
        // FIXED: Use staminaDrainRate in calculation to make the field useful
        float minStaminaRequired = staminaDrainRate * 0.5f; // Require at least half a second of sprint
        return currentStamina > minStaminaRequired;
    }

    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    // Add missing methods needed by StaminaBar
    public float GetMaxStamina()
    {
        return maxStamina;
    }
    
    public float GetStaminaPercentage()
    {
        return currentStamina / maxStamina;
    }
    
    public float GetStaminaDrainRate()
    {
        return staminaDrainRate;
    }
    
    public float GetLowStaminaThreshold()
    {
        return lowStaminaThreshold;
    }

    public void DrainStamina(float amount)
    {
        if (currentStamina <= 0) return;
        
        currentStamina -= amount;
        currentStamina = Mathf.Max(0, currentStamina);
        lastDrainTime = Time.time;
        isRegenerating = false;
        
        UpdateStaminaUI();
        
        // ADDED: Use lowStaminaThreshold for warnings
        if (currentStamina < lowStaminaThreshold)
        {
            // Only log occasionally to avoid spam
            if (Time.frameCount % 60 == 0)
            {
                Debug.LogWarning($"Low stamina: {currentStamina:F1}/{maxStamina}");
            }
        }
    }

    // Add the missing DrainStaminaForDash method
    public void DrainStaminaForDash()
    {
        float dashCost = staminaDrainRate * 1.5f; // Use staminaDrainRate to make it dynamic
        DrainStamina(dashCost);
    }

    public void RegenerateStamina()
    {
        if (Time.time - lastDrainTime < regenDelay) return;
        if (currentStamina >= maxStamina) return;
        
        float regenAmount = staminaRegenRate * Time.deltaTime;
        currentStamina += regenAmount;
        currentStamina = Mathf.Min(maxStamina, currentStamina);
        
        UpdateStaminaUI();
    }
    
    // Add the missing UpdateStaminaUI method
    private void UpdateStaminaUI()
    {
        if (staminaBar != null)
        {
            staminaBar.UpdateStamina(this);
        }
    }
}
