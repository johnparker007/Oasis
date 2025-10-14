using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBfmAlpha : ExtractComponentBase
    {
		public bool Reversed;
		public ColorJSON Colour;
		public int OffLevel;
		public int DigitWidth;
		public int Columns;

		public ExtractComponentBfmAlpha(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
