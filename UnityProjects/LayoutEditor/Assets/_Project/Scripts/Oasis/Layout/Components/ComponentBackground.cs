using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Graphics;
using System.Runtime.Serialization;

namespace Oasis.Layout
{
    public class ComponentBackground : Component, ISerializable 
    {
        public OasisImage OasisImage;

        public new void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("component_type", this.GetType().Name);
            // TODO: Implement serialization for image data
            // Maybe we pack this into base64?
            info.AddValue("image_data", "<not implemented>");
        }
    }

}
