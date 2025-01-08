using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target; // The object the camera will follow (e.g., puck or player)
    public Vector3 offset = new Vector3(0, 10, -10); // Camera offset
    public float followSpeed = 5f; // Speed of camera movement
    public float rotationSpeed = 5f; // Speed of camera rotation

    void LateUpdate()
    {
        if (target != null)
        {
            // Smoothly move the camera to the target's position with an offset
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

            // Optionally rotate the camera to face the target
            Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position);
            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
