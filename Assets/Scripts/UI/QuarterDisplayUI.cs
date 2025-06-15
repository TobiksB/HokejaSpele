using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuarterDisplayUI : MonoBehaviour
{
    [Header("Quarter Display")]
    [SerializeField] private TextMeshProUGUI quarterAssignedText; 

    [Header("Highlight Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;

    public void SetQuarter(int currentQuarter)
    {
        if (quarterAssignedText != null)
        {
            quarterAssignedText.text = $"Q{currentQuarter}";
        }
    }
}
