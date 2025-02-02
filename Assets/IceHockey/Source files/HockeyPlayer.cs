using System.Collections;
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
        
        if (isInitialized) {
            foreach (int i in items) {
                EquipItem(i);
            }
        } else {
            Debug.LogError($"[{gameObject.name}] Cannot equip items, character not properly initialized!");
        }
    }
}