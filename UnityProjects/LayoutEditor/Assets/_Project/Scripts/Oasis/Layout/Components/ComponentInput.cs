using Oasis.Graphics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;

namespace Oasis.Layout
{
    public abstract class ComponentInput : Component, SerializableDictionary
    {
        public class InputData
        {
            public bool Enabled;

            public KeyCode KeyCode;

            // Going for this ButtonNumber approach for now, then derive the Mame port
            // tag/mask during runtime, based on currently selected Platform.  This approach
            // may need revisiting...
            public int ButtonNumber;  
            //public string PortTag;
            //public string FieldMask;

           
        }

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

        public InputData Input = new InputData();
    }

}
