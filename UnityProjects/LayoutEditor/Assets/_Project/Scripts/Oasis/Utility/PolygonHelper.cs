using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oasis.Uility
{
    public static class PolygonHelper 
    {
        public static bool IsPointInPolygon(Vector2 point, List<Vector2> polygonPoints)
        {
            bool inside = false;

            float x = point.x;
            float y = point.y;

            for (int i = 0, j = polygonPoints.Count - 1; i < polygonPoints.Count; i++)
            {
                if (((polygonPoints[i].y < y && polygonPoints[j].y >= y) || (polygonPoints[j].y < y && polygonPoints[i].y >= y))
                    && (polygonPoints[i].x <= x || polygonPoints[j].x <= x))
                {
                    inside ^= (polygonPoints[i].x + (y - polygonPoints[i].y) / (polygonPoints[j].y - polygonPoints[i].y) * (polygonPoints[j].x - polygonPoints[i].x) < x);
                }

                j = i;
            }

            return inside;
        }

        public static bool IsPointInPolygon(Vector2 point, Vector2[] polygonPoints)
        {
            return IsPointInPolygon(point, polygonPoints.ToList());
        }
    }
}

