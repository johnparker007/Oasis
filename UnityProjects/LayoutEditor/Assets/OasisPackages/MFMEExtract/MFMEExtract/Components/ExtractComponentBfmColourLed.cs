using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentBfmColourLed : ExtractComponentBase
    {
		public int DotSize;
		public int Spacing;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentBfmColourLed(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
