using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public class ComponentAlpha : Component
    {
        private bool _reversed = false;
        public bool Reversed
        {
            get => _reversed;
            set { _reversed = value; base.OnValueSetInvoke(); }
        }
    }

}
