using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 5f;
    [SerializeField] private float height = 2f;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private LayerMask wallMask; // Set this to include Wall layer in inspector
    [SerializeField] private float minDistance = 2f; // Minimum distance from player
    [SerializeField] private float collisionOffset = 0.2f; // Distance to keep from walls

    private float currentDistance;

    private void Start()
    {
        currentDistance = distance;
        wallMask = LayerMask.GetMask("Wall"); // Make sure to set up Wall layer in Unity
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Calculate desired camera position
        Vector3 targetPosition = target.position + Vector3.up * height;
        Vector3 directionToCamera = -target.forward;

        // Cast a ray to check for walls
        RaycastHit hit;
        if (Physics.Raycast(targetPosition, directionToCamera, out hit, distance, wallMask))
        {
            // If we hit a wall, position camera at hit point plus offset
            currentDistance = hit.distance - collisionOffset;
        }
        else
        {
            // No wall, use normal distance
            currentDistance = distance;
        }

        // Clamp minimum distance
        currentDistance = Mathf.Max(currentDistance, minDistance);

        // Calculate final camera position
        Vector3 desiredPosition = targetPosition + directionToCamera * currentDistance;
        
        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Look at target
        transform.LookAt(target.position + Vector3.up * 0.5f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            // Initial positioning
            Vector3 targetPosition = target.position + Vector3.up * height - target.forward * currentDistance;
            transform.position = targetPosition;
            transform.LookAt(target.position + Vector3.up * 0.5f);
        }
    }
}
