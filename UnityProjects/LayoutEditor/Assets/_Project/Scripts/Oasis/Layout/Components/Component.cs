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

        private string _fontName;
        public string FontName
        {
            get => _fontName;
            set { _fontName = value; OnValueSetInvoke(); }
        }

        private string _fontStyle;
        public string FontStyle
        {
            get => _fontStyle;
            set { _fontStyle = value; OnValueSetInvoke(); }
        }

        private int _fontSize;
        public int FontSize
        {
            get => _fontSize;
            set { _fontSize = value; OnValueSetInvoke(); }
        }

        // TOIMPROVE: this could be done better, perhaps with some kind of 'OasisRect' or standard Rect
        public Vector2 PointTopLeft
        {
            get
            {
                return new Vector2(Position.x, Position.y);
            }
        }

        public Vector2 PointTopRight
        {
            get
            {
                return new Vector2(Position.x + Size.x, Position.y);
            }
        }

        public Vector2 PointBottomLeft
        {
            get
            {
                return new Vector2(Position.x, Position.y + Size.y);
            }
        }

        public Vector2 PointBottomRight
        {
            get
            {
                return new Vector2(Position.x + Size.x, Position.y + Size.y);
            }
        }

        public static string GetComponentKey(Dictionary<string, object> data) {
                object n, g;
                data.TryGetValue("name", out n);
                data.TryGetValue("guid", out g);
                string name = n != null ? (string)n:"";
                string guid = g != null ? (string)g:"";
                return name + "_" + guid;  
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
                    case "fontname":
                        _fontName = (string)representation[k];
                        break;
                    case "fontstyle":
                        _fontStyle = (string)representation[k];
                        break;
                    case "fontsize":
                        _fontSize = (int)representation[k];
                        break;
                    case "x":
                        _position.x = (int)representation[k];
                        break;
                    case "y":
                        _position.y = (int)representation[k];
                        break;
                    case "width":
                        _size.x = (int)representation[k];
                        break;
                    case "height":
                        _size.y = (int)representation[k];
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
                //TODO: Do we need to include text/fontname etc for every component?
                {"text", _text},
                {"fontname", _fontName},
                {"fontstyle", _fontStyle},
                {"fontsize", _fontSize},
                {"x", _position.x},
                {"y", _position.y},
                {"width", _size.x},
                {"height", _size.y},
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
