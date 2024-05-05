using Oasis.MfmeTools.Shared.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


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

		public ExtractComponentBitmap(MfmeExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
