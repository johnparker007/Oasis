using MfmeTools.UnityStructWrappers;
using System;

namespace MfmeTools.JsonDataStructures
{
    [Serializable]
    public struct ColorJSON
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public ColorJSON(Color color)
        {
            R = color.r;
            G = color.g;
            B = color.b;
            A = color.a;
        }

        public Color ToColor()
        {
            Color color;
            color.r = R;
            color.g = G;
            color.b = B;
            color.a = A;

            return color;
        }
    }
}

