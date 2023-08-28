using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentAlpha : ExtractComponentBase
    {
        public int Number;
        public bool Reversed;
        public ColorJSON Color;
        public int DigitWidth;
        public int Columns;
        public string BmpImageFilename;

        public ExtractComponentAlpha(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
