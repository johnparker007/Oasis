using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentEpochAlpha : ExtractComponentBase
    {
		public int XSize;
		public int YSize;
		public int DotSpacing;
		public int DigitSpacing;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentEpochAlpha(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
