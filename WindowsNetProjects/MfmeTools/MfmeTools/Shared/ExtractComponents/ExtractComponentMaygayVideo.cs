using Oasis.MfmeTools.Shared.Extract;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentMaygayVideo : ExtractComponentBase
    {
		public int Number;
		public bool Vertical;
		public string Quality;

		public ExtractComponentMaygayVideo(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
