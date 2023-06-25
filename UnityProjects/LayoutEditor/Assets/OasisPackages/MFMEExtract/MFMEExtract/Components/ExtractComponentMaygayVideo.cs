using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractComponentMaygayVideo : ExtractComponentBase
    {
		public int Number;
		public bool Vertical;
		public string Quality;

		public ExtractComponentMaygayVideo(MFMEExtractor.ComponentStandardData componentStandardData) : base(componentStandardData)
		{
		}

	}

}
