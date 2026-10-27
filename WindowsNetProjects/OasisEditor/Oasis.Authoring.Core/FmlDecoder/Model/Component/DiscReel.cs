using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class DiscReel : BaseComponent
    {
        public uint HalfSteps { get; set; }
        public uint Stops { get; set; }
        public uint Resolution { get; set; }
        public uint Offset { get; set; }
        public uint OuterLampSize { get; set; }
        public uint OuterH { get; set; }
        public uint OuterL {  get; set; }
        public uint InnerH { get; set; }
        public uint InnerL { get; set; }
        public uint InnerLampSize { get; set; }
        public uint OptoTab { get; set; }
        public bool LampsEnabled { get; set; }
        public uint NumberOfLamps { get; set; }
        public uint Bounce {  get; set; }
        public uint LampPositionsOffset { get; set; }
        public bool LampPositionsGapEnabled { get; set; }
        public bool LampPositionsGap {  get; set; }
        public bool Reversed { get; set; }
        public bool Inverted { get; set; }

        [JsonIgnore]
        public IReadOnlyList<LampSublampTableEntry> SublampTable { get; set; } = Array.Empty<LampSublampTableEntry>();

        [JsonPropertyName("SubLampNumberTable")]
        [JsonConverter(typeof(SublampTableJsonConverter))]
        public SubLampNumberTablePayload SubLampNumberTableForJson => new()
        {
            Entries = SublampTable,
        };
    }
}