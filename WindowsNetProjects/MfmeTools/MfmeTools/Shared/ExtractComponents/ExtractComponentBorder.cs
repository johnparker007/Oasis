using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBorder : ExtractComponentBase
    {
		public int BorderWidth;
		public int Spacing;
		public ColorJSON OuterColour;
		public ColorJSON InnerColour;
		public bool Outer;
		public bool Inner;

		public ExtractComponentBorder(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
