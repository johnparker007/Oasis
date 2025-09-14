using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    /// <summary>
    /// Simple graphic that renders a line along the top edge of a rect transform.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class TabHighlight : BaseMeshEffect
    {
        [SerializeField]
        private float _thickness = 2f;

        [SerializeField]
        private Color _color = Color.white;

        /// <summary>
        /// Thickness of the top highlight line in pixels.
        /// </summary>
        public float Thickness
        {
            get => _thickness;
            set
            {
                _thickness = value;
                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }
            }
        }

        /// <summary>
        /// Color of the highlight line.
        /// </summary>
        public Color color
        {
            get => _color;
            set
            {
                _color = value;
                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        protected override void OnDisable()
        {
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
            base.OnDisable();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }

            Rect rect = graphic.rectTransform.rect;
            float top = rect.yMax;
            float bottom = top - _thickness;

            int startIndex = vh.currentVertCount;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = _color;
            vert.uv0 = Vector2.zero;

            vert.position = new Vector3(rect.xMin, top);
            vh.AddVert(vert);
            vert.position = new Vector3(rect.xMax, top);
            vh.AddVert(vert);
            vert.position = new Vector3(rect.xMax, bottom);
            vh.AddVert(vert);
            vert.position = new Vector3(rect.xMin, bottom);
            vh.AddVert(vert);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }
}

