using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 2.5f, -4f);
    [SerializeField] private float smoothSpeed = 10f;
    
    private Transform target;
    private Vector3 velocity = Vector3.zero;

    void Start()
    {
        // Set initial rotation and keep it fixed
        transform.rotation = Quaternion.Euler(0f, 0f, 0f); // 15 degrees down-tilt
        StartCoroutine(FindPlayer());
    }

    private System.Collections.IEnumerator FindPlayer()
    {
        while (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                Debug.Log("Camera found player target");
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Only follow player position with fixed offset
        Vector3 desiredPosition = target.position + cameraOffset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, Time.deltaTime * smoothSpeed);
    }
}
