using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuarterDisplayUI : MonoBehaviour
{
    [Header("Quarter Display")]
    [SerializeField] private TextMeshProUGUI quarterAssignedText; // The placeholder text (e.g. "Q1")

    [Header("Highlight Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;

    public void SetQuarter(int currentQuarter)
    {
        // Set the assigned quarter text (the big/centered one)
        if (quarterAssignedText != null)
        {
            quarterAssignedText.text = $"Q{currentQuarter}";
        }
    }
}
