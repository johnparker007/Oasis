using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
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

		public ExtractComponentBorder(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
