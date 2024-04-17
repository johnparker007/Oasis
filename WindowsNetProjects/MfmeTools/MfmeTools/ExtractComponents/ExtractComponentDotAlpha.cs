using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
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

        public ExtractComponentDotAlpha(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }

    }

}
