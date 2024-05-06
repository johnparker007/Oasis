using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentDotMatrix : ExtractComponentBase
    {
		public int DotSize;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentDotMatrix(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
