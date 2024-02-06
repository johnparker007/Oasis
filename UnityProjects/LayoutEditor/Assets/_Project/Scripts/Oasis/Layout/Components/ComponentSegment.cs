using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public abstract class ComponentSegment : Component
    {
        private int _number = 0;
        public int Number
        {
            get => _number;
            set { _number = value; base.OnValueSetInvoke(); }
        }
    }

}
