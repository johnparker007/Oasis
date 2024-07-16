using System.Collections.Generic;

namespace Oasis.Layout
{
    public class ComponentSwitch : ComponentInput, SerializableDictionary
    {
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
