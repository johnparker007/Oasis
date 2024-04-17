using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentEpochMatrix : ExtractComponentBase
    {
		public int DotSize;
        public ColorJSON OffColour;
        public ColorJSON OnColourLo;
        public ColorJSON OnColourMed;
        public ColorJSON OnColourHi;
        public ColorJSON BackgroundColour;

        public ExtractComponentEpochMatrix(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
