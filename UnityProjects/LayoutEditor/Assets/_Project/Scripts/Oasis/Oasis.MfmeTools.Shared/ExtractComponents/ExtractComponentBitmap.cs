using Oasis.MfmeTools.Shared.Extract;
using System;


namespace Oasis.MfmeTools.Shared.ExtractComponents
{
    [Serializable]
    public class ExtractComponentBitmap : ExtractComponentBase
    {
		public enum StretchFilterType
        {
			Nearest,
			Draft,
			Linear,
			Cosine,
			Spline,
			Lanczos,
			Mitchell
        }

		public bool Transparent;
		public StretchFilterType StretchFilter;
		public string ImageBmpFilename;

		public ExtractComponentBitmap(ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
