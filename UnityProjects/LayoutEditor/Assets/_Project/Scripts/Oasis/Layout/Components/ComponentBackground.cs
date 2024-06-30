using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Graphics;
using System.Runtime.Serialization;

namespace Oasis.Layout
{
    public class ComponentBackground : Component, SerializableDictionary
    {
        public OasisImage OasisImage;

        public new void SetRepresentation(KeyValuePair<string, Dictionary<string, object>> representation) {
            if (representation.Key == this.GetType().Name) {
                //TODO: Cater for the image somehow
            }
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
