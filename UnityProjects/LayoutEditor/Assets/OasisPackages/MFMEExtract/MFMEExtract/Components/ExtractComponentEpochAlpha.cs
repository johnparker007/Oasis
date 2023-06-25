using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentEpochAlpha : ExtractComponentBase
    {
		public int XSize;
		public int YSize;
		public int DotSpacing;
		public int DigitSpacing;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentEpochAlpha(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
