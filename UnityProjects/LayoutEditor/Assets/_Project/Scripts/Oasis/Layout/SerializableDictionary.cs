using System.Collections.Generic;

namespace Oasis.Layout
{
    public interface SerializableDictionary {

        public void SetRepresentation(KeyValuePair<string, Dictionary<string, object>> representation);
        public KeyValuePair<string,  Dictionary<string, object>> GetRepresentation();
    }
}