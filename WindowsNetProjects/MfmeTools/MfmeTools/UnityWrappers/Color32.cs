namespace Oasis.MfmeTools.UnityWrappers
{
    public struct Color32
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color32(System.Drawing.Color color)
        {
            r = color.R;
            g = color.G;
            b = color.B;
            a = color.A;
        }
    }
}

