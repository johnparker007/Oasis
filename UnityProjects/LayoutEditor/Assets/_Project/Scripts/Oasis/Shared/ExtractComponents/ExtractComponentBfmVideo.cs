using Oasis.MfmeTools.Shared.Extract;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBfmVideo : ExtractComponentBase
    {
		public enum ResolutionType
        {
			_600x800V,
			_480x640V,
			_800x600H,
			_640x480H
        }

		public int Number;
		public ResolutionType Resolution;

		public ExtractComponentBfmVideo(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
