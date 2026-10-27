using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class Border : BaseComponent
    {
        public string OuterColour { get; set; }
        public string InnerColour { get; set; }
        public uint BorderWidth { get; set; }
        public uint Spacing { get; set; }

        /// <summary>Wire BoxX from tag 0x06; observed as always zero. JSON Values uses Geometry.X + BoxX.</summary>
        public uint BoxX { get; set; }

        /// <summary>Wire BoxY from tag 0x06; observed as always zero. JSON Values uses Geometry.Y + BoxY.</summary>
        public uint BoxY { get; set; }

        public uint BoxWidth { get; set; }
        public uint BoxHeight { get; set; }

        /// <summary>Tag 0x07: left column width of the 2×2 pane split (LeftWidth + RightWidth == BoxWidth).</summary>
        public uint LeftWidth { get; set; }

        /// <summary>Tag 0x07: top row height of the 2×2 pane split (TopHeight + BottomHeight == BoxHeight).</summary>
        public uint TopHeight { get; set; }

        /// <summary>Tag 0x07: right column width of the 2×2 pane split.</summary>
        public uint RightWidth { get; set; }

        /// <summary>Tag 0x07: bottom row height of the 2×2 pane split.</summary>
        public uint BottomHeight { get; set; }

        /// <summary>
        /// Ordinals of layout components owned by this border (post-parse assignment).
        /// Null until annotation runs; empty when the border owns no components.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<int> OwnedOrdinalComponentIdentifiers { get; set; }

        public override JsonObject ToJsonObject()
        {
            JsonObject json = base.ToJsonObject();
            if (OwnedOrdinalComponentIdentifiers is null)
            {
                return json;
            }

            JsonObject ordered = new JsonObject();
            foreach (var kvp in json)
            {
                ordered[kvp.Key] = kvp.Value is null ? null : JsonNode.Parse(kvp.Value.ToJsonString());
                if (kvp.Key == "Geometry")
                {
                    JsonArray owned = new JsonArray();
                    foreach (int id in OwnedOrdinalComponentIdentifiers)
                    {
                        owned.Add(id);
                    }

                    ordered["OwnedOrdinalComponentIdentifiers"] = owned;
                }
            }

            return ordered;
        }
    }
}
