using Oasis.MfmeTools.Shared.Extract;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBarcrestBwbVideo : ExtractComponentBase
    {
		public int Number;
		public int LeftSkew;
		public int RightSkew;

		public ExtractComponentBarcrestBwbVideo(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
