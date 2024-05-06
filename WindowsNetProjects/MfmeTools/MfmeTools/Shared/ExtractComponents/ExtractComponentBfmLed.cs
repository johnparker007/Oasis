using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBfmLed : ExtractComponentBase
    {
		public int XSize;
		public int YSize;
		public int DigitSpacing;
		public int LedSize;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackColour;

		public ExtractComponentBfmLed(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
