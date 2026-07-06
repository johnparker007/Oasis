using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using MfmeFmlDecoder.Utilities;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MfmeFmlDecoder.src.Decoder.Component.Core
{
    internal sealed class ExtendedTagParser
    {
        private const uint NestedTagBlockHeaderTag = 0x00004C;
        private const int NestedTagBlockKeyLengthBytes = 3;

        /// <summary>
        /// <c>4C ?? 00</c> opens a nested tag block: a scoped TLV section with its own <see cref="ComponentTagMap"/>
        /// and <c>0x00</c> terminator. When the outer <see cref="ComponentTagMap.NestedTagBlockMap"/> is set, inner
        /// tags are parsed recursively and stored in <see cref="ParseResult.NestedTagBlockResult"/>.
        /// The middle byte (<c>??</c>) is exposed as <see cref="ParseResult.NestedTagBlockOrientationByte"/> for
        /// all components, including those without a mapped nested block.
        /// </summary>
        private static readonly TagInfo NestedTagBlockTagInfo =
            new TagInfo(0x00, "Nested Tag Block (4C ?? 00)", Array.Empty<byte>(), ValueRole.NESTED_TAG_BLOCK);

        internal sealed record Options(
            IReadOnlySet<uint> BitmapTagIds,
            IReadOnlyDictionary<string, FlagDropdownDefinition> FlagDropdownsByName,
            bool LogMatchedTags,
            string LogMatchedTagsPrefix,
            bool IsNestedTagBlockParse = false
        )
        {
            /// <summary>Baseline parse options. <see cref="LogMatchedTags"/> is enabled so TLV tags are traced unless explicitly turned off.</summary>
            public static readonly Options Default = new Options(
                new HashSet<uint>(),
                new Dictionary<string, FlagDropdownDefinition>(StringComparer.Ordinal),
                true,
                string.Empty
            );

            public Options(params uint[] bitmapTagIds)
                : this(
                    new HashSet<uint>(bitmapTagIds ?? Array.Empty<uint>()),
                    new Dictionary<string, FlagDropdownDefinition>(StringComparer.Ordinal),
                    true,
                    string.Empty
                )
            {
            }

            public static Options WithBitmapTags(params uint[] bitmapTagIds) => new Options(bitmapTagIds);

            public Options WithTagLogging(string logPrefix = null)
                => this with
                {
                    LogMatchedTags = true,
                    LogMatchedTagsPrefix = logPrefix ?? string.Empty
                };

            public Options WithoutMatchedTagLogging() => this with { LogMatchedTags = false };

            /// <summary>Marks recursive parse of inner TLV inside a <c>4C ?? 00</c> nested tag block (distinct diagnostic wording).</summary>
            public Options WithNestedTagBlockParseContext() => this with { IsNestedTagBlockParse = true };

            public Options WithFlagDropdown(
                string dropdownName,
                string defaultOption,
                IReadOnlyDictionary<uint, string> optionByTag)
            {
                if (string.IsNullOrWhiteSpace(dropdownName)) throw new ArgumentException("Dropdown name is required.", nameof(dropdownName));
                if (string.IsNullOrWhiteSpace(defaultOption)) throw new ArgumentException("Default option is required.", nameof(defaultOption));
                if (optionByTag is null || optionByTag.Count == 0)
                {
                    throw new ArgumentException("At least one tag mapping is required.", nameof(optionByTag));
                }

                Dictionary<string, FlagDropdownDefinition> flagDropdowns = new(
                    FlagDropdownsByName ?? new Dictionary<string, FlagDropdownDefinition>(StringComparer.Ordinal),
                    StringComparer.Ordinal);
                flagDropdowns[dropdownName] = new FlagDropdownDefinition(defaultOption, optionByTag);
                return this with { FlagDropdownsByName = flagDropdowns };
            }
        }

        internal sealed record ParseResult(
            long Offset,
            IReadOnlyDictionary<uint, object> ValuesByTag,
            IReadOnlyDictionary<string, FontTagEntry> FontsByRole,
            IReadOnlyDictionary<string, string> StringsByAttributeName,
            IReadOnlyDictionary<string, float> FloatsByAttributeName,
            IReadOnlyDictionary<string, uint> UInt32sByAttributeName,
            IReadOnlyDictionary<string, int> Int32sByAttributeName,
            IReadOnlyDictionary<string, ushort> UInt16sByAttributeName,
            IReadOnlyDictionary<string, bool> BooleansByAttributeName,
            IReadOnlyDictionary<string, byte> BytesByAttributeName,
            IReadOnlyDictionary<string, string> ColoursByAttributeName,
            IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> BitmapBlobsByTag,
            IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> DirectBitmapBlobsByTag,
            IReadOnlyDictionary<string, string> FlagDropdownSelectionsByName,
            ParseResult NestedTagBlockResult = null,
            byte? NestedTagBlockOrientationByte = null
        );

        internal static string ResolveNestedTagBlockOrientation(byte orientationByte)
        {
            return orientationByte switch
            {
                0x00 => "Vertical",
                0x01 => "Horizontal",
                _ => throw new InvalidOperationException(
                    $"Unsupported nested tag block orientation byte 0x{orientationByte:X2}."),
            };
        }

        internal sealed record FlagDropdownDefinition(
            string DefaultOption,
            IReadOnlyDictionary<uint, string> OptionByTag
        );

        public ParseResult Parse(ComponentTagMap componentTagMap, byte[] data, long offset, Options options = null)
        {
            if (componentTagMap is null) throw new ArgumentNullException(nameof(componentTagMap));
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

            options ??= Options.Default;
            Dictionary<uint, object> valuesByTag = new();
            Dictionary<string, FontTagEntry> fontsByRole = new(StringComparer.Ordinal);
            Dictionary<string, string> stringsByAttributeName = new(StringComparer.Ordinal);
            Dictionary<string, float> floatsByAttributeName = new(StringComparer.Ordinal);
            Dictionary<string, uint> uint32sByAttributeName = new(StringComparer.Ordinal);
            Dictionary<string, int> int32sByAttributeName = new(StringComparer.Ordinal);
            Dictionary<string, ushort> uint16sByAttributeName = new(StringComparer.Ordinal);
            Dictionary<string, bool> booleansByAttributeName = new(StringComparer.Ordinal);
            Dictionary<string, byte> bytesByAttributeName = new(StringComparer.Ordinal);
            Dictionary<string, string> coloursByAttributeName = new(StringComparer.Ordinal);
            Dictionary<uint, List<byte[]>> bitmapBlobsByTag = new();
            Dictionary<uint, List<byte[]>> directBitmapBlobsByTag = new();
            HashSet<uint> encounteredTags = new();
            ParseResult nestedTagBlockResult = null;
            byte? nestedTagBlockOrientationByte = null;
            long extendedSectionStart = offset;

            while (offset < data.Length)
            {
                if (data[offset] == 0x00)
                {
                    if (offset == extendedSectionStart)
                    {
                        LogExtendedParseHint(
                            options,
                            "Extended TLV: first byte is 0x00 (terminator), so there are no extended tags at the current offset. " +
                            "If you expected tags, the geometry phase likely stopped one byte too late or the layout uses a different boundary.");
                    }

                    offset++;
                    IReadOnlyDictionary<string, string> flagDropdownSelectionsByName =
                        ResolveFlagDropdownSelections(options, encounteredTags, valuesByTag);
                    ApplyDefaultsFromMap(
                        componentTagMap,
                        valuesByTag,
                        floatsByAttributeName,
                        uint32sByAttributeName,
                        int32sByAttributeName,
                        uint16sByAttributeName,
                        booleansByAttributeName,
                        bytesByAttributeName,
                        coloursByAttributeName);
                    return new ParseResult(
                        offset,
                        valuesByTag,
                        fontsByRole,
                        stringsByAttributeName,
                        floatsByAttributeName,
                        uint32sByAttributeName,
                        int32sByAttributeName,
                        uint16sByAttributeName,
                        booleansByAttributeName,
                        bytesByAttributeName,
                        coloursByAttributeName,
                        ToReadOnlyBitmapBlobMap(bitmapBlobsByTag),
                        ToReadOnlyBitmapBlobMap(directBitmapBlobsByTag),
                        flagDropdownSelectionsByName,
                        nestedTagBlockResult,
                        nestedTagBlockOrientationByte);
                }

                if (!TryReadNestedTagBlockHeader(data, offset, out uint tag, out TagInfo tagInfo, out int tagKeyLengthBytes)
                    && !TryReadTag(componentTagMap, data, offset, out tag, out tagInfo, out tagKeyLengthBytes))
                {
                    bool nothingMatchedYet = offset == extendedSectionStart && valuesByTag.Count == 0 && fontsByRole.Count == 0
                        && bitmapBlobsByTag.Count == 0 && directBitmapBlobsByTag.Count == 0;
                    string tailHint = nothingMatchedYet
                        ? options.IsNestedTagBlockParse
                            ? "Nothing matched yet — check that the nested tag block inner offset is correct (inner TLV begins immediately after the 4C ?? 00 header). "
                            : "Nothing matched yet — check that the extended section offset is correct (wrong alignment here usually means the geometry reader skipped or duplicated a byte before the first TLV key). "
                        : string.Empty;
                    string tlvScopeLabel = options.IsNestedTagBlockParse ? "Nested extended TLV" : "Extended TLV";
                    LogExtendedParseHint(
                        options,
                        $"{tlvScopeLabel}: cannot decode tag key at payload index {offset} (0x{offset:X}). {tailHint}" +
                        $"Already parsed {valuesByTag.Count} scalar/boolean/etc. value(s), {fontsByRole.Count} font role(s).");
                    LogTagKeyDecodeContext(options, data, offset);

                    throw new ComponentTagParseException(
                        options.LogMatchedTagsPrefix,
                        options.IsNestedTagBlockParse,
                        offset,
                        data,
                        valuesByTag.Count,
                        fontsByRole.Count,
                        tailHint);
                }
                offset += tagKeyLengthBytes;

                int length = tagInfo.Length;
                if (tagInfo.Role == ValueRole.NESTED_TAG_BLOCK)
                {
                    long valueAfterKey = offset;

                    if (!options.IsNestedTagBlockParse)
                    {
                        nestedTagBlockOrientationByte = data[checked((int)offset - tagKeyLengthBytes + 1)];
                    }

                    StoreEmbeddedNestedTagBlockValue(componentTagMap, data, offset, tagKeyLengthBytes, valuesByTag, encounteredTags);

                    if (componentTagMap.NestedTagBlockMap is not null)
                    {
                        long nestedTagOffset = offset - tagKeyLengthBytes;
                        LogMatchedTag(options, tag, tagInfo, tagOffset: nestedTagOffset, valueOffset: valueAfterKey, length: 0);

                        var nestedResult = new ExtendedTagParser().Parse(
                            componentTagMap.NestedTagBlockMap, data, valueAfterKey, options.WithNestedTagBlockParseContext());
                        nestedTagBlockResult = nestedResult;
                        offset = nestedResult.Offset;
                    }
                    else
                    {
                        LogMatchedTag(options, tag, tagInfo, tagOffset: offset - tagKeyLengthBytes, valueOffset: valueAfterKey, length: 0);
                        offset = valueAfterKey;
                    }
                    continue;
                }
                if (IsBitmapTag(tagInfo))
                {
                    length = ReadBitmapLength(data, offset);
                }
                else if (IsFontTag(tagInfo))
                {
                    length = ReadFontLength(data, offset);
                }
                else if (tagInfo.Role == ValueRole.TEXT)
                {
                    int valueOffset = checked((int)offset);
                    long tagKeyStartOffset = valueOffset - tagKeyLengthBytes;
                    length = ReadDelphiUtf16WideStringTlvLength(data, valueOffset, tagKeyStartOffset);

                    if (length <= 0)
                    {
                        throw new InvalidOperationException(
                            $"TEXT tag at value offset 0x{offset:X} needs a positive payload span.");
                    }
                }
                else if (tagInfo.Role == ValueRole.TLVBLOCK)
                {
                    int valueOffset = checked((int)offset);
                    byte hostTagKeyByte = GetTagKeyFirstByte(data, offset - tagKeyLengthBytes, tagKeyLengthBytes, tag);
                    length = ReadTlvBlockLength(data, valueOffset, hostTagKeyByte);
                }
                else if (tagInfo.Role == ValueRole.LAMP_SUBLAMP_TABLE)
                {
                    length = ReadLampSublampTablePayloadLength(componentTagMap, valuesByTag);
                }
                else if (tagInfo.Role == ValueRole.PROGRAMMABLE_LAMP_TABLE)
                {
                    length = ReadProgrammableLampTablePayloadLength(componentTagMap, valuesByTag);
                }
                if (length < 0) throw new InvalidOperationException($"Invalid tag length {length} for tag 0x{tag:X2}.");
                if (offset + length > data.Length)
                {
                    throw new InvalidOperationException(
                        $"Extended tag 0x{tag:X2} expects {length} bytes but only {data.Length - offset} remain."
                    );
                }

                LogMatchedTag(options, tag, tagInfo, tagOffset: offset - tagKeyLengthBytes, valueOffset: offset, length: length);

                object value = tagInfo.Role == ValueRole.TEXT
                    ? ParseDelphiUtf16WideStringTlv(
                        data,
                        checked((int)offset),
                        length,
                        tagKeyByteOffset: offset - tagKeyLengthBytes)
                    : ParseValue(
                        tagInfo.Role,
                        data,
                        offset,
                        length,
                        hostTagKeyByte: GetTagKeyFirstByte(data, offset - tagKeyLengthBytes, tagKeyLengthBytes, tag));
                if (IsFontTag(tagInfo))
                {
                    if (value is not byte[] fontBytes)
                    {
                        throw new InvalidOperationException($"Font tag 0x{tag:X2} did not parse as byte[] at offset 0x{offset:X}.");
                    }

                    FontTagEntry font = FontTagParser<BaseComponent>.ParseFontBlob(
                        tag,
                        tagInfo.AttributeName,
                        fontBytes,
                        0,
                        fontBytes.Length);
                    AddFontByAttributeName(fontsByRole, tagInfo.AttributeName, font, tag);
                }
                if (tagInfo.Role == ValueRole.TEXT)
                {
                    if (value is not string textValue)
                    {
                        throw new InvalidOperationException(
                            $"TEXT tag 0x{tag:X2} ({tagInfo.AttributeName}) did not parse as string at offset 0x{offset:X}.");
                    }

                    AddStringByAttributeName(stringsByAttributeName, tagInfo.AttributeName, textValue, tag);
                }
                if (tagInfo.Role == ValueRole.FLOAT)
                {
                    if (value is not float floatValue)
                    {
                        throw new InvalidOperationException(
                            $"FLOAT tag 0x{tag:X2} ({tagInfo.AttributeName}) did not parse as float at offset 0x{offset:X}.");
                    }

                    AddFloatByAttributeName(floatsByAttributeName, tagInfo.AttributeName, floatValue, tag);
                }
                if (tagInfo.Role == ValueRole.ARGB_COLOR)
                {
                    if (value is not string colourValue)
                    {
                        throw new InvalidOperationException(
                            $"ARGB_COLOR tag 0x{tag:X2} ({tagInfo.AttributeName}) did not parse as string at offset 0x{offset:X}.");
                    }

                    AddColourByAttributeName(coloursByAttributeName, tagInfo.AttributeName, colourValue, tag);
                }
                if (tagInfo.Role == ValueRole.UINT32)
                {
                    if (value is not uint uint32Value)
                    {
                        throw new InvalidOperationException(
                            $"UINT32 tag 0x{tag:X2} ({tagInfo.AttributeName}) did not parse as uint at offset 0x{offset:X}.");
                    }

                    AddUInt32ByAttributeName(uint32sByAttributeName, tagInfo.AttributeName, uint32Value, tag);
                }
                if (tagInfo.Role == ValueRole.INT32)
                {
                    if (value is not int int32Value)
                    {
                        throw new InvalidOperationException(
                            $"INT32 tag 0x{tag:X2} ({tagInfo.AttributeName}) did not parse as int at offset 0x{offset:X}.");
                    }

                    AddInt32ByAttributeName(int32sByAttributeName, tagInfo.AttributeName, int32Value, tag);
                }
                if (tagInfo.Role == ValueRole.UINT16)
                {
                    if (value is not ushort uint16Value)
                    {
                        throw new InvalidOperationException(
                            $"UINT16 tag 0x{tag:X2} ({tagInfo.AttributeName}) did not parse as ushort at offset 0x{offset:X}.");
                    }

                    AddUInt16ByAttributeName(uint16sByAttributeName, tagInfo.AttributeName, uint16Value, tag);
                }
                if (tagInfo.Role == ValueRole.BOOLEAN)
                {
                    if (value is not bool booleanValue)
                    {
                        throw new InvalidOperationException(
                            $"BOOLEAN tag 0x{tag:X2} ({tagInfo.AttributeName}) did not parse as bool at offset 0x{offset:X}.");
                    }

                    AddBooleanByAttributeName(booleansByAttributeName, tagInfo.AttributeName, booleanValue, tag);
                }
                if (tagInfo.Role == ValueRole.BYTE)
                {
                    if (value is not byte byteValue)
                    {
                        throw new InvalidOperationException(
                            $"BYTE tag 0x{tag:X2} ({tagInfo.AttributeName}) did not parse as byte at offset 0x{offset:X}.");
                    }

                    AddByteByAttributeName(bytesByAttributeName, tagInfo.AttributeName, byteValue, tag);
                }
                AddValue(
                    valuesByTag,
                    tag,
                    tagInfo,
                    value,
                    offset - tagKeyLengthBytes,
                    bitmapBlobsByTag,
                    directBitmapBlobsByTag);
                encounteredTags.Add(tag);
                offset += length;
            }

            // Normal extended TLV ends with an explicit 0x00 after the last tag. Some MFME component blobs
            // end immediately after the final value (no trailing delimiter byte); offset lands on EOF.
            if (offset == data.Length)
            {
                IReadOnlyDictionary<string, string> flagDropdownSelectionsByName =
                    ResolveFlagDropdownSelections(options, encounteredTags, valuesByTag);
                ApplyDefaultsFromMap(
                    componentTagMap,
                    valuesByTag,
                    floatsByAttributeName,
                    uint32sByAttributeName,
                    int32sByAttributeName,
                    uint16sByAttributeName,
                    booleansByAttributeName,
                    bytesByAttributeName,
                    coloursByAttributeName);
                return new ParseResult(
                    offset,
                    valuesByTag,
                    fontsByRole,
                    stringsByAttributeName,
                    floatsByAttributeName,
                    uint32sByAttributeName,
                    int32sByAttributeName,
                    uint16sByAttributeName,
                    booleansByAttributeName,
                    bytesByAttributeName,
                    coloursByAttributeName,
                    ToReadOnlyBitmapBlobMap(bitmapBlobsByTag),
                    ToReadOnlyBitmapBlobMap(directBitmapBlobsByTag),
                    flagDropdownSelectionsByName,
                    nestedTagBlockResult,
                    nestedTagBlockOrientationByte);
            }

            throw new InvalidOperationException("Extended tag section did not terminate with 0x00.");
        }

        public static IReadOnlyList<byte[]> GetBitmapBlobsOrEmpty(IReadOnlyDictionary<uint, object> valuesByTag, uint bitmapTagId)
        {
            if (valuesByTag is null) throw new ArgumentNullException(nameof(valuesByTag));

            if (!valuesByTag.TryGetValue(bitmapTagId, out var value) || value is null)
            {
                return Array.Empty<byte[]>();
            }

            if (value is byte[] single)
            {
                return new[] { single };
            }

            if (value is List<byte[]> list)
            {
                return list;
            }

            if (value is IEnumerable<byte[]> enumerable)
            {
                // Allow future shapes while keeping a consistent output shape.
                return enumerable.ToArray();
            }

            throw new InvalidOperationException(
                $"Bitmap tag 0x{bitmapTagId:X2} had unexpected stored type {value.GetType().Name}.");
        }

        /// <summary>
        /// Variable-length bitmap handling is driven by the active <see cref="ComponentTagMap"/> entry,
        /// so outer and nested TLV scopes can reuse the same tag id with different roles.
        /// </summary>
        private static bool IsBitmapTag(TagInfo tagInfo) => tagInfo?.Role == ValueRole.BITMAP;

        /// <summary>
        /// Variable-length font handling is driven by the active <see cref="ComponentTagMap"/> entry.
        /// Parsed fonts are keyed by <see cref="TagInfo.AttributeName"/>, like TEXT and ARGB_COLOR tags.
        /// </summary>
        private static bool IsFontTag(TagInfo tagInfo) => tagInfo?.Role == ValueRole.FONT;

        private static void LogMatchedTag(
            Options options,
            uint tag,
            TagInfo tagInfo,
            long tagOffset,
            long valueOffset,
            int length)
        {
            if (options is null || !options.LogMatchedTags || RunLog.Quiet)
            {
                return;
            }

            string prefix = string.IsNullOrWhiteSpace(options.LogMatchedTagsPrefix)
                ? string.Empty
                : $"[{options.LogMatchedTagsPrefix}] ";
            string name = tagInfo?.AttributeName ?? "<unknown>";
            RunLog.WriteDiagnosticLine($"{prefix}Matched tag 0x{tag:X2} ({name}) @ tagOffset={tagOffset}, valueOffset={valueOffset}, length={length}");
        }

        private static void LogExtendedParseHint(Options options, string message)
        {
            if (options is null || !options.LogMatchedTags || RunLog.Quiet)
            {
                return;
            }

            string prefix = string.IsNullOrWhiteSpace(options.LogMatchedTagsPrefix)
                ? string.Empty
                : $"[{options.LogMatchedTagsPrefix}] ";
            RunLog.WriteDiagnosticLine($"{prefix}{message}");
        }

        private static void LogTagKeyDecodeContext(Options options, byte[] data, long offset)
        {
            if (options is null || !options.LogMatchedTags || RunLog.Quiet)
            {
                return;
            }

            string prefix = string.IsNullOrWhiteSpace(options.LogMatchedTagsPrefix)
                ? string.Empty
                : $"[{options.LogMatchedTagsPrefix}] ";
            RunLog.WriteDiagnosticLine(
                $"{prefix}Next 10 bytes at payload index {offset} (0x{offset:X}): " +
                $"{HexDumpUtility.FormatNextBytesHex(data, offset, count: 10)}");
            RunLog.WriteDiagnosticLine(
                $"{prefix}Context hex dump (100 bytes around payload index {offset} (0x{offset:X})):");
            HexDumpUtility.PrintHexDumpWindow(data, offset, windowBytes: 100);
        }

        private static void LogTlvBlockOverflowContext(
            byte[] data,
            int lengthPrefixOffset,
            uint payloadLength,
            int blockEndExclusive,
            int? tlvBlockValueOffset = null)
        {
            RunLog.WriteDiagnosticLine(
                $"TLVBLOCK overflow context: payloadLength=0x{payloadLength:X8} ({payloadLength}), " +
                $"length prefix at 0x{lengthPrefixOffset:X}, block end at 0x{blockEndExclusive:X}, " +
                $"buffer length={data.Length}.");
            if (tlvBlockValueOffset.HasValue)
            {
                RunLog.WriteDiagnosticLine($"TLVBLOCK value starts at 0x{tlvBlockValueOffset.Value:X}.");
            }

            RunLog.WriteDiagnosticLine(
                $"16 bytes at length prefix (0x{lengthPrefixOffset:X}): " +
                $"{HexDumpUtility.FormatNextBytesHex(data, lengthPrefixOffset, count: 16)}");
            RunLog.WriteDiagnosticLine(
                $"Hex dump (100 bytes around length prefix 0x{lengthPrefixOffset:X}):");
            HexDumpUtility.WriteDiagnosticHexDumpWindow(data, lengthPrefixOffset, windowBytes: 100);
        }

        /// <summary>
        /// Byte after <c>C0 82 78</c> is a declared payload size; TLV often extends beyond that â€” see <see cref="TryMeasureInnerExtendedPayloadLength"/>.
        /// </summary>
        private static bool TryReadMfme3BDeclaredEnvelopeLength(byte[] data, int valueOffset, out int envelopeLengthBytes)
        {
            envelopeLengthBytes = 0;
            if (valueOffset < 0 || valueOffset + 4 > data.Length)
            {
                return false;
            }

            if (data[valueOffset + 0] != 0xC0 || data[valueOffset + 1] != 0x82 || data[valueOffset + 2] != 0x78)
            {
                return false;
            }

            int declaredPayload = data[valueOffset + 3];
            const int maxPayload = 1_048_576;
            if (declaredPayload > maxPayload || valueOffset + 4 + declaredPayload > data.Length)
            {
                return false;
            }

            envelopeLengthBytes = 4 + declaredPayload;
            return true;
        }

        /// <summary>
        /// Resolved byte span for MFME Delphi <c>C0 82 78 [len]</c> envelopes. Uses <see cref="TryMeasureInnerExtendedPayloadLength"/> so TLV that
        /// extends past the declared length byte (seen on AceMatrix) is fully captured.
        /// </summary>
        private static bool TryReadMfme3BMetadataEnvelopeValueLength(
            ComponentTagMap componentTagMap,
            byte[] data,
            int valueOffset,
            Options options,
            out int envelopeLengthBytes)
        {
            envelopeLengthBytes = 0;
            if (!TryReadMfme3BDeclaredEnvelopeLength(data, valueOffset, out int declaredEnvelope))
            {
                return false;
            }

            int declaredInner = data[valueOffset + 3];
            int innerStart = valueOffset + 4;
            const int slackPastDeclared = 256;
            int scanEndExclusive = Math.Min(data.Length, innerStart + declaredInner + slackPastDeclared);

            if (!TryMeasureInnerExtendedPayloadLength(
                    componentTagMap,
                    data,
                    innerStart,
                    scanEndExclusive,
                    options ?? Options.Default,
                    mfmeNested3BUseDeclaredOnly: true,
                    out int measuredInner))
            {
                envelopeLengthBytes = declaredEnvelope;
                return true;
            }

            int mergedInner = Math.Max(declaredInner, measuredInner);
            int candidateEnvelope = 4 + mergedInner;
            int availableFromTag = data.Length - valueOffset;
            envelopeLengthBytes = Math.Min(candidateEnvelope, availableFromTag);
            if (envelopeLengthBytes <= 4)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Advances through inner extended TLV from <paramref name="innerPayloadStart"/> (first byte after <c>C0 82 78</c>-length byte) until either
        /// a <c>0x00</c> terminator is seen at tag alignment, or no further tag maps. Returns outer span length (exclusive of terminator <c>0x00</c>).
        /// </summary>
        private static bool TryMeasureInnerExtendedPayloadLength(
            ComponentTagMap componentTagMap,
            byte[] data,
            int innerPayloadStart,
            int scanEndExclusive,
            Options options,
            bool mfmeNested3BUseDeclaredOnly,
            out int innerPayloadSpanBytes)
        {
            innerPayloadSpanBytes = 0;
            if (innerPayloadStart < 0 || innerPayloadStart >= data.Length || scanEndExclusive <= innerPayloadStart)
            {
                return false;
            }

            scanEndExclusive = Math.Min(scanEndExclusive, data.Length);
            Dictionary<uint, List<byte[]>> bitmapScratch = new();
            long o = innerPayloadStart;

            try
            {
                while (o < scanEndExclusive)
                {
                    if (data[o] == 0x00)
                    {
                        innerPayloadSpanBytes = checked((int)(o - innerPayloadStart));
                        return true;
                    }

                    uint tag;
                    TagInfo tagInfo;
                    int tagKeyLengthBytes;
                    bool readTagged =
                        TryReadNestedTagBlockHeader(data, o, out tag, out tagInfo, out tagKeyLengthBytes) ||
                        TryReadTag(componentTagMap, data, o, out tag, out tagInfo, out tagKeyLengthBytes);
                    if (!readTagged)
                    {
                        innerPayloadSpanBytes = checked((int)(o - innerPayloadStart));
                        return innerPayloadSpanBytes > 0 || o != innerPayloadStart;
                    }

                    long beforeKeyEnd = o;
                    o += tagKeyLengthBytes;
                    if (o > scanEndExclusive)
                    {
                        return false;
                    }

                    long valueStart = o;
                    int length = tagInfo.Length;
                    try
                    {
                        if (tagInfo.Role == ValueRole.NESTED_TAG_BLOCK)
                        {
                            int scanPos = checked((int)valueStart);
                            while (scanPos < scanEndExclusive && data[scanPos] != 0x00)
                            {
                                scanPos++;
                            }
                            if (scanPos < scanEndExclusive) scanPos++;
                            length = scanPos - checked((int)valueStart);
                        }
                        else if (IsBitmapTag(tagInfo))
                        {
                            length = ReadBitmapLength(data, valueStart);
                        }
                        else if (IsFontTag(tagInfo))
                        {
                            length = ReadFontLength(data, valueStart);
                        }
                        else if (tagInfo.Role == ValueRole.TEXT)
                        {
                            int vo = checked((int)valueStart);
                            length = ReadDelphiUtf16WideStringTlvLength(data, vo, beforeKeyEnd);
                        }
                        else if (tagInfo.Role == ValueRole.TLVBLOCK)
                        {
                            int vo = checked((int)valueStart);
                            byte hostTagKeyByte = GetTagKeyFirstByte(data, beforeKeyEnd, tagKeyLengthBytes, tag);
                            length = ReadTlvBlockLength(data, vo, hostTagKeyByte, scanEndExclusive);
                        }
                        else if (tagInfo.Role == ValueRole.LAMP_SUBLAMP_TABLE)
                        {
                            return false;
                        }
                        else if (tagInfo.Role == ValueRole.PROGRAMMABLE_LAMP_TABLE)
                        {
                            return false;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        innerPayloadSpanBytes = checked((int)(beforeKeyEnd - innerPayloadStart));
                        return innerPayloadSpanBytes > 0;
                    }

                    if (length < 0 || o + length > scanEndExclusive)
                    {
                        return false;
                    }

                    o += length;
                }

                innerPayloadSpanBytes = checked((int)(o - innerPayloadStart));
                return innerPayloadSpanBytes > 0;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// TLV bytes after header <c>C0 82 78 [len]</c>. Uses the full envelope tail (<c>envelopeValue.Length - 4</c>) so TLV is not clipped when
        /// <paramref name="envelopeValue"/>[3] is shorter than the real payload (seen on AceMatrix: tag <c>06</c> extends past declared length).
        /// </summary>
        private static bool TryGetMfme3BEnvelopeInnerPayload(byte[] envelopeValue, out byte[] innerPayload)
        {
            innerPayload = null;
            if (envelopeValue is null || envelopeValue.Length < 5)
            {
                return false;
            }

            if (envelopeValue[0] != 0xC0 || envelopeValue[1] != 0x82 || envelopeValue[2] != 0x78)
            {
                return false;
            }

            int innerLen = envelopeValue.Length - 4;
            innerPayload = new byte[innerLen];
            Buffer.BlockCopy(envelopeValue, 4, innerPayload, 0, innerLen);
            return true;
        }

        /// <summary>
        /// Parses <paramref name="envelopeValue"/> inner TLV bytes and merges them into the parent extended-tag dictionaries.
        /// Returns <c>false</c> when the envelope is not in the recognised form or nested parsing fails â€” caller may store raw bytes.
        /// </summary>
        private static bool TryMergeMfme3BInteriorExtendedTags(
            ComponentTagMap componentTagMap,
            byte[] envelopeValue,
            Options options,
            Dictionary<uint, object> valuesByTag,
            Dictionary<uint, List<byte[]>> bitmapBlobsByTag,
            Dictionary<uint, List<byte[]>> directBitmapBlobsByTag,
            Dictionary<string, FontTagEntry> fontsByRole,
            Dictionary<string, string> stringsByAttributeName,
            Dictionary<string, float> floatsByAttributeName,
            Dictionary<string, uint> uint32sByAttributeName,
            Dictionary<string, int> int32sByAttributeName,
            Dictionary<string, ushort> uint16sByAttributeName,
            Dictionary<string, bool> booleansByAttributeName,
            Dictionary<string, byte> bytesByAttributeName,
            Dictionary<string, string> coloursByAttributeName,
            HashSet<uint> encounteredTags)
        {
            if (!TryGetMfme3BEnvelopeInnerPayload(envelopeValue, out byte[] payload) || payload.Length == 0)
            {
                return false;
            }

            byte[] boundedWithTerminator = new byte[payload.Length + 1];
            Buffer.BlockCopy(payload, 0, boundedWithTerminator, 0, payload.Length);

            ExtendedTagParser nested = new ExtendedTagParser();
            ParseResult nestedResult = nested.Parse(componentTagMap, boundedWithTerminator, offset: 0, options);

            MergeChildExtendedParseInto(
                componentTagMap,
                nestedResult,
                valuesByTag,
                bitmapBlobsByTag,
                directBitmapBlobsByTag,
                fontsByRole,
                stringsByAttributeName,
                floatsByAttributeName,
                uint32sByAttributeName,
                int32sByAttributeName,
                uint16sByAttributeName,
                booleansByAttributeName,
                bytesByAttributeName,
                coloursByAttributeName);

            foreach (var nestedKvp in nestedResult.ValuesByTag)
            {
                encounteredTags.Add(nestedKvp.Key);
            }

            return true;
        }

        private static void MergeChildExtendedParseInto(
            ComponentTagMap componentTagMap,
            ParseResult nested,
            Dictionary<uint, object> valuesByTag,
            Dictionary<uint, List<byte[]>> bitmapBlobsByTag,
            Dictionary<uint, List<byte[]>> directBitmapBlobsByTag,
            Dictionary<string, FontTagEntry> fontsByRole,
            Dictionary<string, string> stringsByAttributeName,
            Dictionary<string, float> floatsByAttributeName,
            Dictionary<string, uint> uint32sByAttributeName,
            Dictionary<string, int> int32sByAttributeName,
            Dictionary<string, ushort> uint16sByAttributeName,
            Dictionary<string, bool> booleansByAttributeName,
            Dictionary<string, byte> bytesByAttributeName,
            Dictionary<string, string> coloursByAttributeName)
        {
            foreach (var nestedKvp in nested.ValuesByTag)
            {
                if (!componentTagMap.TryGetValue(nestedKvp.Key, out TagInfo nestedTagMeta))
                {
                    throw new InvalidOperationException(
                        $"Nested TLV inside 0x3B envelope referenced unknown mapped tag 0x{nestedKvp.Key:X2}.");
                }

                if (nestedTagMeta.Role != ValueRole.BITMAP
                    && nestedTagMeta.Role != ValueRole.FONT
                    && nestedTagMeta.Role != ValueRole.TEXT
                    && nestedTagMeta.Role != ValueRole.ARGB_COLOR
                    && nestedTagMeta.Role != ValueRole.FLOAT
                    && nestedTagMeta.Role != ValueRole.UINT32
                    && nestedTagMeta.Role != ValueRole.INT32
                    && nestedTagMeta.Role != ValueRole.UINT16
                    && nestedTagMeta.Role != ValueRole.BOOLEAN
                    && nestedTagMeta.Role != ValueRole.BYTE
                    && valuesByTag.ContainsKey(nestedKvp.Key))
                {
                    continue;
                }

                if (nestedTagMeta.Role == ValueRole.TEXT
                    && nestedKvp.Value is string nestedTextValue)
                {
                    if (stringsByAttributeName is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot merge nested TEXT tags without a strings dictionary.");
                    }

                    AddStringByAttributeName(
                        stringsByAttributeName,
                        nestedTagMeta.AttributeName,
                        nestedTextValue,
                        nestedKvp.Key);
                }

                if (nestedTagMeta.Role == ValueRole.ARGB_COLOR
                    && nestedKvp.Value is string nestedColourValue)
                {
                    if (coloursByAttributeName is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot merge nested ARGB_COLOR tags without a colours dictionary.");
                    }

                    AddColourByAttributeName(
                        coloursByAttributeName,
                        nestedTagMeta.AttributeName,
                        nestedColourValue,
                        nestedKvp.Key);
                }

                if (nestedTagMeta.Role == ValueRole.FLOAT
                    && nestedKvp.Value is float nestedFloatValue)
                {
                    if (floatsByAttributeName is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot merge nested FLOAT tags without a Floats dictionary.");
                    }

                    AddFloatByAttributeName(
                        floatsByAttributeName,
                        nestedTagMeta.AttributeName,
                        nestedFloatValue,
                        nestedKvp.Key);
                }

                if (nestedTagMeta.Role == ValueRole.UINT32
                    && nestedKvp.Value is uint nestedUInt32Value)
                {
                    if (uint32sByAttributeName is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot merge nested UINT32 tags without a UInt32s dictionary.");
                    }

                    AddUInt32ByAttributeName(
                        uint32sByAttributeName,
                        nestedTagMeta.AttributeName,
                        nestedUInt32Value,
                        nestedKvp.Key);
                }

                if (nestedTagMeta.Role == ValueRole.INT32
                    && nestedKvp.Value is int nestedInt32Value)
                {
                    if (int32sByAttributeName is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot merge nested INT32 tags without an Int32s dictionary.");
                    }

                    AddInt32ByAttributeName(
                        int32sByAttributeName,
                        nestedTagMeta.AttributeName,
                        nestedInt32Value,
                        nestedKvp.Key);
                }

                if (nestedTagMeta.Role == ValueRole.UINT16
                    && nestedKvp.Value is ushort nestedUInt16Value)
                {
                    if (uint16sByAttributeName is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot merge nested UINT16 tags without a UInt16s dictionary.");
                    }

                    AddUInt16ByAttributeName(
                        uint16sByAttributeName,
                        nestedTagMeta.AttributeName,
                        nestedUInt16Value,
                        nestedKvp.Key);
                }

                if (nestedTagMeta.Role == ValueRole.BOOLEAN
                    && nestedKvp.Value is bool nestedBooleanValue)
                {
                    if (booleansByAttributeName is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot merge nested BOOLEAN tags without a Booleans dictionary.");
                    }

                    AddBooleanByAttributeName(
                        booleansByAttributeName,
                        nestedTagMeta.AttributeName,
                        nestedBooleanValue,
                        nestedKvp.Key);
                }

                if (nestedTagMeta.Role == ValueRole.BYTE
                    && nestedKvp.Value is byte nestedByteValue)
                {
                    if (bytesByAttributeName is null)
                    {
                        throw new InvalidOperationException(
                            "Cannot merge nested BYTE tags without a Bytes dictionary.");
                    }

                    AddByteByAttributeName(
                        bytesByAttributeName,
                        nestedTagMeta.AttributeName,
                        nestedByteValue,
                        nestedKvp.Key);
                }

                if (nestedTagMeta.Role == ValueRole.BITMAP && nestedKvp.Value is IEnumerable<byte[]> many && nestedKvp.Value is not byte[])
                {
                    foreach (byte[] blob in many)
                    {
                        AddValue(
                            valuesByTag,
                            nestedKvp.Key,
                            nestedTagMeta,
                            blob,
                            tagOffset: -1,
                            bitmapBlobsByTag,
                            directBitmapBlobsByTag);
                    }

                    continue;
                }

                AddValue(
                    valuesByTag,
                    nestedKvp.Key,
                    nestedTagMeta,
                    nestedKvp.Value,
                    tagOffset: -1,
                    bitmapBlobsByTag,
                    directBitmapBlobsByTag);
            }

            foreach (var kvp in nested.FontsByRole)
            {
                fontsByRole[kvp.Key] = kvp.Value;
            }
        }

        private static void AddStringByAttributeName(
            Dictionary<string, string> stringsByAttributeName,
            string attributeName,
            string value,
            uint? tag)
        {
            if (stringsByAttributeName is null) throw new ArgumentNullException(nameof(stringsByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"TEXT tag 0x{tag.Value:X2} has no attribute name."
                        : "TEXT tag has no attribute name.");
            }

            if (stringsByAttributeName.TryGetValue(attributeName, out string existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting string key '{attributeName}'{tagSuffix}: existing value '{existing}', new value '{value}'.");
            }

            stringsByAttributeName[attributeName] = value;
        }

        private static void AddFloatByAttributeName(
            Dictionary<string, float> floatsByAttributeName,
            string attributeName,
            float value,
            uint? tag)
        {
            if (floatsByAttributeName is null) throw new ArgumentNullException(nameof(floatsByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"FLOAT tag 0x{tag.Value:X2} has no attribute name."
                        : "FLOAT tag has no attribute name.");
            }

            if (attributeName.StartsWith("Unknown", StringComparison.Ordinal))
            {
                return;
            }

            if (floatsByAttributeName.TryGetValue(attributeName, out float existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting Float key '{attributeName}'{tagSuffix}: existing value '{existing}', new value '{value}'.");
            }

            floatsByAttributeName[attributeName] = value;
        }

        private static void AddUInt32ByAttributeName(
            Dictionary<string, uint> uint32sByAttributeName,
            string attributeName,
            uint value,
            uint? tag)
        {
            if (uint32sByAttributeName is null) throw new ArgumentNullException(nameof(uint32sByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"UINT32 tag 0x{tag.Value:X2} has no attribute name."
                        : "UINT32 tag has no attribute name.");
            }

            if (attributeName.StartsWith("Unknown", StringComparison.Ordinal))
            {
                return;
            }

            if (uint32sByAttributeName.TryGetValue(attributeName, out uint existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting UInt32 key '{attributeName}'{tagSuffix}: existing value '{existing}', new value '{value}'.");
            }

            uint32sByAttributeName[attributeName] = value;
        }

        private static void AddInt32ByAttributeName(
            Dictionary<string, int> int32sByAttributeName,
            string attributeName,
            int value,
            uint? tag)
        {
            if (int32sByAttributeName is null) throw new ArgumentNullException(nameof(int32sByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"INT32 tag 0x{tag.Value:X2} has no attribute name."
                        : "INT32 tag has no attribute name.");
            }

            if (attributeName.StartsWith("Unknown", StringComparison.Ordinal))
            {
                return;
            }

            if (int32sByAttributeName.TryGetValue(attributeName, out int existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting Int32 key '{attributeName}'{tagSuffix}: existing value '{existing}', new value '{value}'.");
            }

            int32sByAttributeName[attributeName] = value;
        }

        private static void AddUInt16ByAttributeName(
            Dictionary<string, ushort> uint16sByAttributeName,
            string attributeName,
            ushort value,
            uint? tag)
        {
            if (uint16sByAttributeName is null) throw new ArgumentNullException(nameof(uint16sByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"UINT16 tag 0x{tag.Value:X2} has no attribute name."
                        : "UINT16 tag has no attribute name.");
            }

            if (attributeName.StartsWith("Unknown", StringComparison.Ordinal))
            {
                return;
            }

            if (uint16sByAttributeName.TryGetValue(attributeName, out ushort existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting UInt16 key '{attributeName}'{tagSuffix}: existing value '{existing}', new value '{value}'.");
            }

            uint16sByAttributeName[attributeName] = value;
        }

        private static void AddBooleanByAttributeName(
            Dictionary<string, bool> booleansByAttributeName,
            string attributeName,
            bool value,
            uint? tag)
        {
            if (booleansByAttributeName is null) throw new ArgumentNullException(nameof(booleansByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"BOOLEAN tag 0x{tag.Value:X2} has no attribute name."
                        : "BOOLEAN tag has no attribute name.");
            }

            if (attributeName.StartsWith("Unknown", StringComparison.Ordinal))
            {
                return;
            }

            if (booleansByAttributeName.TryGetValue(attributeName, out bool existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting Boolean key '{attributeName}'{tagSuffix}: existing value '{existing}', new value '{value}'.");
            }

            booleansByAttributeName[attributeName] = value;
        }

        private static void AddByteByAttributeName(
            Dictionary<string, byte> bytesByAttributeName,
            string attributeName,
            byte value,
            uint? tag)
        {
            if (bytesByAttributeName is null) throw new ArgumentNullException(nameof(bytesByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"BYTE tag 0x{tag.Value:X2} has no attribute name."
                        : "BYTE tag has no attribute name.");
            }

            if (attributeName.StartsWith("Unknown", StringComparison.Ordinal))
            {
                return;
            }

            if (bytesByAttributeName.TryGetValue(attributeName, out byte existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting Byte key '{attributeName}'{tagSuffix}: existing value '{existing}', new value '{value}'.");
            }

            bytesByAttributeName[attributeName] = value;
        }

        private static void AddFontByAttributeName(
            Dictionary<string, FontTagEntry> fontsByAttributeName,
            string attributeName,
            FontTagEntry value,
            uint? tag)
        {
            if (fontsByAttributeName is null) throw new ArgumentNullException(nameof(fontsByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"FONT tag 0x{tag.Value:X2} has no attribute name."
                        : "FONT tag has no attribute name.");
            }

            if (fontsByAttributeName.TryGetValue(attributeName, out FontTagEntry existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting font key '{attributeName}'{tagSuffix}: existing role '{existing.Role}', new role '{value.Role}'.");
            }

            fontsByAttributeName[attributeName] = value;
        }

        private static void AddColourByAttributeName(
            Dictionary<string, string> coloursByAttributeName,
            string attributeName,
            string value,
            uint? tag)
        {
            if (coloursByAttributeName is null) throw new ArgumentNullException(nameof(coloursByAttributeName));
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidOperationException(
                    tag.HasValue
                        ? $"ARGB_COLOR tag 0x{tag.Value:X2} has no attribute name."
                        : "ARGB_COLOR tag has no attribute name.");
            }

            if (coloursByAttributeName.TryGetValue(attributeName, out string existing))
            {
                string tagSuffix = tag.HasValue ? $" from tag 0x{tag.Value:X2}" : string.Empty;
                throw new InvalidOperationException(
                    $"Conflicting colour key '{attributeName}'{tagSuffix}: existing value '{existing}', new value '{value}'.");
            }

            coloursByAttributeName[attributeName] = value;
        }

        private static IReadOnlyDictionary<string, string> ResolveFlagDropdownSelections(
            Options options,
            IReadOnlySet<uint> encounteredTags,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if (options?.FlagDropdownsByName is null || options.FlagDropdownsByName.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            Dictionary<string, string> selectedByDropdownName = new(StringComparer.Ordinal);
            foreach (var kvp in options.FlagDropdownsByName)
            {
                string dropdownName = kvp.Key;
                FlagDropdownDefinition definition = kvp.Value;
                if (definition is null) continue;

                string selectedOption = definition.DefaultOption;
                bool hasMatch = false;
                foreach (var optionByTag in definition.OptionByTag)
                {
                    if (!encounteredTags.Contains(optionByTag.Key))
                    {
                        continue;
                    }

                    if (valuesByTag.TryGetValue(optionByTag.Key, out object parsedValue))
                    {
                        // Support "flag based" dropdowns where source tags are boolean-like fields.
                        if (parsedValue is bool boolValue && !boolValue)
                        {
                            continue;
                        }

                        if (parsedValue is byte byteValue && byteValue == 0x00)
                        {
                            continue;
                        }
                    }

                    if (hasMatch)
                    {
                        throw new InvalidOperationException(
                            $"Flag-based dropdown '{dropdownName}' matched multiple tags in the same stream.");
                    }

                    selectedOption = optionByTag.Value;
                    hasMatch = true;
                }

                selectedByDropdownName[dropdownName] = selectedOption;
            }

            return selectedByDropdownName;
        }

        private static bool TryReadTag(
            ComponentTagMap componentTagMap,
            byte[] data,
            long offset,
            out uint tag,
            out TagInfo tagInfo,
            out int tagKeyLengthBytes)
        {
            tag = 0;
            tagInfo = null;
            tagKeyLengthBytes = 0;

            foreach (int keyLength in componentTagMap.GetDistinctKeyLengthsDescending())
            {
                if (offset + keyLength > data.Length) continue;

                uint candidateTag = ReadTagKey(data, offset, keyLength);
                if (!componentTagMap.TryGetValue(candidateTag, keyLength, out TagInfo candidateInfo)) continue;

                tag = candidateTag;
                tagInfo = candidateInfo;
                tagKeyLengthBytes = keyLength;
                return true;
            }

            return false;
        }

        private static uint ReadTagKey(byte[] data, long offset, int keyLengthBytes)
        {
            int i = checked((int)offset);
            return keyLengthBytes switch
            {
                1 => data[i],
                2 => (uint)(data[i] | data[i + 1] << 8),
                3 => (uint)(data[i] | data[i + 1] << 8 | data[i + 2] << 16),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i, 4)),
                _ => throw new InvalidOperationException($"Unsupported tag key length {keyLengthBytes}."),
            };
        }

        /// <summary>
        /// When <c>TryReadNestedTagBlockHeader</c> consumed a 3-byte key whose first byte
        /// also appears in <paramref name="componentTagMap"/> as a 1-byte tag with a value,
        /// extract that embedded value from the key bytes and store it.
        /// </summary>
        private static void StoreEmbeddedNestedTagBlockValue(
            ComponentTagMap componentTagMap,
            byte[] data,
            long offsetAfterKey,
            int tagKeyLengthBytes,
            Dictionary<uint, object> valuesByTag,
            HashSet<uint> encounteredTags)
        {
            if (componentTagMap is null || tagKeyLengthBytes < 2) return;

            uint firstByte = data[checked((int)(offsetAfterKey - tagKeyLengthBytes))];
            if (!componentTagMap.TryGetValue(firstByte, 1, out TagInfo embeddedTagInfo)) return;
            if (embeddedTagInfo.Role == ValueRole.NESTED_TAG_BLOCK) return;
            if (embeddedTagInfo.Length <= 0 || embeddedTagInfo.Length >= tagKeyLengthBytes) return;

            long embeddedValueStart = offsetAfterKey - tagKeyLengthBytes + 1;
            object embeddedValue = ParseValue(embeddedTagInfo.Role, data, embeddedValueStart, embeddedTagInfo.Length);
            valuesByTag[firstByte] = embeddedValue;
            encounteredTags.Add(firstByte);
        }

        private static bool TryReadNestedTagBlockHeader(
            byte[] data,
            long offset,
            out uint tag,
            out TagInfo tagInfo,
            out int tagKeyLengthBytes)
        {
            tag = 0;
            tagInfo = null;
            tagKeyLengthBytes = 0;

            if (offset < 0 || offset + NestedTagBlockKeyLengthBytes > data.Length) return false;

            // Matches [0x4C, ??, 0x00] -- byte[1] carries an embedded tag value
            int i = checked((int)offset);
            if (data[i] != 0x4C || data[i + 2] != 0x00)
            {
                return false;
            }

            tag = NestedTagBlockHeaderTag;
            tagInfo = NestedTagBlockTagInfo;
            tagKeyLengthBytes = NestedTagBlockKeyLengthBytes;
            return true;
        }

        private static void AddValue(
            Dictionary<uint, object> valuesByTag,
            uint tag,
            TagInfo tagInfo,
            object value,
            long tagOffset,
            Dictionary<uint, List<byte[]>> bitmapBlobsByTag,
            Dictionary<uint, List<byte[]>> directBitmapBlobsByTag
        )
        {
            if (tagInfo?.Role == ValueRole.BITMAP || tagInfo?.Role == ValueRole.FONT)
            {
                string valueTypeName = tagInfo.Role == ValueRole.BITMAP ? "Bitmap" : "Font";
                if (value is not byte[] bytes)
                {
                    throw new InvalidOperationException($"{valueTypeName} tag 0x{tag:X2} did not parse as byte[] at offset 0x{tagOffset:X}.");
                }

                if (!valuesByTag.TryGetValue(tag, out var existing))
                {
                    valuesByTag[tag] = new List<byte[]> { bytes };
                    if (tagInfo.Role == ValueRole.BITMAP)
                    {
                        AddBitmapBlob(directBitmapBlobsByTag, tag, bytes);
                    }
                    return;
                }

                if (existing is List<byte[]> list)
                {
                    list.Add(bytes);
                    if (tagInfo.Role == ValueRole.BITMAP)
                    {
                        AddBitmapBlob(directBitmapBlobsByTag, tag, bytes);
                    }
                    return;
                }

                if (existing is byte[] single)
                {
                    valuesByTag[tag] = new List<byte[]> { single, bytes };
                    if (tagInfo.Role == ValueRole.BITMAP)
                    {
                        AddBitmapBlob(directBitmapBlobsByTag, tag, single);
                        AddBitmapBlob(directBitmapBlobsByTag, tag, bytes);
                    }
                    return;
                }

                throw new InvalidOperationException($"{valueTypeName} tag 0x{tag:X2} had unexpected stored type {existing.GetType().Name}.");
            }

            // Non-bitmap tags should not recur; treat as a format error.
            if (valuesByTag.ContainsKey(tag))
            {
                throw new InvalidOperationException($"Extended tag 0x{tag:X2} recurred at offset 0x{tagOffset:X}.");
            }

            valuesByTag[tag] = value;
        }

        private static void AddBitmapBlob(Dictionary<uint, List<byte[]>> bitmapBlobsByTag, uint tag, byte[] blob)
        {
            if (!bitmapBlobsByTag.TryGetValue(tag, out var blobs))
            {
                blobs = new List<byte[]>();
                bitmapBlobsByTag[tag] = blobs;
            }

            blobs.Add(blob);
        }

        private static IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> ToReadOnlyBitmapBlobMap(
            Dictionary<uint, List<byte[]>> bitmapBlobsByTag)
        {
            Dictionary<uint, IReadOnlyList<byte[]>> result = new(bitmapBlobsByTag.Count);
            foreach (var kvp in bitmapBlobsByTag)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }

        // Bitmap values are variable-length; BMP file size is stored at bytes 3..6 (0-based 2..5) of the bitmap value.
        // We use that to advance the extended-tag stream correctly.
        private static int ReadBitmapLength(byte[] data, long offset)
        {
            if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (data.Length - offset < 6)
            {
                throw new InvalidOperationException(
                    $"Bitmap tag at offset 0x{offset:X} requires at least 6 bytes for header, but only {data.Length - offset} remain.");
            }

            int i = checked((int)offset);
            byte b0 = data[i + 0];
            byte b1 = data[i + 1];
            if (b0 != (byte)'B' || b1 != (byte)'M')
            {
                throw new InvalidOperationException(
                    $"Bitmap tag at offset 0x{offset:X} did not start with 'BM' (got 0x{b0:X2} 0x{b1:X2}).");
            }

            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i + 2, 4));
            if (size < 6)
            {
                throw new InvalidOperationException(
                    $"Bitmap tag at offset 0x{offset:X} has invalid BMP size {size} (must be >= 6).");
            }

            if (size > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Bitmap tag at offset 0x{offset:X} has unsupported BMP size {size} (> {int.MaxValue}).");
            }

            return (int)size;
        }

        // Font values are variable-length; encoded as:
        // [nameLen:4][name:nameLen][fontSize:4][scriptStyle:4][padding:1][fontStyle:1]
        // scriptStyle dword: high 24 bits = BGR text colour, low byte = script index
        private static int ReadFontLength(byte[] data, long offset)
        {
            if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            int i = checked((int)offset);
            if (data.Length - i < 4)
            {
                throw new InvalidOperationException(
                    $"Font tag at offset 0x{offset:X} requires at least 4 bytes for name length, but only {data.Length - i} remain.");
            }

            int length = FontTagParser<BaseComponent>.GetFontValueLength(data, i);
            if (length <= 0)
            {
                throw new InvalidOperationException($"Font tag at offset 0x{offset:X} has invalid length {length}.");
            }

            return length;
        }

        public static IReadOnlyList<byte[]> GetFontBlobsOrEmpty(IReadOnlyDictionary<uint, object> valuesByTag, uint fontTagId)
        {
            if (valuesByTag is null) throw new ArgumentNullException(nameof(valuesByTag));

            if (!valuesByTag.TryGetValue(fontTagId, out var value) || value is null)
            {
                return Array.Empty<byte[]>();
            }

            if (value is byte[] single)
            {
                return new[] { single };
            }

            if (value is List<byte[]> list)
            {
                return list;
            }

            if (value is IEnumerable<byte[]> enumerable)
            {
                return enumerable.ToArray();
            }

            throw new InvalidOperationException(
                $"Font tag 0x{fontTagId:X2} had unexpected stored type {value.GetType().Name}.");
        }

        // Tags not present in the stream get a value from DefaultValues (zero-padded to TagInfo.Length) when provided.
        private static void ApplyDefaultsFromMap(
            ComponentTagMap componentTagMap,
            Dictionary<uint, object> valuesByTag,
            Dictionary<string, float> floatsByAttributeName,
            Dictionary<string, uint> uint32sByAttributeName,
            Dictionary<string, int> int32sByAttributeName,
            Dictionary<string, ushort> uint16sByAttributeName,
            Dictionary<string, bool> booleansByAttributeName,
            Dictionary<string, byte> bytesByAttributeName,
            Dictionary<string, string> coloursByAttributeName)
        {
            foreach (var kvp in componentTagMap)
            {
                if (valuesByTag.ContainsKey(kvp.Key)) continue;
                if (!TryGetDefaultValue(kvp.Value, out object defaultValue))
                {
                    continue;
                }

                valuesByTag[kvp.Key] = defaultValue;

                if (kvp.Value.Role == ValueRole.FLOAT
                    && defaultValue is float floatDefault
                    && floatsByAttributeName is not null)
                {
                    AddFloatByAttributeName(
                        floatsByAttributeName,
                        kvp.Value.AttributeName,
                        floatDefault,
                        kvp.Key);
                }

                if (kvp.Value.Role == ValueRole.UINT32
                    && defaultValue is uint uint32Default
                    && uint32sByAttributeName is not null)
                {
                    AddUInt32ByAttributeName(
                        uint32sByAttributeName,
                        kvp.Value.AttributeName,
                        uint32Default,
                        kvp.Key);
                }

                if (kvp.Value.Role == ValueRole.INT32
                    && defaultValue is int int32Default
                    && int32sByAttributeName is not null)
                {
                    AddInt32ByAttributeName(
                        int32sByAttributeName,
                        kvp.Value.AttributeName,
                        int32Default,
                        kvp.Key);
                }

                if (kvp.Value.Role == ValueRole.UINT16
                    && defaultValue is ushort uint16Default
                    && uint16sByAttributeName is not null)
                {
                    AddUInt16ByAttributeName(
                        uint16sByAttributeName,
                        kvp.Value.AttributeName,
                        uint16Default,
                        kvp.Key);
                }

                if (kvp.Value.Role == ValueRole.BOOLEAN
                    && defaultValue is bool booleanDefault
                    && booleansByAttributeName is not null)
                {
                    AddBooleanByAttributeName(
                        booleansByAttributeName,
                        kvp.Value.AttributeName,
                        booleanDefault,
                        kvp.Key);
                }

                if (kvp.Value.Role == ValueRole.BYTE
                    && defaultValue is byte byteDefault
                    && bytesByAttributeName is not null)
                {
                    AddByteByAttributeName(
                        bytesByAttributeName,
                        kvp.Value.AttributeName,
                        byteDefault,
                        kvp.Key);
                }

                if (kvp.Value.Role == ValueRole.ARGB_COLOR
                    && defaultValue is string colourDefault
                    && coloursByAttributeName is not null)
                {
                    AddColourByAttributeName(
                        coloursByAttributeName,
                        kvp.Value.AttributeName,
                        colourDefault,
                        kvp.Key);
                }
            }
        }

        private static bool TryGetDefaultValue(TagInfo tagInfo, out object value)
        {
            value = null;
            if (tagInfo is null) return false;
            int length = tagInfo.Length;
            if (length <= 0) return false;
            byte[] source = tagInfo.DefaultValues;
            if (source is null || source.Length == 0) return false;

            byte[] buffer = new byte[length];
            int copy = Math.Min(source.Length, length);
            Array.Copy(source, 0, buffer, 0, copy);
            // Remainder of buffer is 0; matches missing tail bytes in file for undersized defaults.

            value = ParseValue(tagInfo.Role, buffer, 0, length);
            return true;
        }

        private static object ParseValue(ValueRole role, byte[] data, long offset, int length, byte hostTagKeyByte = 0)
        {
            int i = checked((int)offset);

            return role switch
            {
                ValueRole.UINT16 => ParseUInt16(data, i, length),
                ValueRole.UINT32 => ParseUInt32(data, i, length),
                ValueRole.INT32 => ParseInt32(data, i, length),
                ValueRole.BYTE => ParseByte(data, i, length),
                ValueRole.BOOLEAN => ParseBoolean(data, i, length),
                ValueRole.FLOAT => ParseFloat(data, i, length),
                ValueRole.ARGB_COLOR => ParseArgbColourString(data, i, length),
                ValueRole.SUBLAMP_COUNT => ParseUInt32(data, i, length),
                ValueRole.LAMP_SUBLAMP_TABLE => ParseLampSublampTable(data, i, length),
                ValueRole.PROGRAMMABLE_DIGIT_MASK => CopyRaw(data, i, length),
                ValueRole.DIGIT_FLAG_ARRAY => ParseDigitFlagArray(data, i, length),
                ValueRole.PROGRAMMABLE_LAMP_TABLE => ParseProgrammableLampTable(data, i, length),
                ValueRole.TEXT => ParseDelphiUtf16WideStringTlv(data, i, length, tagKeyByteOffset: -1),
                ValueRole.TLVBLOCK => ParseTlvBlock(data, i, length, hostTagKeyByte),
                _ => CopyRaw(data, i, length),
            };
        }

        private static byte GetTagKeyFirstByte(byte[] data, long tagKeyOffset, int tagKeyLengthBytes, uint tag)
        {
            if (tagKeyLengthBytes <= 0)
            {
                throw new InvalidOperationException("Tag key length must be positive.");
            }

            if (tagKeyLengthBytes == 1)
            {
                return (byte)tag;
            }

            int i = checked((int)tagKeyOffset);
            if (i < 0 || i >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(tagKeyOffset));
            }

            return data[i];
        }

        /// <summary>
        /// Stop scanning inner TLVBLOCK records when the outer TLV stream resumes: EOF, explicit
        /// <c>0x00</c> delimiter, or a tag key byte greater than this block's host tag id.
        /// </summary>
        private static bool ShouldEndTlvBlockScan(byte[] data, int pos, int scanEndExclusive, byte hostTagKeyByte)
        {
            if (pos >= scanEndExclusive)
            {
                return true;
            }

            byte next = data[pos];
            if (next == 0x00)
            {
                return true;
            }

            return next > hostTagKeyByte;
        }

        /// <inheritdoc cref="ValueRole.TLVBLOCK" />
        private static int ReadTlvBlockLength(byte[] data, int valueOffset, byte hostTagKeyByte, int scanEndExclusive = -1)
        {
            checked
            {
                if (data is null) throw new ArgumentNullException(nameof(data));
                if (valueOffset < 0 || valueOffset > data.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(valueOffset));
                }

                if (scanEndExclusive < 0)
                {
                    scanEndExclusive = data.Length;
                }

                if (scanEndExclusive > data.Length)
                {
                    scanEndExclusive = data.Length;
                }

                if (valueOffset > scanEndExclusive)
                {
                    throw new ArgumentOutOfRangeException(nameof(valueOffset));
                }

                int pos = valueOffset;
                while (pos < scanEndExclusive)
                {
                    if (ShouldEndTlvBlockScan(data, pos, scanEndExclusive, hostTagKeyByte))
                    {
                        return pos - valueOffset;
                    }

                    if (pos + 4 > scanEndExclusive)
                    {
                        return pos - valueOffset;
                    }

                    uint payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos, 4));
                    if (payloadLength == 0)
                    {
                        return pos + 4 - valueOffset;
                    }

                    if (payloadLength > int.MaxValue - 4)
                    {
                        throw new InvalidOperationException(
                            $"TLVBLOCK at value offset 0x{valueOffset:X} has unsupported payload length {payloadLength}.");
                    }

                    int tlvRecordBytes = 4 + (int)payloadLength;
                    if (pos + tlvRecordBytes > scanEndExclusive)
                    {
                        LogTlvBlockOverflowContext(
                            data,
                            pos,
                            payloadLength,
                            scanEndExclusive,
                            tlvBlockValueOffset: valueOffset);
                        throw new InvalidOperationException(
                            $"TLVBLOCK TLV payload ({payloadLength} bytes) at offset 0x{pos:X} extends past block end " +
                            $"(block ends at {scanEndExclusive}).");
                    }

                    pos += tlvRecordBytes;
                }

                return pos - valueOffset;
            }
        }

        private static List<byte[]> ParseTlvBlock(byte[] data, int valueOffset, int totalSpanBytes, byte hostTagKeyByte)
        {
            checked
            {
                if (valueOffset < 0 || valueOffset > data.Length - totalSpanBytes)
                {
                    throw new InvalidOperationException(
                        $"TLVBLOCK at payload index {valueOffset}, span={totalSpanBytes}, buffer={data.Length} is out of range.");
                }

                List<byte[]> entries = new();
                int pos = valueOffset;
                int endExclusive = valueOffset + totalSpanBytes;
                while (pos < endExclusive)
                {
                    if (ShouldEndTlvBlockScan(data, pos, endExclusive, hostTagKeyByte))
                    {
                        break;
                    }

                    if (pos + 4 > endExclusive)
                    {
                        break;
                    }

                    uint payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos, 4));
                    if (payloadLength == 0)
                    {
                        break;
                    }

                    if (payloadLength > int.MaxValue - 4)
                    {
                        throw new InvalidOperationException(
                            $"TLVBLOCK at payload index {valueOffset} has unsupported payload length {payloadLength}.");
                    }

                    int payloadBytes = (int)payloadLength;
                    int tlvRecordBytes = 4 + payloadBytes;
                    if (pos + tlvRecordBytes > endExclusive)
                    {
                        LogTlvBlockOverflowContext(
                            data,
                            pos,
                            payloadLength,
                            endExclusive,
                            tlvBlockValueOffset: valueOffset);
                        throw new InvalidOperationException(
                            $"TLVBLOCK TLV payload ({payloadBytes} bytes) at offset 0x{pos:X} extends past measured block span " +
                            $"(span ends at {endExclusive}).");
                    }

                    entries.Add(payloadBytes == 0
                        ? Array.Empty<byte>()
                        : CopyRaw(data, pos + 4, payloadBytes));
                    pos += tlvRecordBytes;
                }

                return entries;
            }
        }

        /// <inheritdoc cref="ValueRole.TEXT" />
        private static int ReadDelphiUtf16WideStringTlvLength(byte[] data, int valueOffset, long tagKeyByteOffset)
        {
            checked
            {
                const uint maxWideChars = 1_000_000;

                if (data is null) throw new ArgumentNullException(nameof(data));
                if (valueOffset < 0 || valueOffset >= data.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(valueOffset));
                }

                if (valueOffset + 4 > data.Length)
                {
                    throw new InvalidOperationException(
                        $"TEXT TLV at value offset 0x{valueOffset:X} needs at least 4 bytes for char count, " +
                        $"but only {data.Length - valueOffset} remain.");
                }

                uint charCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(valueOffset, 4));
                if (charCount > maxWideChars)
                {
                    throw new InvalidOperationException(
                        $"TEXT TLV character count {charCount} exceeds maximum {maxWideChars}.");
                }

                int wcharBytes = (int)(charCount * 2);
                if (valueOffset + 4 + wcharBytes > data.Length)
                {
                    throw new InvalidOperationException(
                        $"TEXT TLV UTF-16 payload ({wcharBytes} bytes) at offset 0x{valueOffset:X} extends past buffer end " +
                        $"(buffer length {data.Length}).");
                }

                return 4 + wcharBytes;
            }
        }

        private static string ParseDelphiUtf16WideStringTlv(byte[] data, int valueOffset, int totalSpanBytes, long tagKeyByteOffset = -1)
        {
            checked
            {
                if (totalSpanBytes < 4)
                {
                    throw new InvalidOperationException(
                        $"TEXT TLV span length must be >= 4, got {totalSpanBytes}.");
                }

                if (valueOffset < 0 || valueOffset > data.Length - totalSpanBytes)
                {
                    throw new InvalidOperationException(
                        $"TEXT TLV at payload index {valueOffset}, span={totalSpanBytes}, buffer={data.Length} is out of range.");
                }

                uint charCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(valueOffset, 4));
                if (charCount == 0)
                {
                    return string.Empty;
                }

                int wcharBytes = (int)(charCount * 2);
                return Encoding.Unicode.GetString(data.AsSpan(valueOffset + 4, wcharBytes));
            }
        }

        private static ushort ParseUInt16(byte[] data, int offset, int length)
        {
            if (length != 2) throw new InvalidOperationException($"UINT16 tag length must be 2, got {length}.");
            return BitConverter.ToUInt16(data, offset);
        }

        private static uint ParseUInt32(byte[] data, int offset, int length)
        {
            if (length != 4) throw new InvalidOperationException($"UINT32 tag length must be 4, got {length}.");
            return BitConverter.ToUInt32(data, offset);
        }

        private static int ParseInt32(byte[] data, int offset, int length)
        {
            if (length != 4) throw new InvalidOperationException($"INT32 tag length must be 4, got {length}.");
            return BitConverter.ToInt32(data, offset);
        }

        private static bool ParseBoolean(byte[] data, int offset, int length)
        {
            return length switch
            {
                1 => data[offset] != 0x00,
                2 => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)) != 0,
                4 => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4)) != 0,
                _ => throw new InvalidOperationException($"BOOLEAN tag length must be 1, 2, or 4, got {length}."),
            };
        }

        private static byte ParseByte(byte[] data, int offset, int length)
        {
            if (length != 1) throw new InvalidOperationException($"BYTE tag length must be 1, got {length}.");
            return data[offset];
        }

        private static float ParseFloat(byte[] data, int offset, int length)
        {
            if (length != 4) throw new InvalidOperationException($"FLOAT tag length must be 4, got {length}.");
            return BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, 4));
        }

        // Input bytes are little-endian ARGB with LSB=B and MSB=Alpha: [B, G, R, A].
        // Output is "#RRGGBBAA" (example: "#556677FF").
        private static string ParseArgbColourString(byte[] data, int offset, int length)
        {
            if (length != 4) throw new InvalidOperationException($"ARGB_Colour tag length must be 4, got {length}.");

            byte b = data[offset + 0];
            byte g = data[offset + 1];
            byte r = data[offset + 2];
            byte a = data[offset + 3];

            return string.Create(
                9,
                (r, g, b, a),
                static (span, c) =>
                {
                    span[0] = '#';
                    WriteHexByte(span.Slice(1, 2), c.r);
                    WriteHexByte(span.Slice(3, 2), c.g);
                    WriteHexByte(span.Slice(5, 2), c.b);
                    WriteHexByte(span.Slice(7, 2), c.a);
                }
            );
        }

        internal static string ArgbLittleEndianBytesToHex(byte[] data, int offset) =>
            ParseArgbColourString(data, offset, 4);

        /// <summary>
        /// Non-throwing wrapper for <see cref="ReadDelphiUtf16WideStringTlvLength"/> (e.g. Label scans raw payload for embedded colours after TEXT).
        /// </summary>
        internal static bool TryMeasureDelphiWideTextValueLength(
            byte[] data,
            int valueOffset,
            long tagKeyByteOffset,
            out int valueLengthBytes)
        {
            valueLengthBytes = 0;
            try
            {
                valueLengthBytes = ReadDelphiUtf16WideStringTlvLength(data, valueOffset, tagKeyByteOffset);
                return valueLengthBytes > 0;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Given a TEXT value span (same bounds as <see cref="TryMeasureDelphiWideTextValueLength"/>),
        /// returns the exclusive offset of the end of the UTF-16 payload.
        /// </summary>
        internal static bool TryGetDelphiWideTextUtf16PayloadEndExclusive(
            byte[] data,
            int valueOffset,
            int valueTotalLengthBytes,
            long tagKeyByteOffset,
            out int utf16PayloadEndExclusive)
        {
            utf16PayloadEndExclusive = 0;
            if (data is null || valueOffset < 0 || valueTotalLengthBytes < 4)
            {
                return false;
            }

            long spanEndLong = (long)valueOffset + valueTotalLengthBytes;
            if (spanEndLong > int.MaxValue || spanEndLong > data.Length)
            {
                return false;
            }

            try
            {
                if (valueOffset + 4 > data.Length) return false;

                uint charCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(valueOffset, 4));
                int wcharBytes = checked((int)(charCount * 2));

                int end = valueOffset + 4 + wcharBytes;
                if (end > data.Length) return false;

                utf16PayloadEndExclusive = end;
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static int ReadLampSublampTablePayloadLength(
            ComponentTagMap componentTagMap,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if (!TryResolveSublampCount(componentTagMap, valuesByTag, out int sublampCount))
            {
                throw new InvalidOperationException(
                    "LAMP_SUBLAMP_TABLE requires a prior SUBLAMP_COUNT value in the same component tag map.");
            }

            return checked(sublampCount * LampSublampTableBytesPerEntry);
        }

        private static bool TryGetTagIdByRole(ComponentTagMap componentTagMap, ValueRole role, out uint tagId)
        {
            foreach (var kvp in componentTagMap)
            {
                if (kvp.Value.Role != role)
                {
                    continue;
                }

                tagId = kvp.Key;
                return true;
            }

            tagId = 0;
            return false;
        }

        private static bool TryResolveSublampCount(
            ComponentTagMap componentTagMap,
            IReadOnlyDictionary<uint, object> valuesByTag,
            out int sublampCount)
        {
            sublampCount = 0;
            if (!TryGetTagIdByRole(componentTagMap, ValueRole.SUBLAMP_COUNT, out uint countTagId))
            {
                return false;
            }

            if (!valuesByTag.TryGetValue(countTagId, out object raw))
            {
                return false;
            }

            uint count = raw switch
            {
                uint u => u,
                int i when i >= 0 => (uint)i,
                _ => throw new InvalidOperationException(
                    $"SUBLAMP_COUNT tag 0x{countTagId:X2} had unexpected stored type {raw.GetType().Name}."),
            };

            sublampCount = checked((int)count);
            return true;
        }

        private const int LampSublampTableBytesPerEntry = 4;

        // Each entry is a 4-byte signed integer, ordered by sublamp index 1..n.
        private static IReadOnlyList<LampSublampTableEntry> ParseLampSublampTable(byte[] data, int offset, int length)
        {
            if (length < 0 || length % LampSublampTableBytesPerEntry != 0)
            {
                throw new InvalidOperationException(
                    $"LAMP_SUBLAMP_TABLE payload length {length} must be a non-negative multiple of {LampSublampTableBytesPerEntry}.");
            }

            int sublampCount = length / LampSublampTableBytesPerEntry;
            List<LampSublampTableEntry> entries = new(sublampCount);
            for (int sublampIndex = 1; sublampIndex <= sublampCount; sublampIndex++)
            {
                int entryOffset = offset + (sublampIndex - 1) * LampSublampTableBytesPerEntry;
                if (entryOffset + LampSublampTableBytesPerEntry > data.Length)
                {
                    throw new InvalidOperationException(
                        $"LAMP_SUBLAMP_TABLE entry {sublampIndex} extends past buffer end.");
                }

                int sublampNumber = BitConverter.ToInt32(data, entryOffset);

                entries.Add(
                    new LampSublampTableEntry(
                        SublampIndex: sublampIndex,
                        SublampNumber: sublampNumber
                    )
                );
            }

            return entries;
        }

        // One-byte boolean per digit (0x00/0x01); the fixed length comes from the mapped TagInfo (typically 48).
        private static bool[] ParseDigitFlagArray(byte[] data, int offset, int length)
        {
            if (length < 0)
            {
                throw new InvalidOperationException($"DIGIT_FLAG_ARRAY length {length} must be non-negative.");
            }

            bool[] flags = new bool[length];
            for (int i = 0; i < length; i++)
            {
                flags[i] = data[offset + i] != 0x00;
            }

            return flags;
        }

        private const int ProgrammableLampsPerDigit = 8;
        private const int ProgrammableLampBytesPerEntry = 4;
        private const int ProgrammableLampBytesPerDigit = ProgrammableLampsPerDigit * ProgrammableLampBytesPerEntry;

        // 0x1B byte span is one fixed 8×4-byte block per digit; digit count comes from the 0x0B mask length.
        private static int ReadProgrammableLampTablePayloadLength(
            ComponentTagMap componentTagMap,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if (!TryResolveProgrammableDigitCount(componentTagMap, valuesByTag, out int digitCount))
            {
                throw new InvalidOperationException(
                    "PROGRAMMABLE_LAMP_TABLE requires a prior PROGRAMMABLE_DIGIT_MASK value in the same component tag map.");
            }

            return checked(digitCount * ProgrammableLampBytesPerDigit);
        }

        private static bool TryResolveProgrammableDigitCount(
            ComponentTagMap componentTagMap,
            IReadOnlyDictionary<uint, object> valuesByTag,
            out int digitCount)
        {
            digitCount = 0;
            if (!TryGetTagIdByRole(componentTagMap, ValueRole.PROGRAMMABLE_DIGIT_MASK, out uint maskTagId))
            {
                return false;
            }

            if (!valuesByTag.TryGetValue(maskTagId, out object raw) || raw is not byte[] mask)
            {
                return false;
            }

            digitCount = mask.Length;
            return true;
        }

        // Every digit stores eight little-endian uint32 lamp numbers. Non-programmable digits are still present,
        // filled with 0xFFFFFFFF sentinels; callers use the PROGRAMMABLE_DIGIT_MASK to decide which blocks are real.
        private static IReadOnlyList<uint[]> ParseProgrammableLampTable(byte[] data, int offset, int length)
        {
            if (length < 0 || length % ProgrammableLampBytesPerDigit != 0)
            {
                throw new InvalidOperationException(
                    $"PROGRAMMABLE_LAMP_TABLE payload length {length} must be a non-negative multiple of {ProgrammableLampBytesPerDigit}.");
            }

            int digitCount = length / ProgrammableLampBytesPerDigit;
            List<uint[]> blocks = new(digitCount);
            for (int digitIndex = 0; digitIndex < digitCount; digitIndex++)
            {
                uint[] lampNumbers = new uint[ProgrammableLampsPerDigit];
                for (int lampIndex = 0; lampIndex < ProgrammableLampsPerDigit; lampIndex++)
                {
                    int entryOffset = offset
                        + digitIndex * ProgrammableLampBytesPerDigit
                        + lampIndex * ProgrammableLampBytesPerEntry;
                    if (entryOffset + ProgrammableLampBytesPerEntry > data.Length)
                    {
                        throw new InvalidOperationException(
                            $"PROGRAMMABLE_LAMP_TABLE digit {digitIndex} lamp {lampIndex} extends past buffer end.");
                    }

                    lampNumbers[lampIndex] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(entryOffset, 4));
                }

                blocks.Add(lampNumbers);
            }

            return blocks;
        }

        private static void WriteHexByte(Span<char> dest, byte value)
        {
            string s = value.ToString("X2", CultureInfo.InvariantCulture);
            dest[0] = s[0];
            dest[1] = s[1];
        }

        private static byte[] CopyRaw(byte[] data, int offset, int length)
        {
            byte[] raw = new byte[length];
            Array.Copy(data, offset, raw, 0, length);
            return raw;
        }
    }
}

