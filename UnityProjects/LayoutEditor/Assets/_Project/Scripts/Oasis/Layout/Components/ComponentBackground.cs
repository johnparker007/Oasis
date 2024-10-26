using System.Collections.Generic;
using Oasis.Graphics;
using UnityEngine;

namespace Oasis.Layout
{
    public class ComponentBackground : Component, SerializableDictionary
    {
        public OasisImage OasisImage;

        private Color _color = Color.white;
        public Color Color
        {
            get => _color;
            set { _color = value; base.OnValueSetInvoke(); }
        }

        public override Component Clone()
        {
            ComponentBackground clone = (ComponentBackground)base.Clone();

            if (OasisImage != null)
            {
                clone.OasisImage = OasisImage.Clone();
            }

            return clone;
        }

        public override void SetRepresentation(Dictionary<string, object> representation)
        {
            base.SetRepresentation(representation);

            if ((string)representation["type"] != GetType().Name)
            {
                return;
            }

            Color color;
            foreach (KeyValuePair<string, object> field in representation) 
            {
                switch(field.Key) 
                {
                    case "color":
                        if (ColorUtility.TryParseHtmlString((string)field.Value, out color))
                            Color = color;
                        break;
                    case "file_path":
                        if (field.Value != null) {
                            OasisImage = ImageOperations.LoadFromPng(string.Format("e:\\SavedLayout\\{0}", (string)field.Value));
                        }
                        break;
                    
                }
            }
        }

        public override Dictionary<string, object> GetRepresentation() 
        {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            representation["file_path"] = null;
            representation["color"] = "#" + ColorUtility.ToHtmlStringRGBA(Color);
            if (OasisImage != null) {
                representation["file_path"] =  Component.GetComponentKey(representation) + ".png";
                ImageOperations.SaveToPNG(OasisImage, (string) representation["file_path"]);
            }
            return representation;
        }
    }

}
