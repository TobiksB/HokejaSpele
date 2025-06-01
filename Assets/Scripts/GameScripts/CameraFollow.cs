using UnityEngine;
using Unity.Netcode;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform target;
    [SerializeField] public Vector3 offset = new Vector3(0, 3, 0); // Camera offset from player
    [SerializeField] public bool lookAtTarget = true;
    [SerializeField] private bool constrainToRink = true;
    [SerializeField] private Vector2 rinkBounds = new Vector2(20f, 10f);

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Calculate offset relative to the player's rotation (so camera is always behind the player)
        Vector3 desiredPosition = target.position + target.rotation * offset;

        // Raycast to prevent camera from passing through walls
        Vector3 direction = (desiredPosition - target.position).normalized;
        float distance = (desiredPosition - target.position).magnitude;
        RaycastHit hit;
        int wallLayerMask = LayerMask.GetMask("Wall"); // Make sure your wall objects are on the "Wall" layer

        if (Physics.Raycast(target.position, direction, out hit, distance, wallLayerMask))
        {
            desiredPosition = hit.point - direction * 0.2f;
        }

        if (constrainToRink)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, -rinkBounds.x, rinkBounds.x);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, -rinkBounds.y, rinkBounds.y);
        }

        transform.position = desiredPosition;

        if (lookAtTarget)
        {
            Vector3 lookDirection = target.position - transform.position;
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            // Use rotation-relative offset for instant snap behind player
            transform.position = target.position + target.rotation * offset;
            Vector3 lookDirection = target.position - transform.position;
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }

    public void SetLookAtEnabled(bool enabled)
    {
        lookAtTarget = enabled;
    }

    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }

    public Transform GetTarget()
    {
        return target;
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        Vector3 lookDirection = target.position - transform.position;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
}