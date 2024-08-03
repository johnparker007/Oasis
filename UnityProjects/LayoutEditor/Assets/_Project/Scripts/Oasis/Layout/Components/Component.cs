using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public abstract class Component : MonoBehaviour, SerializableDictionary
    {
        public delegate void OnValueSetDelegate(Component component);
        public event OnValueSetDelegate OnValueSet;

        public string Guid
        {
            get;
            private set;
        }

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

        // JP TODO - the plan is to change this base Component class to NOT derive from Monobehaviour, but instead
        // be a pure c# class.  At that point, we can add standard contructor/destructor, and so then for instance
        // the Component will set up its own GUID on instantiation.
        // This work can't be done until the current UI Hierarchy and Inspector are rewritten, the current ones were
        // placeholders to get something useable going.
        public void ConstructorPlaceholder()
        {
            AllocateGuid();
        }

        public virtual void SetRepresentation(Dictionary<string, object> representation) 
        {
            if ((string)representation["type"] != GetType().Name)
            {
                return;
            }

            foreach (string k in representation.Keys) 
            {
                int currentValue;
                switch (k)
                {
                    case "guid":
                        Guid = (string)representation[k];
                        break;
                    case "name":
                        _name = (string)representation[k];
                        break;
                    case "text":
                        _text = (string)representation[k];
                        break;
                    case "x":
                        int.TryParse((string)representation[k], out currentValue);
                        _position.x = currentValue;
                        break;
                    case "y":
                        int.TryParse((string)representation[k], out currentValue);
                        _position.y = currentValue;
                        break;
                    case "width":
                        int.TryParse((string)representation[k], out currentValue);
                        _size.x = currentValue;
                        break;
                    case "height":
                        int.TryParse((string)representation[k], out currentValue);
                        _size.y = currentValue;
                        break;
                }
            }
        }

        public virtual Dictionary<string, object> GetRepresentation() 
        {
            return new Dictionary<string, object>
            {
                {"type", GetType().Name},
                {"guid", Guid},
                {"name", _name},
                {"text", _text},
                {"x", _position.x.ToString()},
                {"y", _position.y.ToString()},
                {"width", _size.x.ToString()},
                {"height", _size.y.ToString()},
            };
        }

        protected virtual void OnValueSetInvoke()
        {
            OnValueSet?.Invoke(this);
        }

        private void AllocateGuid()
        {
            if(Guid == null || Guid.Length == 0)
            {
                string newGuid;

                do
                {
                    newGuid = System.Guid.NewGuid().ToString();
                }
                while (Editor.Instance.Project.Layout.GetComponentByGuid(newGuid) != null);

                Guid = newGuid;
            }
        }

    }
}
