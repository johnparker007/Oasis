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
        public int ZOrder; 

        public ExtractComponentBase(ComponentStandardData componentStandardData)
        {
            Position = new Vector2IntJSON(componentStandardData.Position);
            Size = new Vector2IntJSON(componentStandardData.Size);
            AngleAsText = componentStandardData.AngleAsText;
            TextBoxText = componentStandardData.TextBoxText;
            ZOrder = componentStandardData.ZOrder;
        }
    }

}
