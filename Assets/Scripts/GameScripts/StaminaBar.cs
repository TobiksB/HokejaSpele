using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StaminaBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private Gradient staminaGradient = new Gradient();
    [SerializeField] private Canvas staminaCanvas;
    [SerializeField] private Text staminaText;
    [SerializeField] private Text staminaPercentText;
    
    [Header("Animation")]
    [SerializeField] private float smoothFillSpeed = 5f;
    
    private float currentDisplayedStamina = 1f;
    private PlayerStamina playerStamina;

    private void Awake()
    {
        // Create default components if not assigned
        if (staminaFillImage == null)
        {
            CreateStaminaBar();
        }
        
        if (staminaGradient.colorKeys.Length == 0)
        {
            // Create a default gradient
            var colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(Color.red, 0f);
            colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);
            colorKeys[2] = new GradientColorKey(Color.green, 1f);
            
            var alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
            staminaGradient.SetKeys(colorKeys, alphaKeys);
        }
    }

    private void Start()
    {
        // Find player stamina if not assigned
        if (playerStamina == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerStamina = player.GetComponent<PlayerStamina>();
            }
        }
    }

    private void Update()
    {
        if (playerStamina != null)
        {
            // Smooth transition to target value
            float targetFill = playerStamina.GetStaminaPercentage();
            currentDisplayedStamina = Mathf.Lerp(currentDisplayedStamina, targetFill, Time.deltaTime * smoothFillSpeed);
            
            // Update fill amount
            if (staminaFillImage != null)
            {
                staminaFillImage.fillAmount = currentDisplayedStamina;
                staminaFillImage.color = staminaGradient.Evaluate(currentDisplayedStamina);
            }
            
            // Update text if available
            if (staminaText != null)
            {
                staminaText.text = $"{Mathf.CeilToInt(playerStamina.GetCurrentStamina())} / {Mathf.CeilToInt(playerStamina.GetMaxStamina())}";
            }
            
            if (staminaPercentText != null)
            {
                staminaPercentText.text = $"{Mathf.RoundToInt(currentDisplayedStamina * 100)}%";
            }
        }
    }

    private void CreateStaminaBar()
    {
        // Create UI components if they don't exist
        GameObject fillArea = new GameObject("StaminaFill");
        fillArea.transform.SetParent(transform);
        
        staminaFillImage = fillArea.AddComponent<Image>();
        staminaFillImage.color = Color.green;
        staminaFillImage.type = Image.Type.Filled;
        staminaFillImage.fillMethod = Image.FillMethod.Horizontal;
        staminaFillImage.fillOrigin = 0;
        staminaFillImage.fillAmount = 1f;
        
        RectTransform fillRect = staminaFillImage.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    // Public method for updating from PlayerStamina
    public void UpdateStamina(PlayerStamina stamina)
    {
        if (staminaFillImage != null)
        {
            float percentage = stamina.GetStaminaPercentage();
            staminaFillImage.fillAmount = percentage;
            staminaFillImage.color = staminaGradient.Evaluate(percentage);
        }
    }
}
