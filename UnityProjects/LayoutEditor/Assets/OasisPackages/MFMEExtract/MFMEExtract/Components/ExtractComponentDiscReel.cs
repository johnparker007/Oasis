using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentDiscReel : ExtractComponentBase
    {
		public int Number;
		public int Stops;
		public int HalfSteps;
		public int Resolution;
		public int Offset;
		public int OptoTab;
		public int Bounce;

		public bool Lamps;
		public bool Reversed;
		public bool Inverted;
		public bool Transparent;

		public int OuterH;
		public int OuterL;
		public int OuterLampSize;

		public int InnerH;
		public int InnerL;
		public int InnerLampSize;

		public int LampPositionsLamps;
		public int LampPositionsLamp;
		public string LampPositionsNumberAsString;
		public int LampPositionsOffset;
		public bool LampPositionsGap;

		public string DiscBmpImageFilename;
		public string DiscOverlayBmpImageFilename;
		public string LampPositionsBmpImageFilename;

		public string OuterMask1BmpImageFilename;
		public string OuterMask2BmpImageFilename;

		public string InnerMask1BmpImageFilename;
		public string InnerMask2BmpImageFilename;

		public bool HasOverlay;
		public string OverlayBmpImageFilename;

		public ExtractComponentDiscReel(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
