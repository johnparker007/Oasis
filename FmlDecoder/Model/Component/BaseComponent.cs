using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal abstract class BaseComponent
    {
        [JsonIgnore]
        public uint X { get; set; }
        [JsonIgnore]
        public uint Y { get; set; }
        [JsonIgnore]
        public uint Width { get; set; }
        [JsonIgnore]
        public uint Height { get; set; }
        [JsonIgnore]
        public int Number { get; set; }
        [JsonIgnore]
        [JsonConverter(typeof(AngleHalfStepJsonConverter))]
        public float Angle { get; set; }
        [JsonIgnore]
        public string Orientation { get; set; }

        /// <summary>
        /// Set by <see cref="Layout"/> when emitting the component list; not part of parsed FML data.
        /// </summary>
        [JsonIgnore]
        internal int? SerializationZOrder { get; set; }

        public Dictionary<string, FontTagEntry> Fonts { get; } = new Dictionary<string, FontTagEntry>();
        [JsonIgnore]
        public Dictionary<string, string> Strings { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        [JsonIgnore]
        public Dictionary<string, float> Floats { get; } = new Dictionary<string, float>(StringComparer.Ordinal);
        [JsonIgnore]
        public Dictionary<string, uint> UInt32s { get; } = new Dictionary<string, uint>(StringComparer.Ordinal);
        [JsonIgnore]
        public Dictionary<string, int> Int32s { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        [JsonIgnore]
        public Dictionary<string, ushort> UInt16s { get; } = new Dictionary<string, ushort>(StringComparer.Ordinal);
        [JsonIgnore]
        public Dictionary<string, bool> Booleans { get; } = new Dictionary<string, bool>(StringComparer.Ordinal);
        [JsonIgnore]
        public Dictionary<string, byte> Bytes { get; } = new Dictionary<string, byte>(StringComparer.Ordinal);
        [JsonIgnore]
        public Dictionary<string, string> Colours { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, BitmapEntry> Images { get; } = new Dictionary<string, BitmapEntry>(StringComparer.Ordinal);

        private static readonly JsonSerializerOptions ToJsonOptions = CreateToJsonOptions();

        internal static JsonSerializerOptions CreateJsonWriteOptions(bool writeIndented)
        {
            return new JsonSerializerOptions(ToJsonOptions)
            {
                WriteIndented = writeIndented
            };
        }

        public virtual string ToJson(bool indented = true)
        {
            JsonSerializerOptions options = CreateJsonWriteOptions(indented);
            using MemoryStream stream = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(
                stream,
                new JsonWriterOptions { Indented = indented });
            WriteTo(writer, options, propertyIndentSpaces: indented ? 2 : 0);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        internal virtual void WriteTo(Utf8JsonWriter writer, JsonSerializerOptions options, int propertyIndentSpaces)
        {
            ToJsonObject().WriteTo(writer, options);
        }

        /// <summary>
        /// Component payload as a JSON object (same shape as <see cref="ToJson"/>), for embedding in a layout document.
        /// </summary>
        public virtual JsonObject ToJsonObject()
        {
            JsonSerializerOptions options = CreateJsonWriteOptions(writeIndented: false);

            JsonObject json = JsonSerializer.SerializeToNode(this, GetType(), options)?.AsObject() ?? new JsonObject();
            json.Remove(nameof(Images));
            json.Remove("Bitmaps");
            if (Fonts.Count == 0)
            {
                json.Remove(nameof(Fonts));
            }
            json.Remove(nameof(Strings));
            json.Remove(nameof(Floats));
            json.Remove(nameof(UInt32s));
            json.Remove(nameof(Int32s));
            json.Remove(nameof(UInt16s));
            json.Remove(nameof(Booleans));
            json.Remove(nameof(Bytes));
            json.Remove(nameof(Colours));

            JsonObject values = BuildValuesNode();
            if (values.Count > 0)
            {
                json["Values"] = values;
                foreach (var kvp in values)
                {
                    json.Remove(kvp.Key);
                }
            }

            RemoveDuplicateValueProperties(json, values);

            if (Images.Count > 0)
            {
                json["Images"] = BuildImagesNode(options);
            }

            JsonObject ordered = new JsonObject
            {
                ["Type"] = GetType().Name,
            };
            if (SerializationZOrder.HasValue)
            {
                ordered["ZOrder"] = SerializationZOrder.Value;
            }

            ordered["Geometry"] = BuildGeometryNode();
            foreach (var kvp in json)
            {
                // JsonNodes can only have one parent; clone to reorder safely.
                ordered[kvp.Key] = kvp.Value is null ? null : JsonNode.Parse(kvp.Value.ToJsonString());
            }
            return ordered;
        }

        private static JsonSerializerOptions CreateToJsonOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            options.Converters.Add(new BitmapEntryWithoutBytesJsonConverter());
            return options;
        }

        private sealed class AngleHalfStepJsonConverter : JsonConverter<float>
        {
            public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => reader.GetSingle();

            public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
            {
                double stepped = Math.Round((double)value / 0.5, MidpointRounding.AwayFromZero) * 0.5;
                writer.WriteNumberValue(stepped);
            }
        }

        private sealed class BitmapEntryWithoutBytesJsonConverter : JsonConverter<BitmapEntry>
        {
            public override BitmapEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotSupportedException("Deserializing BitmapEntry from JSON is not supported.");

            public override void Write(Utf8JsonWriter writer, BitmapEntry value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber(nameof(BitmapEntry.Width), value.Width);
                writer.WriteNumber(nameof(BitmapEntry.Height), value.Height);
                writer.WriteNumber(nameof(BitmapEntry.BitsPerPixel), value.BitsPerPixel);
                writer.WriteEndObject();
            }
        }

        private JsonObject BuildGeometryNode()
        {
            double steppedAngle = Math.Round((double)Angle / 0.5, MidpointRounding.AwayFromZero) * 0.5;
            JsonObject geometry = new JsonObject
            {
                ["X"] = X,
                ["Y"] = Y,
                ["Width"] = Width,
                ["Height"] = Height,
                ["Number"] = Number,
                ["Angle"] = steppedAngle,
            };
            if (Orientation is not null)
            {
                geometry["Orientation"] = Orientation;
            }

            return geometry;
        }

        private JsonObject BuildImagesNode(JsonSerializerOptions options)
        {
            JsonObject images = new JsonObject();

            foreach (var kvp in Images.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                images[kvp.Key] = JsonSerializer.SerializeToNode(kvp.Value, options);
            }

            return images;
        }

        private JsonObject BuildValuesNode()
        {
            JsonObject values = new JsonObject();
            MergeValues(values, Strings, static v => JsonValue.Create(v));
            MergeValues(values, UInt32s, static v => JsonValue.Create(v));
            MergeValues(values, Int32s, static v => JsonValue.Create(v));
            MergeValues(values, UInt16s, static v => JsonValue.Create(v));
            MergeValues(values, Booleans, static v => JsonValue.Create(v));
            MergeValues(values, Bytes, static v => JsonValue.Create(v));
            MergeValues(values, Floats, static v => JsonValue.Create(v));
            MergeValues(values, Colours, static v => JsonValue.Create(v));
            return values;
        }

        private static void MergeValues<T>(
            JsonObject target,
            IReadOnlyDictionary<string, T> source,
            Func<T, JsonNode> toNode)
        {
            foreach (var kvp in source.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (target.ContainsKey(kvp.Key))
                {
                    throw new InvalidOperationException($"Duplicate Values key '{kvp.Key}'.");
                }

                target[kvp.Key] = toNode(kvp.Value);
            }
        }

        /// <summary>
        /// Removes typed properties from the JSON object when they duplicate entries in <see cref="Values"/>
        /// under a different property name (e.g. <c>Reversed</c> vs tag attribute <c>Reverse</c>).
        /// </summary>
        protected virtual void RemoveDuplicateValueProperties(JsonObject json, JsonObject values)
        {
        }
    }
}