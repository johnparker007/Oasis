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
            BottomLeft,
            BottomRight
        }

        public Vector2[] Points = new Vector2[Enum.GetValues(typeof(PointTypes)).Length];
    }
}
