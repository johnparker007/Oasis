using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
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
