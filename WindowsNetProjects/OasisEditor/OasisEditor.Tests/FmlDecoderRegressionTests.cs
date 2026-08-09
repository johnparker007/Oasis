using System.Text;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component;
using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.Model;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FmlDecoderRegressionTests
{
    [Fact]
    public void FileWalker_PreservesLayoutHeaderDescriptionAndTextNotes()
    {
        var walker = new FileWalker(new ComponentWalker(new ComponentParser()));
        var bytes = BuildTlv(
            (LayoutFileHeader.LayoutDescriptionTag, FixedAscii("Cash King\0Club\0\0", LayoutFileHeader.LayoutDescriptionLength)),
            (LayoutFileHeader.TextNotesTag, Encoding.ASCII.GetBytes("Imported notes\0ignored")),
            (0xFFFFFFFFu, []));

        walker.WalkTlv(bytes, 0);

        Assert.Equal("Cash King\nClub", walker.Header.Description);
        Assert.Equal("Imported notes", walker.Header.TextNotes);
    }

    [Fact]
    public void Layout_ExposesFileHeaderMetadata()
    {
        var header = new LayoutFileHeader { Description = "Header description", TextNotes = "Header notes" };
        var layout = new Layout([], header);

        Assert.Equal("Header description", layout.Description);
        Assert.Equal("Header notes", layout.TextNotes);
        Assert.False(layout.HasSplash);
        Assert.Empty(layout.Images);
    }

    [Fact]
    public void ExtendedTags_DistinguishAbsentDefaultFromExplicitZero()
    {
        var tagMap = new ComponentTagMap
        {
            { 0x18, new TagInfo(4, "ButtonNumber", [0, 0, 0, 0], ValueRole.UINT32) }
        };
        var parser = new ExtendedTagParser();

        var absent = parser.Parse(tagMap, [0x00], 0, ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());
        var explicitZero = parser.Parse(tagMap, [0x18, 0x00, 0x00, 0x00, 0x00, 0x00], 0, ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

        Assert.Equal(0u, absent.UInt32sByAttributeName["ButtonNumber"]);
        Assert.DoesNotContain(0x18u, absent.PresentTagIds);
        Assert.Equal(0u, explicitZero.UInt32sByAttributeName["ButtonNumber"]);
        Assert.Contains(0x18u, explicitZero.PresentTagIds);
    }

    [Fact]
    public void BackgroundNestedOffsets_DecodeAsSignedInt32WithZeroDefaults()
    {
        var bytes = new byte[]
        {
            0x4C, 0x00, 0x00,
            0x0A, 0xEC, 0xFF, 0xFF, 0xFF,
            0x0B, 0xF6, 0xFF, 0xFF, 0xFF,
            0x00,
            0x00
        };

        var parsed = new ExtendedTagParser().Parse(
            BackgroundParser.ComponentTagMap,
            bytes,
            0,
            ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());

        Assert.NotNull(parsed.NestedTagBlockResult);
        Assert.Equal(-20, parsed.NestedTagBlockResult.Int32sByAttributeName["OffsetX"]);
        Assert.Equal(-10, parsed.NestedTagBlockResult.Int32sByAttributeName["OffsetY"]);

        var onlyOffsetX = new byte[] { 0x4C, 0x00, 0x00, 0x0A, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var parsedWithAbsentY = new ExtendedTagParser().Parse(
            BackgroundParser.ComponentTagMap,
            onlyOffsetX,
            0,
            ExtendedTagParser.Options.Default.WithoutMatchedTagLogging());
        Assert.Equal(5, parsedWithAbsentY.NestedTagBlockResult!.Int32sByAttributeName["OffsetX"]);
        Assert.Equal(0, parsedWithAbsentY.NestedTagBlockResult.Int32sByAttributeName["OffsetY"]);
    }

    private static byte[] BuildTlv(params (uint Tag, byte[] Value)[] records)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var (tag, value) in records)
        {
            writer.Write(tag);
            writer.Write((uint)value.Length);
            writer.Write(value);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] FixedAscii(string value, int length)
    {
        var bytes = new byte[length];
        Encoding.ASCII.GetBytes(value, 0, value.Length, bytes, 0);
        return bytes;
    }
}
