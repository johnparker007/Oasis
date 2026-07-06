using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class Reel : BaseComponent
    {
        private const string NonNullSublampCountKey = "NonNullSublampCount";

        [JsonIgnore]
        public IReadOnlyList<LampSublampTableEntry> SublampTable { get; set; } = Array.Empty<LampSublampTableEntry>();

        public string OffColour { get; set; }

        [JsonPropertyName("SubLampNumberTable")]
        [JsonConverter(typeof(SublampTableJsonConverter))]
        public SubLampNumberTablePayload SubLampNumberTableForJson => new()
        {
            NumberOfDefinedLampNumbers = UInt32s.TryGetValue(NonNullSublampCountKey, out uint count)
                ? count
                : null,
            Entries = SublampTable,
        };

        public override JsonObject ToJsonObject()
        {
            JsonObject json = base.ToJsonObject();
            RemoveValueKey(json, NonNullSublampCountKey);
            RemoveValueKey(json, "SelectedOffColourId");
            return json;
        }

        private static void RemoveValueKey(JsonObject json, string key)
        {
            if (json["Values"] is not JsonObject values || !values.Remove(key))
            {
                return;
            }

            if (values.Count == 0)
            {
                json.Remove("Values");
            }
        }
    }
}
