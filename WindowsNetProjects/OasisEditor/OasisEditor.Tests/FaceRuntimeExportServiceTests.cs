using System.Text.Json;
using OasisEditor;
using OasisEditor.Automation;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceRuntimeExportServiceTests : IDisposable
{
    private readonly string _projectDirectory;
    private readonly string _assetsDirectory;
    private readonly string _generatedDirectory;

    public FaceRuntimeExportServiceTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), $"OasisFaceRuntimeExportTests-{Guid.NewGuid():N}");
        _assetsDirectory = Path.Combine(_projectDirectory, "Assets");
        _generatedDirectory = Path.Combine(_projectDirectory, "Generated");
        Directory.CreateDirectory(_assetsDirectory);
        Directory.CreateDirectory(_generatedDirectory);
    }

    [Fact]
    public void Serialize_AndRead_RoundTripsRuntimeRenderAssets()
    {
        var generatedUtc = new DateTime(2026, 6, 10, 1, 2, 3, DateTimeKind.Utc);
        var source = new FaceDocumentModel
        {
            Id = "face-runtime",
            Title = "Runtime Face",
            RuntimeRenderAssets = new FaceRuntimeRenderAssetsModel
            {
                ManifestPath = "Generated/Faces/face-runtime/runtime/face.runtime.json",
                ArtworkPath = "Generated/Faces/face-runtime/runtime/artwork.png",
                MaskPath = "Generated/Faces/face-runtime/runtime/mask.png",
                TrayIdPath = null,
                LampIds0Path = null,
                LampWeights0Path = null,
                LampIds1Path = null,
                LampWeights1Path = null,
                Width = 320,
                Height = 240,
                GeneratedUtc = generatedUtc
            }
        };

        var json = FaceDocumentStorage.Serialize(source);

        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        Assert.Equal(2, file.SchemaVersion);
        Assert.Equal("Generated/Faces/face-runtime/runtime/artwork.png", file.RuntimeRenderAssets!.ArtworkPath);
        Assert.Equal(320, file.RuntimeRenderAssets.Width);

        var model = FaceDocumentStorage.ToModel(file);
        Assert.Equal("Generated/Faces/face-runtime/runtime/face.runtime.json", model.RuntimeRenderAssets!.ManifestPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/mask.png", model.RuntimeRenderAssets.MaskPath);
        Assert.Equal(240, model.RuntimeRenderAssets.Height);
        Assert.Equal(generatedUtc, model.RuntimeRenderAssets.GeneratedUtc);
    }

    [Fact]
    public void Export_WritesManifestArtworkAndMask_AndUpdatesDocumentRuntimeAssets()
    {
        var artworkPath = Path.Combine(_assetsDirectory, "artwork.png");
        var maskPath = Path.Combine(_generatedDirectory, "source-mask.png");
        WriteSolidPng(artworkPath, 4, 4, new SKColor(255, 0, 0, 128));
        WriteSolidPng(maskPath, 4, 4, SKColors.White);
        var document = CreateDocument("Assets/artwork.png", "Generated/source-mask.png");
        var project = CreateProject();

        var result = new FaceRuntimeExportService().Export(document, project);

        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(result.ArtworkPath));
        Assert.True(File.Exists(result.MaskPath));
        Assert.Equal("Generated/Faces/face-runtime/runtime/face.runtime.json", result.Document.RuntimeRenderAssets!.ManifestPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/artwork.png", result.Document.RuntimeRenderAssets.ArtworkPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/mask.png", result.Document.RuntimeRenderAssets.MaskPath);
        Assert.Equal(4, result.Document.RuntimeRenderAssets.Width);
        Assert.Equal(4, result.Document.RuntimeRenderAssets.Height);

        using var manifestJson = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
        var root = manifestJson.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("face-runtime", root.GetProperty("faceId").GetString());
        Assert.Equal("artwork.png", root.GetProperty("artwork").GetString());
        Assert.Equal("mask.png", root.GetProperty("mask").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("trayId").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("lampIds0").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("lampWeights0").ValueKind);
        var lamp = root.GetProperty("lamps")[0];
        Assert.Equal(24, lamp.GetProperty("lampId").GetInt32());
        Assert.Equal("lamp:24", lamp.GetProperty("machineReference").GetString());

        using var exportedArtwork = SKBitmap.Decode(result.ArtworkPath);
        Assert.NotNull(exportedArtwork);
        Assert.Equal(128, exportedArtwork.GetPixel(0, 0).Alpha);
    }


    [Fact]
    public void SaveDocument_ForFaceWithProject_ExportsRuntimePackageAndPersistsAssetReferences()
    {
        var artworkPath = Path.Combine(_assetsDirectory, "artwork.png");
        var maskPath = Path.Combine(_generatedDirectory, "source-mask.png");
        var facePath = Path.Combine(_projectDirectory, "front.face");
        WriteSolidPng(artworkPath, 4, 4, new SKColor(0, 255, 0, 192));
        WriteSolidPng(maskPath, 4, 4, SKColors.White);
        var document = CreateDocument("Assets/artwork.png", "Generated/source-mask.png");
        var current = new DocumentTabViewModel(
            EditorDocument.CreateFaceStub("Front Face").MarkDirty(),
            faceDocumentJson: FaceDocumentStorage.Serialize(document));

        var saved = new DocumentSaveService().SaveDocument(current, facePath, CreateProject());

        Assert.False(saved.IsDirty);
        Assert.True(File.Exists(Path.Combine(_generatedDirectory, "Faces", "face-runtime", "runtime", "face.runtime.json")));
        Assert.True(File.Exists(Path.Combine(_generatedDirectory, "Faces", "face-runtime", "runtime", "artwork.png")));
        Assert.True(File.Exists(Path.Combine(_generatedDirectory, "Faces", "face-runtime", "runtime", "mask.png")));
        Assert.True(FaceDocumentStorage.TryReadValidated(File.ReadAllText(facePath), out var persisted, out var error), error);
        Assert.Equal("Generated/Faces/face-runtime/runtime/face.runtime.json", persisted.RuntimeRenderAssets!.ManifestPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/artwork.png", persisted.RuntimeRenderAssets.ArtworkPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/mask.png", persisted.RuntimeRenderAssets.MaskPath);
    }

    [Fact]
    public void Export_WithMissingArtworkAsset_ThrowsFileNotFoundException()
    {
        var maskPath = Path.Combine(_generatedDirectory, "source-mask.png");
        WriteSolidPng(maskPath, 4, 4, SKColors.White);
        var document = CreateDocument("Assets/missing.png", "Generated/source-mask.png");

        var exception = Assert.Throws<FileNotFoundException>(() => new FaceRuntimeExportService().Export(document, CreateProject()));
        Assert.Contains("Artwork element", exception.Message);
    }

    [Fact]
    public void Export_WithMissingMaskAsset_ThrowsFileNotFoundException()
    {
        var artworkPath = Path.Combine(_assetsDirectory, "artwork.png");
        WriteSolidPng(artworkPath, 4, 4, SKColors.Red);
        var document = CreateDocument("Assets/artwork.png", "Generated/missing-mask.png");

        var exception = Assert.Throws<FileNotFoundException>(() => new FaceRuntimeExportService().Export(document, CreateProject()));
        Assert.Contains("Face mask layer", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectDirectory))
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
    }

    private EditorProject CreateProject()
    {
        return new EditorProject
        {
            Name = "Runtime Export Tests",
            ProjectFilePath = Path.Combine(_projectDirectory, "Runtime Export Tests.oasis"),
            ProjectDirectory = _projectDirectory,
            AssetsDirectory = _assetsDirectory,
            MachinesDirectory = Path.Combine(_projectDirectory, "Machines"),
            GeneratedDirectory = _generatedDirectory
        };
    }

    private static FaceDocumentModel CreateDocument(string artworkAssetPath, string maskAssetPath)
    {
        return new FaceDocumentModel
        {
            Id = "face-runtime",
            Title = "Runtime Face",
            SourceRegion = new FaceSourceRegionModel
            {
                X = 0,
                Y = 0,
                Width = 4,
                Height = 4
            },
            MaskLayer = new FaceMaskLayerModel
            {
                AssetPath = maskAssetPath,
                Width = 4,
                Height = 4,
                GeneratedUtc = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc)
            },
            Elements =
            [
                new FaceArtworkElement
                {
                    ObjectId = "artwork",
                    Name = "Artwork",
                    X = 0,
                    Y = 0,
                    Width = 4,
                    Height = 4,
                    IsVisible = true,
                    AssetPath = artworkAssetPath
                },
                new FaceLampWindowElement
                {
                    ObjectId = "lamp-24",
                    Name = "Lamp 24",
                    X = 1,
                    Y = 1,
                    Width = 2,
                    Height = 2,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(24)
                },
                new FaceReelDisplayElement
                {
                    ObjectId = "reel-1",
                    Name = "Reel 1",
                    X = 2,
                    Y = 0,
                    Width = 1,
                    Height = 4,
                    LinkedMachineObjectReference = MachineObjectReference.Reel(1)
                }
            ]
        };
    }

    private static void WriteSolidPng(string path, int width, int height, SKColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }
}
