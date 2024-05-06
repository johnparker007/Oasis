using Oasis.MfmeTools.Shared.UnityWrappers;
using System;

namespace Oasis.MfmeTools.Shared.JsonDataStructures
{
    [Serializable]
    public struct Vector2IntJSON
    {
        public int X;
        public int Y;

        public Vector2IntJSON(Vector2Int vector2Int)
        {
            X = vector2Int.x;
            Y = vector2Int.y;
        }

        public Vector2Int ToVector2Int()
        {
            Vector2Int vector2Int = Vector2Int.zero;
            vector2Int.x = X;
            vector2Int.y = Y;

            return vector2Int;
        }
    }
}

