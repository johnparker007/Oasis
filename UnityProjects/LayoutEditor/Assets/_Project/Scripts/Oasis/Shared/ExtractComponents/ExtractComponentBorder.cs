using Oasis.MfmeTools.Shared.Extract;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBorder : ExtractComponentBase
    {
		public int BorderWidth;
		public int Spacing;
		public ColorJSON OuterColour;
		public ColorJSON InnerColour;
		public bool Outer;
		public bool Inner;

		public ExtractComponentBorder(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
