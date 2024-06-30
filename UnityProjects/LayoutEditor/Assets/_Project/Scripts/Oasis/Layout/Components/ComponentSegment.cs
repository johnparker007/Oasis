using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;
using System;

namespace Oasis.Layout
{
    public abstract class ComponentSegment : Component, SerializableDictionary
    {
        private int _number = 0;
        public int Number
        {
            get => _number;
            set { _number = value; base.OnValueSetInvoke(); }
        }
        public new void SetRepresentation(KeyValuePair<string, Dictionary<string, object>> representation) {
            foreach (KeyValuePair<string, object> field in representation.Value) {
                switch(field.Key) {
                    case "number":
                    int number = 0;
                    Int32.TryParse((string)field.Value, out number);
                    _number = number;
                    break;
                }
            }
        }

        public new KeyValuePair<string,  Dictionary<string, object>> GetRepresentation() {
            KeyValuePair<string, Dictionary<string, object>> baseRepresentation = base.GetRepresentation();
            Dictionary<string, object> dictionary = baseRepresentation.Value;
            dictionary["number"] = _number.ToString();
            KeyValuePair<string, Dictionary<string, object>> representation = new KeyValuePair<string, Dictionary<string, object>>(this.GetType().Name, dictionary);
            return representation;
        }
    }

}
