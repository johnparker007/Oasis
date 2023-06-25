using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentBarcrestBwbVideo : ExtractComponentBase
    {
		public int Number;
		public int LeftSkew;
		public int RightSkew;

		public ExtractComponentBarcrestBwbVideo(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
