using System.Collections.Generic;
using System.IO;
using Oasis.Graphics;
using UnityEngine;

namespace Oasis.Layout
{
    public class ComponentBackground : Component, SerializableDictionary
    {
        public OasisImage OasisImage;

        public string RelativeAssetPath { get; set; }

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

            clone.RelativeAssetPath = RelativeAssetPath;

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
                        RelativeAssetPath = field.Value as string;
                        if (field.Value != null)
                        {
                            OasisImage = ImageOperations.LoadFromPng(
                                Path.Combine(
                                    Editor.Instance.ProjectsController.ProjectAssetsPath,
                                    (string)field.Value));
                        }
                        break;
                    
                }
            }
        }

        public override Dictionary<string, object> GetRepresentation() 
        {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            representation["color"] = "#" + ColorUtility.ToHtmlStringRGBA(Color);
            string filePath = RelativeAssetPath;
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Component.GetComponentKey(representation) + ".png";
            }

            representation["file_path"] = filePath;

            if (OasisImage != null) {
                ImageOperations.SaveToPNG(OasisImage, filePath);
            }
            return representation;
        }
    }

}
