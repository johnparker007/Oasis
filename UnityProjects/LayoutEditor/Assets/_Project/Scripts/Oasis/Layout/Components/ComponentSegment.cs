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

        public override void SetRepresentation(Dictionary<string, object> representation) 
        {
            base.SetRepresentation(representation);

            if ((string)representation["type"] != GetType().Name) 
            {
                return;
            }

            foreach (KeyValuePair<string, object> field in representation) 
            {
                int iNumber = 0;
                switch (field.Key) 
                {
                    case "number":
                        int.TryParse((string)field.Value, out iNumber);
                        _number = iNumber;
                        break;
                }
            }
        }

        public override Dictionary<string, object> GetRepresentation() 
        {
            Dictionary<string, object> representation = base.GetRepresentation();

            representation["type"] = GetType().Name;
            representation["number"] = _number?.ToString();

            return representation;
        }
    }

}
