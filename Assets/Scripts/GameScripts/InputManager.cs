using UnityEngine;

// DEPRECATED: This InputManager is no longer used and should be deleted.
// Input handling has been moved to:
// - PlayerMovement.cs (movement and sprint input)
// - PuckPickup.cs (pickup input with E key)
// - PlayerShooting.cs (shooting input with mouse buttons)
//
// Please delete this file from your project.

[System.Obsolete("InputManager is deprecated. Input handling is now done in individual component scripts.")]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private void Awake()
    {
        Debug.LogWarning("InputManager is deprecated and should be deleted. Input handling is now done in PlayerMovement, PuckPickup, and PlayerShooting scripts.");

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // All methods marked as obsolete to encourage deletion
    [System.Obsolete("Use PlayerMovement script for movement input")]
    public Vector2 GetMovementInput() => Vector2.zero;

    [System.Obsolete("Use PlayerMovement script for sprint input")]
    public bool GetSprintInput() => false;

    [System.Obsolete("Use PlayerShooting script for shoot input")]
    public bool GetShootInputDown() => false;

    [System.Obsolete("Use PuckPickup script for pickup input")]
    public bool GetPickupInputDown() => false;
}
