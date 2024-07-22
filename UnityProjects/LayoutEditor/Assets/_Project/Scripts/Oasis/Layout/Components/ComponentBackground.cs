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

        public new void SetRepresentation(Dictionary<string, object> representation)
        {
            base.SetRepresentation(representation);
            if ((string)representation["type"] != this.GetType().Name)
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

        public new Dictionary<string, object> GetRepresentation() {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;

            // TODO implement Color encode/decode text format
            Debug.LogWarning("TODO implement Color encode/decode text format");

            return representation;
        }
    }

}
