using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponentSegmentAlpha : EditorComponentSegment
    {
        public const int kMaximumVfdDuty = 31;

        public void Setup(int vfdSegmentNumber)
        {
            _number = vfdSegmentNumber;
        }
    }

}

