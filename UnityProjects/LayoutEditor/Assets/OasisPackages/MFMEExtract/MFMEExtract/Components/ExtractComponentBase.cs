using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentBase 
    {
        public Vector2IntJSON Position;
        public Vector2IntJSON Size;
        public string AngleAsText;
        public string TextBoxText;
        public int ZOrder; 

        public ExtractComponentBase(MFMEExtractor.ComponentStandardData componentStandardData)
        {
            Position = new Vector2IntJSON(componentStandardData.Position);
            Size = new Vector2IntJSON(componentStandardData.Size);
            AngleAsText = componentStandardData.AngleAsText;
            TextBoxText = componentStandardData.TextBoxText;
            ZOrder = componentStandardData.ZOrder;
        }
    }

}
