using MfmeTools.Mfme;
using System;
using System.Collections;
using System.Collections.Generic;


namespace MfmeTools.ExtractComponents
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
