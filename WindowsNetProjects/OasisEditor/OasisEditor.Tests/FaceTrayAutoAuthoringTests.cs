using System.Windows;
using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceTrayAutoAuthoringTests
{
    [Fact]
    public void GenerateFromPanelRegion_AutoAuthorsTrayAndEmitterForLampWindowFallback()
    {
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-7",
                    Name = "Collect",
                    Kind = PanelElementKind.Lamp,
                    X = 25,
                    Y = 35,
                    Width = 40,
                    Height = 50,
                    DisplayNumber = 7,
                    IsVisible = true
                }
            ]
        };

        var region = FaceSourceRegionModel.FromRect(new Rect(20, 30, 100, 100));
        var document = new FaceGenerationService().GenerateFromPanelRegion(
            panel,
            region,
            "Face",
            "panel-doc").Document;
        var regenerated = new FaceGenerationService().GenerateFromPanelRegion(
            panel,
            region,
            "Face",
            "panel-doc").Document;

        Assert.Equal(document.Trays.Single().ObjectId, regenerated.Trays.Single().ObjectId);
        Assert.Equal(document.LampEmitters.Single().ObjectId, regenerated.LampEmitters.Single().ObjectId);

        var tray = Assert.Single(document.Trays);
        Assert.True(tray.IsAutoAuthored);
        Assert.Equal("lampWindowBounds", tray.AutoAuthoringSource);
        Assert.Equal(-2.6d, tray.Bounds!.X, 3);
        Assert.Equal(-3.35d, tray.Bounds.Y, 3);
        Assert.Equal(55.2d, tray.Bounds.Width, 3);
        Assert.Equal(66.7d, tray.Bounds.Height, 3);
        Assert.Equal(4, tray.Vertices.Count);

        var emitter = Assert.Single(document.LampEmitters);
        Assert.True(emitter.IsAutoAuthored);
        Assert.Equal(tray.ObjectId, emitter.TrayObjectId);
        Assert.Equal("lamp:7", emitter.LinkedMachineObjectReference?.ToString());
        Assert.Equal(7, emitter.LampId);
        Assert.Equal(25d, emitter.CenterX);
        Assert.Equal(30d, emitter.CenterY);
    }

    [Fact]
    public void AutoAuthor_PrefersMatchingMaskContributionBounds()
    {
        var document = CreateFaceWithLampAndContribution();

        var result = new FaceTrayAutoAuthoringService().AutoAuthor(document);

        var tray = Assert.Single(result.Trays);
        Assert.Equal("maskContributionBounds", tray.AutoAuthoringSource);
        Assert.Equal(2.575d, tray.Bounds!.X, 3);
        Assert.Equal(3.5d, tray.Bounds.Y, 3);
        Assert.Equal(21.85d, tray.Bounds.Width, 3);
        Assert.Equal(23d, tray.Bounds.Height, 3);
        Assert.Equal("lamp:3", tray.LinkedMachineObjectReference?.ToString());

        var emitter = Assert.Single(result.Emitters);
        Assert.Equal(tray.ObjectId, emitter.TrayObjectId);
        Assert.Equal(20d, emitter.CenterX);
        Assert.Equal(25d, emitter.CenterY);
    }

    [Fact]
    public void AutoAuthor_UsesDeterministicIdsForSameSourceData()
    {
        var left = new FaceTrayAutoAuthoringService().AutoAuthor(CreateFaceWithLampAndContribution());
        var right = new FaceTrayAutoAuthoringService().AutoAuthor(CreateFaceWithLampAndContribution());

        Assert.Equal(left.Trays.Single().ObjectId, right.Trays.Single().ObjectId);
        Assert.Equal(left.Emitters.Single().ObjectId, right.Emitters.Single().ObjectId);
    }

    [Fact]
    public void Serialize_AndRead_RoundTripsAuthoredTraysAndEmitters()
    {
        var authored = new FaceTrayAutoAuthoringService().AutoAuthor(CreateFaceWithLampAndContribution());
        var source = CreateFaceWithLampAndContribution().WithAuthored(authored);

        var json = FaceDocumentStorage.Serialize(source);
        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var savedTray = Assert.Single(file.Trays!);
        var savedEmitter = Assert.Single(file.LampEmitters!);
        Assert.True(savedTray.IsAutoAuthored);
        Assert.True(savedEmitter.IsAutoAuthored);
        Assert.Equal(savedTray.ObjectId, savedEmitter.TrayObjectId);

        var model = FaceDocumentStorage.ToModel(file);
        var tray = Assert.Single(model.Trays);
        var emitter = Assert.Single(model.LampEmitters);
        Assert.Equal(savedTray.ObjectId, tray.ObjectId);
        Assert.Equal(4, tray.Vertices.Count);
        Assert.Equal(tray.ObjectId, emitter.TrayObjectId);
        Assert.Equal("lamp:3", emitter.LinkedMachineObjectReference?.ToString());
    }

    [Fact]
    public void Validate_DetectsDuplicateIdsInvalidBoundsAndMissingTrayReferences()
    {
        var diagnostics = new FaceTrayAutoAuthoringService().Validate(new FaceDocumentModel
        {
            Trays =
            [
                new FaceTrayModel { ObjectId = "tray-a", Name = "A", Bounds = FaceSourceRegionModel.FromRect(new Rect(0, 0, 10, 10)) },
                new FaceTrayModel { ObjectId = "tray-a", Name = "B", Bounds = new FaceSourceRegionModel { X = 1, Y = 1, Width = 0, Height = 10 } }
            ],
            LampEmitters =
            [
                new FaceLampEmitterElement { ObjectId = "emitter-a", TrayObjectId = "missing", CenterX = 1, CenterY = 1 },
                new FaceLampEmitterElement { ObjectId = "emitter-a", TrayObjectId = "tray-a", CenterX = 2, CenterY = 2 }
            ]
        });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.Tray.DuplicateId");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.Emitter.DuplicateId");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.Tray.Bounds.Invalid");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.Emitter.TrayReference.Missing");
    }

    [Fact]
    public void DebugOverlayRenderer_DrawsTrayAndEmitterWithoutVisibleWpfWindow()
    {
        using var bitmap = new SKBitmap(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        var document = new FaceDocumentModel
        {
            Trays =
            [
                new FaceTrayModel
                {
                    ObjectId = "tray-debug",
                    IsAutoAuthored = true,
                    Bounds = FaceSourceRegionModel.FromRect(new Rect(5, 5, 30, 20)),
                    Vertices =
                    [
                        new FacePointModel { X = 5, Y = 5 },
                        new FacePointModel { X = 35, Y = 5 },
                        new FacePointModel { X = 35, Y = 25 },
                        new FacePointModel { X = 5, Y = 25 }
                    ]
                }
            ],
            LampEmitters =
            [
                new FaceLampEmitterElement
                {
                    ObjectId = "emitter-debug",
                    TrayObjectId = "tray-debug",
                    LampId = 9,
                    CenterX = 20,
                    CenterY = 15,
                    IsAutoAuthored = true
                }
            ]
        };

        var drawCount = new FaceTrayDebugOverlayRenderer().Render(canvas, document, PanelViewportTransform.Identity);

        Assert.Equal(2, drawCount);
    }


    [Fact]
    public void AutoAuthor_ClipsPartialOverlapsDeterministically()
    {
        var document = CreateFaceWithLampWindows(
            ("left", 1, 0d, 0d, 10d, 10d),
            ("right", 2, 8d, 0d, 10d, 10d));

        var first = new FaceTrayAutoAuthoringService().AutoAuthor(document);
        var second = new FaceTrayAutoAuthoringService().AutoAuthor(document);

        Assert.All(first.Trays, tray => Assert.Contains("partial-overlap-clipped", tray.Diagnostics));
        Assert.Equal(first.Trays.SelectMany(t => t.Vertices.Select(v => (v.X, v.Y))), second.Trays.SelectMany(t => t.Vertices.Select(v => (v.X, v.Y))));
        Assert.Contains(first.Trays, tray => tray.Vertices.Any(vertex => Math.Abs(vertex.X - 9d) < 0.001d));
    }

    [Fact]
    public void AutoAuthor_DerivesOctagonForRoundishIsolatedLamp()
    {
        var result = new FaceTrayAutoAuthoringService().AutoAuthor(CreateFaceWithLampWindows(("round", 4, 0d, 0d, 20d, 20d)));

        var tray = Assert.Single(result.Trays);
        Assert.Equal(8, tray.Vertices.Count);
        Assert.Contains("isolated-roundish-octagon", tray.Diagnostics);
    }

    [Fact]
    public void AutoAuthor_AddsContainmentDiagnosticWithoutMerging()
    {
        var result = new FaceTrayAutoAuthoringService().AutoAuthor(CreateFaceWithLampWindows(
            ("large", 5, 0d, 0d, 40d, 40d),
            ("small", 6, 10d, 10d, 10d, 10d)));

        Assert.Equal(2, result.Trays.Count);
        Assert.All(result.Trays, tray => Assert.Contains("contained-tray-candidate", tray.Diagnostics));
    }

    [Fact]
    public void AutoAuthor_AddsSharedTrayDiagnosticWithoutMerging()
    {
        var result = new FaceTrayAutoAuthoringService().AutoAuthor(CreateFaceWithLampWindows(
            ("first", 7, 0d, 0d, 20d, 20d),
            ("second", 8, 2d, 2d, 20d, 20d)));

        Assert.Equal(2, result.Trays.Count);
        Assert.All(result.Trays, tray => Assert.Contains("possible-shared-tray-candidate", tray.Diagnostics));
    }

    [Fact]
    public void Serialize_AndRead_RoundTripsTrayDiagnostics()
    {
        var authored = new FaceTrayAutoAuthoringService().AutoAuthor(CreateFaceWithLampWindows(("round", 9, 0d, 0d, 20d, 20d)));
        var json = FaceDocumentStorage.Serialize(new FaceDocumentModel { Trays = authored.Trays, LampEmitters = authored.Emitters });

        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var model = FaceDocumentStorage.ToModel(file);

        Assert.Contains("isolated-roundish-octagon", Assert.Single(model.Trays).Diagnostics);
    }

    private static FaceDocumentModel CreateFaceWithLampAndContribution()
    {
        return new FaceDocumentModel
        {
            GenerationSettings = FaceGenerationSettingsModel.Default,
            MaskLayer = new FaceMaskLayerModel
            {
                Contributions =
                [
                    new FaceMaskContributionModel
                    {
                        SourcePanel2DElementId = "panel-lamp-3",
                        LinkedMachineObjectReference = MachineObjectReference.Lamp(3),
                        Bounds = FaceSourceRegionModel.FromRect(new Rect(8, 9, 11, 12)),
                        PixelCount = 24
                    }
                ]
            },
            Elements =
            [
                new FaceLampWindowElement
                {
                    ObjectId = "face-panel-lamp-3",
                    Name = "Lamp Three",
                    X = 10,
                    Y = 10,
                    Width = 20,
                    Height = 30,
                    IsVisible = true,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(3),
                    LinkedPanel2DElementId = "panel-lamp-3"
                }
            ]
        };
    }
    private static FaceDocumentModel CreateFaceWithLampWindows(params (string Id, int LampId, double X, double Y, double Width, double Height)[] lamps)
    {
        return new FaceDocumentModel
        {
            GenerationSettings = new FaceGenerationSettingsModel
            {
                TrayBoundsInflationPercent = 0,
                TrayBoundsPaddingPixels = 0,
                ClampTrayBoundsToLampWindow = false
            },
            Elements = lamps.Select(lamp => new FaceLampWindowElement
            {
                ObjectId = $"face-{lamp.Id}",
                Name = lamp.Id,
                X = lamp.X,
                Y = lamp.Y,
                Width = lamp.Width,
                Height = lamp.Height,
                IsVisible = true,
                LinkedMachineObjectReference = MachineObjectReference.Lamp(lamp.LampId),
                LinkedPanel2DElementId = $"panel-{lamp.Id}"
            }).ToArray()
        };
    }

}

internal static class FaceTrayAutoAuthoringTestExtensions
{
    public static FaceDocumentModel WithAuthored(this FaceDocumentModel source, FaceTrayAutoAuthoringResult authored)
    {
        return new FaceDocumentModel
        {
            Id = source.Id,
            Title = source.Title,
            MaskLayer = source.MaskLayer,
            Trays = authored.Trays,
            LampEmitters = authored.Emitters,
            Elements = source.Elements
        };
    }
}
