using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentIgtVfd : ExtractComponentBase
    {
		public int Number;
		public int DotSize;
		public int DotSpacing;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentIgtVfd(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
