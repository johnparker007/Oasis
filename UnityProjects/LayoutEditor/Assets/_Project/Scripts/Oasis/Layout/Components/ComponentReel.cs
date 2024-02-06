using Oasis.Graphics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public class ComponentReel : Component
    {
        private int _number = 0;
        public int Number
        {
            get => _number;
            set { _number = value; base.OnValueSetInvoke(); }
        }

        private bool _reversed = false;
        public bool Reversed
        {
            get => _reversed;
            set { _reversed = value; base.OnValueSetInvoke(); }
        }

        public OasisImage BandOasisImage;
    }

}
