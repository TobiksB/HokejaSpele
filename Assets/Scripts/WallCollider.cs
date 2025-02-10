using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class WallCollider : MonoBehaviour
{
    private void Awake()
    {
        BoxCollider wallCollider = GetComponent<BoxCollider>();
        wallCollider.isTrigger = false;

        // Create and assign wall physics material
        PhysicsMaterial wallMaterial = new PhysicsMaterial("WallMaterial");
        wallMaterial.dynamicFriction = 0.1f;
        wallMaterial.staticFriction = 0.1f;
        wallMaterial.bounciness = 0.5f;
        wallMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        wallMaterial.bounceCombine = PhysicsMaterialCombine.Average;
        
        wallCollider.material = wallMaterial;
        
        // Ensure wall has the correct tag
        gameObject.tag = "Wall";
    }
}
