using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
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

        public ExtractComponentAlpha(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
