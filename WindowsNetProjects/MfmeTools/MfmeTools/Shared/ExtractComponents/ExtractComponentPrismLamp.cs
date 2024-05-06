using Oasis.MfmeTools.Shared.Extract;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentPrismLamp : ExtractComponentBase
    {
		public string Lamp1NumberAsString;
		public string Lamp2NumberAsString;
		public int HorzSpacing;
		public int VertSpacing;
		public int Tilt;
		public bool Style;
		public bool Horizontal;
		public bool CentreLine;
		public string Lamp1ImageBmpFilename;
		public string Lamp1MaskBmpFilename;
		public string Lamp2ImageBmpFilename;
		public string Lamp2MaskBmpFilename;
		public string OffImageBmpFilename;

		public ExtractComponentPrismLamp(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
