using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentPlasma : ExtractComponentBase
    {
		public int DotSize;
		public ColorJSON OffColour;
		public ColorJSON OnColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentPlasma(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
