using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentAceMatrix : ExtractComponentBase
    {
		public int DotSize;
		public bool Flip180;
		public bool Vertical;
        public ColorJSON OnColour;
        public ColorJSON OffColour;
        public ColorJSON BackgroundColour;

        public ExtractComponentAceMatrix(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
