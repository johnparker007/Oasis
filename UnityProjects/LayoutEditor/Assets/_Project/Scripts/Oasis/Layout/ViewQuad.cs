using Oasis.Uility;
using System;
using UnityEngine;

namespace Oasis.Layout
{
    public class ViewQuad
    {
        public enum PointTypes
        {
            TopLeft,
            TopRight,
            BottomRight,
            BottomLeft
        }

        public string Name = string.Empty;

        public Vector2[] Points = new Vector2[Enum.GetValues(typeof(PointTypes)).Length];

        public bool ContainsPoint(Vector2 point)
        {
            return PolygonHelper.IsPointInPolygon(point, Points);
        }

        public bool ContainsAllPoints(params Vector2[] points)
        {
            foreach(Vector2 point in points)
            {
                if(!ContainsPoint(point))
                {
                    return false;
                }
            }

            return true;
        }

        public bool ContainsAnyPoint(params Vector2[] points)
        {
            foreach (Vector2 point in points)
            {
                if (ContainsPoint(point))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
