using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
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

		public ExtractComponentBitmap(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
