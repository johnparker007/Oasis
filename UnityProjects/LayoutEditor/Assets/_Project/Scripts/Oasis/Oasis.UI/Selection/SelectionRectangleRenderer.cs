using UnityEngine;
using UnityEngine.UI;

namespace Oasis.UI.Selection
{
    using UnityEngine;
    using UnityEngine.UI;

    public class SelectionRectangleRenderer : MonoBehaviour
    {
        private const int kTextureSize = 3;
        private const float kPixelsPerUnit = 100;

        [Tooltip("RectTransform of the fill Image.")]
        [SerializeField] private RectTransform _fillRect;

        [Tooltip("RectTransform of the border Image.")]
        [SerializeField] private RectTransform _borderRect;

        private Image _borderImage;
        private SelectionManager _selectionManager = null;

        private void Awake()
        {
            _selectionManager = GetComponentInParent<SelectionManager>();

            Hide();

            _borderImage = _borderRect.GetComponent<Image>();

            if (_borderImage == null)
            {
                Debug.LogError("SelectionRectangleRenderer: Border RectTransform does not have an Image component.");
                return;
            }

            Sprite borderSprite = CreateBorderSprite();
            _borderImage.sprite = borderSprite;
            _borderImage.type = Image.Type.Sliced;
            _borderImage.fillCenter = false;
            _borderImage.color = Color.white;
        }

        private Sprite CreateBorderSprite()
        {
            Texture2D texture = new Texture2D(kTextureSize, kTextureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color transparent = new Color(0, 0, 0, 0);
            Color white = Color.white;

            for (int y = 0; y < kTextureSize; ++y)
            {
                for (int x = 0; x < kTextureSize; ++x)
                {
                    texture.SetPixel(x, y, transparent);
                }
            }

            for (int x = 0; x < kTextureSize; ++x)
            {
                texture.SetPixel(x, 0, white);
                texture.SetPixel(x, kTextureSize - 1, white);
            }

            for (int y = 0; y < kTextureSize; ++y)
            {
                texture.SetPixel(0, y, white);
                texture.SetPixel(kTextureSize - 1, y, white);
            }

            texture.Apply();

            Vector4 spriteBorder = new Vector4(1, 1, 1, 1);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, kTextureSize, kTextureSize),
                new Vector2(0.5f, 0.5f),
                kPixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                spriteBorder
            );

            return sprite;
        }

        public void Show(Vector2 start, Vector2 end)
        {
            gameObject.SetActive(true);
            UpdateSelection(start, end);
        }

        public void UpdateSelection(Vector2 start, Vector2 end)
        {
            Debug.LogError($"UpdateSelection: {start} -> {end}");


            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end);

            RectTransform canvasRect = _selectionManager.Canvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, min, _selectionManager.Canvas.worldCamera, out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, max, _selectionManager.Canvas.worldCamera, out Vector2 localMax);

            Vector2 size = localMax - localMin;
            Vector2 topLeft = localMin;

            _fillRect.anchoredPosition = _borderRect.anchoredPosition = topLeft;
            _fillRect.sizeDelta = _borderRect.sizeDelta = size;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }

}