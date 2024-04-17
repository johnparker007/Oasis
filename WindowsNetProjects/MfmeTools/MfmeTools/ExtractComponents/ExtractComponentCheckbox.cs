using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentCheckbox : ExtractComponentBase
    {
        public int Number;
        public ColorJSON TextColor;
        public string Text;
        public bool State;

        public ExtractComponentCheckbox(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
        {
        }
    }

}
