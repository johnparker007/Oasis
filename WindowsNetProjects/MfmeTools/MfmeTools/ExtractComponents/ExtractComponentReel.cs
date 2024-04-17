using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentReel : ExtractComponentBase
    {
        public int Number;
		public int Stops;
		public int HalfSteps;
		public int Resolution;
		public int BandOffset;
		public int OptoTab;
		public int Height;
		public int WidthDiff;
		public int Bounce;
		public bool Horizontal;
		public bool Reversed;

		public bool Lamps;
		public bool LampsLEDs;
		public bool Mirrored;

		public string[] LampNumbersAsStrings = new string[MFMEConstants.kReelLampCount];

		public int WinLinesCount;

		// in MFME MFMEWinLinesOffset actually changes the reel band offset 
		// - could be MFME bug, will need to replicate setting in data layout
		public int WinLinesOffset;

		public bool HasOverlay;

		public string BandBmpImageFilename;
		public string OverlayBmpImageFilename;

		public ExtractComponentReel(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
