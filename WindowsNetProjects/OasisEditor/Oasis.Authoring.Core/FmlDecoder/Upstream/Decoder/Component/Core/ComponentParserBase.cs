using MfmeFmlDecoder.src.Model.Component;
using MfmeFmlDecoder.Utilities;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace MfmeFmlDecoder.src.Decoder.Component.Core
{
    internal abstract class ComponentParserBase<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>
        where T : BaseComponent, new()
    {
        private static bool ShouldUseV20BaseSectionReader(MFMEComponentType kind, byte[] data)
        {
            if (data is null || data.Length < 36)
            {
                return false;
            }

            // 20.1 base-section signatures found by the full archive scan:
            //   Button:    0x06 before the 0x07 angle tag
            //   DiscReel:  07 01 0A 00 (zero angle, then base 0x0A/0x0B)
            //   AlphaNew:  07 01 36 42 4D (zero angle, then the overlay bitmap)
            switch (kind)
            {
                case MFMEComponentType.Button:
                    return data[30] == 0x06 && data[35] == 0x07;
                case MFMEComponentType.DiscReel:
                    return data[30] == 0x07
                        && data[31] == 0x01
                        && data[32] == 0x0A
                        && data[33] == 0x00;
                case MFMEComponentType.AlphaNew:
                    return data[30] == 0x07
                        && data[31] == 0x01
                        && data[32] == 0x36
                        && data[33] == 0x42
                        && data[34] == 0x4D;
                default:
                    return false;
            }
        }

        private static BaseSectionMode ResolveBaseSectionMode(ComponentProfile profile, byte[] data)
        {
            if (profile.BaseSection.Mode != BaseSectionMode.V20Signature
                && profile.BaseSection.Mode != BaseSectionMode.V20SignatureOrLegacyTwoSection)
            {
                return profile.BaseSection.Mode;
            }

            if (ShouldUseV20BaseSectionReader(profile.Kind, data))
            {
                return BaseSectionMode.V20Signature;
            }

            return profile.BaseSection.Mode == BaseSectionMode.V20Signature
                ? BaseSectionMode.SingleStream
                : BaseSectionMode.LegacyTwoSection;
        }

        protected (T component, long offset, byte[] parseData) ParseBase(
            ParsingContext context,
            ComponentProfile profile,
            long componentOffset,
            uint componentId,
            byte[] data)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            if (data is null) throw new ArgumentNullException(nameof(data));

            // Record layout: 4-byte component id + 4-byte length, then payload in `data`.
            long payloadFileOffset = componentOffset + 8;
            BaseSectionMode mode = ResolveBaseSectionMode(profile, data);

            if (mode != BaseSectionMode.V20Signature)
            {
                GeometryAngleNormalization.Rule normalizationRule =
                    profile.Angle.NormalizationRule ?? new GeometryAngleNormalization.Rule();
                (byte[] normalizedData, int offsetAdjustment, int? preferredExtendedOffset, string normalizationModeLog) =
                    GeometryAngleNormalization.Normalize(data, normalizationRule, profile.Angle.PreListTag6);

                RunLog.WriteLine($"\nProcessing {typeof(T).Name} Component at {componentOffset:X8} (ID: {componentId}, Length: {normalizedData.Length}, payload @ {payloadFileOffset:X8})\n");
                RunLog.WriteLine("Initial hex dump:\n");
                HexDumpUtility.PrintHexDump((uint)payloadFileOffset, normalizedData);
                RunLog.WriteLine("");

                var oldComponent = new T();
                var oldGeometryParser = new GeometryParser();
                long oldOffset = oldGeometryParser.ParseInto(
                    oldComponent,
                    normalizedData,
                    offset: 5,
                    validAngleTrailingSkipBytes: profile.Angle.ValidAngleTrailingSkipBytes).Offset;
                oldOffset += offsetAdjustment;
                if (preferredExtendedOffset.HasValue)
                {
                    oldOffset = preferredExtendedOffset.Value;
                }
                oldOffset += profile.Angle.FinalOffsetAdjustment;
                RunLog.WriteLine($"\nFinal offset position: {oldOffset} (index into payload; file position {payloadFileOffset + oldOffset:X8})");
                if (!string.IsNullOrWhiteSpace(normalizationModeLog))
                {
                    RunLog.WriteLine(normalizationModeLog);
                }

                DumpRemaining(componentOffset, normalizedData, oldOffset);

                return (oldComponent, oldOffset, normalizedData);
            }

            byte[] parseData = data;

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
                validAngleTrailingSkipBytes: 0,
                v20BaseSectionMode: true).Offset;
            RunLog.WriteLine($"\nBase section starts at payload index {offset} (file position {payloadFileOffset + offset:X8})");

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
            ComponentTagMap componentTagMap,
            bool clearExisting = true)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (parseResult is null) throw new ArgumentNullException(nameof(parseResult));

            AssignBitmapEntriesByTag(
                component,
                parseResult,
                componentTagMap,
                clearExisting);
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
            ComponentProfile profile,
            byte[] data,
            long offset)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            if (profile.Tags is null) throw new ArgumentNullException(nameof(profile.Tags));
            if (data is null) throw new ArgumentNullException(nameof(data));

            ComponentTagMap componentTagMap = profile.EffectiveClassTags;
            ExtendedTagParser.Options options = profile.Options ?? ExtendedTagParser.Options.Default;
            BaseSectionMode mode = ResolveBaseSectionMode(profile, data);

            if (mode == BaseSectionMode.SingleStream)
            {
                ExtendedTagParser.ParseResult legacyResult =
                    new ExtendedTagParser().Parse(componentTagMap, data, offset, options);
                AssignParseResult(component, legacyResult, componentTagMap, clearExisting: true);
                ApplyProfileDefaults(component, profile);
                return legacyResult;
            }

            if (mode == BaseSectionMode.LegacyTwoSection)
            {
                ExtendedTagParser.ParseResult twoSectionResult =
                    ParseLegacyTwoSections(component, profile, data, offset);
                ApplyProfileDefaults(component, profile);
                return twoSectionResult;
            }

            // Types with a nested 4C block keep the existing single-stream parse: the 4C opens
            // their own-section nested map, not a base-section terminator.
            if (componentTagMap.NestedTagBlockMap is not null)
            {
                ExtendedTagParser.ParseResult fullResult =
                    new ExtendedTagParser().Parse(componentTagMap, data, offset, options);
                AssignParseResult(component, fullResult, componentTagMap, clearExisting: true);
                ApplyProfileDefaults(component, profile);
                return fullResult;
            }

            BaseSectionTagMapBuilder.BaseSectionTagMap baseTagMap =
                BaseSectionTagMapBuilder.Build(componentTagMap);
            ExtendedTagParser.ParseResult baseResult = null;
            long ownSectionOffset = offset;

            try
            {
                baseResult = new ExtendedTagParser().Parse(
                    baseTagMap.Map,
                    data,
                    offset,
                    options.WithBaseSectionParse());
                ownSectionOffset = baseResult.Offset;
            }
            catch (InvalidOperationException ex)
            {
                RunLog.WriteDiagnosticLine(
                    $"Base section parse failed at payload index {offset} (0x{offset:X}); " +
                    $"falling back to the type map at that offset. {ex.Message}");
                baseResult = null;
                ownSectionOffset = offset;
            }

            ExtendedTagParser.ParseResult ownResult =
                new ExtendedTagParser().Parse(componentTagMap, data, ownSectionOffset, options);

            AssignParseResult(component, ownResult, componentTagMap, clearExisting: true);
            if (baseResult is not null)
            {
                AssignParseResult(component, baseResult, baseTagMap.Map, clearExisting: false);
            }

            ApplyProfileDefaults(component, profile);
            return MergeParseResults(ownResult, baseResult, baseTagMap.TypeMappedTags);
        }

        private ExtendedTagParser.ParseResult ParseLegacyTwoSections(
            T component,
            ComponentProfile profile,
            byte[] data,
            long offset)
        {
            ComponentTagMap firstTags = profile.EffectiveBaseTags;
            ComponentTagMap secondTags = profile.BaseSection.SecondSectionTags ?? firstTags;
            ExtendedTagParser.Options options = profile.Options ?? ExtendedTagParser.Options.Default;

            ExtendedTagParser.ParseResult firstResult =
                new ExtendedTagParser().Parse(firstTags, data, offset, options, stopAtUnknownTag: true);

            bool hasSecondSection = firstResult.Offset < data.Length;
            if (!hasSecondSection && ReferenceEquals(firstTags, secondTags))
            {
                AssignParseResult(component, firstResult, firstTags, clearExisting: true);
                return firstResult;
            }

            ExtendedTagParser.ParseResult secondResult;
            try
            {
                secondResult = hasSecondSection
                    ? new ExtendedTagParser().Parse(
                        secondTags,
                        data,
                        firstResult.Offset,
                        options,
                        defaultSuppressionTags: ReferenceEquals(firstTags, secondTags)
                            ? firstResult.ExplicitTags
                            : null,
                        stopAtUnknownTag: true)
                    : new ExtendedTagParser().Parse(
                        secondTags,
                        new byte[] { 0x00 },
                        0,
                        options);
            }
            catch (Exception ex) when (ex is ComponentTagParseException || ex is InvalidOperationException)
            {
                RunLog.WriteDiagnosticLine(
                    $"Legacy second section parse failed at payload index {firstResult.Offset} " +
                    $"(0x{firstResult.Offset:X}); keeping the first section. {ex.Message}");
                AssignParseResult(component, firstResult, firstTags, clearExisting: true);
                return firstResult;
            }

            AssignParseResult(component, firstResult, firstTags, clearExisting: true);
            AssignParseResult(component, secondResult, secondTags, clearExisting: false);
            return MergeLegacySections(firstResult, secondResult, firstTags, secondTags);
        }

        private static ExtendedTagParser.ParseResult MergeLegacySections(
            ExtendedTagParser.ParseResult first,
            ExtendedTagParser.ParseResult second,
            ComponentTagMap firstTags,
            ComponentTagMap secondTags)
        {
            var mergedValuesByTag = new Dictionary<uint, object>(first.ValuesByTag);
            foreach (var kvp in second.ValuesByTag)
            {
                if (firstTags.TryGetValue(kvp.Key, out TagInfo firstInfo)
                    && (firstInfo.Role == ValueRole.LAMP_SUBLAMP_TABLE
                        || firstInfo.Role == ValueRole.SUBLAMP_COUNT
                        || (firstInfo.Role != ValueRole.BITMAP
                            && secondTags.TryGetValue(kvp.Key, out TagInfo secondInfo)
                            && secondInfo.Role == ValueRole.BITMAP)))
                {
                    continue;
                }
                mergedValuesByTag[kvp.Key] = kvp.Value;
            }

            return second with
            {
                ValuesByTag = mergedValuesByTag,
                FontsByRole = MergePreferSecond(first.FontsByRole, second.FontsByRole),
                StringsByAttributeName = MergePreferSecond(first.StringsByAttributeName, second.StringsByAttributeName),
                FloatsByAttributeName = MergePreferSecond(first.FloatsByAttributeName, second.FloatsByAttributeName),
                UInt32sByAttributeName = MergePreferSecond(first.UInt32sByAttributeName, second.UInt32sByAttributeName),
                Int32sByAttributeName = MergePreferSecond(first.Int32sByAttributeName, second.Int32sByAttributeName),
                UInt16sByAttributeName = MergePreferSecond(first.UInt16sByAttributeName, second.UInt16sByAttributeName),
                BooleansByAttributeName = MergePreferSecond(first.BooleansByAttributeName, second.BooleansByAttributeName),
                BytesByAttributeName = MergePreferSecond(first.BytesByAttributeName, second.BytesByAttributeName),
                ColoursByAttributeName = MergePreferSecond(first.ColoursByAttributeName, second.ColoursByAttributeName),
                BitmapBlobsByTag = MergePreferSecond(first.BitmapBlobsByTag, second.BitmapBlobsByTag),
                DirectBitmapBlobsByTag = MergePreferSecond(first.DirectBitmapBlobsByTag, second.DirectBitmapBlobsByTag),
                FlagDropdownSelectionsByName = MergePreferSecond(first.FlagDropdownSelectionsByName, second.FlagDropdownSelectionsByName),
                NestedTagBlockResult = second.NestedTagBlockResult ?? first.NestedTagBlockResult,
                NestedTagBlockOrientationByte =
                    second.NestedTagBlockOrientationByte ?? first.NestedTagBlockOrientationByte,
                BaseSectionValuesByTag = first.ValuesByTag,
                BaseSectionExplicitTags = first.ExplicitTags,
            };
        }

        private static void ApplyDictionaryDefaults(T component, DefaultsProfile defaults)
        {
            if (defaults.Values is null || defaults.Values.Count == 0)
            {
                return;
            }

            foreach (var kvp in defaults.Values)
            {
                switch (kvp.Value)
                {
                    case uint uintValue:
                        component.UInt32s.TryAdd(kvp.Key, uintValue);
                        break;
                    case int intValue:
                        component.Int32s.TryAdd(kvp.Key, intValue);
                        break;
                    case bool boolValue:
                        component.Booleans.TryAdd(kvp.Key, boolValue);
                        break;
                    case byte byteValue:
                        component.Bytes.TryAdd(kvp.Key, byteValue);
                        break;
                    case float floatValue:
                        component.Floats.TryAdd(kvp.Key, floatValue);
                        break;
                    case string stringValue:
                        component.Strings.TryAdd(kvp.Key, stringValue);
                        break;
                }
            }
        }

        private static void ApplyProfileDefaults(T component, ComponentProfile profile)
        {
            if (profile.Defaults is null)
            {
                return;
            }

            ApplyDictionaryDefaults(component, profile.Defaults);

            if (component.Orientation is not null && !profile.Defaults.OverrideOrientation)
            {
                return;
            }

            component.Orientation = profile.Defaults.Orientation switch
            {
                OrientationDefaultMode.Vertical => "Vertical",
                OrientationDefaultMode.Horizontal => "Horizontal",
                OrientationDefaultMode.ReelAspect => component.Width >= component.Height
                    ? "Horizontal"
                    : "Vertical",
                _ => null,
            };

            if (component.Orientation is null
                && profile.Defaults.TryGetValue("Orientation", out object orientation)
                && orientation is string orientationText)
            {
                component.Orientation = orientationText;
            }
        }

        private void AssignParseResult(
            T component,
            ExtendedTagParser.ParseResult parseResult,
            ComponentTagMap componentTagMap,
            bool clearExisting)
        {
            if (!clearExisting)
            {
                RemoveParseResultKeys(component, parseResult);
            }

            OasisSourcePresence.Record(component, componentTagMap, parseResult);
            AssignStringsFromParseResult(component, parseResult);
            AssignFloatsFromParseResult(component, parseResult);
            AssignUInt32sFromParseResult(component, parseResult);
            AssignInt32sFromParseResult(component, parseResult);
            AssignUInt16sFromParseResult(component, parseResult);
            AssignBooleansFromParseResult(component, parseResult);
            AssignBytesFromParseResult(component, parseResult);
            AssignFontsFromParseResult(component, parseResult);
            AssignColoursFromParseResult(component, parseResult);
            AssignBitmapEntriesFromParseResult(component, parseResult, componentTagMap, clearExisting);
            ApplyNestedTagBlockOrientation(component, parseResult);
        }

        private static void RemoveParseResultKeys(
            T component,
            ExtendedTagParser.ParseResult parseResult)
        {
            foreach (string key in parseResult.StringsByAttributeName.Keys) component.Strings.Remove(key);
            foreach (string key in parseResult.FloatsByAttributeName.Keys) component.Floats.Remove(key);
            foreach (string key in parseResult.UInt32sByAttributeName.Keys) component.UInt32s.Remove(key);
            foreach (string key in parseResult.Int32sByAttributeName.Keys) component.Int32s.Remove(key);
            foreach (string key in parseResult.UInt16sByAttributeName.Keys) component.UInt16s.Remove(key);
            foreach (string key in parseResult.BooleansByAttributeName.Keys) component.Booleans.Remove(key);
            foreach (string key in parseResult.BytesByAttributeName.Keys) component.Bytes.Remove(key);
            foreach (string key in parseResult.FontsByRole.Keys) component.Fonts.Remove(key);
            foreach (string key in parseResult.ColoursByAttributeName.Keys) component.Colours.Remove(key);
        }

        private static ExtendedTagParser.ParseResult MergeParseResults(
            ExtendedTagParser.ParseResult ownResult,
            ExtendedTagParser.ParseResult baseResult,
            IReadOnlySet<uint> baseTypeMappedTags)
        {
            if (baseResult is null)
            {
                return ownResult;
            }

            var mergedValuesByTag = new Dictionary<uint, object>(ownResult.ValuesByTag);
            foreach (var kvp in baseResult.ValuesByTag)
            {
                if (baseTypeMappedTags.Contains(kvp.Key) || !mergedValuesByTag.ContainsKey(kvp.Key))
                {
                    mergedValuesByTag[kvp.Key] = kvp.Value;
                }
            }

            return ownResult with
            {
                ValuesByTag = mergedValuesByTag,
                FontsByRole = MergePreferSecond(ownResult.FontsByRole, baseResult.FontsByRole),
                StringsByAttributeName = MergePreferSecond(ownResult.StringsByAttributeName, baseResult.StringsByAttributeName),
                FloatsByAttributeName = MergePreferSecond(ownResult.FloatsByAttributeName, baseResult.FloatsByAttributeName),
                UInt32sByAttributeName = MergePreferSecond(ownResult.UInt32sByAttributeName, baseResult.UInt32sByAttributeName),
                Int32sByAttributeName = MergePreferSecond(ownResult.Int32sByAttributeName, baseResult.Int32sByAttributeName),
                UInt16sByAttributeName = MergePreferSecond(ownResult.UInt16sByAttributeName, baseResult.UInt16sByAttributeName),
                BooleansByAttributeName = MergePreferSecond(ownResult.BooleansByAttributeName, baseResult.BooleansByAttributeName),
                BytesByAttributeName = MergePreferSecond(ownResult.BytesByAttributeName, baseResult.BytesByAttributeName),
                ColoursByAttributeName = MergePreferSecond(ownResult.ColoursByAttributeName, baseResult.ColoursByAttributeName),
                BitmapBlobsByTag = MergePreferSecond(ownResult.BitmapBlobsByTag, baseResult.BitmapBlobsByTag),
                DirectBitmapBlobsByTag = MergePreferSecond(ownResult.DirectBitmapBlobsByTag, baseResult.DirectBitmapBlobsByTag),
                FlagDropdownSelectionsByName = MergePreferSecond(ownResult.FlagDropdownSelectionsByName, baseResult.FlagDropdownSelectionsByName),
                NestedTagBlockOrientationByte =
                    baseResult.NestedTagBlockOrientationByte ?? ownResult.NestedTagBlockOrientationByte,
            };
        }

        private static IReadOnlyDictionary<TKey, TValue> MergePreferSecond<TKey, TValue>(
            IReadOnlyDictionary<TKey, TValue> first,
            IReadOnlyDictionary<TKey, TValue> second)
        {
            var merged = new Dictionary<TKey, TValue>(first);
            foreach (var kvp in second)
            {
                merged[kvp.Key] = kvp.Value;
            }

            return merged;
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
            Rule rule,
            bool preListTag6 = false)
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

            if (preListTag6 && pos + 5 <= data.Length && data[pos] == 0x06)
            {
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
