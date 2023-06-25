using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentAceMatrix : ExtractComponentBase
    {
		public int DotSize;
		public bool Flip180;
		public bool Vertical;
        public ColorJSON OnColour;
        public ColorJSON OffColour;
        public ColorJSON BackgroundColour;

        public ExtractComponentAceMatrix(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
