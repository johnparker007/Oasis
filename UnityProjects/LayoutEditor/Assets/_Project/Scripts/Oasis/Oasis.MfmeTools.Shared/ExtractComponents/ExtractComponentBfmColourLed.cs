using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBfmColourLed : ExtractComponentBase
    {
		public int DotSize;
		public int Spacing;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentBfmColourLed(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
