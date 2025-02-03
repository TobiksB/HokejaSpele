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

    protected bool isInitialized = false;
    public bool IsInitialized => isInitialized;

    protected virtual void Awake() {
        bodyParts = new List<GameObject>();
        equipmentSlots = new List<EquipmentSlot>();
        InitializeDefaultValues();
        SetupSlots();
    }

    protected virtual void Start() {
        Transform torusTransform = transform.FindDeepChild("Torus");
        if (torusTransform == null) {
            Debug.LogError($"[{gameObject.name}] Could not find 'Torus' child object!");
            return;
        }

        referenceMesh = torusTransform.GetComponent<SkinnedMeshRenderer>();
        if (referenceMesh == null) {
            Debug.LogError($"[{gameObject.name}] No SkinnedMeshRenderer found on Torus!");
            return;
        }

        isInitialized = true;
        SetupBody();
    }

    protected void InitializeDefaultValues() {
        if (equipmentSlots == null) {
            equipmentSlots = new List<EquipmentSlot>();
        }
        if (bodyParts == null) {
            bodyParts = new List<GameObject>();
        }
        
        gender = 0;
        hairStyle = 1;
        beardStyle = 1;
        eyebrowStyle = 1;
        
        skinColor = Color.white;
        eyeColor = Color.blue;
        hairColor = Color.black;
        mouthColor = Color.red;
    }

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
        UnloadSlot(Item.EquipmentSlots.hair);
        LoadItem(hairStyle, ItemType.Hair);
        SetupBody();
    }

    public void ChangeEyebrowstyle(int id) {
        eyebrowStyle = id;
        UnloadSlot(Item.EquipmentSlots.eyebrow);
        LoadItem(eyebrowStyle, ItemType.Eyebrow);
        SetupBody();
    }

    public void ChangeBeardstyle(int id) {
        beardStyle = id;
        UnloadSlot(Item.EquipmentSlots.beard);
        LoadItem(beardStyle, ItemType.Beard);
        SetupBody();
    }

    public void ChangeGender(int id) {
        gender = id;
        UnequipAll();
        SetupBody();
    }

    public void ChangeSkinColor(Color c) {
        skinColor = c;
        ClearBody();
        SetupBody();
        foreach(EquipmentSlot slot in equipmentSlots) {
            if (slot.inUse && slot.activeObject != null) {
                SetItemColor(slot.activeObject);
            }
        }
    }

    public void ChangeHairColor(Color c) {
        hairColor = c;
        foreach(EquipmentSlot slot in equipmentSlots) {
            if (slot.inUse && slot.activeObject != null) {
                SetItemColor(slot.activeObject);
            }
        }
    }

    public void ChangeEyeColor(Color c) {
        eyeColor = c;
        // Implement the logic to change the eye color
    }

    public List<string> GetAvailableHairStyles() {
        // Return a list of available hair styles
        return new List<string> { "1", "2", "3", "4", "5", "6" }; // Example IDs
    }

    public List<string> GetAvailableBeardStyles() {
        // Return a list of available beard styles
        return new List<string> { "1", "2", "3", "4", "5", "6"}; // Example IDs
    }

    public List<string> GetAvailableEyebrowStyles() {
        // Return a list of available eyebrow styles
        return new List<string> { "1"}; // Example IDs
    }

    public void ClearBody() {
        foreach (GameObject g in bodyParts) {
            GameObject.Destroy(g);
        }
        bodyParts.Clear();
    }

    protected void SetupBody() {
        if (!isInitialized) {
            Debug.LogWarning($"[{gameObject.name}] Trying to setup body before initialization is complete.");
            return;
        }

        if (referenceMesh == null || equipmentSlots == null) {
            Debug.LogError($"[{gameObject.name}] Reference mesh or equipment slots are null. Cannot setup body.");
            return;
        }

        try {
            ClearBody();

            // Load base body parts first
            LoadItem(4, ItemType.BodyParts); // head
            LoadItem(3, ItemType.BodyParts); // torso
            LoadItem(5, ItemType.BodyParts); // hands
            LoadItem(1, ItemType.BodyParts); // legs
            LoadItem(2, ItemType.BodyParts); // feet

            // Then handle equipment and customization
            var headSlot = GetEquipmentSlot(Item.EquipmentSlots.head);
            if (headSlot != null && headSlot.inUse && headSlot.item != null) {
                if (headSlot.item.showHair) {
                    LoadItem(hairStyle, ItemType.Hair);
                }
                if (headSlot.item.showEyebrow) {
                    LoadItem(eyebrowStyle, ItemType.Eyebrow);
                }
                if (gender == 0 && headSlot.item.showBeard) {
                    LoadItem(beardStyle, ItemType.Beard);
                }
            } else {
                LoadItem(hairStyle, ItemType.Hair);
                LoadItem(eyebrowStyle, ItemType.Eyebrow);
                if (gender == 0) {
                    LoadItem(beardStyle, ItemType.Beard);
                }
            }
        }
        catch (System.Exception e) {
            Debug.LogError($"[{gameObject.name}] Error in SetupBody: {e.Message}\n{e.StackTrace}");
        }
    }

    private void UnloadSlot(Item.EquipmentSlots slot) {
        var equipSlot = GetEquipmentSlot(slot);
        if (equipSlot != null && equipSlot.inUse) {
            if (equipSlot.instancedObject != null) {
                GameObject.Destroy(equipSlot.instancedObject);
            }
            equipSlot.inUse = false;
            equipSlot.item = null;
            equipSlot.activeObject = null;
            equipSlot.instancedObject = null;
        }
    }

    public EquipmentSlot GetEquipmentSlot(Item.EquipmentSlots slot) {
        foreach(EquipmentSlot e in equipmentSlots) {
            if(e.slot == slot) {
                return e;
            }
        }
        EquipmentSlot newSlot = new EquipmentSlot() { slot = slot };
        equipmentSlots.Add(newSlot);
        return newSlot;
    }

    private Item LoadItem(int itemId) {
        return LoadItem(itemId, ItemType.Equipment);
    }

    private Item LoadItem(int itemId, ItemType itemType) {
        if (itemId == 0 || referenceMesh == null) {
            return null;
        }

        try {
            string resourcePath = "CustomizableCharacters/" + itemType.ToString() + "/" + itemId;
            object loadedObject = Resources.Load(resourcePath, typeof(GameObject));
            
            if (loadedObject == null) {
                Debug.LogError($"[{gameObject.name}] Failed to load item {itemId} from path: {resourcePath}");
                return null;
            }

            GameObject loadedItem = Instantiate((GameObject)loadedObject);
            if (loadedItem == null) {
                Debug.LogError($"[{gameObject.name}] Failed to instantiate item {itemId}");
                return null;
            }

            Item item = loadedItem.GetComponent<Item>();
            
            if (item == null) {
                Debug.LogError($"Item {itemId} prefab does not have an Item component!");
                GameObject.Destroy(loadedItem);
                return null;
            }

            EquipmentSlot slot = GetEquipmentSlot(item.equipmentSlot);
            GameObject itemObject = null;
            
            item.male.SetActive(true);  // Always use male model since we're removing female
            item.female.SetActive(false);
            itemObject = item.male;     // Always use male model

            if (item.skinned && itemObject != null) {
                var renderer = itemObject.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null && referenceMesh != null && referenceMesh.bones != null) {
                    renderer.bones = referenceMesh.bones;
                    renderer.updateWhenOffscreen = true;
                    item.transform.SetParent(transform);
                }
            } else if (slot?.container != null) {
                item.transform.SetParent(slot.container);
            } else {
                item.transform.SetParent(transform);
            }

            loadedItem.transform.localPosition = Vector3.zero;
            loadedItem.transform.localScale = Vector3.one;
            loadedItem.transform.localRotation = Quaternion.identity;

            if (slot != null) {
                if (slot.inUse) {
                    GameObject.Destroy(slot.instancedObject);
                }
                slot.item = item;
                slot.itemId = itemId;
                slot.inUse = true;
                slot.instancedObject = loadedItem;
                slot.activeObject = itemObject;
            }

            SetItemColor(itemObject);
            return item;
        }
        catch (System.Exception e) {
            Debug.LogError($"[{gameObject.name}] Error loading item {itemId}: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }

    void SetupSlots() {
        equipmentSlots.Clear(); // Clear existing slots before adding new ones
        
        void AddSlot(Item.EquipmentSlots slotType, string containerPath = null) {
            var slot = new EquipmentSlot { slot = slotType };
            if (!string.IsNullOrEmpty(containerPath)) {
                slot.container = transform.FindDeepChild(containerPath);
            }
            equipmentSlots.Add(slot);
        }

        AddSlot(Item.EquipmentSlots.body);
        AddSlot(Item.EquipmentSlots.feet);
        AddSlot(Item.EquipmentSlots.hair, "EQ_Head");
        AddSlot(Item.EquipmentSlots.head, "EQ_Head");
        AddSlot(Item.EquipmentSlots.legs);
        AddSlot(Item.EquipmentSlots.mainhand, "EQ_MainHand");
        AddSlot(Item.EquipmentSlots.offhand, "EQ_OffHand");
        AddSlot(Item.EquipmentSlots.hands);
        AddSlot(Item.EquipmentSlots.beard);
        AddSlot(Item.EquipmentSlots.eyebrow);
        AddSlot(Item.EquipmentSlots.neck);
        AddSlot(Item.EquipmentSlots.accessory);
        AddSlot(Item.EquipmentSlots.back, "EQ_Back");
    }

    void SetItemColor(GameObject itemObject) {
        if (itemObject == null) return;
        
        ColorChanger colorChanger = itemObject.GetComponent<ColorChanger>();
        if (colorChanger == null || colorChanger.changers == null) return;

        foreach (ColorChanger.Changer c in colorChanger.changers) {
            if (c == null || c.rend == null || c.materialIndex < 0 || 
                c.materialIndex >= c.rend.materials.Length) continue;

            Material m = c.rend.materials[c.materialIndex];
            if (m == null) continue;

            switch (c.type) {
                case ColorChanger.Type.Skin:
                    m.color = skinColor;
                    break;
                case ColorChanger.Type.Hair:
                    m.color = hairColor;
                    break;
                case ColorChanger.Type.Mouth:
                    m.color = mouthColor;
                    break;
                case ColorChanger.Type.Eyes:
                    m.color = eyeColor;
                    break;
            }
        }
    }

    void OnDestroy() {
        // Clean up resources
        ClearBody();
        foreach (var slot in equipmentSlots) {
            if (slot.instancedObject != null) {
                Destroy(slot.instancedObject);
            }
        }
        equipmentSlots.Clear();
    }
}