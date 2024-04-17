using MfmeTools.JsonDataStructures;
using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
{
    [Serializable]
    public class ExtractComponentFlipReel : ExtractComponentBase
    {
		public int Number;
		public int Stops;
		public int HalfSteps;
		public int Offset;
		public bool Reversed;
		public bool Inverted;

		public ColorJSON BorderColour;
		public int BorderWidth;

		public bool Lamps;
		public string Lamp1AsString;
		public string Lamp2AsString;
		public string Lamp3AsString;

		public string BandBmpImageFilename;

		public string LampMask1BmpImageFilename;
		public string LampMask2BmpImageFilename;
		public string LampMask3BmpImageFilename;

		public bool HasOverlay;
		public string OverlayBmpImageFilename;

		public ExtractComponentFlipReel(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
