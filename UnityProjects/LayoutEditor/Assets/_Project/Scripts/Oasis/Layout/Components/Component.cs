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

        private Vector2Int _position;
        public Vector2Int Position
        {
            get => _position;
            set { _position = value; OnValueSetInvoke(); }
        }

        private Vector2Int _size;
        public Vector2Int Size
        {
            get => _size;
            set { _size = value; OnValueSetInvoke(); }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnValueSetInvoke(); }
        }

        private string _text;
        public string Text
        {
            get => _text;
            set { _text = value; OnValueSetInvoke(); }
        }

        protected virtual void OnValueSetInvoke()
        {
            OnValueSet?.Invoke(this);
        }

    }

}
