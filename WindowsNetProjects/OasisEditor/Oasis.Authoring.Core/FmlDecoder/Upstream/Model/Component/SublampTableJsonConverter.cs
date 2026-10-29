using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal sealed class SublampTableJsonConverter : JsonConverter<SubLampNumberTablePayload>
    {
        /// <summary>MFME uses 0xFFFFFFFE (-2) when a sublamp number is not defined.</summary>
        internal const int UndefinedSublampNumber = -2;

        public override SubLampNumberTablePayload Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
            => throw new NotSupportedException("Deserializing sublamp tables from JSON is not supported.");

        public override void Write(
            Utf8JsonWriter writer,
            SubLampNumberTablePayload value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value.NumberOfDefinedLampNumbers.HasValue)
            {
                writer.WriteNumber("NumberOfDefinedLampNumbers", value.NumberOfDefinedLampNumbers.Value);
            }

            IReadOnlyList<LampSublampTableEntry> entries = value.Entries;
            if (entries is not null)
            {
                foreach (LampSublampTableEntry entry in entries.OrderBy(e => e.SublampIndex))
                {
                    if (entry.SublampNumber == UndefinedSublampNumber)
                    {
                        continue;
                    }

                    writer.WritePropertyName($"Lamp{entry.SublampIndex}");
                    writer.WriteNumberValue(entry.SublampNumber);
                }
            }

            writer.WriteEndObject();
        }
    }
}