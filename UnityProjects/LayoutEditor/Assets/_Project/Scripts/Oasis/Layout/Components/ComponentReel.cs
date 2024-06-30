using Oasis.Graphics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization;
using System;

namespace Oasis.Layout
{
    public class ComponentReel : Component, SerializableDictionary
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

        public new void SetRepresentation(KeyValuePair<string, Dictionary<string, object>> representation) {
            foreach (KeyValuePair<string, object> field in representation.Value) {
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

        public new KeyValuePair<string,  Dictionary<string, object>> GetRepresentation() {
            KeyValuePair<string, Dictionary<string, object>> baseRepresentation = base.GetRepresentation();
            Dictionary<string, object> dictionary = baseRepresentation.Value;
            dictionary["stops"] = _stops.ToString();
            dictionary["visible_scale_2d"] = _visibleScale2D.ToString();
            dictionary["is_reversed"] = _reversed ? "true": "false";
            KeyValuePair<string, Dictionary<string, object>> representation = new KeyValuePair<string, Dictionary<string, object>>(this.GetType().Name, dictionary);
            return representation;
        }
    }

}
