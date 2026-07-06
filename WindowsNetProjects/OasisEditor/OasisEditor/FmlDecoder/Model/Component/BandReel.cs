using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class BandReel : BaseComponent
    {

        public uint View {  get; set; }

        public string OffColour { get; set; }


        [JsonIgnore]
        public MaskSlotData MaskSlots { get; set; }

        public BandReel()
        {
            MaskSlots = new MaskSlotData();
        }

        public override JsonObject ToJsonObject()
        {
            JsonObject json = base.ToJsonObject();
            json["Masks"] = MaskSlots.ToJsonObject(View);
            return json;
        }

        protected override void RemoveDuplicateValueProperties(JsonObject json, JsonObject values)
        {
            base.RemoveDuplicateValueProperties(json, values);
            if (values.ContainsKey("Reverse"))
            {
                json.Remove("Reversed");
            }
        }

        internal override void WriteTo(Utf8JsonWriter writer, JsonSerializerOptions options, int propertyIndentSpaces)
        {
            JsonObject json = ToJsonObject();
            writer.WriteStartObject();
            foreach (KeyValuePair<string, JsonNode> kvp in json)
            {
                writer.WritePropertyName(kvp.Key);
                if (kvp.Key == "Masks")
                {
                    int contentIndentSpaces = propertyIndentSpaces + (options.WriteIndented ? 2 : 0);
                    MaskSlots.WriteFormattedTo(writer, View, options.WriteIndented, contentIndentSpaces);
                }
                else
                {
                    kvp.Value?.WriteTo(writer, options);
                }
            }

            writer.WriteEndObject();
        }
    }

    internal class MaskSlotData
    {
        public Dictionary<string, Dictionary<string, SlotValue>> Masks { get; set; }

        public MaskSlotData()
        {
            Masks = new Dictionary<string, Dictionary<string, SlotValue>>
            {
                ["mask1"] = CreateSlots(),
                ["mask2"] = CreateSlots(),
                ["mask3"] = CreateSlots()
            };
        }

        private static Dictionary<string, SlotValue> CreateSlots()
        {
            var slots = new Dictionary<string, SlotValue>(20);
            for (int i = 1; i <= 10; i++)
            {
                slots[$"{i}a"] = new SlotValue();
                slots[$"{i}b"] = new SlotValue();
            }
            return slots;
        }

        internal JsonObject ToJsonObject(uint view)
        {
            int maxRow = GetMaxVisibleRow(view);
            JsonObject masks = new JsonObject();
            foreach (string maskName in new[] { "mask1", "mask2", "mask3" })
            {
                JsonObject slots = new JsonObject();
                Dictionary<string, SlotValue> maskSlots = Masks[maskName];
                for (int row = 1; row <= maxRow; row++)
                {
                    slots[$"{row}a"] = maskSlots[$"{row}a"].SublampNumber;
                    slots[$"{row}b"] = maskSlots[$"{row}b"].SublampNumber;
                }

                masks[maskName] = slots;
            }

            return masks;
        }

        internal static int GetMaxVisibleRow(uint view) => Math.Min(10, (int)view);

        internal void WriteFormattedTo(Utf8JsonWriter writer, uint view, bool indented, int contentIndentSpaces)
        {
            int maxRow = GetMaxVisibleRow(view);
            writer.WriteStartObject();
            foreach (string maskName in new[] { "mask1", "mask2", "mask3" })
            {
                writer.WritePropertyName(maskName);
                if (indented)
                {
                    WriteMaskTwoColumnIndented(writer, Masks[maskName], maxRow, contentIndentSpaces);
                }
                else
                {
                    WriteMaskCompact(writer, Masks[maskName], maxRow);
                }
            }

            writer.WriteEndObject();
        }

        private static void WriteMaskTwoColumnIndented(
            Utf8JsonWriter writer,
            Dictionary<string, SlotValue> slots,
            int maxRow,
            int contentIndentSpaces)
        {
            int maskNameIndent = contentIndentSpaces;
            int rowIndent = contentIndentSpaces + 2;
            string rowPrefix = new string(' ', rowIndent);
            string closePrefix = new string(' ', maskNameIndent);

            StringBuilder sb = new StringBuilder();
            sb.Append('{').AppendLine();
            for (int row = 1; row <= maxRow; row++)
            {
                sb.Append(rowPrefix);
                sb.Append('"').Append(row).Append("a\": ");
                sb.Append(slots[$"{row}a"].SublampNumber.ToString(CultureInfo.InvariantCulture));
                sb.Append(", \"").Append(row).Append("b\": ");
                sb.Append(slots[$"{row}b"].SublampNumber.ToString(CultureInfo.InvariantCulture));
                if (row < maxRow)
                {
                    sb.Append(',');
                }

                sb.AppendLine();
            }

            sb.Append(closePrefix).Append('}');
            writer.WriteRawValue(sb.ToString());
        }

        private static void WriteMaskCompact(Utf8JsonWriter writer, Dictionary<string, SlotValue> slots, int maxRow)
        {
            writer.WriteStartObject();
            for (int row = 1; row <= maxRow; row++)
            {
                writer.WriteNumber($"{row}a", slots[$"{row}a"].SublampNumber);
                writer.WriteNumber($"{row}b", slots[$"{row}b"].SublampNumber);
            }

            writer.WriteEndObject();
        }
    }

    internal class SlotValue
    {
        public int SublampNumber { get; set; }
    }
}