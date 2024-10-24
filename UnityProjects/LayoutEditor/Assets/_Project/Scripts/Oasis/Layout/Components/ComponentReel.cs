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

        public List<string> ReelSymbolText;

        public OasisImage BandOasisImage;
        // Not sure about this being in here, for MFME Import stage only,
        // will copy into Background transparency when converted to an Oasis panel
        public OasisImage OverlayOasisImage;


        public override void SetRepresentation(Dictionary<string, object> representation) 
        {
            base.SetRepresentation(representation);

            if ((string)representation["type"] != GetType().Name) 
            {
                return;
            }

            foreach (KeyValuePair<string, object> field in representation) 
            {
                switch(field.Key) 
                {
                    case "stops":
                        int.TryParse((string)field.Value, out int stops);
                        _stops = stops;
                        break;
                    case "visible_scale_2d":
                        float.TryParse((string)field.Value, out float visibleScale2d);
                        _visibleScale2D = visibleScale2d;
                        break;
                    case "is_reversed":
                        _reversed = (string)field.Value == "true";
                        break;

                        // TODO need to do IO of string Lists for List<string> ReelSymbolText
                }
            }
        }

        public override Dictionary<string, object> GetRepresentation() 
        {
            Dictionary<string, object> representation = base.GetRepresentation();

            representation["type"] = GetType().Name;
            representation["stops"] = _stops;
            representation["visible_scale_2d"] = _visibleScale2D;
            representation["is_reversed"] = _reversed;
            representation["number"] = _number;
            representation["reel_symbol_text"] = ReelSymbolText;
            representation["file_path_band_image"] = null;
            representation["file_path_overlay_image"] = null;
            if (BandOasisImage != null) {
                representation["file_path_band_image"] =  "reel_band_" + Component.GetComponentKey(representation) + ".png";
                ImageOperations.SaveToPNG(BandOasisImage, (string) representation["file_path_band_image"]);
            }
            if (OverlayOasisImage != null) {
                representation["file_path_overlay_image"] =  "reel_overlay_" + Component.GetComponentKey(representation) + ".png";
                ImageOperations.SaveToPNG(OverlayOasisImage, (string) representation["file_path_overlay_image"]);
            }
            return representation;
        }
    }

}
