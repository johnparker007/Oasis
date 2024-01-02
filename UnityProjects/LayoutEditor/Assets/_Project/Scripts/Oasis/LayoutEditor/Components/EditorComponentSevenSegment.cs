using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponentSevenSegment : EditorComponent2D
    {
        private int _number = -1;
        private Material _material = null;

        protected override void Awake()
        {
            Image image = GetComponent<Image>();
            _material = new Material(image.material);
            image.material = _material;
        }

        protected void OnDestroy()
        {
            if(_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }

        public override void Initialise(
            Layout.Component component, Editor layoutEditor)
        {
            base.Initialise(component, layoutEditor);

            ComponentSevenSegment componentSevenSegment = (ComponentSevenSegment)component;

            _number = componentSevenSegment.Number;
        }

        protected override void UpdateStateFromEmulation()
        {
            // TOIMPROVE using a -1 for this stuff is crappy code!
            if (_number == -1)
            {
                return;
            }

            int segmentValue = LayoutEditor.MameController.DigitValues[_number];

            // TODO need to map these correctly to the MAME segment bits
            _material.SetFloat("_SegmentBrightness0", (segmentValue >> 7) & 1);
            _material.SetFloat("_SegmentBrightness1", (segmentValue >> 6) & 1);
            _material.SetFloat("_SegmentBrightness2", (segmentValue >> 5) & 1);
            _material.SetFloat("_SegmentBrightness3", (segmentValue >> 4) & 1);
            _material.SetFloat("_SegmentBrightness4", (segmentValue >> 3) & 1);
            _material.SetFloat("_SegmentBrightness5", (segmentValue >> 2) & 1);
            _material.SetFloat("_SegmentBrightness6", (segmentValue >> 1) & 1);
            _material.SetFloat("_SegmentBrightness7", (segmentValue >> 0) & 1);
        }

    }

}

