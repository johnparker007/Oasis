using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using uWindowCapture;

namespace TempArcadeSimComponents
{
	public class ComponentBandReel : ComponentBase
	{
		public static readonly int kReelLampMaskCount = 3; // just scrape the initial page to get started

		public static readonly int kReelLampColumns = 2;
		public static readonly int kReelLampRows = 5;
		public static readonly int kReelLampCount = kReelLampColumns * kReelLampRows;

	}
}
