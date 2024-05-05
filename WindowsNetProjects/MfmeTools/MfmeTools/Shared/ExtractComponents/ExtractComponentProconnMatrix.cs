using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentProconnMatrix : ExtractComponentBase
    {
		public int DotSize;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentProconnMatrix(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
