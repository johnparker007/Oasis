using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public class Component16SemicolonSegment : ComponentSegment
    {
        private int _segmentNumber = 0;

        public void Setup(int segmentNumber)
        {
            _segmentNumber = segmentNumber;
        }
    }

}
