using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentEpochMatrix : ExtractComponentBase
    {
		public int DotSize;
        public ColorJSON OffColour;
        public ColorJSON OnColourLo;
        public ColorJSON OnColourMed;
        public ColorJSON OnColourHi;
        public ColorJSON BackgroundColour;

        public ExtractComponentEpochMatrix(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
