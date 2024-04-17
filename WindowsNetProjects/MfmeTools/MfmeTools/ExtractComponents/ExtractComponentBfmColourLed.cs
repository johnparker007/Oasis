using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBfmColourLed : ExtractComponentBase
    {
		public int DotSize;
		public int Spacing;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentBfmColourLed(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
