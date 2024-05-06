using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentPlasma : ExtractComponentBase
    {
		public int DotSize;
		public ColorJSON OffColour;
		public ColorJSON OnColour;
		public ColorJSON BackgroundColour;

		public ExtractComponentPlasma(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
