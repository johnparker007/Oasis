using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentProconnMatrix : ExtractComponentBase
    {
		public int DotSize;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentProconnMatrix(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
