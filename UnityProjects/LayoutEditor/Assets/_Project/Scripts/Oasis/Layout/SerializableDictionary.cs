using System.Collections.Generic;

namespace Oasis.Layout
{
    public interface SerializableDictionary {

        public void SetRepresentation(Dictionary<string, object> representation);
        public Dictionary<string, object> GetRepresentation();
    }
}