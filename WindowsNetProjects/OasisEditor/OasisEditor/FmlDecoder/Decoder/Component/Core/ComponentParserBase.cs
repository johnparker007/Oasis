using MfmeFmlDecoder.src.Model.Component;
using MfmeFmlDecoder.Utilities;
using MfmeFmlDecoder.Model;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace MfmeFmlDecoder.src.Decoder.Component.Core
{
    internal abstract class ComponentParserBase<T> where T : BaseComponent, new()
    {
        protected (T component, long offset, byte[] parseData) ParseBase(
            long componentOffset,
            uint componentId,
            byte[] data,
            GeometryAngleNormalization.Rule normalizationRule = null,
            int finalOffsetAdjustment = 0,
            int geometryValidAngleTrailingSkipBytes = 5)
        {
            // Record layout: 4-byte component id + 4-byte length, then payload in `data`.
            long payloadFileOffset = componentOffset + 8;
            normalizationRule ??= new GeometryAngleNormalization.Rule();
            (byte[] parseData, int offsetAdjustment, int? preferredExtendedOffset, string normalizationModeLog) =
                GeometryAngleNormalization.Normalize(data, normalizationRule);

            RunLog.WriteLine($"\nProcessing {typeof(T).Name} Component at {componentOffset:X8} (ID: {componentId}, Length: {parseData.Length}, payload @ {payloadFileOffset:X8})\n");
            RunLog.WriteLine("Initial hex dump:\n");
            HexDumpUtility.PrintHexDump((uint)payloadFileOffset, parseData);
            RunLog.WriteLine("");

            var component = new T();
            var geometryParser = new GeometryParser();
            long offset = geometryParser.ParseInto(
                component,
                parseData,
                offset: 5,
                validAngleTrailingSkipBytes: geometryValidAngleTrailingSkipBytes).Offset;
            offset += offsetAdjustment;
            if (preferredExtendedOffset.HasValue)
            {
                offset = preferredExtendedOffset.Value;
            }
            offset += finalOffsetAdjustment;
            RunLog.WriteLine($"\nFinal offset position: {offset} (index into payload; file position {payloadFileOffset + offset:X8})");
            if (!string.IsNullOrWhiteSpace(normalizationModeLog))
            {
                RunLog.WriteLine(normalizationModeLog);
            }

            DumpRemaining(componentOffset, parseData, offset);

            return (component, offset, parseData);
        }

        protected void DumpRemaining(long componentOffset, byte[] data, long offset)
        {
            long payloadFileOffset = componentOffset + 8;

            if (offset < data.Length)
            {
                RunLog.WriteLine($"\nRemaining data after payload index {offset} (file @ {payloadFileOffset + offset:X8}):\n");
                byte[] remainingData = new byte[data.Length - offset];
                Array.Copy(data, offset, remainingData, 0, remainingData.Length);
                HexDumpUtility.PrintHexDump((uint)(payloadFileOffset + offset), remainingData);
            }
            else
            {
                RunLog.WriteLine("\nNo remaining data after final offset");
            }
        }

        protected void DumpRemaining(long componentOffset, byte[] data, long offset, int maxBytes)
        {
            long payloadFileOffset = componentOffset + 8;

            if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));

            if (offset < data.Length)
            {
                int remainingLen = checked((int)(data.Length - offset));
                int dumpLen = Math.Min(remainingLen, maxBytes);
                RunLog.WriteLine(
                    $"\nRemaining data after payload index {offset} (file @ {payloadFileOffset + offset:X8}) " +
                    $"showing {dumpLen} of {remainingLen} bytes:\n");

                byte[] remainingData = new byte[dumpLen];
                Array.Copy(data, offset, remainingData, 0, dumpLen);
                HexDumpUtility.PrintHexDump((uint)(payloadFileOffset + offset), remainingData);
            }
            else
            {
                RunLog.WriteLine("\nNo remaining data after final offset");
            }
        }

        private void AssignBitmapEntriesFromParseResult(
            T component,
            ExtendedTagParser.ParseResult parseResult,
            ComponentTagMap componentTagMap)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignBitmapEntriesByTag(
                component,
                parseResult,
                componentTagMap,
                clearExisting: true);
            if (parseResult.NestedTagBlockResult is not null && componentTagMap?.NestedTagBlockMap is not null)
            {
                AssignBitmapEntriesByTag(
                    component,
                    parseResult.NestedTagBlockResult,
                    componentTagMap.NestedTagBlockMap,
                    clearExisting: false);
            }
        }

        private void AssignBitmapEntriesByTag(
            T component,
            ExtendedTagParser.ParseResult parseResult,
            ComponentTagMap componentTagMap,
            bool clearExisting = true)
        {
            IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> bitmapBlobsByTag = parseResult.BitmapBlobsByTag;
            IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> directBitmapBlobsByTag = parseResult.DirectBitmapBlobsByTag;

            if (component is null) throw new ArgumentNullException(nameof(component));
            IReadOnlyDictionary<uint, string> derivedBitmapTagPurposesByTag =
                BuildBitmapPurposeMapFromComponentTags(componentTagMap);

            string ResolveDirectPurpose(uint tag) =>
                derivedBitmapTagPurposesByTag is not null && derivedBitmapTagPurposesByTag.TryGetValue(tag, out string purpose)
                    ? purpose
                    : $"tag_0x{tag:X2}";

            if (clearExisting)
            {
                component.Images.Clear();
            }

            AssignBitmapEntriesFromBlobMap(
                component,
                bitmapBlobsByTag,
                tag => $"tag_0x{tag:X2}",
                clearExisting);
            AssignBitmapEntriesFromBlobMap(
                component,
                directBitmapBlobsByTag,
                ResolveDirectPurpose,
                clearExisting);
        }

        private static void AssignBitmapEntriesFromBlobMap(
            BaseComponent component,
            IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> bitmapBlobsByTag,
            Func<uint, string> resolvePurpose,
            bool clearExisting)
        {
            foreach (var kvp in BitmapEntryUtility.BuildEntryByTag(bitmapBlobsByTag))
            {
                string purpose = resolvePurpose(kvp.Key);
                if (string.IsNullOrWhiteSpace(purpose))
                {
                    purpose = $"tag_0x{kvp.Key:X2}";
                }

                if (!clearExisting && component.Images.ContainsKey(purpose))
                {
                    continue;
                }

                component.Images[purpose] = kvp.Value with { Purpose = purpose };
            }
        }

        /// <summary>
        /// Parses extended TLV tags and assigns TEXT-derived <see cref="BaseComponent.Strings"/>,
        /// UINT32-derived <see cref="BaseComponent.UInt32s"/>, INT32-derived <see cref="BaseComponent.Int32s"/>,
        /// UINT16-derived <see cref="BaseComponent.UInt16s"/>, BOOLEAN-derived <see cref="BaseComponent.Booleans"/>,
        /// FONT-derived <see cref="BaseComponent.Fonts"/>,
        /// ARGB_COLOR-derived <see cref="BaseComponent.Colours"/>,
        /// and BITMAP-derived <see cref="BaseComponent.Images"/> from the result (including nested tag blocks).
        /// Call once per component parse.
        /// </summary>
        protected ExtendedTagParser.ParseResult ParseExtendedTags(
            T component,
            ComponentTagMap componentTagMap,
            byte[] data,
            long offset,
            ExtendedTagParser.Options options = null)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (componentTagMap is null) throw new ArgumentNullException(nameof(componentTagMap));
            if (data is null) throw new ArgumentNullException(nameof(data));

            ExtendedTagParser.ParseResult parseResult =
                new ExtendedTagParser().Parse(componentTagMap, data, offset, options);
            AssignStringsFromParseResult(component, parseResult);
            AssignFloatsFromParseResult(component, parseResult);
            AssignUInt32sFromParseResult(component, parseResult);
            AssignInt32sFromParseResult(component, parseResult);
            AssignUInt16sFromParseResult(component, parseResult);
            AssignBooleansFromParseResult(component, parseResult);
            AssignBytesFromParseResult(component, parseResult);
            AssignFontsFromParseResult(component, parseResult);
            AssignColoursFromParseResult(component, parseResult);
            AssignBitmapEntriesFromParseResult(component, parseResult, componentTagMap);
            ApplyNestedTagBlockOrientation(component, parseResult);
            return parseResult;
        }

        /// <summary>
        /// Maps a parsed <c>4C ?? 00</c> nested-tag opener to <see cref="BaseComponent.Orientation"/>.
        /// When no opener was encountered, <see cref="BaseComponent.Orientation"/> remains unset (null).
        /// </summary>
        protected static void ApplyNestedTagBlockOrientation(
            BaseComponent component,
            ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            if (!parseResult.NestedTagBlockOrientationByte.HasValue)
            {
                return;
            }

            component.Orientation = ExtendedTagParser.ResolveNestedTagBlockOrientation(
                parseResult.NestedTagBlockOrientationByte.Value);
        }

        protected void AssignStringsFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignStrings(component, parseResult.StringsByAttributeName);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignStrings(component, parseResult.NestedTagBlockResult.StringsByAttributeName);
            }
        }

        protected void AssignFloatsFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignFloats(component, parseResult.FloatsByAttributeName);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignFloats(component, parseResult.NestedTagBlockResult.FloatsByAttributeName);
            }
        }

        protected void AssignUInt32sFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignUInt32s(component, parseResult.UInt32sByAttributeName);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignUInt32s(component, parseResult.NestedTagBlockResult.UInt32sByAttributeName);
            }
        }

        protected void AssignInt32sFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignInt32s(component, parseResult.Int32sByAttributeName);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignInt32s(component, parseResult.NestedTagBlockResult.Int32sByAttributeName);
            }
        }

        protected void AssignUInt16sFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignUInt16s(component, parseResult.UInt16sByAttributeName);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignUInt16s(component, parseResult.NestedTagBlockResult.UInt16sByAttributeName);
            }
        }

        protected void AssignBooleansFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignBooleans(component, parseResult.BooleansByAttributeName);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignBooleans(component, parseResult.NestedTagBlockResult.BooleansByAttributeName);
            }
        }

        protected void AssignBytesFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignBytes(component, parseResult.BytesByAttributeName);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignBytes(component, parseResult.NestedTagBlockResult.BytesByAttributeName);
            }
        }

        protected void AssignFontsFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignFonts(component, parseResult.FontsByRole);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignFonts(component, parseResult.NestedTagBlockResult.FontsByRole);
            }
        }

        protected void AssignColoursFromParseResult(T component, ExtendedTagParser.ParseResult parseResult)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignColours(component, parseResult.ColoursByAttributeName);
            if (parseResult.NestedTagBlockResult is not null)
            {
                AssignColours(component, parseResult.NestedTagBlockResult.ColoursByAttributeName);
            }
        }

        private static void AssignStrings(T component, IReadOnlyDictionary<string, string> stringsByAttributeName)
        {
            if (stringsByAttributeName is null || stringsByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in stringsByAttributeName)
            {
                if (component.Strings.TryGetValue(kvp.Key, out string existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting string key '{kvp.Key}': existing value '{existing}', new value '{kvp.Value}'.");
                }

                component.Strings[kvp.Key] = kvp.Value;
            }
        }

        private static void AssignFloats(T component, IReadOnlyDictionary<string, float> floatsByAttributeName)
        {
            if (floatsByAttributeName is null || floatsByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in floatsByAttributeName)
            {
                if (component.Floats.TryGetValue(kvp.Key, out float existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting Float key '{kvp.Key}': existing value '{existing}', new value '{kvp.Value}'.");
                }

                component.Floats[kvp.Key] = kvp.Value;
            }
        }

        private static void AssignUInt32s(T component, IReadOnlyDictionary<string, uint> uint32sByAttributeName)
        {
            if (uint32sByAttributeName is null || uint32sByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in uint32sByAttributeName)
            {
                if (component.UInt32s.TryGetValue(kvp.Key, out uint existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting UInt32 key '{kvp.Key}': existing value '{existing}', new value '{kvp.Value}'.");
                }

                component.UInt32s[kvp.Key] = kvp.Value;
            }
        }

        private static void AssignInt32s(T component, IReadOnlyDictionary<string, int> int32sByAttributeName)
        {
            if (int32sByAttributeName is null || int32sByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in int32sByAttributeName)
            {
                if (component.Int32s.TryGetValue(kvp.Key, out int existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting Int32 key '{kvp.Key}': existing value '{existing}', new value '{kvp.Value}'.");
                }

                component.Int32s[kvp.Key] = kvp.Value;
            }
        }

        private static void AssignUInt16s(T component, IReadOnlyDictionary<string, ushort> uint16sByAttributeName)
        {
            if (uint16sByAttributeName is null || uint16sByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in uint16sByAttributeName)
            {
                if (component.UInt16s.TryGetValue(kvp.Key, out ushort existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting UInt16 key '{kvp.Key}': existing value '{existing}', new value '{kvp.Value}'.");
                }

                component.UInt16s[kvp.Key] = kvp.Value;
            }
        }

        private static void AssignBooleans(T component, IReadOnlyDictionary<string, bool> booleansByAttributeName)
        {
            if (booleansByAttributeName is null || booleansByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in booleansByAttributeName)
            {
                if (component.Booleans.TryGetValue(kvp.Key, out bool existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting Boolean key '{kvp.Key}': existing value '{existing}', new value '{kvp.Value}'.");
                }

                component.Booleans[kvp.Key] = kvp.Value;
            }
        }

        private static void AssignBytes(T component, IReadOnlyDictionary<string, byte> bytesByAttributeName)
        {
            if (bytesByAttributeName is null || bytesByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in bytesByAttributeName)
            {
                if (component.Bytes.TryGetValue(kvp.Key, out byte existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting Byte key '{kvp.Key}': existing value '{existing}', new value '{kvp.Value}'.");
                }

                component.Bytes[kvp.Key] = kvp.Value;
            }
        }

        private static void AssignFonts(T component, IReadOnlyDictionary<string, FontTagEntry> fontsByAttributeName)
        {
            if (fontsByAttributeName is null || fontsByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in fontsByAttributeName)
            {
                if (component.Fonts.TryGetValue(kvp.Key, out FontTagEntry existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting font key '{kvp.Key}': existing role '{existing.Role}', new role '{kvp.Value.Role}'.");
                }

                component.Fonts[kvp.Key] = kvp.Value;
            }
        }

        protected static void AssignColours(T component, IReadOnlyDictionary<string, string> coloursByAttributeName)
        {
            if (coloursByAttributeName is null || coloursByAttributeName.Count == 0)
            {
                return;
            }

            foreach (var kvp in coloursByAttributeName)
            {
                if (component.Colours.TryGetValue(kvp.Key, out string existing))
                {
                    throw new InvalidOperationException(
                        $"Conflicting colour key '{kvp.Key}': existing value '{existing}', new value '{kvp.Value}'.");
                }

                component.Colours[kvp.Key] = kvp.Value;
            }
        }

        protected static void OverrideColour(T component, string attributeName, string value)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (string.IsNullOrWhiteSpace(attributeName)) throw new ArgumentException("Attribute name is required.", nameof(attributeName));
            if (value is null) throw new ArgumentNullException(nameof(value));

            component.Colours[attributeName] = value;
        }

        protected static void ApplyMappedStringTags(
            T component,
            IReadOnlyDictionary<uint, object> valuesByTag,
            IReadOnlyDictionary<uint, Action<T, string>> setterByTag)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (valuesByTag is null) throw new ArgumentNullException(nameof(valuesByTag));
            if (setterByTag is null) throw new ArgumentNullException(nameof(setterByTag));

            foreach (var mappedSetter in setterByTag)
            {
                if (valuesByTag.TryGetValue(mappedSetter.Key, out object value) && value is string mappedValue)
                {
                    mappedSetter.Value(component, mappedValue);
                }
            }
        }

        protected static void ApplyMappedBooleanTags(
            T component,
            IReadOnlyDictionary<uint, object> valuesByTag,
            IReadOnlyDictionary<uint, Action<T, bool>> setterByTag)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (valuesByTag is null) throw new ArgumentNullException(nameof(valuesByTag));
            if (setterByTag is null) throw new ArgumentNullException(nameof(setterByTag));

            foreach (var mappedSetter in setterByTag)
            {
                if (valuesByTag.TryGetValue(mappedSetter.Key, out object value) && value is bool boolValue)
                {
                    mappedSetter.Value(component, boolValue);
                }
                else if (valuesByTag.TryGetValue(mappedSetter.Key, out value) && value is byte byteValue)
                {
                    // Keep compatibility with any legacy/raw boolean-like tags.
                    mappedSetter.Value(component, byteValue != 0);
                }
            }
        }

        private static readonly HashSet<ValueRole> AutoMappableRoles = new HashSet<ValueRole>
        {
            ValueRole.UINT32,
            ValueRole.INT32,
            ValueRole.UINT16,
            ValueRole.BOOLEAN,
            ValueRole.BYTE,
            ValueRole.FLOAT,
            ValueRole.TEXT,
            ValueRole.ARGB_COLOR,
        };

        private static readonly ConcurrentDictionary<string, PropertyInfo> PropertyCache = new();

        /// <summary>
        /// Automatically applies parsed tag values to component properties by matching
        /// <see cref="TagInfo.AttributeName"/> to property names on <typeparamref name="T"/>.
        /// Only processes roles in <see cref="AutoMappableRoles"/>; complex roles (BITMAP, FONT,
        /// LAMP_SUBLAMP_TABLE, etc.) are skipped and should be handled explicitly.
        /// </summary>
        protected static void ApplyExtendedTagsByReflection(
            T component,
            ExtendedTagParser.ParseResult parseResult,
            ComponentTagMap componentTagMap)
        {
            IReadOnlyDictionary<uint, object> valuesByTag = parseResult.ValuesByTag;
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (valuesByTag is null) throw new ArgumentNullException(nameof(valuesByTag));
            if (componentTagMap is null) throw new ArgumentNullException(nameof(componentTagMap));

            var componentType = typeof(T);

            foreach (var (tag, tagInfo) in componentTagMap)
            {
                if (!AutoMappableRoles.Contains(tagInfo.Role))
                    continue;

                if (!valuesByTag.TryGetValue(tag, out var value))
                    continue;

                string cacheKey = $"{componentType.FullName}.{tagInfo.AttributeName.Replace(" ","")}";
                var property = PropertyCache.GetOrAdd(cacheKey, _ =>
                {
                    var prop = componentType.GetProperty(tagInfo.AttributeName,
                        BindingFlags.Public | BindingFlags.Instance);
                    return prop != null && prop.CanWrite ? prop : null;
                });

                if (property is null)
                    continue;

                object coerced = CoerceValue(value, property.PropertyType);
                if (coerced is not null)
                {
                    property.SetValue(component, coerced);
                }
            }

            var nestedMap = componentTagMap.NestedTagBlockMap;
            var nestedResult = parseResult.NestedTagBlockResult;
            if (nestedMap is not null && nestedResult?.ValuesByTag is not null)
            {
                foreach (var (tag, tagInfo) in nestedMap)
                {
                    if (!AutoMappableRoles.Contains(tagInfo.Role))
                        continue;

                    if (!nestedResult.ValuesByTag.TryGetValue(tag, out var value))
                        continue;

                    string cacheKey = $"{componentType.FullName}.{tagInfo.AttributeName.Replace(" ","")}";
                    var property = PropertyCache.GetOrAdd(cacheKey, _ =>
                    {
                        var prop = componentType.GetProperty(tagInfo.AttributeName,
                            BindingFlags.Public | BindingFlags.Instance);
                        return prop != null && prop.CanWrite ? prop : null;
                    });

                    if (property is null)
                        continue;

                    object nestedCoerced = CoerceValue(value, property.PropertyType);
                    if (nestedCoerced is not null)
                    {
                        property.SetValue(component, nestedCoerced);
                    }
                }
            }
        }

        private static object CoerceValue(object value, Type targetType)
        {
            if (value is null)
                return null;

            var valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
                return value;

            if (targetType == typeof(bool))
            {
                if (value is byte b) return b != 0;
                if (value is uint u) return u != 0;
                if (value is int i) return i != 0;
            }

            if (targetType == typeof(uint) && value is int intVal)
                return unchecked((uint)intVal);

            if (targetType == typeof(int) && value is uint uintVal)
                return unchecked((int)uintVal);

            try
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        protected static ExtendedTagParser.Options WithComponentTagLogging(ExtendedTagParser.Options options)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            return options.WithTagLogging(typeof(T).Name);
        }

        private static IReadOnlyDictionary<uint, string> BuildBitmapPurposeMapFromComponentTags(ComponentTagMap componentTagMap)
        {
            if (componentTagMap is null || componentTagMap.Count == 0)
            {
                return null;
            }

            Dictionary<uint, string> purposesByTag = null;
            foreach (var (tag, tagInfo) in componentTagMap)
            {
                if (tagInfo is null || tagInfo.Role != ValueRole.BITMAP)
                {
                    continue;
                }

                string derivedPurpose = SlugifyPurpose(tagInfo.AttributeName);
                if (string.IsNullOrWhiteSpace(derivedPurpose))
                {
                    continue;
                }

                purposesByTag ??= new Dictionary<uint, string>();
                purposesByTag[tag] = derivedPurpose;
            }

            return purposesByTag;
        }

        private static string SlugifyPurpose(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            StringBuilder sb = new StringBuilder(source.Length);
            bool prevWasSeparator = false;
            bool prevWasLowerOrDigit = false;

            foreach (char c in source)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (char.IsUpper(c) && prevWasLowerOrDigit && !prevWasSeparator && sb.Length > 0)
                    {
                        sb.Append('_');
                    }

                    sb.Append(char.ToLowerInvariant(c));
                    prevWasSeparator = false;
                    prevWasLowerOrDigit = char.IsLower(c) || char.IsDigit(c);
                    continue;
                }

                if (!prevWasSeparator && sb.Length > 0)
                {
                    sb.Append('_');
                    prevWasSeparator = true;
                }

                prevWasLowerOrDigit = false;
            }

            while (sb.Length > 0 && sb[sb.Length - 1] == '_')
            {
                sb.Length--;
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
    }

    internal static class GeometryAngleNormalization
    {
        private const int StartOffset = 5;
        private const int AngleTagTotalLength = 1 + MfmeAngleWireCodec.WireLength;
        /// <summary>
        /// After tag <c>0x07</c>, copy from this offset: skip the tag and the wire's first byte (<c>01</c>),
        /// preserving the remaining five wire bytes (e.g. <c>36 42 4D …</c>) plus everything after the wire.
        /// </summary>
        private const int RewriteCopyStartOffsetFromTag = 2;
        private static readonly byte[] defaultBytesAfterNormalizedAngle = new byte[] { 0x00 };
        private static readonly byte[] normalizedZeroAngleWire = MfmeAngleWireCodec.AngleToBytes(0.0);

        // Angle validity is decided by MFME six-byte wire round-trip (MfmeAngleWireCodec).
        internal sealed record Rule(
            byte[] RewriteBytesAfterNormalizedAngle = null,
            int RewriteTriggerOffsetDelta = 0,
            int ValidAngleOffsetDelta = 0
        );

        public static (byte[] normalizedData, int offsetAdjustment, int? preferredExtendedOffset, string normalizationModeLog) Normalize(
            byte[] data,
            Rule rule)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            rule ??= new Rule();

            using var stream = new System.IO.MemoryStream();
            if (data.Length <= StartOffset)
            {
                return (EnsureTrailingTerminator(data), 0, null, null);
            }

            stream.Write(data, 0, StartOffset);
            int pos = StartOffset;

            for (int tag = 0x01; tag <= 0x05; tag++)
            {
                if (pos + 5 > data.Length) break;
                stream.Write(data, pos, 5);
                pos += 5;
            }

            if (pos + AngleTagTotalLength <= data.Length && data[pos] == 0x07)
            {
                ReadOnlySpan<byte> wire = data.AsSpan(pos + 1, MfmeAngleWireCodec.WireLength);
                bool isAngleValid = MfmeAngleWireCodec.IsValidWireAngle(wire);
                string angleText = isAngleValid
                    ? MfmeAngleWireCodec.BytesToAngle(wire).ToString(CultureInfo.InvariantCulture)
                    : FormatWireHex(wire);
                if (!isAngleValid)
                {
                    stream.WriteByte(0x07);
                    stream.Write(normalizedZeroAngleWire);

                    byte[] bytesAfterNormalizedAngle =
                        rule.RewriteBytesAfterNormalizedAngle ?? defaultBytesAfterNormalizedAngle;
                    stream.Write(bytesAfterNormalizedAngle);
                    int continuationOffset = checked((int)stream.Position);

                    // Insert zero angle + terminator, then append from wire byte 1 onward so overlapped
                    // bytes (e.g. 36 42 4D) that are not in the last three wire bytes are preserved.
                    // Use RewriteTriggerOffsetDelta to skip any prelude before the real extended TLVs.
                    int copyStart = pos + RewriteCopyStartOffsetFromTag;
                    if (copyStart < data.Length)
                    {
                        stream.Write(data, copyStart, data.Length - copyStart);
                    }

                    byte[] normalizedData = stream.ToArray();
                    int? preferredExtendedOffset = continuationOffset + rule.RewriteTriggerOffsetDelta;
                    string modeLog =
                        $"Normalization mode: rewrite-trigger (wire={angleText}, continuationOffset={continuationOffset}, copyStart={copyStart}, delta={rule.RewriteTriggerOffsetDelta})";
                    return (EnsureTrailingTerminator(normalizedData), 0, preferredExtendedOffset, modeLog);
                }

                // Valid-angle path: six-byte MFME wire round-trips.
                int afterAngleOffset = pos + AngleTagTotalLength;
                if (afterAngleOffset <= data.Length)
                {
                    int anchor;
                    if (afterAngleOffset < data.Length && data[afterAngleOffset] == 0x00)
                    {
                        anchor = afterAngleOffset + 1 + rule.ValidAngleOffsetDelta;
                        while (anchor < data.Length && data[anchor] == 0x00)
                        {
                            anchor++;
                        }
                    }
                    else
                    {
                        // Extended data follows immediately (e.g. 36 42 4D bitmap prelude).
                        anchor = afterAngleOffset + rule.ValidAngleOffsetDelta;
                    }

                    if (anchor >= 0 && anchor <= data.Length)
                    {
                        string modeLog =
                            $"Normalization mode: angle-valid (angle={angleText}, anchor={anchor}, delta={rule.ValidAngleOffsetDelta})";
                        return (EnsureTrailingTerminator(data), 0, anchor, modeLog);
                    }
                }
            }

            stream.Write(data, pos, data.Length - pos);
            return (EnsureTrailingTerminator(stream.ToArray()), 0, null, null);
        }

        private static string FormatWireHex(ReadOnlySpan<byte> wire)
        {
            var sb = new StringBuilder(wire.Length * 3);
            for (int i = 0; i < wire.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(wire[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        private static byte[] EnsureTrailingTerminator(byte[] data)
        {
            if (data is null || data.Length == 0 || data[data.Length - 1] == 0x00)
            {
                return data;
            }

            byte[] withTerminator = new byte[data.Length + 1];
            Array.Copy(data, withTerminator, data.Length);
            withTerminator[withTerminator.Length - 1] = 0x00;
            return withTerminator;
        }
    }
}
