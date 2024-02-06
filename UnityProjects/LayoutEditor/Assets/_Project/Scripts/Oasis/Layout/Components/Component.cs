using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public abstract class Component : MonoBehaviour
    {
        public delegate void OnValueSetDelegate(Component component);
        public event OnValueSetDelegate OnValueSet;

        private RectInt _rectInt;
        public RectInt RectInt
        {
            get => _rectInt;
            set { _rectInt = value; OnValueSetInvoke(); }
        }

        protected virtual void OnValueSetInvoke()
        {
            OnValueSet?.Invoke(this);
        }

    }

}
