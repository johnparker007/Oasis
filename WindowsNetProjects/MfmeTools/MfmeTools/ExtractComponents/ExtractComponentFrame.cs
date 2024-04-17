using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentFrame : ExtractComponentBase
    {
        public string ShapeAsString;
        public string BevelAsString;

        public ExtractComponentFrame(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
