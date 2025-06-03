using UnityEngine;
using Unity.Netcode;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.1f, -0.8f); // Extremely close third-person
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Look Settings")]
    [SerializeField] private bool lookAtTarget = true;
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.2f, 0f); // Look at upper body

    [Header("Smoothing")]
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private bool smoothRotation = true;

    [Header("Free Look")]
    [SerializeField] private KeyCode freeLookKey = KeyCode.LeftAlt;
    [SerializeField] private float mouseSensitivity = 80f;

    private bool isFreeLook = false;
    private float yaw = 0f;
    private float pitch = 10f; // Slight downward angle

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }
        cam.fieldOfView = 50f; // Even narrower FOV for a very close-up feel
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
    }

    private void Update()
    {
        // Toggle free look mode on key press
        if (Input.GetKeyDown(freeLookKey))
        {
            isFreeLook = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // Initialize yaw/pitch from current rotation RELATIVE TO TARGET
            if (target != null)
            {
                Vector3 camDir = (transform.position - target.position).normalized;
                // Calculate yaw and pitch from camera's direction relative to target
                yaw = Mathf.Atan2(camDir.x, camDir.z) * Mathf.Rad2Deg;
                pitch = Mathf.Asin(camDir.y) * Mathf.Rad2Deg;
            }
            else
            {
                Vector3 angles = transform.eulerAngles;
                yaw = angles.y;
                pitch = angles.x;
            }
        }
        else if (Input.GetKeyUp(freeLookKey))
        {
            isFreeLook = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (isFreeLook)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            yaw += mouseX * mouseSensitivity * Time.deltaTime;
            pitch -= mouseY * mouseSensitivity * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -80f, 80f); // Allow looking almost fully up/down
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition;
        if (isFreeLook)
        {
            // Free look: orbit camera around the player at the offset distance, but keep offset length
            float offsetDistance = offset.magnitude;
            Quaternion freeLookRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 camOffset = freeLookRot * (Vector3.back * offsetDistance);
            desiredPosition = target.position + camOffset + Vector3.up * offset.y;

            if (smoothFollow)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
            }
            else
            {
                transform.position = desiredPosition;
            }

            transform.rotation = Quaternion.LookRotation(target.position + lookOffset - transform.position, Vector3.up);
        }
        else
        {
            // Calculate desired position
            desiredPosition = target.position + target.TransformDirection(offset);

            // Move camera
            if (smoothFollow)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
            }
            else
            {
                transform.position = desiredPosition;
            }

            if (lookAtTarget)
            {
                Vector3 lookTarget = target.position + lookOffset;
                Vector3 direction = (lookTarget - transform.position).normalized;

                if (direction != Vector3.zero)
                {
                    Quaternion desiredRotation = Quaternion.LookRotation(direction);

                    if (smoothRotation)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
                    }
                    else
                    {
                        transform.rotation = desiredRotation;
                    }
                }
            }
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
}