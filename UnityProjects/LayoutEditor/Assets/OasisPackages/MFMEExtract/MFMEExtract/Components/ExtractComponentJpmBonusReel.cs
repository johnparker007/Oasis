using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentJpmBonusReel : ExtractComponentBase
    {
		public string Lamp1AsString;
		public string Lamp2AsString;
		public string Lamp3AsString;
		public string Lamp4AsString;

		public int Number;
		public int SymbolPos;

		public string Lamp1OnImageBmpImageFilename;
		public string Lamp2OnImageBmpImageFilename;
		public string Lamp3OnImageBmpImageFilename;
		public string Lamp4OnImageBmpImageFilename;

		public string MaskBmpImageFilename;
		public string BackgroundBmpImageFilename;

		public bool HasOverlay;
		public string OverlayBmpImageFilename;

		public ExtractComponentJpmBonusReel(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
