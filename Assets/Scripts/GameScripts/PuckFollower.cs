using UnityEngine;

public class PuckFollower : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = false;
    [SerializeField] private bool enableDebugLogs = false;
    
    private bool isFollowing = false;
    private Rigidbody puckRigidbody;
    
    private void Awake()
    {
        puckRigidbody = GetComponent<Rigidbody>();
    }
    
    private void Update()
    {
        if (isFollowing && target != null)
        {
            FollowTarget();
        }
    }
    
    private void FollowTarget()
    {
        if (followPosition)
        {
            Vector3 targetPosition = target.position + offset;
            transform.position = targetPosition;
        }
        
        if (followRotation)
        {
            transform.rotation = target.rotation;
        }
    }
    
    public void StartFollowing(Transform followTarget, Vector3 followOffset = default)
    {
        target = followTarget;
        offset = followOffset;
        isFollowing = true;
        
        // Disable physics while following
        if (puckRigidbody != null)
        {
            puckRigidbody.isKinematic = true;
            puckRigidbody.useGravity = false;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"PuckFollower: Started following {target.name} with offset {offset}");
        }
    }

    // Add this overload for convenience
    public void StartFollowing(Transform followTarget)
    {
        StartFollowing(followTarget, Vector3.zero);
    }
    
    public void StopFollowing()
    {
        isFollowing = false;
        target = null;
        
        // Re-enable physics when stopped following
        if (puckRigidbody != null)
        {
            puckRigidbody.isKinematic = false;
            puckRigidbody.useGravity = true;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log("PuckFollower: Stopped following");
        }
    }
    
    public bool IsFollowing()
    {
        return isFollowing;
    }
    
    public Transform GetTarget()
    {
        return target;
    }
}
