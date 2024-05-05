using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBase 
    {
        public Vector2IntJSON Position;
        public Vector2IntJSON Size;
        public string AngleAsText;
        public string TextBoxText;
        public int ZOrder; 

        public ExtractComponentBase(MfmeExtractor.ComponentStandardData componentStandardData)
        {
            Position = new Vector2IntJSON(componentStandardData.Position);
            Size = new Vector2IntJSON(componentStandardData.Size);
            AngleAsText = componentStandardData.AngleAsText;
            TextBoxText = componentStandardData.TextBoxText;
            ZOrder = componentStandardData.ZOrder;
        }
    }

}
