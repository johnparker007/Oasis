using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;

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

        protected virtual void OnValueSetInvoke()
        {
            OnValueSet?.Invoke(this);
        }

        public void GetObjectData (System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            info.AddValue("x", _position.x);
            info.AddValue("y", _position.y);
            info.AddValue("width", _size.x);
            info.AddValue("height", _size.y);
        }
    }

}
