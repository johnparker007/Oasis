using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using System;
using System.Collections.Generic;
using Xunit;

namespace MfmeFmlDecoder.src.Decoder.Component.Core
{
    public class TlvBlockParserTests
    {
        [Fact]
        public void Parse_TlvBlock_StopsBeforeHigherOuterTag()
        {
            byte[] data =
            {
                0x68,
                0x08, 0x00, 0x00, 0x00,
                0x08, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                0x08, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF,
                0x69, 0x01,
                0x6C, 0x01,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x68, new TagInfo(0x00, "Unknown 0x68 (TLV Block)", new byte[0], ValueRole.TLVBLOCK) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.ValuesByTag.TryGetValue(0x68, out object tlvValue));
            var entries = Assert.IsType<List<byte[]>>(tlvValue);
            Assert.Equal(2, entries.Count);
            Assert.Equal(new byte[] { 0x08, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 }, entries[0]);
            Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF }, entries[1]);
            Assert.Equal(25, result.Offset);
        }

        [Fact]
        public void Parse_TlvBlock_StopsOnZeroDelimiterByte()
        {
            byte[] data =
            {
                0x68,
                0x04, 0x00, 0x00, 0x00,
                0xAA, 0xBB, 0xCC, 0xDD,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x68, new TagInfo(0x00, "TLV Block", new byte[0], ValueRole.TLVBLOCK) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.ValuesByTag.TryGetValue(0x68, out object tlvValue));
            var entries = Assert.IsType<List<byte[]>>(tlvValue);
            Assert.Single(entries);
            Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, entries[0]);
            Assert.Equal(10, result.Offset);
        }
        [Fact]
        public void Parse_ConflictingTextAttributeName_Throws()
        {
            byte[] data =
            {
                0x3F,
                0x01, 0x00, 0x00, 0x00,
                0x41, 0x00,
                0x26,
                0x01, 0x00, 0x00, 0x00,
                0x42, 0x00,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x3F, new TagInfo(0x00, "Caption", new byte[0], ValueRole.TEXT) },
                { 0x26, new TagInfo(0x00, "Caption", new byte[0], ValueRole.TEXT) },
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                new ExtendedTagParser().Parse(
                    tagMap,
                    data,
                    offset: 0,
                    ExtendedTagParser.Options.Default.WithoutMatchedTagLogging()));

            Assert.Contains("Conflicting string key 'Caption'", ex.Message);
        }

        [Fact]
        public void Parse_UInt32Tag_PopulatesUInt32sByAttributeName()
        {
            byte[] data =
            {
                0x39,
                0x17, 0x00, 0x00, 0x00,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x04, "Lamp", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.UInt32sByAttributeName.TryGetValue("Lamp", out uint lamp));
            Assert.Equal(23u, lamp);
        }

        [Fact]
        public void Parse_MissingUInt32TagWithDefault_UsesDefaultInUInt32sByAttributeName()
        {
            byte[] data = { 0x00 };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x04, "Lamp", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x38, new TagInfo(0x04, "Unknown 0x38", new byte[] { 0x05, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.UInt32sByAttributeName.TryGetValue("Lamp", out uint lamp));
            Assert.Equal(0u, lamp);
            Assert.False(result.UInt32sByAttributeName.ContainsKey("Unknown 0x38"));
        }

        [Fact]
        public void Parse_UInt32TagWithUnknownAttributeName_IsOmittedFromUInt32sByAttributeName()
        {
            byte[] data =
            {
                0x3B,
                0x17, 0x00, 0x00, 0x00,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.False(result.UInt32sByAttributeName.ContainsKey("Unknown 0x3B"));
            Assert.True(result.ValuesByTag.TryGetValue(0x3B, out object rawValue));
            Assert.Equal(23u, rawValue);
        }

        [Fact]
        public void Parse_ConflictingUInt32AttributeName_Throws()
        {
            byte[] data =
            {
                0x39,
                0x01, 0x00, 0x00, 0x00,
                0x26,
                0x02, 0x00, 0x00, 0x00,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x04, "Lamp", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x26, new TagInfo(0x04, "Lamp", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                new ExtendedTagParser().Parse(
                    tagMap,
                    data,
                    offset: 0,
                    ExtendedTagParser.Options.Default.WithoutMatchedTagLogging()));

            Assert.Contains("Conflicting UInt32 key 'Lamp'", ex.Message);
        }

        [Fact]
        public void Parse_FloatTag_PopulatesFloatsByAttributeName()
        {
            byte[] data =
            {
                0x06,
                0x00, 0x00, 0x40, 0x40,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x06, new TagInfo(0x04, "Thickness", new byte[] { 0x00, 0x00, 0x00, 0x40 }, ValueRole.FLOAT) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.FloatsByAttributeName.TryGetValue("Thickness", out float thickness));
            Assert.Equal(3f, thickness);
        }

        [Fact]
        public void Parse_MissingFloatTagWithDefault_UsesDefaultInFloatsByAttributeName()
        {
            byte[] data = { 0x00 };

            var tagMap = new ComponentTagMap
            {
                { 0x06, new TagInfo(0x04, "Thickness", new byte[] { 0x00, 0x00, 0x00, 0x40 }, ValueRole.FLOAT) },
                { 0x07, new TagInfo(0x04, "Unknown 0x07", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.FLOAT) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.FloatsByAttributeName.TryGetValue("Thickness", out float thickness));
            Assert.Equal(2f, thickness);
            Assert.False(result.FloatsByAttributeName.ContainsKey("Unknown 0x07"));
        }

        [Fact]
        public void Parse_ConflictingFloatAttributeName_Throws()
        {
            byte[] data =
            {
                0x06,
                0x00, 0x00, 0x40, 0x40,
                0x07,
                0x00, 0x00, 0x00, 0x40,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x06, new TagInfo(0x04, "Thickness", new byte[] { 0x00, 0x00, 0x00, 0x40 }, ValueRole.FLOAT) },
                { 0x07, new TagInfo(0x04, "Thickness", new byte[] { 0x00, 0x00, 0x00, 0x40 }, ValueRole.FLOAT) },
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                new ExtendedTagParser().Parse(
                    tagMap,
                    data,
                    offset: 0,
                    ExtendedTagParser.Options.Default.WithoutMatchedTagLogging()));

            Assert.Contains("Conflicting Float key 'Thickness'", ex.Message);
        }

        [Fact]
        public void Parse_Int32Tag_PopulatesInt32sByAttributeName()
        {
            byte[] data =
            {
                0x39,
                0xE9, 0xFF, 0xFF, 0xFF,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x04, "SelectedCoinNote", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.Int32sByAttributeName.TryGetValue("SelectedCoinNote", out int selectedCoinNote));
            Assert.Equal(-23, selectedCoinNote);
        }

        [Fact]
        public void Parse_MissingInt32TagWithDefault_UsesDefaultInInt32sByAttributeName()
        {
            byte[] data = { 0x00 };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x04, "SelectedCoinNote", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
                { 0x38, new TagInfo(0x04, "Unknown 0x38", new byte[] { 0x05, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.Int32sByAttributeName.TryGetValue("SelectedCoinNote", out int selectedCoinNote));
            Assert.Equal(0, selectedCoinNote);
            Assert.False(result.Int32sByAttributeName.ContainsKey("Unknown 0x38"));
        }

        [Fact]
        public void Parse_UInt16Tag_PopulatesUInt16sByAttributeName()
        {
            byte[] data =
            {
                0x39,
                0x17, 0x00,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x02, "HorizontalVertical", new byte[] { 0x00, 0x00 }, ValueRole.UINT16) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.UInt16sByAttributeName.TryGetValue("HorizontalVertical", out ushort horizontalVertical));
            Assert.Equal(23, horizontalVertical);
        }

        [Fact]
        public void Parse_MissingUInt16TagWithDefault_UsesDefaultInUInt16sByAttributeName()
        {
            byte[] data = { 0x00 };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x02, "HorizontalVertical", new byte[] { 0x00, 0x00 }, ValueRole.UINT16) },
                { 0x38, new TagInfo(0x02, "Unknown 0x38", new byte[] { 0x05, 0x00 }, ValueRole.UINT16) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.UInt16sByAttributeName.TryGetValue("HorizontalVertical", out ushort horizontalVertical));
            Assert.Equal(0, horizontalVertical);
            Assert.False(result.UInt16sByAttributeName.ContainsKey("Unknown 0x38"));
        }

        [Fact]
        public void Parse_BooleanTag_PopulatesBooleansByAttributeName()
        {
            byte[] data =
            {
                0x39,
                0x01,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x01, "Visible", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.BooleansByAttributeName.TryGetValue("Visible", out bool visible));
            Assert.True(visible);
        }

        [Fact]
        public void Parse_MissingBooleanTagWithDefault_UsesDefaultInBooleansByAttributeName()
        {
            byte[] data = { 0x00 };

            var tagMap = new ComponentTagMap
            {
                { 0x39, new TagInfo(0x01, "Visible", new byte[] { 0x01 }, ValueRole.BOOLEAN) },
                { 0x38, new TagInfo(0x01, "Unknown 0x38", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.BooleansByAttributeName.TryGetValue("Visible", out bool visible));
            Assert.True(visible);
            Assert.False(result.BooleansByAttributeName.ContainsKey("Unknown 0x38"));
        }

        [Fact]
        public void Parse_ByteTag_PopulatesBytesByAttributeName()
        {
            byte[] data =
            {
                0x17,
                0x40,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x17, new TagInfo(0x01, "OffLevel", new byte[] { 0x00 }, ValueRole.BYTE) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.BytesByAttributeName.TryGetValue("OffLevel", out byte offLevel));
            Assert.Equal(0x40, offLevel);
        }

        [Fact]
        public void Parse_MissingByteTagWithDefault_UsesDefaultInBytesByAttributeName()
        {
            byte[] data = { 0x00 };

            var tagMap = new ComponentTagMap
            {
                { 0x17, new TagInfo(0x01, "OffLevel", new byte[] { 0x40 }, ValueRole.BYTE) },
                { 0x18, new TagInfo(0x01, "Unknown 0x18", new byte[] { 0x00 }, ValueRole.BYTE) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.True(result.BytesByAttributeName.TryGetValue("OffLevel", out byte offLevel));
            Assert.Equal(0x40, offLevel);
            Assert.False(result.BytesByAttributeName.ContainsKey("Unknown 0x18"));
        }

        [Fact]
        public void Parse_UnmappedNestedTagBlock_CapturesVerticalOrientationByte()
        {
            byte[] data =
            {
                0x4C, 0x00, 0x00,
                0x01, 0x02, 0x00, 0x00, 0x00,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x01, new TagInfo(0x04, "Example", new byte[4], ValueRole.UINT32) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.Equal<byte?>(0x00, result.NestedTagBlockOrientationByte);
        }

        [Fact]
        public void Parse_UnmappedNestedTagBlock_CapturesHorizontalOrientationByte()
        {
            byte[] data =
            {
                0x4C, 0x01, 0x00,
                0x00,
            };

            var result = new ExtendedTagParser().Parse(
                new ComponentTagMap(),
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.Equal<byte?>(0x01, result.NestedTagBlockOrientationByte);
        }

        [Fact]
        public void Parse_WithoutNestedTagBlockOpener_LeavesOrientationByteNull()
        {
            byte[] data =
            {
                0x01, 0x02, 0x00, 0x00, 0x00,
                0x00,
            };

            var tagMap = new ComponentTagMap
            {
                { 0x01, new TagInfo(0x04, "Example", new byte[4], ValueRole.UINT32) },
            };

            var result = new ExtendedTagParser().Parse(
                tagMap,
                data,
                offset: 0,
                ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

            Assert.Null(result.NestedTagBlockOrientationByte);
        }

        [Theory]
        [InlineData(0x00, "Vertical")]
        [InlineData(0x01, "Horizontal")]
        public void ResolveNestedTagBlockOrientation_MapsKnownBytes(byte orientationByte, string expected)
        {
            Assert.Equal(expected, ExtendedTagParser.ResolveNestedTagBlockOrientation(orientationByte));
        }
    }
}

