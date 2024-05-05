using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBfmAlpha : ExtractComponentBase
    {
		public bool Reversed;
		public ColorJSON Colour;
		public int OffLevel;
		public int DigitWidth;
		public int Columns;

		public ExtractComponentBfmAlpha(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
