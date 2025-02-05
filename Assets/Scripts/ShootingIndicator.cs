using UnityEngine;

public class ShootingIndicator : MonoBehaviour
{
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2f;
    [SerializeField] private Color minColor = Color.green;
    [SerializeField] private Color maxColor = Color.red;
    
    private Transform arrowTransform;
    private Material arrowMaterial;

    void Start()
    {
        // Assuming the arrow is a child object with a renderer
        arrowTransform = transform.GetChild(0);
        arrowMaterial = arrowTransform.GetComponent<Renderer>().material;
        gameObject.SetActive(false);
    }

    public void UpdatePower(float percentage)
    {
        float scale = Mathf.Lerp(minScale, maxScale, percentage);
        arrowTransform.localScale = new Vector3(scale, scale, scale);
        arrowMaterial.color = Color.Lerp(minColor, maxColor, percentage);
    }
}
