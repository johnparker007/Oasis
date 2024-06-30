using Oasis.Graphics;
using System;
using System.Collections.Generic;


namespace Oasis.Layout
{
    public class ComponentReel : Component, SerializableDictionary
    {
        private int? _number = null;
        public int? Number
        {
            get => _number;
            set { _number = value; base.OnValueSetInvoke(); }
        }

        private int? _stops = null;
        public int? Stops
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

        public new void SetRepresentation(Dictionary<string, object> representation) {
            base.SetRepresentation(representation);
            if ((string)representation["type"] != this.GetType().Name) {
                return;
            }
            foreach (KeyValuePair<string, object> field in representation) {
                int iNumber = 0;
                float fNumber = 0;
                switch(field.Key) {
                    case "stops":
                    Int32.TryParse((string)field.Value, out iNumber);
                    _stops = iNumber;
                    break;
                    case "visible_scale_2d":
                    float.TryParse((string)field.Value, out fNumber);
                    _visibleScale2D = fNumber;
                    break;
                    case "is_reversed":
                    _reversed = (string)(field.Value) == "true";
                    break;
                }
            }
        }

        public new Dictionary<string, object> GetRepresentation() {
            Dictionary<string, object> representation = base.GetRepresentation();
            representation["type"] = GetType().Name;
            representation["stops"] = _stops.ToString();
            representation["visible_scale_2d"] = _visibleScale2D.ToString();
            representation["is_reversed"] = _reversed ? "true" : "false";
            return representation;
        }
    }

}
