using System.Collections.Generic;
using System;

namespace Oasis.Layout
{
    public abstract class ComponentSegment : Component, SerializableDictionary
    {
        private int? _number = null;
        public int? Number
        {
            get => _number;
            set { _number = value; base.OnValueSetInvoke(); }
        }
        public new void SetRepresentation(Dictionary<string, object> representation) {
            base.SetRepresentation(representation);
            if ((string)representation["type"] != this.GetType().Name) {
                return;
            }
            foreach (KeyValuePair<string, object> field in representation) {
                switch(field.Key) {
                    case "number":
                    try {
                        _number = Int32.Parse((string)field.Value);
                    }
                    catch {
                        _number = null;
                    }
                    break;
                }
            }
        }

        public new Dictionary<string, object> GetRepresentation() {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            representation["number"] = _number?.ToString();
            return representation;
        }
    }

}
