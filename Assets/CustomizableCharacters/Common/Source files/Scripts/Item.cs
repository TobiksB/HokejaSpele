using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour {
    public GameObject male;
    public GameObject female;
    public bool skinned; // static or rigged item?


    public bool showHead; // show head model, hair, beard, eyebrows under helmet?
    public bool showHair;
    public bool showBeard;
    public bool showEyebrow;

    public enum EquipmentSlots {
        head,
        body,
        legs,
        feet,
        hair,
        beard,
        mainhand,
        offhand,
        hands,
        none,
        eyebrow,
        neck,
        back,
        accessory
    }

    public EquipmentSlots equipmentSlot;


}
