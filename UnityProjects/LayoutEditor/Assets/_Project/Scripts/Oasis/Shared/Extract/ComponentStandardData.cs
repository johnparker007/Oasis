using Oasis.MfmeTools.Shared.UnityWrappers;

namespace Oasis.MfmeTools.Shared.Extract
{
    public struct ComponentStandardData
    {
        public Vector2Int Position;
        public Vector2Int Size;
        public string AngleAsText;

        public string TextBoxText;
        public string TextBoxFontName;
        public string TextBoxFontStyle;
        public string TextBoxFontSize;

        public int ZOrder;

        public ComponentStandardData(
            string x, string y, string width, string height, string angle, 
            string textBoxText, string textBoxFontName, string textBoxFontStyle, string textBoxFontSize,
            int zOrder)
        {
            Position = new Vector2Int(int.Parse(x), int.Parse(y));
            Size = new Vector2Int(int.Parse(width), int.Parse(height));
            AngleAsText = angle;

            TextBoxText = textBoxText;
            TextBoxFontName = textBoxFontName;
            TextBoxFontStyle = textBoxFontStyle;
            TextBoxFontSize = textBoxFontSize;

            ZOrder = zOrder;
        }
    }
}
