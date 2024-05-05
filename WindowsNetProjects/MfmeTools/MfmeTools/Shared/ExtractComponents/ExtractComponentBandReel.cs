using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBandReel : ExtractComponentBase
    {
        public int Number;
		public int Stops;
		public int HalfSteps;
		public int View;
		public int Offset;
		public int Spacing;
		public int OptoTab;

		public bool Reversed;
		public bool Inverted;
		public bool Vertical;
		public bool Opaque;

		public bool Lamps;
		public bool Custom;

		public string[] LampNumbersAsStrings = new string[MFMEConstants.kBandReelLampCount];

		public bool HasOverlay;

		public string BandBmpImageFilename;
		public string OverlayBmpImageFilename;

		public ExtractComponentBandReel(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
