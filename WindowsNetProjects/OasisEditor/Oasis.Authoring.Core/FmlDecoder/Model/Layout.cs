using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;

namespace MfmeFmlDecoder.src.Model
{
    internal sealed class Layout
    {
        public IReadOnlyList<BaseComponent> Components { get; }

        public LayoutFileHeader Header { get; }

        public Layout(IEnumerable<BaseComponent> components, LayoutFileHeader header = null)
        {
            Components = components != null ? components.ToList() : new List<BaseComponent>();
            Header = header ?? new LayoutFileHeader();
        }

        public bool HasSplash => Header.HasSplash;

        public string Description => Header.Description ?? string.Empty;

        public string TextNotes => Header.TextNotes ?? string.Empty;

        public IReadOnlyDictionary<string, BitmapEntry> Images => Header.Images;

        public string ToJson(bool indented = true)
        {
            JsonSerializerOptions options = BaseComponent.CreateJsonWriteOptions(indented);
            using MemoryStream stream = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(
                stream,
                new JsonWriterOptions { Indented = indented });

            writer.WriteStartObject();

            writer.WriteStartArray("Components");
            for (int i = 0; i < Components.Count; i++)
            {
                BaseComponent component = Components[i];
                component.OrdinalComponentIdentifier = i;
                component.WriteTo(writer, options, propertyIndentSpaces: indented ? 6 : 0);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
