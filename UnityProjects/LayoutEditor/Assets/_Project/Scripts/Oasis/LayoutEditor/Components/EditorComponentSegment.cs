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


        protected int _number = -1;
        protected Material _material = null;

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

