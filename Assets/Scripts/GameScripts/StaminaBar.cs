using UnityEngine;
using UnityEngine.UI;

public class StaminaBar : MonoBehaviour
{
    public static StaminaBar Instance { get; private set; }
    
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image staminaFillImage;
    
    [Header("UI Settings")]
    [SerializeField] private Color staminaColor = Color.green;
    [SerializeField] private Vector2 barSize = new Vector2(200f, 20f);
    [SerializeField] private Vector2 screenPosition = new Vector2(20f, 20f);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            SetupStaminaBar();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetupStaminaBar()
    {
        // Setup background
        if (backgroundImage == null)
        {
            GameObject bgGo = new GameObject("StaminaBackground");
            bgGo.transform.SetParent(transform);
            backgroundImage = bgGo.AddComponent<Image>();
            backgroundImage.color = Color.black;
        }

        // Setup fill
        if (staminaFillImage == null)
        {
            GameObject fillGo = new GameObject("StaminaFill");
            fillGo.transform.SetParent(backgroundImage.transform);
            staminaFillImage = fillGo.AddComponent<Image>();
            staminaFillImage.color = staminaColor;
        }

        // Position and size the bar
        RectTransform canvasRect = GetComponent<RectTransform>();
        RectTransform bgRect = backgroundImage.rectTransform;
        RectTransform fillRect = staminaFillImage.rectTransform;

        // Set canvas to stretch
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.sizeDelta = Vector2.zero;

        // Position background
        bgRect.anchorMin = new Vector2(0, 0);
        bgRect.anchorMax = new Vector2(0, 0);
        bgRect.sizeDelta = barSize;
        bgRect.anchoredPosition = screenPosition;

        // Set up fill image
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
    }

    public void UpdateStamina(float fillAmount)
    {
        if (staminaFillImage != null)
        {
            staminaFillImage.transform.localScale = new Vector3(fillAmount, 1, 1);
        }
    }
}
