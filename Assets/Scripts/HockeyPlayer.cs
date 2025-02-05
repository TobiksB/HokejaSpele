using System.Collections.Generic;
using UnityEngine;

public class HockeyPlayer : CharacterBase {
    public int number;
    [Header("List of equipped items:")]
    public List<int> items = new List<int>();

    protected override void Awake() {
        base.Awake(); // Ensure base initialization happens first
    }

    protected override void Start() {
        base.Start();
        
        if (IsInitialized) { // Fixed: changed isInitialized to IsInitialized
            // Apply saved customization first
            ApplyCustomization();
            
            // Then apply equipment
            foreach (int itemId in items) {
                EquipItem(itemId);
            }
        } else {
            Debug.LogError($"[{gameObject.name}] Cannot equip items, character not properly initialized!");
        }
    }

    private void ApplyCustomization()
    {
        PlayerCustomizationData.Current.LoadFromPrefs();
        
        // Apply the loaded customization
        ChangeSkinColor(new Color(PlayerCustomizationData.Current.skinColorValue, 
                                PlayerCustomizationData.Current.skinColorValue, 
                                PlayerCustomizationData.Current.skinColorValue));
        ChangeHairstyle(PlayerCustomizationData.Current.hairStyle);
        ChangeBeardstyle(PlayerCustomizationData.Current.beardStyle);
        ChangeEyebrowstyle(PlayerCustomizationData.Current.eyebrowStyle);
    }

    // Added missing EquipItem method
    private void EquipItem(int itemId)
    {
        // TODO: Implement equipment logic here
        Debug.Log($"Equipping item with ID: {itemId}");
    }
}
