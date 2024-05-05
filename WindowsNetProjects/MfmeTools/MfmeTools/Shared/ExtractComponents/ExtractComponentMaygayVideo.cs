using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentMaygayVideo : ExtractComponentBase
    {
		public int Number;
		public bool Vertical;
		public string Quality;

		public ExtractComponentMaygayVideo(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
