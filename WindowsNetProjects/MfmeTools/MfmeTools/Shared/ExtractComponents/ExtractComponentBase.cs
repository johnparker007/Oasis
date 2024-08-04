using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;

namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public abstract class ExtractComponentBase
    {
        public Vector2IntJSON Position;
        public Vector2IntJSON Size;
        public string AngleAsText;

        public string TextBoxText;
        public string TextBoxFontName;
        public string TextBoxFontStyle;
        public string TextBoxFontSize;

        public int ZOrder;

        public ExtractComponentBase(ExtractComponentBase sourceExtractComponent)
        {
            Position = sourceExtractComponent.Position;
            Size = sourceExtractComponent.Size;
            AngleAsText = sourceExtractComponent.AngleAsText;

            TextBoxText = sourceExtractComponent.TextBoxText;
            TextBoxFontName = sourceExtractComponent.TextBoxFontName;
            TextBoxFontStyle = sourceExtractComponent.TextBoxFontStyle;
            TextBoxFontSize = sourceExtractComponent.TextBoxFontSize;

            ZOrder = sourceExtractComponent.ZOrder;
        }

        public ExtractComponentBase(ComponentStandardData componentStandardData)
        {
            Position = new Vector2IntJSON(componentStandardData.Position);
            Size = new Vector2IntJSON(componentStandardData.Size);
            AngleAsText = componentStandardData.AngleAsText;

            TextBoxText = componentStandardData.TextBoxText;
            TextBoxFontName = componentStandardData.TextBoxFontName;
            TextBoxFontStyle = componentStandardData.TextBoxFontStyle;
            TextBoxFontSize = componentStandardData.TextBoxFontSize;

            ZOrder = componentStandardData.ZOrder;
        }

    }

}
