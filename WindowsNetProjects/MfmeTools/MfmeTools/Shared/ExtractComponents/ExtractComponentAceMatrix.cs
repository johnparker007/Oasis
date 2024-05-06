using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;

namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentAceMatrix : ExtractComponentBase
    {
		public int DotSize;
		public bool Flip180;
		public bool Vertical;
        public ColorJSON OnColour;
        public ColorJSON OffColour;
        public ColorJSON BackgroundColour;

        public ExtractComponentAceMatrix(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
