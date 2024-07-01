using Oasis.Graphics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;

namespace Oasis.Layout
{
    public class ComponentReel : Component, ISerializable 
    {
        private int _number = 0;
        public int Number
        {
            get => _number;
            set { _number = value; base.OnValueSetInvoke(); }
        }

        private int _stops = 0;
        public int Stops
        {
            get => _stops;
            set { _stops = value; base.OnValueSetInvoke(); }
        }

        private bool _reversed = false;
        public bool Reversed
        {
            get => _reversed;
            set { _reversed = value; base.OnValueSetInvoke(); }
        }

        private float _visibleScale2D = 1f;
        public float VisibleScale2D
        {
            get => _visibleScale2D;
            set { _visibleScale2D = value; base.OnValueSetInvoke(); }
        }

        public OasisImage BandOasisImage;
        // Not sure about this being in here, for MFME Import stage only,
        // will copy into Background transparency when converted to an Oasis panel
        public OasisImage OverlayOasisImage;

        public new void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {

            base.GetObjectData(info, context);
            info.AddValue("component_type", this.GetType().Name);
            info.AddValue("stops", _stops.ToString());
            info.AddValue("is_reversed", _reversed.ToString());
            info.AddValue("visible_scale_2D", _visibleScale2D.ToString()); 
        }
    }

}
