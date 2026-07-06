using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class SevenSeg : BaseComponent
    {
        // MFME sentinels for an undefined sublamp number (0xFFFFFFFE = -2, 0xFFFFFFFF = -1).
        private const int UndefinedSublampNumber = -2;

        /// <summary>
        /// Per-segment lamp numbers decoded from the SubLampTable (tag <c>0x39</c>), in sublamp index
        /// order. Serialized as a flat array of lamp numbers (see <see cref="ToJsonObject"/>). Null when
        /// the component has no SubLampTable.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<LampSublampTableEntry> SublampTable { get; set; }

        public override JsonObject ToJsonObject()
        {
            JsonObject json = base.ToJsonObject();

            JsonArray segmentLamps = BuildSegmentLampsArray();
            if (segmentLamps is not null)
            {
                json["SegmentLamps"] = segmentLamps;
            }

            return json;
        }

        // A flat array of the defined lamp numbers, ordered by sublamp index (undefined sentinels dropped).
        private JsonArray BuildSegmentLampsArray()
        {
            if (SublampTable is null || SublampTable.Count == 0)
            {
                return null;
            }

            JsonArray lamps = new();
            foreach (LampSublampTableEntry entry in SublampTable.OrderBy(e => e.SublampIndex))
            {
                if (entry.SublampNumber < 0 || entry.SublampNumber == UndefinedSublampNumber)
                {
                    continue;
                }

                lamps.Add(entry.SublampNumber);
            }

            return lamps.Count > 0 ? lamps : null;
        }
    }
}
