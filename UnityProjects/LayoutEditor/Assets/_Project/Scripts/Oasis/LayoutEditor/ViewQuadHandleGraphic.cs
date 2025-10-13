using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    /// <summary>
    /// Renders a simple rectangular outline for the view quad drag handles.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ViewQuadHandleGraphic : MaskableGraphic
    {
        [SerializeField]
        private float _lineWidth = 2f;

        /// <summary>
        /// Width of the outline stroke in local units.
        /// </summary>
        public float LineWidth
        {
            get => _lineWidth;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (!Mathf.Approximately(_lineWidth, clamped))
                {
                    _lineWidth = clamped;
                    SetVerticesDirty();
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float maxLineWidth = Mathf.Min(rect.width, rect.height) * 0.5f;
            float stroke = Mathf.Clamp(_lineWidth, 0f, maxLineWidth);
            if (stroke <= 0f)
            {
                return;
            }

            float xMin = rect.xMin;
            float xMax = rect.xMax;
            float yMin = rect.yMin;
            float yMax = rect.yMax;

            // Top edge
            AddQuad(vh, new Vector2(xMin, yMax - stroke), new Vector2(xMax, yMax));
            // Bottom edge
            AddQuad(vh, new Vector2(xMin, yMin), new Vector2(xMax, yMin + stroke));
            // Left edge
            AddQuad(vh, new Vector2(xMin, yMin + stroke), new Vector2(xMin + stroke, yMax - stroke));
            // Right edge
            AddQuad(vh, new Vector2(xMax - stroke, yMin + stroke), new Vector2(xMax, yMax - stroke));
        }

        private void AddQuad(VertexHelper vh, Vector2 min, Vector2 max)
        {
            int startIndex = vh.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector2(min.x, min.y);
            vh.AddVert(vertex);

            vertex.position = new Vector2(min.x, max.y);
            vh.AddVert(vertex);

            vertex.position = new Vector2(max.x, max.y);
            vh.AddVert(vertex);

            vertex.position = new Vector2(max.x, min.y);
            vh.AddVert(vertex);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }
}
