using UnityEngine;
using UnityEngine.UI;

namespace Oasis.UI
{
    public class ImageRaycastTransparency : Image
    {
        // Override the default raycast method to ignore fully transparent pixels
        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            // Get the rectTransform position relative to the screen
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint);

            // Normalize local point to get texture coordinates
            Rect rect = rectTransform.rect;
            float normalizedX = (localPoint.x - rect.x) / rect.width;
            float normalizedY = (localPoint.y - rect.y) / rect.height;

            // Check if the texture is within the bounds
            if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1)
                return false;

            // Get the pixel color at the point and return false if it's fully transparent
            Color color = sprite.texture.GetPixelBilinear(normalizedX, normalizedY);
            return color.a > 0.1f; // Set an alpha threshold, like 0.1f
        }
    }

}
