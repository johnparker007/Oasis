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
