using OasisEditor;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class FaceReelAuthoringTests
{
    [Fact]
    public void ReelMetadataHistoryDoesNotRequireRegeneratingAnUnchangedArtworkRecipe()
    {
        using var fixture=new Fixture();var session=fixture.OpenFace();var image=Path.Combine(fixture.Root,"artwork.png");File.WriteAllBytes(image,CreateImage());
        session.ImportArtwork(image);var id=session.AddReelMount("Reel",0,0,30,40,0);session.Save();
        var artwork=session.Document!.Artwork!;var output=Path.Combine(fixture.Root,artwork.OutputAssetPath!);var pixels=File.ReadAllBytes(output);
        File.Delete(Path.Combine(fixture.Root,artwork.Source.AssetPath!));
        session.UpdateReelMount(id,new(){Stops=20});session.Undo();Assert.Null(session.Document!.Elements.OfType<FaceReelMount>().Single().Stops);
        session.Redo();Assert.Equal(20,session.Document!.Elements.OfType<FaceReelMount>().Single().Stops);Assert.Equal(pixels,File.ReadAllBytes(output));
    }
    [Fact]
    public void AddAndUpdateReelMountPersistsNativeFieldsAndLampSlots()
    {
        using var fixture = new Fixture();
        var session = fixture.OpenFace();
        var mountId = session.AddReelMount("Main reel", 12, 34, 56, 78, reelIndex: 2);
        Assert.True(session.CanUndo);
        Assert.True(session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions
        {
            Stops = 24,
            VisibleScale = .75,
            BandOffset = -.125,
            IsReversed = true,
            ReelLampsEnabled = false,
            TopLampNumber = 11,
            MiddleLampNumber = 12,
            BottomLampNumber = 13,
            IsOpaqueReel = true
        }));

        var mount = Assert.Single(session.Document!.Elements.OfType<FaceReelMount>());
        Assert.Equal(MachineObjectReference.Reel(2), mount.LinkedMachineObjectReference);
        Assert.Equal(24, mount.Stops);
        Assert.Equal(.75, mount.VisibleScale);
        Assert.Equal(-.125, mount.BandOffset);
        Assert.True(mount.IsReversed);
        Assert.False(mount.ReelLampsEnabled);
        Assert.Equal(new int?[] { 11, 12, 13 }, mount.ReelLamps.Select(lamp => lamp.LampNumber));
        Assert.Equal(new[] { 1d / 6d, .5, 5d / 6d }, mount.ReelLamps.Select(lamp => lamp.LocalVerticalCenter));

        session.Save();
        var reopened = fixture.ReopenFace();
        mount = Assert.Single(reopened.Document!.Elements.OfType<FaceReelMount>());
        Assert.Equal(24, mount.Stops);
        Assert.Equal(13, mount.ReelLamps.Single(lamp => lamp.Position == ReelLampSlotPosition.Bottom).LampNumber);
        Assert.Equal(FaceDocumentStorage.CurrentSchemaVersion, ReadFace(fixture.FacePath).SchemaVersion);
    }

    [Fact]
    public void ReelMountUpdateEnforcesLockedGeometryAndExplicitClearOptions()
    {
        using var fixture = new Fixture();
        var session = fixture.OpenFace();
        var mountId = session.AddReelMount("Locked reel", 1, 2, 30, 40, reelIndex: 0);
        Assert.True(session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions
        {
            Locked = true,
            LogicalReelIndex = 4,
            Stops = 20,
            VisibleScale = 1,
            BandOffset = .2,
            TopLampNumber = 3
        }));
        Assert.Throws<InvalidOperationException>(() => session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions { X = 7 }));

        Assert.True(session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions
        {
            ClearLogicalReelReference = true,
            ClearStops = true,
            ClearVisibleScale = true,
            ClearBandOffset = true,
            ClearTopLampNumber = true,
            ClearSymbolBandAssetPath = true,
            ClearTransmissionMaskAssetPath = true
        }));
        var mount = Assert.Single(session.Document!.Elements.OfType<FaceReelMount>());
        Assert.Null(mount.LinkedMachineObjectReference);
        Assert.Null(mount.Stops);
        Assert.Null(mount.VisibleScale);
        Assert.Null(mount.BandOffset);
        Assert.Null(mount.ReelLamps.Single(lamp => lamp.Position == ReelLampSlotPosition.Top).LampNumber);
        Assert.Throws<ArgumentException>(() => session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions
        { Stops = 5, ClearStops = true }));
    }

    [Fact]
    public void UpdatingReelLampNumbersToTheirCurrentValuesIsANoop()
    {
        using var fixture = new Fixture();
        var session = fixture.OpenFace();
        var mountId = session.AddReelMount("Lamp reel", 0, 0, 30, 40, reelIndex: 0);
        Assert.True(session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions { TopLampNumber = 7 }));

        Assert.False(session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions { TopLampNumber = 7 }));
        session.Undo();
        var mount = Assert.Single(session.Document!.Elements.OfType<FaceReelMount>());
        Assert.Null(mount.ReelLamps.Single(lamp => lamp.Position == ReelLampSlotPosition.Top).LampNumber);
    }

    [Fact]
    public void ReelArtworkReferencesMustResolveToProjectImages()
    {
        using var fixture = new Fixture();
        var session = fixture.OpenFace();
        var mountId = session.AddReelMount("Image reel", 0, 0, 100, 100, reelIndex: 1);
        Directory.CreateDirectory(Path.Combine(fixture.Root, "Assets", "Images"));
        File.WriteAllBytes(Path.Combine(fixture.Root, "Assets", "Images", "band.png"), CreateImage());
        Assert.True(session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions
        { SymbolBandAssetPath = "Assets/Images/band.png", TransmissionMaskAssetPath = "Assets/Images/band.png" }));
        Assert.Throws<ArgumentException>(() => session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions { SymbolBandAssetPath = "../outside.png" }));
        Assert.Throws<FileNotFoundException>(() => session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions { TransmissionMaskAssetPath = "Assets/Images/missing.png" }));
        Assert.Equal("Assets/Images/band.png", Assert.Single(session.Document!.Elements.OfType<FaceReelMount>()).AssetPath);
    }

    [Fact]
    public void ImportSymbolBandCopiesRealImageAndGeneratesOpaqueTransmissionMask()
    {
        using var fixture = new Fixture();
        var session = fixture.OpenFace();
        var face = ReadFace(fixture.FacePath);
        File.WriteAllText(fixture.FacePath, FaceDocumentStorage.Serialize(face with { Title = "Different display title" }));
        session.OpenFace(fixture.FacePath);
        var mountId = session.AddReelMount("Opaque reel", 0, 0, 90, 120, reelIndex: 0);
        session.UpdateReelMount(mountId, new FaceReelMountUpdateOptions { IsOpaqueReel = true });
        var sourcePath = Path.Combine(fixture.Root, "Generated", "selected-band.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllBytes(sourcePath, CreateImage());

        var imported = session.ImportSymbolBand(mountId, sourcePath);
        var mount = Assert.Single(session.Document!.Elements.OfType<FaceReelMount>());
        Assert.Equal(imported, mount.AssetPath);
        Assert.StartsWith("Assets/Faces/Arcade/ReelBands/", imported, StringComparison.Ordinal);
        Assert.Equal("Different display title", session.FaceTitle);
        Assert.True(File.Exists(Path.Combine(fixture.Root, imported.Replace('/', Path.DirectorySeparatorChar))));
        Assert.NotNull(mount.ReelLampTransmissionMaskAssetPath);
        Assert.True(File.Exists(Path.Combine(fixture.Root, mount.ReelLampTransmissionMaskAssetPath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(session.Save());
        Assert.Equal(imported, Assert.Single(fixture.ReopenFace().Document!.Elements.OfType<FaceReelMount>()).AssetPath);
    }

    [Fact]
    public void GenerateTransmissionMaskKeepsBandImageAndSupportsUndoRedo()
    {
        using var fixture = new Fixture();
        var session = fixture.OpenFace();
        var mountId = session.AddReelMount("Transparent reel", 0, 0, 90, 120, reelIndex: 1);
        var sourcePath = Path.Combine(fixture.Root, "Generated", "band-to-preserve.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        var originalImage = CreateImage();
        File.WriteAllBytes(sourcePath, originalImage);
        var bandPath = session.ImportSymbolBand(mountId, sourcePath);
        var storedBandPath = Path.Combine(fixture.Root, bandPath.Replace('/', Path.DirectorySeparatorChar));
        var bandBefore = File.ReadAllBytes(storedBandPath);
        Assert.Null(Assert.Single(session.Document!.Elements.OfType<FaceReelMount>()).ReelLampTransmissionMaskAssetPath);

        var maskPath = session.GenerateTransmissionMask(mountId);
        var maskOnDisk = Path.Combine(fixture.Root, maskPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(maskOnDisk));
        using (var generatedMask = SKBitmap.Decode(maskOnDisk)) Assert.NotNull(generatedMask);
        var mountWithMask = Assert.Single(session.Document!.Elements.OfType<FaceReelMount>());
        Assert.Equal(bandPath, mountWithMask.AssetPath);
        Assert.Equal(maskPath, mountWithMask.ReelLampTransmissionMaskAssetPath);
        Assert.Equal(bandBefore, File.ReadAllBytes(storedBandPath));

        session.Undo();
        var undone = Assert.Single(session.Document!.Elements.OfType<FaceReelMount>());
        Assert.Equal(bandPath, undone.AssetPath);
        Assert.Null(undone.ReelLampTransmissionMaskAssetPath);
        Assert.Equal(bandBefore, File.ReadAllBytes(storedBandPath));
        session.Redo();
        var redone = Assert.Single(session.Document!.Elements.OfType<FaceReelMount>());
        Assert.Equal(maskPath, redone.ReelLampTransmissionMaskAssetPath);
        Assert.Equal(originalImage, File.ReadAllBytes(sourcePath));
        Assert.Equal(bandBefore, File.ReadAllBytes(storedBandPath));
    }

    private static FaceDocumentFile ReadFace(string path)
    {
        Assert.True(FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var document, out var error), error);
        return document;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "OasisFaceReels-" + Guid.NewGuid().ToString("N"));
        public string Root => _tempRoot;
        public string ProjectPath => Path.Combine(Root, "Project.oasisproj");
        public string FacePath => Path.Combine(Root, "Assets", "Faces", "Arcade", ProjectAssetPathService.FaceManifestFileName);
        public AuthoringSession ProjectSession { get; }

        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Assets"));
            Directory.CreateDirectory(Path.Combine(Root, "Generated"));
            File.WriteAllText(ProjectPath, """{"name":"Face Reels","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            ProjectSession = new AuthoringSession(Root);
            ProjectSession.OpenProject(ProjectPath);
        }

        public FaceAuthoringSession OpenFace()
        {
            var session = new FaceAuthoringSession(ProjectSession);
            session.CreateFace("Arcade");
            return session;
        }

        public FaceAuthoringSession ReopenFace()
        {
            var session = new FaceAuthoringSession(ProjectSession);
            session.OpenFace(FacePath);
            return session;
        }

        public void Dispose() { try { Directory.Delete(Root, recursive: true); } catch { } }
    }

    private static byte[] CreateImage()
    {
        using var bitmap = new SKBitmap(16, 32, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                bitmap.SetPixel(x, y, (x + y) % 3 == 0 ? SKColors.White : new SKColor(40, 10, 80, 255));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        data!.SaveTo(stream);
        return stream.ToArray();
    }
}
