using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    /// <summary>
    /// Simple graphic that renders a line along the top edge of a rect transform.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TabHighlight : MaskableGraphic
    {
        [SerializeField]
        private float _thickness = 2f;

        /// <summary>
        /// Thickness of the top highlight line in pixels.
        /// </summary>
        public float Thickness
        {
            get => _thickness;
            set
            {
                _thickness = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect();
            float top = rect.yMax;
            float bottom = top - _thickness;

            vh.AddVert(new Vector3(rect.xMin, top), color, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(rect.xMax, top), color, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(rect.xMax, bottom), color, new Vector2(1f, 0f));
            vh.AddVert(new Vector3(rect.xMin, bottom), color, new Vector2(0f, 0f));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
