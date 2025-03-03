using System.Collections.Generic;
using UnityEngine;

namespace MainGame
{
    public class HockeyPlayer : MonoBehaviour
    {
        public int number;
        [Header("List of equipped items:")]
        public List<int> items = new List<int>();

        public bool IsInitialized { get; private set; }

        protected void Awake()
        {
            IsInitialized = true;
        }

        protected void Start()
        {
            if (IsInitialized)
            {
                // Apply saved customization first
                ApplyCustomization();

                // Then apply equipment
                foreach (int itemId in items)
                {
                    EquipItem(itemId);
                }
            }
            else
            {
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

        private void EquipItem(int itemId)
        {
            // TODO: Implement equipment logic here
            Debug.Log($"Equipping item with ID: {itemId}");
        }

        public void ChangeSkinColor(Color color)
        {
            // TODO: Implement skin color change logic
            Debug.Log($"Changing skin color to: {color}");
        }

        public void ChangeHairstyle(int style)
        {
            // TODO: Implement hairstyle change logic
            Debug.Log($"Changing hairstyle to: {style}");
        }

        public void ChangeBeardstyle(int style)
        {
            // TODO: Implement beardstyle change logic
            Debug.Log($"Changing beardstyle to: {style}");
        }

        public void ChangeEyebrowstyle(int style)
        {
            // TODO: Implement eyebrow style change logic
            Debug.Log($"Changing eyebrow style to: {style}");
        }
    }
}
