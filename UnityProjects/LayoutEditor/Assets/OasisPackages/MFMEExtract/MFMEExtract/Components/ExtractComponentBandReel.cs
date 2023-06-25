using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TempArcadeSimComponents;

namespace MFMEExtract
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

		public string[] LampNumbersAsStrings = new string[ComponentBandReel.kReelLampCount];

		public bool HasOverlay;

		public string BandBmpImageFilename;
		public string OverlayBmpImageFilename;

		public ExtractComponentBandReel(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
