using UnityEngine;

public class PlayerUpright : MonoBehaviour
{
    public float uprightForce = 10f; // Force to keep the player upright
    public float uprightSpeed = 2f; // Speed of upright adjustment

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        MaintainUpright();
    }

    void MaintainUpright()
    {
        // Calculate the target rotation (upright)
        Quaternion targetRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);

        // Smoothly rotate the player towards the upright position
        Quaternion currentRotation = transform.rotation;
        Quaternion smoothedRotation = Quaternion.Slerp(currentRotation, targetRotation, uprightSpeed * Time.fixedDeltaTime);

        // Apply torque to correct the orientation
        rb.MoveRotation(smoothedRotation);
    }
}
