using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HockeyPlayer : CharacterBase {
    public int number;
    [Header("List of equipped items:")]
    public List<int> items = new List<int>();
    void Start() {
        foreach (int i in items) {
            EquipItem(i);
        }
    }
}
