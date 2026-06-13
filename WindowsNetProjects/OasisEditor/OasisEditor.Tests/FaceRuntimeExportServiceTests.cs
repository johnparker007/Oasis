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
                TrayIdPath = "Generated/Faces/face-runtime/runtime/trayId.png",
                LampIds0Path = "Generated/Faces/face-runtime/runtime/lampIds0.png",
                LampWeights0Path = "Generated/Faces/face-runtime/runtime/lampWeights0.png",
                LampIds1Path = null,
                LampWeights1Path = null,
                TrayIdDebugPath = "Generated/Faces/face-runtime/runtime/trayId_debug.png",
                LampWeightsDebugPath = "Generated/Faces/face-runtime/runtime/lampWeights_debug.png",
                Width = 320,
                Height = 240,
                GeneratedUtc = generatedUtc
            }
        };

        var json = FaceDocumentStorage.Serialize(source);

        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        Assert.Equal(FaceDocumentStorage.CurrentSchemaVersion, file.SchemaVersion);
        Assert.Equal("Generated/Faces/face-runtime/runtime/artwork.png", file.RuntimeRenderAssets!.ArtworkPath);
        Assert.Equal(320, file.RuntimeRenderAssets.Width);
        Assert.Equal("Generated/Faces/face-runtime/runtime/trayId_debug.png", file.RuntimeRenderAssets.TrayIdDebugPath);

        var model = FaceDocumentStorage.ToModel(file);
        Assert.Equal("Generated/Faces/face-runtime/runtime/face.runtime.json", model.RuntimeRenderAssets!.ManifestPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/mask.png", model.RuntimeRenderAssets.MaskPath);
        Assert.Equal(240, model.RuntimeRenderAssets.Height);
        Assert.Equal(generatedUtc, model.RuntimeRenderAssets.GeneratedUtc);
        Assert.Equal("Generated/Faces/face-runtime/runtime/lampWeights_debug.png", model.RuntimeRenderAssets.LampWeightsDebugPath);
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
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "trayId.png")));
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "lampIds0.png")));
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "lampWeights0.png")));
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "trayId_debug.png")));
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "lampWeights_debug.png")));
        Assert.Equal("Generated/Faces/face-runtime/runtime/face.runtime.json", result.Document.RuntimeRenderAssets!.ManifestPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/artwork.png", result.Document.RuntimeRenderAssets.ArtworkPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/mask.png", result.Document.RuntimeRenderAssets.MaskPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/trayId.png", result.Document.RuntimeRenderAssets.TrayIdPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/lampIds0.png", result.Document.RuntimeRenderAssets.LampIds0Path);
        Assert.Equal("Generated/Faces/face-runtime/runtime/lampWeights0.png", result.Document.RuntimeRenderAssets.LampWeights0Path);
        Assert.Equal("Generated/Faces/face-runtime/runtime/trayId_debug.png", result.Document.RuntimeRenderAssets.TrayIdDebugPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/lampWeights_debug.png", result.Document.RuntimeRenderAssets.LampWeightsDebugPath);
        Assert.Equal(4, result.Document.RuntimeRenderAssets.Width);
        Assert.Equal(4, result.Document.RuntimeRenderAssets.Height);

        using var manifestJson = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
        var root = manifestJson.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("face-runtime", root.GetProperty("faceId").GetString());
        Assert.Equal("artwork.png", root.GetProperty("artwork").GetString());
        Assert.Equal("mask.png", root.GetProperty("mask").GetString());
        Assert.Equal("trayId.png", root.GetProperty("trayId").GetString());
        Assert.Equal("lampIds0.png", root.GetProperty("lampIds0").GetString());
        Assert.Equal("lampWeights0.png", root.GetProperty("lampWeights0").GetString());
        Assert.Equal("trayId_debug.png", root.GetProperty("trayIdDebug").GetString());
        Assert.Equal("lampWeights_debug.png", root.GetProperty("lampWeightsDebug").GetString());
        var lamp = root.GetProperty("lamps")[0];
        Assert.Equal("runtime-emitter-lamp-24", lamp.GetProperty("objectId").GetString());
        Assert.Equal(24, lamp.GetProperty("lampId").GetInt32());
        Assert.Equal(1, lamp.GetProperty("trayId").GetInt32());
        Assert.Equal("lamp:24", lamp.GetProperty("machineReference").GetString());
        var tray = root.GetProperty("trays")[0];
        Assert.Equal(1, tray.GetProperty("trayId").GetInt32());
        Assert.Equal("runtime-tray-lamp-24", tray.GetProperty("objectId").GetString());

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
        Assert.True(File.Exists(Path.Combine(_generatedDirectory, "Faces", "face-runtime", "runtime", "trayId.png")));
        Assert.True(File.Exists(Path.Combine(_generatedDirectory, "Faces", "face-runtime", "runtime", "lampIds0.png")));
        Assert.True(File.Exists(Path.Combine(_generatedDirectory, "Faces", "face-runtime", "runtime", "lampWeights0.png")));
        Assert.True(File.Exists(Path.Combine(_generatedDirectory, "Faces", "face-runtime", "runtime", "trayId_debug.png")));
        Assert.True(File.Exists(Path.Combine(_generatedDirectory, "Faces", "face-runtime", "runtime", "lampWeights_debug.png")));
        Assert.True(FaceDocumentStorage.TryReadValidated(File.ReadAllText(facePath), out var persisted, out var error), error);
        Assert.Equal("Generated/Faces/face-runtime/runtime/face.runtime.json", persisted.RuntimeRenderAssets!.ManifestPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/artwork.png", persisted.RuntimeRenderAssets.ArtworkPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/mask.png", persisted.RuntimeRenderAssets.MaskPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/trayId.png", persisted.RuntimeRenderAssets.TrayIdPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/lampIds0.png", persisted.RuntimeRenderAssets.LampIds0Path);
        Assert.Equal("Generated/Faces/face-runtime/runtime/lampWeights0.png", persisted.RuntimeRenderAssets.LampWeights0Path);
        Assert.Equal("Generated/Faces/face-runtime/runtime/trayId_debug.png", persisted.RuntimeRenderAssets.TrayIdDebugPath);
        Assert.Equal("Generated/Faces/face-runtime/runtime/lampWeights_debug.png", persisted.RuntimeRenderAssets.LampWeightsDebugPath);
    }


    [Fact]
    public void Generate_UsesDerivedAuthoredPolygonVerticesForTrayAndLampTextures()
    {
        var outputDirectory = Path.Combine(_generatedDirectory, "derived-polygon-texture-test");
        var document = new FaceDocumentModel
        {
            Trays =
            [
                new FaceTrayModel
                {
                    ObjectId = "triangle-tray",
                    Bounds = new FaceSourceRegionModel { X = 0, Y = 0, Width = 4, Height = 4 },
                    Vertices =
                    [
                        new FacePointModel { X = 0, Y = 0 },
                        new FacePointModel { X = 4, Y = 0 },
                        new FacePointModel { X = 0, Y = 4 }
                    ]
                }
            ],
            LampEmitters =
            [
                new FaceLampEmitterElement { ObjectId = "triangle-emitter", TrayObjectId = "triangle-tray", TrayId = 1, LampId = 24 }
            ]
        };

        new FaceRuntimeTextureGenerator().Generate(document, 4, 4, outputDirectory);

        using var trayId = SKBitmap.Decode(Path.Combine(outputDirectory, "trayId.png"));
        using var lampIds0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampIds0.png"));
        using var lampWeights0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampWeights0.png"));
        Assert.Equal(1, trayId.GetPixel(0, 0).Red);
        Assert.Equal(24, lampIds0.GetPixel(0, 0).Red);
        Assert.Equal(255, lampWeights0.GetPixel(0, 0).Red);
        Assert.Equal(0, trayId.GetPixel(3, 3).Red);
        Assert.Equal(0, lampIds0.GetPixel(3, 3).Red);
        Assert.Equal(0, lampWeights0.GetPixel(3, 3).Red);
    }


    [Fact]
    public void Generate_WithSharedAuthoredTray_WritesMultipleEmitterChannels()
    {
        var outputDirectory = Path.Combine(_generatedDirectory, "shared-tray-texture-test");
        var document = new FaceDocumentModel
        {
            Trays =
            [
                new FaceTrayModel
                {
                    ObjectId = "shared-tray",
                    Bounds = new FaceSourceRegionModel { X = 0, Y = 0, Width = 2, Height = 2 },
                    Vertices =
                    [
                        new FacePointModel { X = 0, Y = 0 },
                        new FacePointModel { X = 2, Y = 0 },
                        new FacePointModel { X = 2, Y = 2 },
                        new FacePointModel { X = 0, Y = 2 }
                    ]
                }
            ],
            LampEmitters =
            [
                new FaceLampEmitterElement { ObjectId = "emitter-a", TrayObjectId = "shared-tray", TrayId = 1, LampId = 11, CenterX = 0.5, CenterY = 0.5 },
                new FaceLampEmitterElement { ObjectId = "emitter-b", TrayObjectId = "shared-tray", TrayId = 1, LampId = 12, CenterX = 1.5, CenterY = 1.5 }
            ]
        };

        var result = new FaceRuntimeTextureGenerator().Generate(document, 2, 2, outputDirectory);

        Assert.Single(result.Plan.Trays);
        Assert.Equal(2, result.Plan.Emitters.Count);
        using var trayId = SKBitmap.Decode(Path.Combine(outputDirectory, "trayId.png"));
        using var lampIds0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampIds0.png"));
        using var lampWeights0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampWeights0.png"));
        Assert.Equal(1, trayId.GetPixel(0, 0).Red);
        Assert.Equal(11, lampIds0.GetPixel(0, 0).Red);
        Assert.Equal(12, lampIds0.GetPixel(0, 0).Green);
        Assert.True(lampWeights0.GetPixel(0, 0).Red > lampWeights0.GetPixel(0, 0).Green);
        Assert.True(lampWeights0.GetPixel(0, 0).Red + lampWeights0.GetPixel(0, 0).Green + lampWeights0.GetPixel(0, 0).Blue <= 255);
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


    [Fact]
    public void CreatePlan_GeneratesDeterministicTemporaryEmittersFromVisibleLampWindows()
    {
        var document = CreateDocument("Assets/artwork.png", "Generated/source-mask.png");

        var plan = new FaceRuntimeTextureGenerator().CreatePlan(document, 4, 4);

        var emitter = Assert.Single(plan.Emitters);
        Assert.Equal("runtime-emitter-lamp-24", emitter.ObjectId);
        Assert.Equal("lamp-24", emitter.SourceLampWindowObjectId);
        Assert.Equal(1, emitter.TrayId);
        Assert.Equal(24, emitter.LampId);
        Assert.Equal(2, emitter.CenterX);
        Assert.Equal(2, emitter.CenterY);
    }

    [Fact]
    public void Generate_WritesTrayAndLampTexturesFromTemporaryTrays()
    {
        var outputDirectory = Path.Combine(_generatedDirectory, "texture-test");
        var document = CreateDocument("Assets/artwork.png", "Generated/source-mask.png");

        new FaceRuntimeTextureGenerator().Generate(document, 4, 4, outputDirectory);

        using var trayId = SKBitmap.Decode(Path.Combine(outputDirectory, "trayId.png"));
        using var lampIds0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampIds0.png"));
        using var lampWeights0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampWeights0.png"));
        using var trayDebug = SKBitmap.Decode(Path.Combine(outputDirectory, "trayId_debug.png"));
        using var weightDebug = SKBitmap.Decode(Path.Combine(outputDirectory, "lampWeights_debug.png"));

        Assert.Equal(0, trayId.GetPixel(0, 0).Red);
        Assert.Equal(1, trayId.GetPixel(1, 1).Red);
        Assert.Equal(24, lampIds0.GetPixel(1, 1).Red);
        Assert.Equal(0, lampIds0.GetPixel(1, 1).Green);
        Assert.Equal(255, lampWeights0.GetPixel(1, 1).Red);
        Assert.Equal(0, lampWeights0.GetPixel(0, 0).Red);
        Assert.NotEqual(SKColors.Transparent, trayDebug.GetPixel(1, 1));
        Assert.Equal(255, weightDebug.GetPixel(1, 1).Red);
    }


    [Fact]
    public void CreatePlan_UsesMaskContributionBoundsForTemporaryTrayOwnership()
    {
        var document = new FaceDocumentModel
        {
            Id = "face-runtime",
            Title = "Runtime Face",
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 4, Height = 4 },
            MaskLayer = new FaceMaskLayerModel
            {
                Width = 4,
                Height = 4,
                Contributions =
                [
                    new FaceMaskContributionModel
                    {
                        SourcePanel2DElementId = "panel-a",
                        LinkedMachineObjectReference = MachineObjectReference.Lamp(1),
                        Bounds = new FaceSourceRegionModel { X = 0, Y = 0, Width = 1, Height = 1 },
                        PixelCount = 1
                    },
                    new FaceMaskContributionModel
                    {
                        SourcePanel2DElementId = "panel-b",
                        LinkedMachineObjectReference = MachineObjectReference.Lamp(2),
                        Bounds = new FaceSourceRegionModel { X = 2, Y = 2, Width = 1, Height = 1 },
                        PixelCount = 1
                    }
                ]
            },
            Elements =
            [
                new FaceLampWindowElement
                {
                    ObjectId = "a",
                    Name = "A",
                    X = 0,
                    Y = 0,
                    Width = 3,
                    Height = 3,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(1),
                    LinkedPanel2DElementId = "panel-a"
                },
                new FaceLampWindowElement
                {
                    ObjectId = "b",
                    Name = "B",
                    X = 1,
                    Y = 1,
                    Width = 3,
                    Height = 3,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(2),
                    LinkedPanel2DElementId = "panel-b"
                }
            ]
        };

        var plan = new FaceRuntimeTextureGenerator().CreatePlan(document, 4, 4);

        Assert.Collection(
            plan.Trays,
            tray =>
            {
                Assert.Equal(0, tray.X);
                Assert.Equal(0, tray.Y);
                Assert.Equal(1, tray.Width);
                Assert.Equal(1, tray.Height);
            },
            tray =>
            {
                Assert.Equal(2, tray.X);
                Assert.Equal(2, tray.Y);
                Assert.Equal(1, tray.Width);
                Assert.Equal(1, tray.Height);
            });
    }

    [Fact]
    public void CreatePlan_WithOverlappingTemporaryTrays_RecordsValidationOverlap()
    {
        var document = CreateDocumentWithLampWindows(
            new FaceLampWindowElement { ObjectId = "a", Name = "A", X = 0, Y = 0, Width = 2, Height = 2, LinkedMachineObjectReference = MachineObjectReference.Lamp(1) },
            new FaceLampWindowElement { ObjectId = "b", Name = "B", X = 1, Y = 1, Width = 2, Height = 2, LinkedMachineObjectReference = MachineObjectReference.Lamp(2) });

        var plan = new FaceRuntimeTextureGenerator().CreatePlan(document, 4, 4);

        var overlap = Assert.Single(plan.Overlaps);
        Assert.Equal(1, overlap.X);
        Assert.Equal(1, overlap.Y);
        Assert.Equal(1, overlap.ExistingTrayId);
        Assert.Equal(2, overlap.OverlappingTrayId);
    }


    [Fact]
    public void Generate_WithOverlappingTemporaryTrays_UsesFirstTrayOwnership()
    {
        var outputDirectory = Path.Combine(_generatedDirectory, "overlap-texture-test");
        var document = CreateDocumentWithLampWindows(
            new FaceLampWindowElement { ObjectId = "a", Name = "A", X = 0, Y = 0, Width = 2, Height = 2, LinkedMachineObjectReference = MachineObjectReference.Lamp(1) },
            new FaceLampWindowElement { ObjectId = "b", Name = "B", X = 1, Y = 1, Width = 2, Height = 2, LinkedMachineObjectReference = MachineObjectReference.Lamp(2) });

        new FaceRuntimeTextureGenerator().Generate(document, 4, 4, outputDirectory);

        using var trayId = SKBitmap.Decode(Path.Combine(outputDirectory, "trayId.png"));
        using var lampIds0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampIds0.png"));

        Assert.Equal(1, trayId.GetPixel(1, 1).Red);
        Assert.Equal(1, lampIds0.GetPixel(1, 1).Red);
        Assert.Equal(2, trayId.GetPixel(2, 2).Red);
        Assert.Equal(2, lampIds0.GetPixel(2, 2).Red);
    }

    [Fact]
    public void CreatePlan_WithInvalidLampId_ThrowsValidationError()
    {
        var document = CreateDocumentWithLampWindows(
            new FaceLampWindowElement { ObjectId = "bad", Name = "Bad", X = 0, Y = 0, Width = 1, Height = 1 });

        var exception = Assert.Throws<InvalidOperationException>(() => new FaceRuntimeTextureGenerator().CreatePlan(document, 4, 4));
        Assert.Contains("invalid lamp ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void CreatePlan_WithInvalidOutputDimensions_ThrowsValidationError()
    {
        var document = CreateDocument("Assets/artwork.png", "Generated/source-mask.png");

        var exception = Assert.Throws<InvalidOperationException>(() => new FaceRuntimeTextureGenerator().CreatePlan(document, 0, 4));
        Assert.Contains("dimensions", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateManifest_IncludesTexturePathsEmitterMetadataAndTrayMetadata()
    {
        var document = CreateDocument("Assets/artwork.png", "Generated/source-mask.png");

        var manifest = new FaceRuntimeExportService().CreateManifest(document, 4, 4);

        Assert.Equal("trayId.png", manifest.TrayId);
        Assert.Equal("lampIds0.png", manifest.LampIds0);
        Assert.Equal("lampWeights0.png", manifest.LampWeights0);
        Assert.Equal("trayId_debug.png", manifest.TrayIdDebug);
        Assert.Equal("lampWeights_debug.png", manifest.LampWeightsDebug);
        var emitter = Assert.Single(manifest.Lamps);
        Assert.Equal("runtime-emitter-lamp-24", emitter.ObjectId);
        Assert.Equal(1, emitter.TrayId);
        var tray = Assert.Single(manifest.Trays);
        Assert.Equal("runtime-tray-lamp-24", tray.ObjectId);
        Assert.Equal("runtime-emitter-lamp-24", tray.LampEmitterObjectId);
        Assert.Equal(24, tray.LampId);
    }


    [Fact]
    public void CreatePlan_WithAuthoredTraysAndEmitters_UsesAuthoredExportSource()
    {
        var document = CreateDocumentWithAuthoredTrayAndEmitter(
            trayBounds: new FaceSourceRegionModel { X = 0, Y = 0, Width = 2, Height = 2 },
            trayId: 7,
            lampId: 42);

        var plan = new FaceRuntimeTextureGenerator().CreatePlan(document, 4, 4);

        Assert.Equal(FaceRuntimeTextureExportSource.Authored, plan.ExportSource);
        var tray = Assert.Single(plan.Trays);
        Assert.Equal("authored-tray", tray.ObjectId);
        Assert.Equal(7, tray.TrayId);
        Assert.Equal(42, tray.LampId);
        var emitter = Assert.Single(plan.Emitters);
        Assert.Equal("authored-emitter", emitter.ObjectId);
    }

    [Fact]
    public void CreatePlan_WithoutAuthoredData_UsesLampWindowBridgeFallback()
    {
        var document = CreateDocumentWithLampWindows(
            new FaceLampWindowElement { ObjectId = "fallback-lamp", X = 1, Y = 1, Width = 2, Height = 2, LinkedMachineObjectReference = MachineObjectReference.Lamp(9) });

        var plan = new FaceRuntimeTextureGenerator().CreatePlan(document, 4, 4);

        Assert.Equal(FaceRuntimeTextureExportSource.LampWindowBridge, plan.ExportSource);
        Assert.Equal("runtime-tray-fallback-lamp", Assert.Single(plan.Trays).ObjectId);
        Assert.Equal("runtime-emitter-fallback-lamp", Assert.Single(plan.Emitters).ObjectId);
    }

    [Fact]
    public void Generate_WithAuthoredTrayAndEmitter_WritesTrayIdsLampIdsAndFullSingleEmitterWeightsFromAuthoredGeometry()
    {
        var outputDirectory = Path.Combine(_generatedDirectory, "authored-texture-test");
        var document = CreateDocumentWithAuthoredTrayAndEmitter(
            trayBounds: new FaceSourceRegionModel { X = 1, Y = 1, Width = 2, Height = 2 },
            trayId: 5,
            lampId: 77);

        new FaceRuntimeTextureGenerator().Generate(document, 4, 4, outputDirectory);

        using var trayId = SKBitmap.Decode(Path.Combine(outputDirectory, "trayId.png"));
        using var lampIds0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampIds0.png"));
        using var lampWeights0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampWeights0.png"));

        Assert.Equal(4, trayId.Width);
        Assert.Equal(4, trayId.Height);
        Assert.Equal(4, lampIds0.Width);
        Assert.Equal(4, lampIds0.Height);
        Assert.Equal(4, lampWeights0.Width);
        Assert.Equal(4, lampWeights0.Height);
        Assert.Equal(5, trayId.GetPixel(1, 1).Red);
        Assert.Equal(77, lampIds0.GetPixel(1, 1).Red);
        Assert.Equal(255, lampWeights0.GetPixel(1, 1).Red);
        Assert.Equal(0, trayId.GetPixel(0, 0).Alpha);
        Assert.Equal(0, lampIds0.GetPixel(0, 0).Alpha);
        Assert.Equal(0, lampWeights0.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void Generate_WithTwoAuthoredEmitters_WritesSmoothIntensityFalloffWeights()
    {
        var outputDirectory = Path.Combine(_generatedDirectory, "authored-smooth-two-emitter-test");
        var document = CreateDocumentWithAuthoredTrayAndEmitters(
            new FaceSourceRegionModel { X = 0, Y = 0, Width = 10, Height = 1 },
            new TestEmitter("left-emitter", 11, 1, 0.5, 2),
            new TestEmitter("right-emitter", 12, 8, 0.5, 2));

        new FaceRuntimeTextureGenerator().Generate(document, 10, 1, outputDirectory);

        using var lampIds0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampIds0.png"));
        using var lampWeights0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampWeights0.png"));
        using var weightDebug = SKBitmap.Decode(Path.Combine(outputDirectory, "lampWeights_debug.png"));

        Assert.Equal(new SKColor(11, 12, 0, 255), lampIds0.GetPixel(0, 0));
        var nearLeft = lampWeights0.GetPixel(0, 0);
        var middle = lampWeights0.GetPixel(4, 0);
        var nearRight = lampWeights0.GetPixel(9, 0);
        Assert.True(nearLeft.Red > nearLeft.Green);
        Assert.True(nearRight.Green > nearRight.Red);
        Assert.True(nearLeft.Red > 200);
        Assert.True(nearRight.Green > 180);
        Assert.True(nearLeft.Red > middle.Red);
        Assert.True(nearRight.Green > middle.Green);
        Assert.True(middle.Red + middle.Green + middle.Blue < 255);
        Assert.NotEqual(SKColors.White, weightDebug.GetPixel(4, 0));
        Assert.Equal(middle, weightDebug.GetPixel(4, 0));
    }

    [Fact]
    public void Generate_WithOverlappingAuthoredEmitters_RemainsDeterministicAndCapped()
    {
        var firstOutputDirectory = Path.Combine(_generatedDirectory, "authored-overlap-test-a");
        var secondOutputDirectory = Path.Combine(_generatedDirectory, "authored-overlap-test-b");
        var document = CreateDocumentWithAuthoredTrayAndEmitters(
            new FaceSourceRegionModel { X = 0, Y = 0, Width = 3, Height = 1 },
            new TestEmitter("a-emitter", 21, 1.5, 0.5, 1),
            new TestEmitter("b-emitter", 22, 1.5, 0.5, 1));

        var generator = new FaceRuntimeTextureGenerator();
        generator.Generate(document, 3, 1, firstOutputDirectory);
        generator.Generate(document, 3, 1, secondOutputDirectory);

        using var firstIds = SKBitmap.Decode(Path.Combine(firstOutputDirectory, "lampIds0.png"));
        using var firstWeights = SKBitmap.Decode(Path.Combine(firstOutputDirectory, "lampWeights0.png"));
        using var secondIds = SKBitmap.Decode(Path.Combine(secondOutputDirectory, "lampIds0.png"));
        using var secondWeights = SKBitmap.Decode(Path.Combine(secondOutputDirectory, "lampWeights0.png"));

        Assert.Equal(firstIds.GetPixel(1, 0), secondIds.GetPixel(1, 0));
        Assert.Equal(firstWeights.GetPixel(1, 0), secondWeights.GetPixel(1, 0));
        Assert.Equal(new SKColor(21, 22, 0, 255), firstIds.GetPixel(1, 0));
        Assert.True(firstWeights.GetPixel(1, 0).Red + firstWeights.GetPixel(1, 0).Green + firstWeights.GetPixel(1, 0).Blue <= 255);
        Assert.InRange(firstWeights.GetPixel(1, 0).Red, 120, 135);
        Assert.InRange(firstWeights.GetPixel(1, 0).Green, 120, 135);
    }

    [Fact]
    public void Generate_WithAuthoredPolygon_LeavesPixelsOutsidePolygonEmptyInsideBounds()
    {
        var outputDirectory = Path.Combine(_generatedDirectory, "authored-polygon-texture-test");
        var document = CreateDocumentWithAuthoredTrayAndEmitter(
            trayBounds: new FaceSourceRegionModel { X = 0, Y = 0, Width = 4, Height = 4 },
            trayId: 3,
            lampId: 12,
            vertices:
            [
                new FacePointModel { X = 0, Y = 0 },
                new FacePointModel { X = 4, Y = 0 },
                new FacePointModel { X = 0, Y = 4 }
            ]);

        new FaceRuntimeTextureGenerator().Generate(document, 4, 4, outputDirectory);

        using var trayId = SKBitmap.Decode(Path.Combine(outputDirectory, "trayId.png"));
        using var lampWeights0 = SKBitmap.Decode(Path.Combine(outputDirectory, "lampWeights0.png"));

        Assert.Equal(3, trayId.GetPixel(0, 0).Red);
        Assert.Equal(255, lampWeights0.GetPixel(0, 0).Red);
        Assert.Equal(0, trayId.GetPixel(3, 3).Alpha);
        Assert.Equal(0, lampWeights0.GetPixel(3, 3).Alpha);
    }

    [Fact]
    public void CreateManifest_WithAuthoredExport_IncludesAuthoredTrayAndEmitterMetadata()
    {
        var document = CreateDocumentWithAuthoredTrayAndEmitter(
            trayBounds: new FaceSourceRegionModel { X = 1, Y = 1, Width = 2, Height = 2 },
            trayId: 6,
            lampId: 31);

        var manifest = new FaceRuntimeExportService().CreateManifest(document, 4, 4);

        var lamp = Assert.Single(manifest.Lamps);
        Assert.Equal("authored-emitter", lamp.ObjectId);
        Assert.Equal(6, lamp.TrayId);
        Assert.Equal(31, lamp.LampId);
        var tray = Assert.Single(manifest.Trays);
        Assert.Equal("authored-tray", tray.ObjectId);
        Assert.Equal("authored-emitter", tray.LampEmitterObjectId);
        Assert.Equal(6, tray.TrayId);
        Assert.Equal(31, tray.LampId);
    }

    [Fact]
    public void CreatePlan_WithInvalidAuthoredData_ReportsUsefulDiagnostics()
    {
        var document = CreateDocumentWithAuthoredTrayAndEmitter(
            trayBounds: new FaceSourceRegionModel { X = 0, Y = 0, Width = -1, Height = 1 },
            trayId: 1,
            lampId: 10);
        document = new FaceDocumentModel
        {
            Id = document.Id,
            Title = document.Title,
            SourceRegion = document.SourceRegion,
            Trays =
            [
                document.Trays[0],
                new FaceTrayModel { ObjectId = "orphan-tray", Name = "Orphan", Bounds = new FaceSourceRegionModel { X = 2, Y = 2, Width = 1, Height = 1 } }
            ],
            LampEmitters =
            [
                document.LampEmitters[0],
                new FaceLampEmitterElement { ObjectId = "authored-emitter", TrayObjectId = "missing-tray", TrayId = 2, LampId = 11 }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new FaceRuntimeTextureGenerator().CreatePlan(document, 4, 4));

        Assert.Contains("invalid tray geometry", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not have an emitter", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("references missing tray", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate emitter ID", exception.Message, StringComparison.OrdinalIgnoreCase);
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



    private static FaceDocumentModel CreateDocumentWithLampWindows(params FaceLampWindowElement[] lampWindows)
    {
        return new FaceDocumentModel
        {
            Id = "face-runtime",
            Title = "Runtime Face",
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 4, Height = 4 },
            Elements = lampWindows
        };
    }

    private static FaceDocumentModel CreateDocumentWithAuthoredTrayAndEmitter(
        FaceSourceRegionModel trayBounds,
        int trayId,
        int lampId,
        IReadOnlyList<FacePointModel>? vertices = null)
    {
        return new FaceDocumentModel
        {
            Id = "face-runtime",
            Title = "Runtime Face",
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 4, Height = 4 },
            Trays =
            [
                new FaceTrayModel
                {
                    ObjectId = "authored-tray",
                    Name = "Authored Tray",
                    SourceLampWindowObjectId = "source-lamp-window",
                    Bounds = trayBounds,
                    Vertices = vertices ?? []
                }
            ],
            LampEmitters =
            [
                new FaceLampEmitterElement
                {
                    ObjectId = "authored-emitter",
                    Name = "Authored Emitter",
                    SourceLampWindowObjectId = "source-lamp-window",
                    TrayObjectId = "authored-tray",
                    TrayId = trayId,
                    LampId = lampId,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(lampId),
                    CenterX = trayBounds.X + (trayBounds.Width / 2d),
                    CenterY = trayBounds.Y + (trayBounds.Height / 2d),
                    Width = trayBounds.Width,
                    Height = trayBounds.Height
                }
            ]
        };
    }

    private static FaceDocumentModel CreateDocumentWithAuthoredTrayAndEmitters(FaceSourceRegionModel trayBounds, params TestEmitter[] emitters)
    {
        return new FaceDocumentModel
        {
            Id = "face-runtime",
            Title = "Runtime Face",
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = Math.Max(1, trayBounds.X + trayBounds.Width), Height = Math.Max(1, trayBounds.Y + trayBounds.Height) },
            Trays =
            [
                new FaceTrayModel
                {
                    ObjectId = "authored-tray",
                    Name = "Authored Tray",
                    SourceLampWindowObjectId = "source-lamp-window",
                    Bounds = trayBounds
                }
            ],
            LampEmitters = emitters
                .Select(emitter => new FaceLampEmitterElement
                {
                    ObjectId = emitter.ObjectId,
                    Name = emitter.ObjectId,
                    SourceLampWindowObjectId = "source-lamp-window",
                    TrayObjectId = "authored-tray",
                    TrayId = 5,
                    LampId = emitter.LampId,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(emitter.LampId),
                    CenterX = emitter.CenterX,
                    CenterY = emitter.CenterY,
                    Radius = emitter.Radius,
                    Width = trayBounds.Width,
                    Height = trayBounds.Height
                })
                .ToArray()
        };
    }

    private sealed record TestEmitter(string ObjectId, int LampId, double CenterX, double CenterY, double? Radius = null);

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
