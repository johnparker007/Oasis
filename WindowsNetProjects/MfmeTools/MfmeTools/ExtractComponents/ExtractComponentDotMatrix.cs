using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentDotMatrix : ExtractComponentBase
    {
		public int DotSize;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentDotMatrix(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
