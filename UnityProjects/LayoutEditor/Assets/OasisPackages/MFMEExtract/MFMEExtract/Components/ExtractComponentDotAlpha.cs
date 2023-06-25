using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentDotAlpha : ExtractComponentBase
    {
        public int Number;
        public int XSize;
        public int YSize;
        public int DotSpacing;
        public ColorJSON OnColor;
        public ColorJSON OffColor;
        public ColorJSON BackgroundColor;

        public ExtractComponentDotAlpha(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }

    }

}
