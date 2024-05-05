using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
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
