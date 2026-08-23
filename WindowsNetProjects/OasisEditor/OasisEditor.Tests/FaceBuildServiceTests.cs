using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceBuildServiceTests
{
    [Fact]
    public void ArtworkCorrection_InvalidatesArtworkAndRuntimeOnly()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, true, true, true, true);
        new FaceBuildService().Invalidate(state, FaceBuildInput.ArtworkProcessing);
        Assert.Equal(FaceBuildStatus.Current, state.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.RuntimeAssets).Status);
        Assert.Equal(FaceBuildStatus.Current, state.Get(FaceGeneratedProduct.Trays).Status);
    }

    [Fact]
    public void MaskSettings_InvalidatesIlluminationChainButNotArtwork()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, true, true, true, true);

        new FaceBuildService().Invalidate(state, FaceBuildInput.MaskSettings);

        Assert.Equal(FaceBuildStatus.Current, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.Trays).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.RuntimeAssets).Status);
    }

    [Fact]
    public void Build_InvokesOnlyStaleConfiguredProducts()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, false, true, false, false);
        state.Get(FaceGeneratedProduct.ArtworkOutput).Status = FaceBuildStatus.Stale;
        var calls = new List<FaceGeneratedProduct>();
        var result = new FaceBuildService().Build(state, Builders(calls));
        Assert.Equal([FaceGeneratedProduct.ArtworkOutput], result.Built);
        Assert.DoesNotContain(FaceGeneratedProduct.Trays, calls);
    }

    [Fact]
    public void Rebuild_InvokesEveryConfiguredProduct()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, true, true, true, true);
        var calls = new List<FaceGeneratedProduct>();
        var result = new FaceBuildService().Build(state, Builders(calls), force: true);
        Assert.True(result.Succeeded);
        Assert.Equal(6, calls.Count);
    }

    [Fact]
    public void Failure_RetainsErrorAndSkipsDependentProducts()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(false, true, true, true, true);
        state.Get(FaceGeneratedProduct.LampMask).Status = FaceBuildStatus.Stale;
        state.Get(FaceGeneratedProduct.Trays).Status = FaceBuildStatus.Stale;
        var builders = Builders([]).ToDictionary(pair => pair.Key, pair => pair.Value);
        builders[FaceGeneratedProduct.LampMask] = () => new(FaceGeneratedProduct.LampMask, false, "mask unavailable");
        var result = new FaceBuildService().Build(state, builders);
        Assert.False(result.Succeeded);
        Assert.Equal(FaceBuildStatus.Error, state.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal("mask unavailable", state.Get(FaceGeneratedProduct.LampMask).ErrorMessage);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.Trays).Status);
    }

    [Fact]
    public void RelevantChange_RecoversErrorToStale_ThenBuildToCurrent()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false);
        state.Get(FaceGeneratedProduct.ArtworkOutput).Status = FaceBuildStatus.Error;
        state.Get(FaceGeneratedProduct.ArtworkOutput).ErrorMessage = "old failure";
        var service = new FaceBuildService();
        service.Invalidate(state, FaceBuildInput.ArtworkProcessing);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Null(state.Get(FaceGeneratedProduct.ArtworkOutput).ErrorMessage);
        service.Build(state, Builders([]));
        Assert.Equal(FaceBuildStatus.Current, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
    }

    [Fact]
    public void BaseFailure_SkipsOutputAndLeavesItStale()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false);
        new FaceBuildService().Invalidate(state, FaceBuildInput.ArtworkSource);
        var builders = Builders([]).ToDictionary(pair => pair.Key, pair => pair.Value);
        builders[FaceGeneratedProduct.BaseArtwork] = () => new(FaceGeneratedProduct.BaseArtwork, false, "source unavailable");

        var result = new FaceBuildService().Build(state, builders);

        Assert.False(result.Succeeded);
        Assert.Equal(FaceBuildStatus.Error, state.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.DoesNotContain(FaceGeneratedProduct.ArtworkOutput, result.Built);
    }

    [Fact]
    public void NotConfigured_IsSkippedWithoutFailure()
    {
        var result = new FaceBuildService().Build(new FaceBuildStateModel(), new Dictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>>());
        Assert.True(result.Succeeded);
        Assert.Empty(result.Built);
    }

    [Fact]
    public void ForcedRebuild_SkipsNotConfiguredRuntimeAssets()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false);
        var calls = new List<FaceGeneratedProduct>();

        var result = new FaceBuildService().Build(state, Builders(calls), force: true);

        Assert.True(result.Succeeded);
        Assert.Equal([FaceGeneratedProduct.ArtworkCorrectionInput, FaceGeneratedProduct.BaseArtwork, FaceGeneratedProduct.ArtworkOutput], calls);
        Assert.Equal(FaceBuildStatus.NotConfigured, state.Get(FaceGeneratedProduct.RuntimeAssets).Status);
    }

    [Fact]
    public void SharpeningChange_InvalidatesCorrectionInputAndDownstreamArtwork()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false);

        new FaceBuildService().Invalidate(state, FaceBuildInput.ArtworkPreprocessing);

        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
    }

    [Fact]
    public void StaleIlluminationChain_BuildsMaskThenTraysThenRuntime()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(false, true, true, true, true);
        new FaceBuildService().Invalidate(state, FaceBuildInput.MaskSettings);
        var calls = new List<FaceGeneratedProduct>();

        var result = new FaceBuildService().Build(state, Builders(calls));

        Assert.True(result.Succeeded);
        Assert.Equal([FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeAssets], calls);
        Assert.All(calls, product => Assert.Equal(FaceBuildStatus.Current, state.Get(product).Status));
    }

    [Fact]
    public void LampMaskFailure_DoesNotInvokeDependentBuilders()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(false, true, true, true, true);
        new FaceBuildService().Invalidate(state, FaceBuildInput.MaskSettings);
        var calls = new List<FaceGeneratedProduct>();
        var builders = Builders(calls).ToDictionary(pair => pair.Key, pair => pair.Value);
        builders[FaceGeneratedProduct.LampMask] = () =>
        {
            calls.Add(FaceGeneratedProduct.LampMask);
            return new(FaceGeneratedProduct.LampMask, false, "source unavailable");
        };

        new FaceBuildService().Build(state, builders);

        Assert.Equal([FaceGeneratedProduct.LampMask], calls);
        Assert.Equal(FaceBuildStatus.Error, state.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.Trays).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.RuntimeAssets).Status);
    }

    [Fact]
    public void ForcedDocumentRebuild_UsesLampMaskExecutorRatherThanMissingBuilderFailure()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(false, true, false, false, false);
        var model = new FaceDocumentModel
        {
            Title = "Mask Face", SourcePanel2DDocumentPath = "Assets/source.panel2d",
            SourceFaceShapeId = "shape-1", BuildState = state
        };
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Mask Face"),
            faceDocumentJson: FaceDocumentStorage.Serialize(model));

        var result = document.BuildFace(force: true);

        var failure = Assert.Single(result.Failed);
        Assert.Equal(FaceGeneratedProduct.LampMask, failure.Product);
        Assert.Contains("no project is open", failure.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No builder is available", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentBuild_ReusesSourceShapeMaskGeneratorAndUpdatesMaskLayer()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-mask-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.panel2d");
            var lampPath = Path.Combine(directory, "lamp.png");
            var facePath = Path.Combine(directory, "asset.face");
            WriteOpaquePng(lampPath);
            var sourceLamp = new PanelElementModel
            {
                ObjectId = "lamp-1", Name = "Lamp", Kind = PanelElementKind.Lamp,
                X = 0, Y = 0, Width = 10, Height = 10, AssetPath = "lamp.png", IsVisible = true
            };
            var shape = new PanelFaceSourceShapeModel
            {
                Id = "shape-1", Name = "Face", TopLeft = new() { X = 0, Y = 0 },
                TopRight = new() { X = 10, Y = 0 }, BottomRight = new() { X = 10, Y = 10 },
                BottomLeft = new() { X = 0, Y = 10 }
            };
            File.WriteAllText(sourcePath, Panel2DDocumentStorage.Serialize("Source", string.Empty,
                [Panel2DDocumentStorage.ToStorageElement(sourceLamp)],
                [Panel2DDocumentStorage.ToStorageFaceSourceShape(shape)]));
            var state = FaceBuildStateFactory.CreateGeneratedState(false, true, false, false, false);
            state.Get(FaceGeneratedProduct.LampMask).Status = FaceBuildStatus.Stale;
            var model = new FaceDocumentModel
            {
                Title = "Mask Face", SourcePanel2DDocumentPath = sourcePath, SourceFaceShapeId = shape.Id,
                Artwork = new FaceArtworkModel { OutputWidth = 4, OutputHeight = 4 }, BuildState = state,
                MaskLayer = new FaceMaskLayerModel { AssetPath = "Generated/Faces/Mask Face/mask.png", Width = 4, Height = 4 },
                Elements = [new FaceLampWindowElement { ObjectId = "window-1", LinkedPanel2DElementId = sourceLamp.ObjectId, X = 0, Y = 0, Width = 4, Height = 4, IsVisible = true }]
            };
            File.WriteAllText(facePath, FaceDocumentStorage.Serialize(model));
            var document = new DocumentTabViewModel(EditorDocument.CreateFromFile(facePath, "Mask Face"),
                faceDocumentJson: File.ReadAllText(facePath));
            document.SetProjectAccessor(() => Project(directory));

            var result = document.BuildFace();

            Assert.True(result.Succeeded);
            Assert.Equal([FaceGeneratedProduct.LampMask], result.Built);
            Assert.Equal(FaceBuildStatus.Current, document.GetFaceDocument().BuildState.Get(FaceGeneratedProduct.LampMask).Status);
            Assert.Single(document.GetFaceDocument().MaskLayer!.Contributions);
            Assert.True(File.Exists(Path.Combine(directory, "Generated", "Faces", "Mask Face", "mask.png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ArtworkCorrection_StandaloneFaceWithoutCabinetBuildsArtworkAndSkipsRuntimeAssets()
    {
        var face = new FaceDocumentModel
        {
            Artwork = new FaceArtworkModel { OutputWidth = 4, OutputHeight = 4 },
            MaskLayer = new FaceMaskLayerModel { Width = 4, Height = 4 },
            Trays = [new FaceTrayModel { ObjectId = "tray-1" }],
            Elements = [new FaceReelDisplayElement { ObjectId = "reel-1", ReelSpecificationId = "standard" }],
            BuildState = FaceBuildStateFactory.CreateGeneratedState(true, true, true, false, false)
        };
        var configuration = new FaceRuntimeAssetsConfigurationService();
        configuration.Reconcile(face, configuration.Evaluate(face, Project(Path.GetTempPath()), []));
        new FaceBuildService().Invalidate(face.BuildState, FaceBuildInput.ArtworkProcessing);
        var runtimeInvoked = false;
        var executors = new Dictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>>
        {
            [FaceGeneratedProduct.BaseArtwork] = () => new(FaceGeneratedProduct.BaseArtwork, true),
            [FaceGeneratedProduct.ArtworkOutput] = () => new(FaceGeneratedProduct.ArtworkOutput, true),
            [FaceGeneratedProduct.RuntimeAssets] = () =>
            {
                runtimeInvoked = true;
                return new(FaceGeneratedProduct.RuntimeAssets, true);
            }
        };

        var result = new FaceBuildService().Build(face.BuildState, executors);

        Assert.True(result.Succeeded);
        Assert.Equal(FaceBuildStatus.Current, face.BuildState.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.NotConfigured, face.BuildState.Get(FaceGeneratedProduct.RuntimeAssets).Status);
        Assert.False(runtimeInvoked);
    }

    [Fact]
    public void StandaloneCabinetCapability_ConfiguresRuntimeAssetsAndRemovalReturnsToNotConfigured()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-runtime-capability-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var cabinetPath = Path.Combine(directory, "cabinet.cabinet3d");
            File.WriteAllText(cabinetPath, CabinetDocumentStorage.Serialize(new CabinetDocument(
                5, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default,
                [new CabinetReelSpecification("standard", "Standard", 210, 50)], "standard")));
            var face = new FaceDocumentModel
            {
                AssignedCabinetAssetPath = "cabinet.cabinet3d",
                Artwork = new FaceArtworkModel { OutputWidth = 4, OutputHeight = 4 },
                Elements = [new FaceReelDisplayElement { ObjectId = "reel-1", ReelSpecificationId = "standard" }],
                BuildState = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false)
            };
            var service = new FaceRuntimeAssetsConfigurationService();

            var configured = service.Evaluate(face, Project(directory), []);
            service.Reconcile(face, configured);

            Assert.True(configured.IsConfigured, configured.Reason);
            Assert.Equal(FaceBuildStatus.Stale, face.BuildState.Get(FaceGeneratedProduct.RuntimeAssets).Status);

            var removed = new FaceDocumentModel
            {
                Artwork = face.Artwork, BuildState = face.BuildState, RuntimeRenderAssets = face.RuntimeRenderAssets
            };
            service.Reconcile(removed, service.Evaluate(removed, Project(directory), []));
            Assert.Equal(FaceBuildStatus.NotConfigured, removed.BuildState.Get(FaceGeneratedProduct.RuntimeAssets).Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IReadOnlyDictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>> Builders(IList<FaceGeneratedProduct> calls) =>
        Enum.GetValues<FaceGeneratedProduct>().ToDictionary(product => product, product => (Func<FaceBuildNodeResult>)(() =>
        {
            calls.Add(product);
            return new FaceBuildNodeResult(product, true);
        }));

    private static EditorProject Project(string directory) => new()
    {
        Name = "Test", ProjectFilePath = Path.Combine(directory, "test.oasis"), ProjectDirectory = directory,
        AssetsDirectory = Path.Combine(directory, "Assets"), MachinesDirectory = Path.Combine(directory, "Machines"),
        GeneratedDirectory = Path.Combine(directory, "Generated")
    };

    private static void WriteOpaquePng(string path)
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
