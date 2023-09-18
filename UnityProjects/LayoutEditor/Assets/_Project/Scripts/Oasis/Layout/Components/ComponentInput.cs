using Oasis.Graphics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    public abstract class ComponentInput : Component
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

        public InputData Input = new InputData();
    }

}
