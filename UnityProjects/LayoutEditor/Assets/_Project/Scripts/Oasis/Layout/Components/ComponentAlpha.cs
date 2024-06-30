using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;

namespace Oasis.Layout
{
    public class ComponentAlpha : Component, SerializableDictionary 
    {
        private bool _reversed = false;
        public bool Reversed
        {
            get => _reversed;
            set { _reversed = value; base.OnValueSetInvoke(); }
        }

        public new void SetRepresentation(KeyValuePair<string, Dictionary<string, object>> representation) {
            foreach (KeyValuePair<string, object> field in representation.Value) {
                switch(field.Key) {
                    case "is_reversed":
                    _reversed = (string)(field.Value) == "true";
                    break;
                }
            }
        }

        public new KeyValuePair<string,  Dictionary<string, object>> GetRepresentation() {
            KeyValuePair<string, Dictionary<string, object>> baseRepresentation = base.GetRepresentation();
            Dictionary<string, object> dictionary = baseRepresentation.Value;
            dictionary["is_reversed"] = _reversed ? "true": "false";
            KeyValuePair<string, Dictionary<string, object>> representation = new KeyValuePair<string, Dictionary<string, object>>(this.GetType().Name, dictionary);
            return representation;
        }
        
    }

}
