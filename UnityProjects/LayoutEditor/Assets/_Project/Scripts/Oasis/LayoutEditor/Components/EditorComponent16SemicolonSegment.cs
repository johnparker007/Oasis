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
        public override string HierarchyPseudoSceneName => null;
        public override string HierarchyName => null;

        public const int kMaximumVfdDuty = 31;

        public override void Initialise(
            Layout.Component component, Editor layoutEditor)
        {
            LayoutEditor = layoutEditor;

            //base.Initialise(component, layoutEditor);

            //Component16SemicolonSegment component16SemicolonSegment
            //    = (Component16SemicolonSegment)component;

            //_number = component16SemicolonSegment.Number;
        }

        public void Setup(int vfdSegmentNumber)
        {
            _number = vfdSegmentNumber;
        }

        //public void UpdateStateFromEmulationPublic()
        //{
        //    UpdateStateFromEmulation();
        //}

        protected override void UpdateStateFromEmulation()
        {
            // TOIMPROVE using a -1 for this stuff is crappy code!
            if (_number == -1)
            {
                return;
            }

            int segmentValue = LayoutEditor.MameController.VfdValues[_number];

            // listed in MAME-defined bit order from rendlay.cpp:

            // TOIMPROVE - this would be more efficient as a shader parameter?
            float dutyNormalised = (float)LayoutEditor.MameController.VfdDuty[0] / kMaximumVfdDuty;

            // top-left bar (0 red)
            _material.SetFloat("_SegmentBrightness0", 
                GetSegmentBrightness((segmentValue >> 0) & 1, dutyNormalised));
            // top-right bar (0 green)
            _material.SetFloat("_SegmentBrightness1", 
                GetSegmentBrightness((segmentValue >> 1) & 1, dutyNormalised));
            // right-top bar (0 blue)
            _material.SetFloat("_SegmentBrightness2", 
                GetSegmentBrightness((segmentValue >> 2) & 1, dutyNormalised));
            // right-bottom bar (0 alpha)
            _material.SetFloat("_SegmentBrightness3", 
                GetSegmentBrightness((segmentValue >> 3) & 1, dutyNormalised));
            // bottom-right bar (1 red)
            _material.SetFloat("_SegmentBrightness4", 
                GetSegmentBrightness((segmentValue >> 4) & 1, dutyNormalised));
            // bottom-left bar (1 green)
            _material.SetFloat("_SegmentBrightness5", 
                GetSegmentBrightness((segmentValue >> 5) & 1, dutyNormalised));
            // left-bottom bar (1 blue)
            _material.SetFloat("_SegmentBrightness6", 
                GetSegmentBrightness((segmentValue >> 6) & 1, dutyNormalised));
            // left-top bar (1 alpha)
            _material.SetFloat("_SegmentBrightness7", 
                GetSegmentBrightness((segmentValue >> 7) & 1, dutyNormalised));
            // horizontal-middle-left bar (2 red)
            _material.SetFloat("_SegmentBrightness8", 
                GetSegmentBrightness((segmentValue >> 8) & 1, dutyNormalised));
            // horizontal-middle-right bar (2 green)
            _material.SetFloat("_SegmentBrightness9", 
                GetSegmentBrightness((segmentValue >> 9) & 1, dutyNormalised));
            // vertical-middle-top bar (2 blue)
            _material.SetFloat("_SegmentBrightness10", 
                GetSegmentBrightness((segmentValue >> 10) & 1, dutyNormalised));
            // vertical-middle-bottom bar (2 alpha)
            _material.SetFloat("_SegmentBrightness11", 
                GetSegmentBrightness((segmentValue >> 11) & 1, dutyNormalised));
            // diagonal-left-bottom bar (3 red)
            _material.SetFloat("_SegmentBrightness12", 
                GetSegmentBrightness((segmentValue >> 12) & 1, dutyNormalised));
            // diagonal-left-top bar (3 green)
            _material.SetFloat("_SegmentBrightness13", 
                GetSegmentBrightness((segmentValue >> 13) & 1, dutyNormalised));
            // diagonal-right-top bar (3 blue)
            _material.SetFloat("_SegmentBrightness14", 
                GetSegmentBrightness((segmentValue >> 14) & 1, dutyNormalised));
            // diagonal-right-bottom bar (3 alpha)
            _material.SetFloat("_SegmentBrightness15", 
                GetSegmentBrightness((segmentValue >> 15) & 1, dutyNormalised));

            // decimal point (4 red)
            _material.SetFloat("_SegmentBrightness16", 
                GetSegmentBrightness((segmentValue >> 16) & 1, dutyNormalised));
            // comma tail (4 green)
            //_material.SetFloat("_SegmentBrightness17", GetSegmentBrightness((segmentValue >> 17) & 1));

        }
    }
}

