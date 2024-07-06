using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponent7Segment : EditorComponentSegment
    {
        public override string HierarchyPseudoSceneName => "7 Segments";
        public override string HierarchyName => "7 Segment";

        protected override void UpdateStateFromEmulation()
        {
            if (!_number.HasValue)
            {
                return;
            }

            int segmentValue = LayoutEditor.MameController.DigitValues[(int)_number];

            // listed in MAME-defined bit order from rendlay.cpp:

            // top bar
            _material.SetFloat("_SegmentBrightness1", GetSegmentBrightness((segmentValue >> 0) & 1));
            // top-right bar
            _material.SetFloat("_SegmentBrightness6", GetSegmentBrightness((segmentValue >> 1) & 1));
            // bottom-right bar
            _material.SetFloat("_SegmentBrightness5", GetSegmentBrightness((segmentValue >> 2) & 1));
            // bottom bar 
            _material.SetFloat("_SegmentBrightness4", GetSegmentBrightness((segmentValue >> 3) & 1));
            // bottom-left bar
            _material.SetFloat("_SegmentBrightness3", GetSegmentBrightness((segmentValue >> 4) & 1));
            // top-left bar
            _material.SetFloat("_SegmentBrightness2", GetSegmentBrightness((segmentValue >> 5) & 1));
            // middle bar
            _material.SetFloat("_SegmentBrightness7", GetSegmentBrightness((segmentValue >> 6) & 1));
            // decimal point
            _material.SetFloat("_SegmentBrightness0", GetSegmentBrightness((segmentValue >> 7) & 1));
        }
    }
}

