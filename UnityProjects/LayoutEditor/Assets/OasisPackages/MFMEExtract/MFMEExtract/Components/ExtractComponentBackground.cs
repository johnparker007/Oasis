using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentBackground : ExtractComponentBase
    {
        public string BmpImageFilename;

        public ExtractComponentBackground(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }

    }

}
