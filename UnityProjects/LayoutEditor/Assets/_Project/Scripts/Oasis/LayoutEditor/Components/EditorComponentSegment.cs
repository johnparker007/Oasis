using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponentSegment : EditorComponent2D
    {
        private const float kOnBrightness = 1f;
        private const float kOffBrightness = 0.04f;
        private const float kBrightnessRange = kOnBrightness - kOffBrightness;


        protected int? _number = null;
        protected Color _color = Color.white;
        protected Material _material = null;

        protected override void Awake()
        {
            base.Awake();

            Image image = GetComponent<Image>();
            _material = new Material(image.material);
            image.material = _material;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if(_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }

        protected override void Refresh()
        {
            base.Refresh();

            _number = ((ComponentSegment)Component).Number;
            _color = ((ComponentSegment)Component).Color;
        }

        protected float GetSegmentBrightness(int segmentBitValue)
        {
            if (segmentBitValue == 1)
            {
                return kOnBrightness;
            }
            else
            {
                return kOffBrightness;
            }
        }

        protected float GetSegmentBrightness(int segmentBitValue, float dutyNormalised)
        {
            float brightness = kOffBrightness;
            if (segmentBitValue == 1)
            {
                brightness += kBrightnessRange * dutyNormalised;
            }

            return brightness;
        }
    }

}

