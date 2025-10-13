using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class ViewQuadFillGraphic : MaskableGraphic
    {
        private readonly Vector2[] _points = new Vector2[4];
        private bool _hasPoints = false;

        public void SetPoints(Vector2[] points)
        {
            if (points == null || points.Length < _points.Length)
            {
                _hasPoints = false;
                SetVerticesDirty();
                return;
            }

            for (int i = 0; i < _points.Length; ++i)
            {
                _points[i] = points[i];
            }

            _hasPoints = true;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (!_hasPoints)
            {
                return;
            }

            Vector2[] localPoints = new Vector2[_points.Length];
            for (int i = 0; i < _points.Length; ++i)
            {
                Vector2 point = _points[i];
                localPoints[i] = new Vector2(point.x, -point.y);
            }

            vh.AddVert(localPoints[0], color, Vector2.zero);
            vh.AddVert(localPoints[1], color, Vector2.zero);
            vh.AddVert(localPoints[2], color, Vector2.zero);
            vh.AddVert(localPoints[3], color, Vector2.zero);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
