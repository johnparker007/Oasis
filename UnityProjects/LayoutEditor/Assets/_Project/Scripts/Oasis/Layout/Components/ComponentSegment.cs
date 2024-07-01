using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;

namespace Oasis.Layout
{
    public abstract class ComponentSegment : Component
    {
        private int _number = 0;
        public int Number
        {
            get => _number;
            set { _number = value; base.OnValueSetInvoke(); }
        }
        public new void GetObjectData (System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("component_type", this.GetType().Name);
            info.AddValue("number", _number);
        }
    }

}
