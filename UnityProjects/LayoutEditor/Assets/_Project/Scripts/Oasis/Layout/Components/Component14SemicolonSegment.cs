using System.Collections.Generic;

namespace Oasis.Layout
{
    public class Component14SemicolonSegment : ComponentSegment, SerializableDictionary 
    {
        public override void SetRepresentation(Dictionary<string, object> representation) 
        {
            base.SetRepresentation(representation);

            if ((string)representation["type"] != GetType().Name) 
            {
                return;
            }
        }

        public override Dictionary<string, object> GetRepresentation() 
        {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            return representation;
        }
    }
}
