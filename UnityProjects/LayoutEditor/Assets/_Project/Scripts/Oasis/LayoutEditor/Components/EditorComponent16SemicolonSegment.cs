using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponent16SemicolonSegment : EditorComponentSegment
    {
        public override void Initialise(
            Layout.Component component, Editor layoutEditor)
        {
            base.Initialise(component, layoutEditor);

            Component16SemicolonSegment component16SemicolonSegment
                = (Component16SemicolonSegment)component;

            _number = component16SemicolonSegment.Number;
        }

        protected override void UpdateStateFromEmulation()
        {
            // TOIMPROVE using a -1 for this stuff is crappy code!
            if (_number == -1)
            {
                return;
            }

            int segmentValue = LayoutEditor.MameController.DigitValues[_number];


            // listed in MAME-defined bit order from rendlay.cpp:

            // top-left bar (0 red)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 0) & 1));
            // top-right bar (0 green)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 1) & 1));
            // right-top bar (0 blue)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 2) & 1));
            // right-bottom bar (0 alpha)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 3) & 1));
            // bottom-right bar (1 red)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 4) & 1));
            // bottom-left bar (1 green)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 5) & 1));
            // left-bottom bar (1 blue)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 6) & 1));
            // left-top bar (1 alpha)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 7) & 1));
            // horizontal-middle-left bar (2 red)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 8) & 1));
            // horizontal-middle-right bar (2 green)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 9) & 1));
            // vertical-middle-top bar (2 blue)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 10) & 1));
            // vertical-middle-bottom bar (2 alpha)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 11) & 1));
            // diagonal-left-bottom bar (3 red)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 12) & 1));
            // diagonal-left-top bar (3 green)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 13) & 1));
            // diagonal-right-top bar (3 blue)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 14) & 1));
            // diagonal-right-bottom bar (3 alpha)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 15) & 1));

            // decimal point (4 red)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 16) & 1));
            // comma tail (4 green)
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 17) & 1));

        }
    }
}

