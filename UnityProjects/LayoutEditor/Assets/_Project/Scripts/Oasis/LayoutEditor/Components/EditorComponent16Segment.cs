using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponent16Segment : EditorComponentSegment
    {
        public override string HierarchyPseudoSceneName => null;
        public override string HierarchyName => null;

        public override void Initialise(
            Layout.Component component, Editor layoutEditor)
        {
            base.Initialise(component, layoutEditor);

            Component16Segment component16Segment = (Component16Segment)component;

            _number = component16Segment.Number;
        }

        protected override void UpdateStateFromEmulation()
        {
            if (!_number.HasValue)
            {
                return;
            }

            int segmentValue = LayoutEditor.MameController.VfdValues[(int)_number];

            // listed in MAME-defined bit order from rendlay.cpp:

            // top-left bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 0) & 1));
            // top-right bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 1) & 1));
            // right-top bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 2) & 1));
            // right-bottom bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 3) & 1));
            // bottom-right bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 4) & 1));
            // bottom-left bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 5) & 1));
            // left-bottom bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 6) & 1));
            // left-top bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 7) & 1));
            // horizontal-middle-left bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 8) & 1));
            // horizontal-middle-right bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 9) & 1));
            // vertical-middle-top bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 10) & 1));
            // vertical-middle-bottom bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 11) & 1));
            // diagonal-left-bottom bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 12) & 1));
            // diagonal-left-top bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 13) & 1));
            // diagonal-right-top bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 14) & 1));
            // diagonal-right-bottom bar
            _material.SetFloat("_SegmentBrightness__TODO__", GetSegmentBrightness((segmentValue >> 15) & 1));
        }
    }

}

