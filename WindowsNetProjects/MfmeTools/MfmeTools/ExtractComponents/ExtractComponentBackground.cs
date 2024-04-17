using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBackground : ExtractComponentBase
    {
        public string BmpImageFilename;

        public ExtractComponentBackground(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }

    }

}
