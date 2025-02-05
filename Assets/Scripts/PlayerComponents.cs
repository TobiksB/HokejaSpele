using UnityEngine;

[RequireComponent(typeof(CharacterBase))]
[RequireComponent(typeof(HockeyPlayer))]
public class PlayerComponents : MonoBehaviour
{
    private void Awake()
    {
        // Verify required components exist
        if (GetComponent<CharacterBase>() == null || GetComponent<HockeyPlayer>() == null)
        {
            Debug.LogError($"[{gameObject.name}] Missing required components!");
        }
    }
}
