using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentPlasma : ExtractComponentBase
    {
		public int DotSize;
		public ColorJSON OffColour;
		public ColorJSON OnColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentPlasma(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
