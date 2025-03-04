using UnityEngine;

namespace MainGame
{
    public class RinkSetup : MonoBehaviour
    {
        [Header("Ice Surface Setup")]
        public GameObject iceSurface;
        public bool autoConfigureIce = true;
        public float iceThickness = 0.05f;
        
        [Header("Ice Physics")]
        public float iceFriction = 0.01f;
        
        private void Start()
        {
            // Try to find ice surface if not assigned
            if (iceSurface == null)
            {
                // Look for an object on the Ice layer
                GameObject[] allObjects = FindObjectsOfType<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj.layer == LayerMask.NameToLayer("Ice"))
                    {
                        iceSurface = obj;
                        break;
                    }
                }
            }
            
            if (iceSurface != null && autoConfigureIce)
            {
                ConfigureIceSurface();
            }
        }
        
        public void ConfigureIceSurface()
        {
            if (iceSurface == null)
            {
                Debug.LogWarning("No ice surface assigned to configure!");
                return;
            }
            
            // Make sure ice has a collider
            Collider iceCollider = iceSurface.GetComponent<Collider>();
            if (iceCollider == null)
            {
                // Try to add a box collider
                BoxCollider boxCol = iceSurface.AddComponent<BoxCollider>();
                boxCol.center = new Vector3(0, -iceThickness/2, 0);
                boxCol.size = new Vector3(
                    iceSurface.transform.localScale.x, 
                    iceThickness, 
                    iceSurface.transform.localScale.z);
                
                iceCollider = boxCol;
            }
            
            // Set layer to Ice
            int iceLayer = LayerMask.NameToLayer("Ice");
            if (iceLayer >= 0)
            {
                iceSurface.layer = iceLayer;
                Debug.Log($"Set {iceSurface.name} to Ice layer");
            }
            else
            {
                Debug.LogError("Ice layer does not exist! Please create this layer in your project.");
            }
            
            // Apply a slippery physics material
            PhysicsMaterial iceMaterial = new PhysicsMaterial("IceMaterial");
            iceMaterial.dynamicFriction = iceFriction;
            iceMaterial.staticFriction = iceFriction;
            iceMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
            iceMaterial.bounciness = 0.1f;
            
            if (iceCollider != null)
            {
                iceCollider.material = iceMaterial;
                iceCollider.isTrigger = false;
                
                Debug.Log($"Applied slippery physics material to {iceSurface.name}");
            }
        }
    }
}
