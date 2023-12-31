﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
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

		public ExtractComponentBfmVideo(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
