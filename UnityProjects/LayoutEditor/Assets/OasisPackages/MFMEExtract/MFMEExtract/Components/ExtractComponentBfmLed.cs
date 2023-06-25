using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
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

		public ExtractComponentBfmLed(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
