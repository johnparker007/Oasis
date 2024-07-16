using System;
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

        public void SetRepresentation(Dictionary<string, object> representation) {
            int currentValue = 0;
            if ((string)representation["type"] != this.GetType().Name) {
                return;
            }
            foreach (string k in representation.Keys) {
                switch(k) {
                    case "x":
                    Int32.TryParse((string)representation[k], out currentValue);
                    _position.x = currentValue;
                    break;
                    case "y":
                    Int32.TryParse((string)representation[k], out currentValue);
                    _position.y = currentValue;
                    break;
                    case "width":
                    Int32.TryParse((string)representation[k], out currentValue);
                    _size.x = currentValue;
                    break;
                    case "height":
                    Int32.TryParse((string)representation[k], out currentValue);
                    _size.y = currentValue;
                    break;
                }
            }
        }

        public Dictionary<string, object> GetRepresentation() {
            return new Dictionary<string, object>
            {
                {"type", GetType().Name},
                {"name", _name},
                {"text", _text},
                {"x", _position.x.ToString()},
                {"y", _position.y.ToString()},
                {"width", _size.x.ToString()},
                {"height", _size.y.ToString()},
            };
        }

    }
}
