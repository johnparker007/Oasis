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
            const float kOnBrightness = 1f;
            const float kOffBrightness = 0.1f;

            if (segmentBitValue == 1)
            {
                return kOnBrightness;
            }
            else
            {
                return kOffBrightness;
            }
        }
    }

}

