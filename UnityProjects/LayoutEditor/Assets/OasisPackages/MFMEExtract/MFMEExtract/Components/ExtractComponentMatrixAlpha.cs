using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentMatrixAlpha : ExtractComponentBase
    {
        public int Number;
        public int XSize;
        public int YSize;
        public int DotSpacing;
        public int DigitSpacing;
        public ColorJSON OnColor;
        public ColorJSON OffColor;
        public ColorJSON BackgroundColor;

        public ExtractComponentMatrixAlpha(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
