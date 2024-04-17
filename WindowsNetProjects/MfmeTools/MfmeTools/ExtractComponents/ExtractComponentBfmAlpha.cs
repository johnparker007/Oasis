using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
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
