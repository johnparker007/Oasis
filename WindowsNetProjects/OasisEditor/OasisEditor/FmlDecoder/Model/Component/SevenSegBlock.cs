using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class SevenSegBlock : BaseComponent
    {
        private const string RowsKey = "Rows";
        private const string ColumnsKey = "Columns";

        // 0xFFFFFFFF sentinel (read as int -1) marks a digit with no assigned single lamp number.
        private const int SentinelLampNumber = -1;

        // Per-digit flags emitted (in this order) on each digit object, when present.
        private static readonly string[] DigitFlagOrder =
        {
            "Visible",
            "Programmable",
            "DPOn",
            "DPOff",
            "AutoDP",
            "ZeroOn",
        };

        /// <summary>
        /// Programmable lamp numbers decoded from tag <c>0x1B</c>, keyed by digit index (0-based).
        /// Each value is the eight lamp numbers for that digit. Only digits flagged programmable in the
        /// tag <c>0x0B</c> mask are present; non-programmable digits (stored as <c>0xFFFFFFFF</c> sentinels
        /// in the stream) are omitted. Null when the component has no programmable-digit data.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<int, uint[]> ProgrammableLampNumbers { get; set; }

        /// <summary>
        /// Per-digit boolean display flags decoded from the nested tag block, keyed by flag name
        /// (e.g. "DPOn", "Programmable", "ZeroOn", "AutoDP", "Visible", "DPOff"). Each value is a
        /// per-digit array (one entry per digit). Null when the component has no flag data.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<string, bool[]> DigitFlags { get; set; }

        /// <summary>
        /// Single lamp number per digit, decoded from the SubLampTable (tag <c>0x39</c>), indexed by digit.
        /// Used by non-programmable digits; programmable digits carry 0 here (their lamps live in
        /// <see cref="ProgrammableLampNumbers"/>). Null when the component has no SubLampTable.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<int> DigitLampNumbers { get; set; }

        public override JsonObject ToJsonObject()
        {
            JsonObject json = base.ToJsonObject();

            JsonArray digits = BuildDigitsArray();
            if (digits is not null)
            {
                json["Digits"] = digits;
            }

            return json;
        }

        // One object per active digit (Rows x Columns), each carrying its flags and, for programmable
        // digits, its eight lamp numbers.
        private JsonArray BuildDigitsArray()
        {
            if (DigitFlags is null && ProgrammableLampNumbers is null && DigitLampNumbers is null)
            {
                return null;
            }

            if (!UInt32s.TryGetValue(RowsKey, out uint rows)
                || !UInt32s.TryGetValue(ColumnsKey, out uint columns))
            {
                return null;
            }

            long activeDigitCount = (long)rows * columns;
            if (activeDigitCount <= 0)
            {
                return null;
            }

            JsonArray digits = new();
            for (int digitIndex = 0; digitIndex < activeDigitCount; digitIndex++)
            {
                JsonObject digit = new() { ["DigitNumber"] = digitIndex + 1 };

                if (DigitFlags is not null)
                {
                    foreach (string flagName in DigitFlagOrder)
                    {
                        if (DigitFlags.TryGetValue(flagName, out bool[] flags) && digitIndex < flags.Length)
                        {
                            digit[flagName] = flags[digitIndex];
                        }
                    }
                }

                // Programmable digits carry the 8-lamp array; non-programmable digits carry the single lamp number.
                if (ProgrammableLampNumbers is not null
                    && ProgrammableLampNumbers.TryGetValue(digitIndex, out uint[] lampNumbers))
                {
                    JsonArray lamps = new();
                    foreach (uint lampNumber in lampNumbers)
                    {
                        lamps.Add(lampNumber);
                    }

                    digit["SegmentLamps"] = lamps;
                }
                else if (DigitLampNumbers is not null
                    && digitIndex < DigitLampNumbers.Count
                    && DigitLampNumbers[digitIndex] != SentinelLampNumber)
                {
                    digit["LampNumber"] = DigitLampNumbers[digitIndex];
                }

                digits.Add(digit);
            }

            return digits;
        }
    }
}
