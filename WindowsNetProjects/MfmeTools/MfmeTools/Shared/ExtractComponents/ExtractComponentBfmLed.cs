using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBfmLed : ExtractComponentBase
    {
		public int XSize;
		public int YSize;
		public int DigitSpacing;
		public int LedSize;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackColour;

		public ExtractComponentBfmLed(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
