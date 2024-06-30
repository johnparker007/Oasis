using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public abstract class Component : MonoBehaviour, SerializableDictionary
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

        public void SetRepresentation(KeyValuePair<string, Dictionary<string, object>> representation) {
            int currentValue = 0;
            if (representation.Key == this.GetType().Name) {
                foreach (string k in representation.Value.Keys) {
                    switch(k) {
                        case "x":
                        Int32.TryParse((string)representation.Value[k], out currentValue);
                        _position.x = currentValue;
                        break;
                        case "y":
                        Int32.TryParse((string)representation.Value[k], out currentValue);
                        _position.y = currentValue;
                        break;
                        case "width":
                        Int32.TryParse((string)representation.Value[k], out currentValue);
                        _size.x = currentValue;
                        break;
                        case "height":
                        Int32.TryParse((string)representation.Value[k], out currentValue);
                        _size.y = currentValue;
                        break;
                    }
                }
            }
        }

        public KeyValuePair<string,  Dictionary<string, object>> GetRepresentation() {
            return new KeyValuePair<string, Dictionary<string, object>>
            (
                this.GetType().Name,
                new Dictionary<string, object>
                {
                    {"x", _position.x.ToString()},
                    {"y", _position.y.ToString()},
                    {"width", _size.x.ToString()},
                    {"height", _size.y.ToString()},
                }
            );
        }

    }
}
