using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Model.Component
{
    internal class AlphaNew : BaseComponent
    {
        [JsonIgnore]
        public string Unknown0x04 { get; set; } = "";
        [JsonIgnore]
        public string Unknown0x0D { get; set; } = "";
        [JsonIgnore]
        public string Unknown0x0E { get; set; } = "";

        [JsonConverter(typeof(AlphaNewCharsetJsonConverter))]
        public AlphaNewCharset Charset { get; set; }
        private sealed class AlphaNewCharsetJsonConverter : JsonConverter<AlphaNewCharset>
        {
            public override AlphaNewCharset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotSupportedException("Charset JSON deserialization is not supported.");

            public override void Write(Utf8JsonWriter writer, AlphaNewCharset value, JsonSerializerOptions options)
            {
                string label = (int)value switch
                {
                    0 => "Old Charset",
                    1 => "OKI 1937",
                    2 => "BFM Charset",
                    _ => value.ToString(),
                };
                writer.WriteStringValue(label);
            }
        }
    }
}
