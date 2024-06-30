using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;

namespace Oasis.Layout
{
    public class ComponentSwitch : Component, SerializableDictionary
    {
        public new void SetRepresentation(KeyValuePair<string, Dictionary<string, object>> representation) {
        }

        public new KeyValuePair<string,  Dictionary<string, object>> GetRepresentation() {
            KeyValuePair<string, Dictionary<string, object>> baseRepresentation = base.GetRepresentation();
            Dictionary<string, object> dictionary = baseRepresentation.Value;
            // Add fields to dictionary from this component
            // ...
            KeyValuePair<string, Dictionary<string, object>> representation = new KeyValuePair<string, Dictionary<string, object>>(this.GetType().Name, dictionary);
            return representation;
        }
    }

}
