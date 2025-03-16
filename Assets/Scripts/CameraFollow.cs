using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 5f;
    [SerializeField] private float height = 2f;
    [SerializeField] private float smoothSpeed = 10f;

    private void Start()
    {
        // Remove the camera disable code since we want it enabled by default
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Calculate desired position behind the player
        Vector3 targetPosition = target.position - target.forward * distance + Vector3.up * height;
        
        // Smooth the camera movement
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        
        // Look at a point slightly above the player
        transform.LookAt(target.position + Vector3.up * 0.5f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        // Position camera immediately to avoid jarring movement
        if (target != null)
        {
            Vector3 targetPosition = target.position - target.forward * distance + Vector3.up * height;
            transform.position = targetPosition;
            transform.LookAt(target.position + Vector3.up * 0.5f);
        }
    }
}
