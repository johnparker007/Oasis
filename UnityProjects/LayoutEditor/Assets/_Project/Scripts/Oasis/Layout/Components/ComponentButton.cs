using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;

namespace Oasis.Layout
{
    public class ComponentButton : Component, ISerializable 
    {
        public new void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("component_type", this.GetType().Name);
        }
    }

}
