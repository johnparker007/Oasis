using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentLabel : ExtractComponentBase
    {
        public string LampNumberAsText;
        public bool Transparent;
        public ColorJSON TextColor;
        public ColorJSON BackgroundColor;

        public ExtractComponentLabel(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
