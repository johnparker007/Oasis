using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oasis.Utility;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentBfmAlpha : ExtractComponentBase
    {
		public bool Reversed;
		public ColorJSON Colour;
		public int OffLevel;
		public int DigitWidth;
		public int Columns;

		public ExtractComponentBfmAlpha(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
