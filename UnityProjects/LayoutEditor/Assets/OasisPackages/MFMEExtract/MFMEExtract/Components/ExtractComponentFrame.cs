using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentFrame : ExtractComponentBase
    {
        public string ShapeAsString;
        public string BevelAsString;

        public ExtractComponentFrame(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
