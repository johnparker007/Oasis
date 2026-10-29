using System.Text;
using MfmeFmlDecoder.Application;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Decryption;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using MfmeFmlDecoder.Utilities;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FmlUpstreamIntegrationTests
{
    [Theory]
    [InlineData("v5")]
    [InlineData("5.2")]
    [InlineData("6.0")]
    [InlineData("6.1")]
    [InlineData("18.0")]
    [InlineData("19.0")]
    [InlineData("19.8")]
    [InlineData("20.0")]
    [InlineData("20.1")]
    public void VersionProfilesResolveWithoutNearestVersionFallback(string version)
    {
        var parsed = MfmeVersion.Parse(version);
        Assert.Equal(parsed, ComponentProfileCatalog.Resolve(MFMEComponentType.Button, parsed).Version);
        Assert.Throws<UnsupportedComponentProfileException>(() =>
            ComponentProfileCatalog.Resolve(MFMEComponentType.Button, MfmeVersion.Parse("21.0")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModernSplitSectionsKeepExplicitZeroInputAndSignedTextOffsets(bool explicitButton)
    {
        var payload = ButtonPayload(explicitButton);
        var component = new ButtonParser().Parse(ParsingContext.V20_1, 0, (uint)MFMEComponentType.Button, payload);
        Assert.Equal(explicitButton, component.WasValuePresent("Button Number"));
        if (explicitButton) Assert.Equal(0u, component.UInt32s["Button Number"]);
        Assert.Equal(-2, component.Int32s["XOffset"]);
        Assert.Equal(-1, component.Int32s["YOffset"]);
        Assert.DoesNotContain("PresentValueKeys", component.ToJson());
    }

    [Theory]
    [InlineData("v5")]
    [InlineData("5.2")]
    [InlineData("6.0")]
    [InlineData("6.1")]
    [InlineData("19.8")]
    public void LegacyLayoutsDecodeThroughVersionDispatchAndRetainSourcePresence(string version)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
        writer.Write(new byte[] { 0x3D, 0, 0, 0, 0 });
        foreach (var (tag, value) in new (byte, int)[] { (1, 10), (2, 20), (3, 40), (4, 60), (5, 9) })
        { writer.Write(tag); writer.Write(value); }
        writer.Write((byte)7); writer.Write(MfmeAngleWireCodec.AngleToBytes(45));
        writer.Write(new byte[5]);
        writer.Write((byte)0x18); writer.Write(0u); writer.Write((byte)0);
        var root = Path.Combine(Path.GetTempPath(), "OasisFmlLegacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "legacy.fml");
            File.WriteAllBytes(source, FmlDecryptor.Encode(Layout(version, data.ToArray()), "01279247"));
            var decoded = new FmlDecoderService().DecodeToLayout(source);
            Assert.True(decoded.Succeeded, string.Join("; ", decoded.Errors));
            var button = Assert.IsType<Button>(Assert.Single(decoded.Layout!.Components));
            Assert.Equal(45, button.Angle);
            Assert.True(button.WasValuePresent("Button Number"));
            Assert.Equal(0u, button.UInt32s["Button Number"]);
            Assert.Equal("Vertical", button.Orientation);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DatAndEncryptedMultiChunkFmlUseTheProductionImportAndHistory(bool encrypted)
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisFmlUpstream-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var bytes = Layout("20.1", ButtonPayload(true));
            var source = Path.Combine(root, encrypted ? "test.fml" : "test.dat");
            File.WriteAllBytes(source, encrypted ? FmlDecryptor.Encode(bytes, "01279247", chunkSize: 64) : bytes);
            var decoded = new FmlDecoderService().DecodeToJson(source);
            Assert.True(decoded.Succeeded, string.Join("; ", decoded.Errors));
            Assert.Contains("Button", decoded.Json);
            var session = new AuthoringSession(root);
            session.CreateProject("Import"); session.CreatePanel("Panel");
            var result = session.ImportFml(source);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
            Assert.True(result.ImportedElementCount > 0);
            Assert.Equal("0", Assert.Single(result.InputDefinitions).ButtonNumber);
            Assert.True(session.IsDirty);
            var count = session.Elements.Count;
            session.Undo(); Assert.Empty(session.Elements); Assert.False(session.IsDirty);
            session.Redo(); Assert.Equal(count, session.Elements.Count);
            session.Save(); Assert.False(session.IsDirty);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void UnsupportedVersionLeavesThePanelAndHistoryUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisFmlUnsupported-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var session = new AuthoringSession(root);
            session.CreateProject("Import"); session.CreatePanel("Panel");
            var source = Path.Combine(root, "future.dat");
            File.WriteAllBytes(source, Layout("21.0", ButtonPayload(true)));
            var result = session.ImportFml(source);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Contains("21.0"));
            Assert.Empty(session.Elements); Assert.False(session.IsDirty); Assert.False(session.CanUndo);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReimportAfterDiscardKeepsEarlierArtworkAndUsesAnIndependentPackage(bool newPanel)
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisFmlRetry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var session = new AuthoringSession(root);
            var project = session.CreateProject("Retry"); var panel = session.CreatePanel("Panel");
            var source = Path.Combine(root, "Layout.fml");
            File.WriteAllBytes(source, FmlDecryptor.Encode(Layout("20.1", ButtonPayload(true, artwork: true)), "01279247"));
            var first = session.ImportFml(source);
            Assert.True(first.Succeeded, string.Join("; ", first.Errors)); Assert.NotEmpty(first.CopiedAssetRelativePaths);
            var projectRoot = Path.GetDirectoryName(project)!;
            var preserved = first.CopiedAssetRelativePaths.ToDictionary(p => p, p => File.ReadAllBytes(Path.Combine(projectRoot, p)));

            // A fresh session reads the initial saved (empty) panel, just as
            // closing/discarding and reopening does. Imported files remain.
            var reopened = new AuthoringSession(root); reopened.OpenProject(project);
            if (newPanel) reopened.CreatePanel("Another"); else reopened.OpenPanel(panel);
            Assert.Empty(reopened.Elements);
            var second = reopened.ImportFml(source);
            Assert.True(second.Succeeded, string.Join("; ", second.Errors)); Assert.NotEmpty(second.CopiedAssetRelativePaths);
            Assert.All(second.CopiedAssetRelativePaths, p => Assert.StartsWith("Assets/FmlImport/Layout_2/", p));
            Assert.All(second.CopiedAssetRelativePaths, p => Assert.True(File.Exists(Path.Combine(projectRoot, p))));
            foreach (var pair in preserved) Assert.Equal(pair.Value, File.ReadAllBytes(Path.Combine(projectRoot, pair.Key)));

            reopened.Save(); var savedCount = reopened.Elements.Count;
            var savedArtwork = second.CopiedAssetRelativePaths.ToDictionary(p => p, p => File.ReadAllBytes(Path.Combine(projectRoot, p)));
            var third = reopened.ImportFml(source);
            Assert.True(third.Succeeded, string.Join("; ", third.Errors));
            Assert.All(third.CopiedAssetRelativePaths, p => Assert.StartsWith("Assets/FmlImport/Layout_3/", p));
            reopened.Undo(); Assert.Equal(savedCount, reopened.Elements.Count); Assert.False(reopened.IsDirty);
            reopened.Redo(); Assert.Equal(2 * savedCount, reopened.Elements.Count);
            foreach (var pair in savedArtwork) Assert.Equal(pair.Value, File.ReadAllBytes(Path.Combine(projectRoot, pair.Key)));
        }
        finally { Directory.Delete(root, true); }
    }

    private static byte[] ButtonPayload(bool explicitButton, bool artwork = false)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
        writer.Write(new byte[] { 0x3D, 0, 0, 0, 0 });
        foreach (var (tag, value) in new (byte, int)[] { (1, 10), (2, 20), (3, 40), (4, 60), (5, 9), (6, 0) })
        { writer.Write(tag); writer.Write(value); }
        writer.Write(new byte[] { 7, 1 });
        if (explicitButton) { writer.Write((byte)0x18); writer.Write(0u); }
        writer.Write(new byte[] { 0x4C, 0, 0 });
        if (artwork)
        {
            var bmp = new byte[58]; bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
            BitConverter.GetBytes(58).CopyTo(bmp, 2); BitConverter.GetBytes(54).CopyTo(bmp, 10);
            BitConverter.GetBytes(40).CopyTo(bmp, 14); BitConverter.GetBytes(1).CopyTo(bmp, 18);
            BitConverter.GetBytes(1).CopyTo(bmp, 22); BitConverter.GetBytes((ushort)1).CopyTo(bmp, 26);
            BitConverter.GetBytes((ushort)24).CopyTo(bmp, 28); BitConverter.GetBytes(4).CopyTo(bmp, 34);
            bmp[56] = 255; writer.Write((byte)0x06); writer.Write(bmp);
        }
        writer.Write((byte)0x0D); writer.Write(-2);
        writer.Write((byte)0x0E); writer.Write(-1);
        writer.Write((byte)0);
        return data.ToArray();
    }

    private static byte[] Layout(string version, byte[] component)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
        var text = Encoding.ASCII.GetBytes(version + "\0");
        writer.Write(0x2Fu); writer.Write((uint)text.Length); writer.Write(text);
        writer.Write(uint.MaxValue); writer.Write(0u);
        writer.Write((uint)MFMEComponentType.Button); writer.Write((uint)component.Length + 8); writer.Write(component);
        return data.ToArray();
    }
}
