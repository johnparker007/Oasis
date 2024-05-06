namespace Oasis.MfmeTools.Shared.UnityWrappers
{
    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            a = 1f;
        }

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color(Color32 color32)
        {
            r = color32.r / 255f;
            g = color32.g / 255f;
            b = color32.b / 255f;
            a = color32.a / 255f;
        }

    }
}
