using UnityEngine;
using UnityEngine.UI;

public class PlayerCustomization : MonoBehaviour
{
    public GameObject customizationPanel;
    public GameObject mainMenuPanel;
    public Button openCustomizationButton;
    public Button closeCustomizationButton;

    private void Start()
    {
        openCustomizationButton.onClick.AddListener(OpenCustomizationPanel);
        closeCustomizationButton.onClick.AddListener(CloseCustomizationPanel);

        if (customizationPanel != null)
        {
            customizationPanel.SetActive(false); // Ensure the panel is hidden at the start
        }
    }

    public void OpenCustomizationPanel()
    {
        customizationPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
    }

    public void CloseCustomizationPanel()
    {
        customizationPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}