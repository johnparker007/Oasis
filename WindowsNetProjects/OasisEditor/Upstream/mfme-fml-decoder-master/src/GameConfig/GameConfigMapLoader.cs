using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MfmeFmlDecoder.GameConfig
{
    internal sealed class GameConfigMap
    {
        public string System { get; }
        public string SourcePath { get; }
        public IReadOnlyDictionary<string, JsonElement> Controls { get; }

        public GameConfigMap(string system, string sourcePath, Dictionary<string, JsonElement> controls)
        {
            System = system;
            SourcePath = sourcePath;
            Controls = controls;
        }

        /// <summary>
        /// Parse an embedded UTF-8 GameConfig JSON document into a map.
        /// Control values are <see cref="JsonElement.Clone"/>d so the document can be disposed.
        /// </summary>
        public static GameConfigMap FromUtf8Json(string system, string sourcePath, byte[] utf8Json)
        {
            if (utf8Json == null || utf8Json.Length == 0)
                throw new ArgumentException("Map JSON is empty.", nameof(utf8Json));

            using JsonDocument doc = JsonDocument.Parse(utf8Json);
            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("controls", out JsonElement controlsEl) ||
                controlsEl.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Map '{sourcePath}' has no controls object.");
            }

            var controls = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (JsonProperty prop in controlsEl.EnumerateObject())
            {
                controls[prop.Name] = prop.Value.Clone();
            }

            string mapSystem = root.TryGetProperty("system", out JsonElement sysEl)
                ? sysEl.GetString()
                : system;

            return new GameConfigMap(mapSystem ?? system, sourcePath, controls);
        }

        public static GameConfigMap FromJson(string system, string sourcePath, string json) =>
            FromUtf8Json(system, sourcePath, Encoding.UTF8.GetBytes(json ?? string.Empty));
    }

    internal static class GameConfigMapLoader
    {
        /// <summary>
        /// Loads a GameConfig map for <paramref name="system"/> from the in-assembly registry.
        /// <paramref name="mapsDirectory"/> is ignored (kept for call-site compatibility).
        /// </summary>
        public static bool TryLoad(string system, string mapsDirectory, out GameConfigMap map, out string error)
        {
            _ = mapsDirectory;
            return GameConfigMapRegistry.TryGet(system, out map, out error);
        }
    }
}
