using System.Text;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model;
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
