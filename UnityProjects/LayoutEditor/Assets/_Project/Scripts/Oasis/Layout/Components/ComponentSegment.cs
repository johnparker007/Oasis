using System.Collections.Generic;
using System;
using UnityEngine;

namespace Oasis.Layout
{
    public abstract class ComponentSegment : Component, SerializableDictionary
    {
        private int? _number = null;
        public int? Number
        {
            get => _number;
            set { _number = value; base.OnValueSetInvoke(); }
        }

        private Color _color = UnityEngine.Color.white;
        public Color Color
        {
            get => _color;
            set { _color = value; base.OnValueSetInvoke(); }
        }

        public override void SetRepresentation(Dictionary<string, object> representation) 
        {
            base.SetRepresentation(representation);

            if ((string)representation["type"] != GetType().Name) 
            {
                return;
            }

            foreach (KeyValuePair<string, object> field in representation) 
            {
                switch (field.Key) 
                {
                    case "number":
                        int.TryParse((string)field.Value, out int number);
                        _number = number;
                        break;
                    case "color":
                        // TODO implement Color encode/decode text format
                        Debug.LogWarning("TODO implement Color encode/decode text format");
                        break;
                }
            }
        }

        public override Dictionary<string, object> GetRepresentation() 
        {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            representation["number"] = _number;
            representation["color"] = "#" + ColorUtility.ToHtmlStringRGB(Color);
            return representation;
        }
    }

}
