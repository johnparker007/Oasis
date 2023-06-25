using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentLabel : ExtractComponentBase
    {
        public string LampNumberAsText;
        public bool Transparent;
        public ColorJSON TextColor;
        public ColorJSON BackgroundColor;

        public ExtractComponentLabel(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
