using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
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
