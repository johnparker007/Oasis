using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentIgtVfd : ExtractComponentBase
    {
		public int Number;
		public int DotSize;
		public int DotSpacing;
		public ColorJSON OnColour;
		public ColorJSON OffColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentIgtVfd(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
