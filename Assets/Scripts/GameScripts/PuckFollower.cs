using UnityEngine;

public class PuckFollower : MonoBehaviour
{    [Header("Follow Settings")]
    [SerializeField] private float followSpeed = 20f; // Palielināts labākai atsaucībai sekošanas laikā
    [SerializeField] private float positionThreshold = 0.1f; // Pārtrauc sekošanu, kad ir pietiekami tuvu
    
    private Transform targetTransform;
    private Vector3 offsetPosition;
    private bool isFollowing = false;
      [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false; // Samazināts paziņojumu skaits
    
    private Rigidbody puckRigidbody;
    
    private void Awake()
    {
        puckRigidbody = GetComponent<Rigidbody>();
    }
    
    private void FixedUpdate()
    {
        if (isFollowing && targetTransform != null)
        {            // Aprēķina mērķa pozīciju ar nobīdi
            Vector3 targetPosition = targetTransform.position + targetTransform.TransformDirection(offsetPosition);
            
            // Pārbauda, vai esam pietiekami tuvu, lai pārtrauktu sekošanu
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance < positionThreshold)
            {
                // We're close enough, just set position directly and stop physics
                if (puckRigidbody != null)
                {
                    puckRigidbody.linearVelocity = Vector3.zero;
                    puckRigidbody.angularVelocity = Vector3.zero;
                }
                transform.position = targetPosition;
                transform.rotation = targetTransform.rotation;
                return;
            }
              // Pārvietojas uz mērķi, izmantojot transform (nevis fiziku, lai izvairītos no atlēkšanas)
            Vector3 newPosition = Vector3.MoveTowards(transform.position, targetPosition, followSpeed * Time.fixedDeltaTime);
            transform.position = newPosition;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetTransform.rotation, followSpeed * Time.fixedDeltaTime);
              // Saglabā fiziku nekustīgu sekošanas laikā
            if (puckRigidbody != null)
            {
                puckRigidbody.linearVelocity = Vector3.zero;
                puckRigidbody.angularVelocity = Vector3.zero;
                puckRigidbody.position = newPosition;
                puckRigidbody.rotation = transform.rotation;
            }
            
            if (enableDebugLogs && Time.fixedTime % 1f < Time.fixedDeltaTime) // Ieraksta žurnālā reizi sekundē
            {
                Debug.Log($"PuckFollower: Following {targetTransform.name} - Distance: {distance:F2}");
            }
        }
    }
    
    public void StartFollowing(Transform target, Vector3 offset)
    {
        if (target == null)
        {
            Debug.LogError("PuckFollower: Cannot start following - target is null!");
            return;
        }
        
        targetTransform = target;
        offsetPosition = offset;
        isFollowing = true;
        
        // Ensure physics is kinematic while following
        if (puckRigidbody != null)
        {
            puckRigidbody.isKinematic = true;
            puckRigidbody.useGravity = false;
            puckRigidbody.linearVelocity = Vector3.zero;
            puckRigidbody.angularVelocity = Vector3.zero;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"PuckFollower: Started following {target.name} with offset {offset}");
        }
    }
    
    public void StopFollowing()
    {
        isFollowing = false;
        targetTransform = null;
        
        // Re-enable physics when stopping
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
        return isFollowing && targetTransform != null;
    }
}
