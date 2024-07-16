using System.Collections.Generic;
using UnityEngine;

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

        public new void SetRepresentation(Dictionary<string, object> representation) {
            base.SetRepresentation(representation);
            if ((string)representation["type"] != this.GetType().Name) {
                return;
            }
        }

        public new Dictionary<string, object> GetRepresentation() {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            representation["enabled"] = Input.Enabled ? "true" : "false";
            representation["key_code"] = Input.KeyCode.ToString();
            representation["button_number"] = Input.ButtonNumber;
            return representation;
        }

        public InputData Input = new InputData();
    }

}
