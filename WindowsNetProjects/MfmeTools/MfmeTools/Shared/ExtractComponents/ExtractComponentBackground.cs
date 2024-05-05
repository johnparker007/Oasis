using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
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
