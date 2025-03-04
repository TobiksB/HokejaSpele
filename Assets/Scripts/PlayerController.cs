using UnityEngine;
using MainGame;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public HockeyStickController stickController;
    public Camera playerCamera;

    private void Awake()
    {
        Debug.Log("PlayerController: Setting up player dependencies");
        
        // Find and set up camera
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                Debug.LogError("Player camera not found in children. Please add a camera to the player prefab.");
            }
        }
        
        // Find and set up stick controller
        if (stickController == null)
        {
            stickController = GetComponentInChildren<HockeyStickController>();
            if (stickController == null)
            {
                Debug.LogError("Hockey stick not found in children. Please add a hockey stick to the player prefab.");
            }
            else
            {
                stickController.playerTransform = transform;
                
                // Try to set hockey player reference
                MainGame.HockeyPlayer hockeyPlayer = GetComponent<MainGame.HockeyPlayer>();
                if (hockeyPlayer != null)
                {
                    stickController.player = hockeyPlayer;
                }
            }
        }
    }
    
    private void Start()
    {
        // Lock cursor for first-person control
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Print debug information about mouse input
        Debug.Log("Player controller initialized. Use mouse to control the hockey stick.");
    }
}
