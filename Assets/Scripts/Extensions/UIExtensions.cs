using UnityEngine;
using UnityEngine.UI;

public static class UIExtensions
{
    public static void SetColorWithAlpha(this Image image, Color color)
    {
        if (image != null)
        {
            color.a = image.color.a; // Preserve original alpha
            image.color = color;
        }
    }
}
