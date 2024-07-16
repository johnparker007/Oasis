using System.Collections.Generic;
using Oasis.Graphics;

namespace Oasis.Layout
{
    public class ComponentBackground : Component, SerializableDictionary
    {
        public OasisImage OasisImage;

        public new void SetRepresentation(Dictionary<string, object> representation) {
            base.SetRepresentation(representation);
            if ((string)representation["type"] != this.GetType().Name) {
                return;
            }
        }

        public new Dictionary<string, object> GetRepresentation() {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            return representation;
        }
    }

}
