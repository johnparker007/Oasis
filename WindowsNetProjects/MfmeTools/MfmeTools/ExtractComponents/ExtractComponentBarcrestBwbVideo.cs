using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBarcrestBwbVideo : ExtractComponentBase
    {
		public int Number;
		public int LeftSkew;
		public int RightSkew;

		public ExtractComponentBarcrestBwbVideo(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
