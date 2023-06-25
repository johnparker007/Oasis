using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentIgtVfd : ExtractComponentBase
    {
		public int Number;
		public int DotSize;
		public int DotSpacing;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentIgtVfd(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
