using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class ViewQuadFillGraphic : MaskableGraphic, ICanvasRaycastFilter
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

        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!_hasPoints)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out Vector2 localPoint))
            {
                return false;
            }

            Vector2[] polygon = new Vector2[_points.Length];
            for (int i = 0; i < _points.Length; ++i)
            {
                Vector2 point = _points[i];
                polygon[i] = new Vector2(point.x, -point.y);
            }

            if (IsPointOnEdge(localPoint, polygon))
            {
                return true;
            }

            return PointInPolygon(localPoint, polygon);
        }

        private bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;

            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];

                bool intersects = ((pi.y > point.y) != (pj.y > point.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x);

                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private bool IsPointOnEdge(Vector2 point, Vector2[] polygon)
        {
            const float tolerance = 0.001f;

            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if (IsPointOnSegment(point, polygon[j], polygon[i], tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointOnSegment(Vector2 point, Vector2 start, Vector2 end, float tolerance)
        {
            Vector2 segment = end - start;
            Vector2 toPoint = point - start;

            float segmentLengthSquared = segment.sqrMagnitude;
            if (segmentLengthSquared <= Mathf.Epsilon)
            {
                return toPoint.sqrMagnitude <= tolerance * tolerance;
            }

            float projection = Vector2.Dot(toPoint, segment) / segmentLengthSquared;
            if (projection < 0f || projection > 1f)
            {
                return false;
            }

            Vector2 projected = start + projection * segment;
            float distanceSquared = (point - projected).sqrMagnitude;

            return distanceSquared <= tolerance * tolerance;
        }
    }
}
