using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentEpochMatrix : ExtractComponentBase
    {
		public int DotSize;
        public ColorJSON OffColour;
        public ColorJSON OnColourLo;
        public ColorJSON OnColourMed;
        public ColorJSON OnColourHi;
        public ColorJSON BackgroundColour;

        public ExtractComponentEpochMatrix(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
