using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public abstract class ComponentInput : Component, SerializableDictionary
    {
        public class InputData
        {
            public bool Enabled;
            public bool Inverted;

            public KeyCode KeyCode;

            // Going for this ButtonNumber approach for now, then derive the Mame port
            // tag/mask during runtime, based on currently selected Platform.  This approach
            // may need revisiting...
            public int ButtonNumber;
            //public string PortTag;
            //public string FieldMask;

            public InputData Clone()
            {
                // TOIMPROVE should change this to memberwise clone for the immutables
                return new InputData
                {
                    Enabled = this.Enabled,
                    Inverted = this.Inverted,
                    KeyCode = this.KeyCode,
                    ButtonNumber = this.ButtonNumber
                };
            }
        }

        public InputData Input = new InputData();


        public override Component Clone()
        {
            ComponentInput clone = (ComponentInput)base.Clone();

            if (Input != null)
            {
                clone.Input = Input.Clone();
            }

            return clone;
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
                switch (field.Key)
                {
                    case "enabled":
                        Input.Enabled = (bool) field.Value;
                        break;
                    case "inverted":
                        Input.Inverted = (bool) field.Value;
                        break;
                    case "key_code":
                        Input.KeyCode = (KeyCode) Enum.Parse(typeof(KeyCode), (string)field.Value) ;
                        break;
                    case "button_number":
                        Input.ButtonNumber = (int)field.Value;
                        break;
                }
            }
        }

        public override Dictionary<string, object> GetRepresentation() 
        {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            representation["enabled"] = Input.Enabled;
            representation["inverted"] = Input.Inverted;
            representation["key_code"] = Input.KeyCode.ToString();
            representation["button_number"] = Input.ButtonNumber;
            return representation;
        }


    }

}
