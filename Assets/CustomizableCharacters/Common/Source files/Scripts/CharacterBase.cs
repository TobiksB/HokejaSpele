using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterBase : MonoBehaviour {

    public int gender; // 0 = male, 1 = female
    public int hairStyle;
    public int beardStyle;
    public int eyebrowStyle;

    public Color skinColor;
    public Color eyeColor;
    public Color hairColor;
    public Color mouthColor;

    private enum ItemType {
        Equipment,
        Hair,
        Beard,
        BodyParts,
        Eyebrow
    }

    public class EquipmentSlot {
        public Item item;
        public Item.EquipmentSlots slot;
        public Transform container;
        public GameObject instancedObject;
        public GameObject activeObject; // male or female variation
        public bool inUse;
        public int itemId;
    }

    private List<EquipmentSlot> equipmentSlots = new List<EquipmentSlot>();

    private List<GameObject> bodyParts = new List<GameObject>();

    [HideInInspector]
    public SkinnedMeshRenderer referenceMesh; // used for rigging items to the skeleton

    public Item EquipItem(int itemId) { // use this to equip items
        Item i = LoadItem(itemId);
        SetupBody();
        return i;
    }
    public void UnequipSlot(Item.EquipmentSlots slot) {
        UnloadSlot(slot);
        SetupBody();
    }
    public void UnequipAll() {
        foreach (EquipmentSlot s in equipmentSlots) {
            if (s.inUse) {
                GameObject.Destroy(s.instancedObject);
            }
            s.inUse = false;
        }
        SetupBody();
    }
    public void ChangeHairstyle(int id) {
        hairStyle = id;
        SetupBody();
    }
    public void ChangeEyebrowstyle(int id) {
        eyebrowStyle = id;
        SetupBody();
    }
    public void ChangeBeardstyle(int id) {
        beardStyle = id;
        SetupBody();
    }
    public void ChangeGender(int id) {
        gender = id;
        UnequipAll();
        SetupBody();
    }
    public void ChangeSkinColor(Color c) {
        skinColor = c;
        SetupBody();
        foreach(EquipmentSlot slot in equipmentSlots) {
            if (slot.inUse) {
                SetItemColor(slot.activeObject);
            }
        }
    }
    public void ChangeHairColor(Color c) {
        hairColor = c;
        SetupBody();
    }
    public void ChangeEyeColor(Color c) {
        eyeColor = c;
        SetupBody();
    }


    public void ClearBody() {
        foreach (GameObject g in bodyParts) {
            GameObject.Destroy(g);
        }
    }

    void UnloadSlot(Item.EquipmentSlots slot) {
        if (GetEquipmentSlot(slot).inUse) {
            GetEquipmentSlot(slot).inUse = false;
            GameObject.Destroy(GetEquipmentSlot(slot).instancedObject);
        }
    }

    public EquipmentSlot GetEquipmentSlot(Item.EquipmentSlots slot) {
        foreach(EquipmentSlot e in equipmentSlots) {
            if(e.slot == slot) {
                return e;
            }
        }
        return null;
    }


    private Item LoadItem(int itemId) {
        return LoadItem(itemId, ItemType.Equipment);
    }
    private Item LoadItem(int itemId, ItemType itemType) {
        if (itemId == 0) {
            return null;

        }
        GameObject loadedItem = null;
        object loadedObject = Resources.Load("CustomizableCharacters/" + itemType.ToString() + "/" + itemId, typeof(GameObject));
        if (loadedObject == null) {
            Debug.LogError("Failed to load item "+itemId+". Make sure you are using the correct item ID!");
            return null;
        }
            loadedItem = GameObject.Instantiate((GameObject)loadedObject) as GameObject;
        
        Item item = loadedItem.GetComponent<Item>();
        EquipmentSlot slot = GetEquipmentSlot(item.equipmentSlot);
        GameObject itemObject = null;
        item.male.SetActive(false);
        item.female.SetActive(false);

        if (gender == 0) {
            itemObject = item.male;
        } else {
            itemObject = item.female;
        }
        itemObject.SetActive(true);

        if (item.skinned) {
            itemObject.GetComponentInChildren<SkinnedMeshRenderer>().bones = referenceMesh.bones;
            itemObject.GetComponentInChildren<SkinnedMeshRenderer>().updateWhenOffscreen = true;
            item.transform.SetParent(transform);
        } else {
            item.transform.SetParent(slot.container);
        }


        loadedItem.transform.localPosition = Vector3.zero;
        loadedItem.transform.localScale = new Vector3(1, 1, 1);
        loadedItem.transform.localRotation = Quaternion.identity;

        if (slot != null) {
            if (slot.inUse) { // replace current item, if you need to remove stats of the previous item, do it here (e.g. character health, damage)
                GameObject.Destroy(slot.instancedObject);
            }
            slot.item = item;
            slot.itemId = itemId;
            slot.inUse = true;
            slot.instancedObject = loadedItem;
            slot.activeObject = itemObject;
        }

        if (itemType == ItemType.BodyParts) {
            bodyParts.Add(loadedItem);
        }
        referenceMesh.enabled = false;
        SetItemColor(itemObject);
        return item;
    }

    void SetItemColor(GameObject itemObject) {
        ColorChanger colorChanger = itemObject.GetComponent<ColorChanger>();
        if (colorChanger != null) {
            foreach (ColorChanger.Changer c in colorChanger.changers) {
                Renderer r = c.rend;
                Material m = r.materials[c.materialIndex];
                if (c.type == ColorChanger.Type.Skin) {
                    m.color = skinColor;
                } else if (c.type == ColorChanger.Type.Hair) {
                    m.color = hairColor;
                } else if (c.type == ColorChanger.Type.Mouth) {
                    m.color = mouthColor;
                } else if (c.type == ColorChanger.Type.Eyes) {
                    m.color = eyeColor;
                }
            }
        }
    }



    public void SetupBody() { // load body parts, hair, beard, eyebrows
        ClearBody();

        if (GetEquipmentSlot(Item.EquipmentSlots.head).inUse) {
            if (GetEquipmentSlot(Item.EquipmentSlots.head).item.showHead) {
                LoadItem(4, ItemType.BodyParts); // head 
            }
            if (GetEquipmentSlot(Item.EquipmentSlots.head).item.showHair) {
                LoadItem(hairStyle, ItemType.Hair);
            } else {
                UnloadSlot(Item.EquipmentSlots.hair);
            }
            if (GetEquipmentSlot(Item.EquipmentSlots.head).item.showEyebrow) {
                LoadItem(eyebrowStyle, ItemType.Eyebrow);
            } else {
                UnloadSlot(Item.EquipmentSlots.eyebrow);
            }
            if (gender == 0) {
                if (GetEquipmentSlot(Item.EquipmentSlots.head).item.showBeard) {
                    LoadItem(beardStyle, ItemType.Beard);
                } else {
                    UnloadSlot(Item.EquipmentSlots.beard);
                }
            } else {
                UnloadSlot(Item.EquipmentSlots.beard);
            }
        } else {
            LoadItem(4, ItemType.BodyParts); // head 
            LoadItem(hairStyle, ItemType.Hair);
            LoadItem(eyebrowStyle, ItemType.Eyebrow);
            if (gender == 0) {
                LoadItem(beardStyle, ItemType.Beard);
            } else {
                UnloadSlot(Item.EquipmentSlots.beard);
            }
        }


        if (GetEquipmentSlot(Item.EquipmentSlots.body).inUse == false) {
            LoadItem(3, ItemType.BodyParts); // torso 
        }
        if (GetEquipmentSlot(Item.EquipmentSlots.hands).inUse == false) {
            LoadItem(5, ItemType.BodyParts); // hands
        }
        if (GetEquipmentSlot(Item.EquipmentSlots.legs).inUse == false) {
            LoadItem(1, ItemType.BodyParts); // legs
        }
        if (GetEquipmentSlot(Item.EquipmentSlots.feet).inUse == false) {
            LoadItem(2, ItemType.BodyParts); // feet
        }
    }

    public void Awake() {
        if (referenceMesh == null) referenceMesh = transform.FindDeepChild("Torus").GetComponent<SkinnedMeshRenderer>();
        SetupSlots();
        SetupBody();
    }
    void SetupSlots() {
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.body });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.feet });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.hair, container = transform.FindDeepChild("EQ_Head") });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.head, container = transform.FindDeepChild("EQ_Head") });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.legs });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.mainhand, container = transform.FindDeepChild("EQ_MainHand") });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.offhand, container = transform.FindDeepChild("EQ_OffHand") });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.hands });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.beard });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.eyebrow });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.neck });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.accessory });
        equipmentSlots.Add(new EquipmentSlot() { slot = Item.EquipmentSlots.back, container = transform.FindDeepChild("EQ_Back") });
    }

}
