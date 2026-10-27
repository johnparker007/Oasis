using OasisEditor;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class FaceBuildWorkflowTests : IDisposable
{
    private static string Payload(FaceDocumentModel face) => FaceDocumentStorage.SerializePayload(FaceDocumentStorage.ToFile(face));
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "OasisFaceBuildTests", Guid.NewGuid().ToString("N"));

    public FaceBuildWorkflowTests() => Directory.CreateDirectory(_workspace);

    [Fact]
    public void BuildFace_NormalizesAuthoredMaskAndAutoAuthorsTraysAsOneUndoableEdit()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Mask Build");
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var faces = new FaceAuthoringSession(project);
        faces.CreateFace("Front");
        var artworkSource = Path.Combine(_workspace, "artwork.png");
        var maskSource = Path.Combine(_workspace, "mask.png");
        WriteImage(artworkSource, 30, 20, (_, _) => new SKColor(210, 40, 20));
        WriteImage(maskSource, 7, 5, (x, y) => new SKColor((byte)(20 + x), (byte)(60 + y), 190, (byte)(120 + x + y)));
        faces.ImportArtwork(artworkSource);
        faces.ImportLampMask(maskSource);
        faces.AddLamp("Start", 5, 6, 8, 10, lampIndex: 3);

        var before = Payload(faces.Document!);
        var authoredMaskPath = Path.Combine(root, faces.Document!.MaskLayer!.AuthoredAssetPath!.Replace('/', Path.DirectorySeparatorChar));
        var authoredMaskBytes = File.ReadAllBytes(authoredMaskPath);
        faces.BuildFace();

        var built = faces.Document!;
        var mask = built.MaskLayer!;
        Assert.Equal(FaceLampMaskSourceKind.AuthoredImage, mask.SourceKind);
        Assert.NotEqual(mask.AuthoredAssetPath, mask.AssetPath);
        Assert.Equal(authoredMaskBytes, File.ReadAllBytes(authoredMaskPath));
        Assert.Equal(built.Artwork!.FinalOutputWidth, mask.Width);
        Assert.Equal(built.Artwork.FinalOutputHeight, mask.Height);
        using (var normalizedMask = SKBitmap.Decode(Path.Combine(root, mask.AssetPath!.Replace('/', Path.DirectorySeparatorChar))))
        {
            Assert.NotNull(normalizedMask);
            Assert.Equal(built.Artwork.FinalOutputWidth, normalizedMask!.Width);
            Assert.Equal(built.Artwork.FinalOutputHeight, normalizedMask.Height);
        }
        var tray = Assert.Single(built.Trays);
        Assert.True(tray.IsAutoAuthored);
        Assert.Equal("Start", tray.Name.Replace(" Tray", string.Empty, StringComparison.Ordinal));
        Assert.True(Assert.Single(built.LampEmitters).IsAutoAuthored);
        Assert.Equal(FaceBuildStatus.Current, built.BuildState.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Current, built.BuildState.Get(FaceGeneratedProduct.Trays).Status);

        var builtPayload = Payload(built);
        faces.Undo();
        Assert.Equal(before, Payload(faces.Document!));
        faces.Redo();
        Assert.Equal(builtPayload, Payload(faces.Document!));
    }

    [Fact]
    public void BuildFace_RejectsExternalGeneratedChangesBeforeWriting()
    {
        var (_, faces, root, source, _) = CreateBuildableFace("Generated Conflict");
        faces.BuildFace();
        var builtPayload = Payload(faces.Document!);
        var generatedPath = Path.Combine(root, faces.Document!.MaskLayer!.AssetPath!.Replace('/', Path.DirectorySeparatorChar));
        File.AppendAllText(generatedPath, "external edit");
        var externallyChanged = File.ReadAllBytes(generatedPath);

        Assert.Throws<IOException>(() => faces.BuildFace());

        Assert.Equal(builtPayload, Payload(faces.Document!));
        Assert.Equal(externallyChanged, File.ReadAllBytes(generatedPath));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void BuildFace_RestoresGeneratedFilesAndHistoryWhenRuntimeExportFails()
    {
        var (_, faces, root, _, _) = CreateBuildableFace("Rollback");
        var before = Payload(faces.Document!);
        var filesBefore = GeneratedFingerprint(root, "Rollback");
        var canUndoBefore = faces.CanUndo;

        Assert.Throws<InvalidOperationException>(() => faces.BuildFace(_ => throw new InvalidOperationException("runtime export failed")));

        Assert.Equal(before, Payload(faces.Document!));
        Assert.Equal(canUndoBefore, faces.CanUndo);
        Assert.Equal(filesBefore, GeneratedFingerprint(root, "Rollback"));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(root), path => Path.GetFileName(path).StartsWith(".oasis-face-build-", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildFace_PreservesRecoveryFilesWhenRollbackIsBlockedByAnOpenFile()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows sharing rules prevent deleting an open generated output.
        var (_, faces, root, _, _) = CreateBuildableFace("Recovery");
        var generated = Path.Combine(root, "Generated", "Faces", "Recovery");
        var originalFiles = Directory.GetFiles(generated, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(generated, path), File.ReadAllBytes);
        var before = Payload(faces.Document!);
        FileStream? locked = null;
        try
        {
            var error = Assert.Throws<IOException>(() => faces.BuildFace(face =>
            {
                var path = Path.Combine(root, "Generated", "Faces", "Recovery", "held-open.txt");
                locked = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
                throw new InvalidOperationException("Export failed with a locked generated file.");
            }));
            var recovery = Assert.Single(Directory.GetDirectories(root, ".oasis-face-build-*"));
            Assert.Contains(recovery, error.Message);
            Assert.Equal(before, Payload(faces.Document!));
            foreach (var file in originalFiles)
                Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(recovery, file.Key)));
        }
        finally { locked?.Dispose(); }
    }

    [Fact]
    public void BuildFace_RestoresGeneratedFilesAndHistoryWhenCanceledAfterGeneration()
    {
        var (_, faces, root, _, authoredMask) = CreateBuildableFace("Canceled");
        var before = Payload(faces.Document!);
        var filesBefore = GeneratedFingerprint(root, "Canceled");
        var authoredBefore = File.ReadAllBytes(authoredMask);
        using var cancellation = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() => faces.BuildFace(face =>
        {
            cancellation.Cancel();
            return face;
        }, cancellation.Token));

        Assert.Equal(before, Payload(faces.Document!));
        Assert.Equal(filesBefore, GeneratedFingerprint(root, "Canceled"));
        Assert.Equal(authoredBefore, File.ReadAllBytes(authoredMask));
    }

    [Fact]
    public void UsePanelArtworkSource_PreservesComponentsIlluminationOverrideAndPipelineThenBuildsNativeOutputs()
    {
        var (project, root, panelPath, panelRelative, _) = CreateSourcePanel("Use Panel Artwork");
        var faces = new FaceAuthoringSession(project);
        faces.CreateFace("Front");
        var importedArtwork = Path.Combine(_workspace, "imported-artwork.png");
        var overrideSource = Path.Combine(_workspace, "override-artwork.png");
        var maskSource = Path.Combine(_workspace, "authored-mask.png");
        WriteImage(importedArtwork, 24, 16, (_, _) => new SKColor(220, 30, 20));
        WriteImage(overrideSource, 10, 8, (_, _) => new SKColor(15, 40, 230, 220));
        WriteImage(maskSource, 12, 6, (_, _) => new SKColor(50, 80, 110, 150));
        faces.ImportArtwork(importedArtwork);
        faces.SetArtworkCalibration(new ArtworkCalibrationOperationModel { Strength = 32, CorrectSpatialBrightness = false });
        faces.ImportArtworkOverride(overrideSource);
        faces.SetArtworkOverrideAlignment(.1, .1, .8, .8, FaceArtworkOverrideAlphaSource.OverrideImage, FacePerspectiveRegistrationModel.FullImage);
        faces.ImportLampMask(maskSource);
        faces.AddButton("Start button", 10, 10, inputId: "start");
        faces.AddReelMount("Reel 1", 12, 20, 20, 40, reelIndex: 0);
        faces.AddLamp("Start lamp", 4, 5, 9, 9, lampIndex: 1);
        faces.BuildFace();

        var facePath = faces.FacePath!;
        faces.Save();
        var linked = FaceDocumentStorage.ToFile(faces.Document!) with
        { SourcePanel2DDocumentPath = panelRelative, SourceFaceShapeId = "front-shape" };
        File.WriteAllText(facePath, FaceDocumentStorage.Serialize(linked));
        faces.Close();
        faces.OpenFace(facePath);
        var before = faces.Document!;
        var oldOutputPath = Path.Combine(root, before.Artwork!.OutputAssetPath!);
        var oldPixels = ReadPixels(oldOutputPath);
        var oldElements = before.Elements.Select(element => (element.ObjectId, element.Name, element.GetType())).ToArray();
        var oldTrays = before.Trays.Select(tray => tray.ObjectId).ToArray();
        var oldEmitters = before.LampEmitters.Select(emitter => emitter.ObjectId).ToArray();
        var authoredMaskPath = Path.Combine(root, before.MaskLayer!.AuthoredAssetPath!.Replace('/', Path.DirectorySeparatorChar));
        var authoredMaskBytes = File.ReadAllBytes(authoredMaskPath);
        var overridePath = before.Artwork.Override!.AssetPath;
        var calibration = Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(before.Artwork.ProcessingPipeline.Operations));

        Assert.True(faces.UsePanelArtworkSource());
        var switched = faces.Document!;
        Assert.Equal(FaceArtworkSourceKind.Panel2DFaceSourceShape, switched.Artwork!.Source.Kind);
        Assert.Equal(panelRelative, switched.Artwork.Source.Panel2DDocumentPath);
        Assert.Equal("front-shape", switched.Artwork.Source.FaceSourceShapeId);
        Assert.Equal(calibration.Strength, Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(switched.Artwork.ProcessingPipeline.Operations)).Strength);
        Assert.Equal(overridePath, switched.Artwork.Override!.AssetPath);
        Assert.Equal(oldElements, switched.Elements.Select(element => (element.ObjectId, element.Name, element.GetType())).ToArray());
        Assert.Equal(oldTrays, switched.Trays.Select(tray => tray.ObjectId).ToArray());
        Assert.Equal(oldEmitters, switched.LampEmitters.Select(emitter => emitter.ObjectId).ToArray());
        Assert.Equal(authoredMaskBytes, File.ReadAllBytes(authoredMaskPath));

        faces.BuildFace();

        var built = faces.Document!;
        Assert.NotEqual(oldPixels, ReadPixels(Path.Combine(root, built.Artwork!.OutputAssetPath!)));
        Assert.Equal(FaceBuildStatus.Current, built.BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Current, built.BuildState.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.Current, built.BuildState.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Current, built.BuildState.Get(FaceGeneratedProduct.Trays).Status);
        Assert.True(File.Exists(Path.Combine(root, built.MaskLayer!.AssetPath!)));
        Assert.Equal(authoredMaskBytes, File.ReadAllBytes(authoredMaskPath));
        Assert.True(File.Exists(panelPath));
    }

    [Fact]
    public void UseDerivedLampMask_PreservesImmutableAuthoredImageWhileBuildingLinkedSourceMask()
    {
        var (project, _, panelPath, _, _) = CreateSourcePanel("Derived Mask");
        var faces = new FaceAuthoringSession(project);
        faces.CreateFromPanelFaceSourceShape("front-shape", "DerivedFace");
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var authoredSource = Path.Combine(_workspace, "authored-mask.png");
        WriteImage(authoredSource, 9, 5, (x, y) => new SKColor((byte)(x * 12), (byte)(y * 20), 140, 200));
        faces.ImportLampMask(authoredSource);
        var authoredPath = Path.Combine(root, faces.Document!.MaskLayer!.AuthoredAssetPath!.Replace('/', Path.DirectorySeparatorChar));
        var authoredBytes = File.ReadAllBytes(authoredPath);
        var generatedPath = faces.Document.MaskLayer.AssetPath;

        faces.UseDerivedLampMask();

        Assert.Equal(FaceLampMaskSourceKind.Derived, faces.Document!.MaskLayer!.SourceKind);
        Assert.Null(faces.Document.MaskLayer.AuthoredAssetPath);
        Assert.Equal(generatedPath, faces.Document.MaskLayer.AssetPath);
        Assert.Equal(authoredBytes, File.ReadAllBytes(authoredPath));
        Assert.Equal(FaceBuildStatus.Stale, faces.Document.BuildState.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Stale, faces.Document.BuildState.Get(FaceGeneratedProduct.Trays).Status);

        faces.BuildFace();

        var mask = faces.Document!.MaskLayer!;
        Assert.Equal(FaceLampMaskSourceKind.Derived, mask.SourceKind);
        Assert.Null(mask.AuthoredAssetPath);
        Assert.True(File.Exists(Path.Combine(root, mask.AssetPath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(authoredBytes, File.ReadAllBytes(authoredPath));
        Assert.True(File.Exists(panelPath));
    }

    [Fact]
    public void AuthoredEditsAfterBuildMarkOnlyAffectedDownstreamProductsStale()
    {
        var (_, faces, _, _, _) = CreateBuildableFace("Stale Products");
        faces.BuildFace(face => face);
        var current = faces.Document!.BuildState;
        Assert.All(Enum.GetValues<FaceGeneratedProduct>(), product => Assert.Equal(FaceBuildStatus.Current, current.Get(product).Status));

        var lamp = Assert.Single(faces.Document.Elements.OfType<FaceLampWindowElement>());
        faces.UpdateElement(lamp.ObjectId, x: lamp.X + 1);
        var afterLampEdit = faces.Document!.BuildState;
        Assert.Equal(FaceBuildStatus.Current, afterLampEdit.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Current, afterLampEdit.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.Stale, afterLampEdit.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Stale, afterLampEdit.Get(FaceGeneratedProduct.Trays).Status);
        Assert.Equal(FaceBuildStatus.Stale, afterLampEdit.Get(FaceGeneratedProduct.RuntimeAssets).Status);

        faces.BuildFace(face => face);
        faces.AddReelMount("Reel 1", 10, 15, 20, 35, reelIndex: 0);
        var afterMountEdit = faces.Document!.BuildState;
        Assert.Equal(FaceBuildStatus.Current, afterMountEdit.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Current, afterMountEdit.Get(FaceGeneratedProduct.Trays).Status);
        Assert.Equal(FaceBuildStatus.Stale, afterMountEdit.Get(FaceGeneratedProduct.RuntimeAssets).Status);

        faces.BuildFace(face => face);
        faces.SetArtworkCalibration(new ArtworkCalibrationOperationModel { Strength = 18, CorrectSpatialBrightness = false });
        var afterRecipeEdit = faces.Document!.BuildState;
        Assert.All(Enum.GetValues<FaceGeneratedProduct>(), product => Assert.Equal(FaceBuildStatus.Stale, afterRecipeEdit.Get(product).Status));
    }

    [Fact]
    public void CreateArtworkOverrideFromBaseRequiresCurrentBaseAndCopiesWithoutChangingSource()
    {
        var (_, faces, root, _, _) = CreateBuildableFace("Base Override");
        faces.SetArtworkCalibration(new ArtworkCalibrationOperationModel { Strength = 24, CorrectSpatialBrightness = false });
        Assert.Throws<InvalidOperationException>(() => faces.CreateArtworkOverrideFromBase());

        faces.BuildFace();
        var artworkBefore = faces.CurrentArtwork!;
        var sourceAssetPath = Path.Combine(root, artworkBefore.Source.AssetPath!);
        var sourceBytes = File.ReadAllBytes(sourceAssetPath);
        var basePath = Path.Combine(root, artworkBefore.BaseAssetPath!);
        var baseBytes = File.ReadAllBytes(basePath);
        var originalSourcePath = artworkBefore.Source.AssetPath;
        var pipeline = Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(artworkBefore.ProcessingPipeline.Operations));

        faces.CreateArtworkOverrideFromBase();

        var artworkAfter = faces.CurrentArtwork!;
        var overridePath = Path.Combine(root, artworkAfter.Override!.AssetPath!);
        Assert.True(artworkAfter.Override.Enabled);
        Assert.Equal(baseBytes, File.ReadAllBytes(overridePath));
        Assert.Equal(sourceBytes, File.ReadAllBytes(sourceAssetPath));
        Assert.Equal(originalSourcePath, artworkAfter.Source.AssetPath);
        Assert.Equal(pipeline.Strength, Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(artworkAfter.ProcessingPipeline.Operations)).Strength);
        Assert.Equal(FaceBuildStatus.Stale, faces.Document!.BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status);
    }

    [Fact]
    public void RebuildComponentsFromSource_ReplacesConvertedComponentsAndPreservesAuthoredFaceContent()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Component Rebuild");
        var panelPath = project.CreatePanel("Source");
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var sourceArtwork = Path.Combine(Path.GetDirectoryName(panelPath)!, "source.png");
        WriteImage(sourceArtwork, 100, 60, (_, _) => new SKColor(80, 90, 100));
        var relativeArtwork = Path.GetRelativePath(root, sourceArtwork).Replace('\\', '/');
        var sourceShapeId = "front-shape";
        File.WriteAllText(panelPath, Panel2DDocumentStorage.Serialize("Source", string.Empty,
        [new PanelElementFile { ObjectId = "background", Name = "Background", Kind = "background", X = 0, Y = 0, Width = 100, Height = 60, AssetPath = relativeArtwork },
            new PanelElementFile { ObjectId = "display", Name = "Old display", Kind = "sevenSegment", X = 5, Y = 5, Width = 20, Height = 8 }],
        [new PanelFaceSourceShapeFile { Id = sourceShapeId, Name = "Front", Type = "perspectiveRectangle", Points = [new(0, 0), new(100, 0), new(100, 60), new(0, 60)] }]));
        project.OpenProject(project.ProjectFilePath!);
        project.OpenPanel(panelPath);
        var faces = new FaceAuthoringSession(project);
        faces.CreateFromPanelFaceSourceShape(sourceShapeId, "ComponentFace");
        var sourceDisplay = Assert.Single(faces.Document!.Elements.OfType<FaceSevenSegmentDisplayElement>());
        var image = Path.Combine(_workspace, "mask.png");
        WriteImage(image, 8, 6, (_, _) => SKColors.White);
        faces.ImportLampMask(image);
        faces.AddLamp("Hand-authored lamp", 20, 15, 12, 12, lampIndex: 8);

        project.UpdateElement("display", name: "Updated display");
        project.Save();
        faces.RebuildComponentsFromSource();

        var rebuilt = faces.Document!;
        var display = Assert.Single(rebuilt.Elements.OfType<FaceSevenSegmentDisplayElement>());
        Assert.Equal(sourceDisplay.ObjectId, display.ObjectId);
        Assert.Equal("Updated display", display.Name);
        Assert.Contains(rebuilt.Elements.OfType<FaceLampWindowElement>(), lamp => lamp.Name == "Hand-authored lamp");
        Assert.NotNull(rebuilt.MaskLayer);
        Assert.NotNull(rebuilt.Artwork);
    }

    [Fact]
    public void ArtworkOverrideCanBeDisabledAndRemovedWithoutChangingAuthoredSource()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Override Controls");
        var faces = new FaceAuthoringSession(project);
        faces.CreateFace("OverrideFace");
        var source = Path.Combine(_workspace, "base.png");
        var overlay = Path.Combine(_workspace, "overlay.png");
        WriteImage(source, 24, 16, (_, _) => new SKColor(220, 30, 20, 255));
        WriteImage(overlay, 24, 16, (_, _) => new SKColor(20, 40, 230, 255));
        faces.ImportArtwork(source);
        faces.ImportArtworkOverride(overlay);
        faces.SetArtworkOverrideAlignment(0, 0, 1, 1, FaceArtworkOverrideAlphaSource.OverrideImage, FacePerspectiveRegistrationModel.FullImage);
        faces.RebuildArtwork();
        var artwork = faces.CurrentArtwork!;
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var outputPath = Path.Combine(root, artwork.OutputAssetPath!);
        var enabledPixels = ReadPixels(outputPath);
        var overridePath = artwork.Override!.AssetPath;
        Assert.True(faces.SetArtworkOverrideEnabled(false));
        faces.RebuildArtwork();
        var disabledPixels = ReadPixels(outputPath);
        Assert.NotEqual(enabledPixels, disabledPixels);
        Assert.False(faces.CurrentArtwork!.Override!.Enabled);
        Assert.True(File.Exists(Path.Combine(root, overridePath!)));

        Assert.True(faces.RemoveArtworkOverride());
        faces.RebuildArtwork();
        Assert.Null(faces.CurrentArtwork!.Override);
        Assert.Equal(disabledPixels, ReadPixels(outputPath));
    }

    private (AuthoringSession Project, string Root, string PanelPath, string PanelRelativePath, string SourceArtworkPath) CreateSourcePanel(string projectName)
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject(projectName);
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var panelPath = project.CreatePanel("Source");
        var sourceArtwork = Path.Combine(Path.GetDirectoryName(panelPath)!, "source.png");
        WriteImage(sourceArtwork, 48, 32, (x, y) => new SKColor((byte)(20 + x), (byte)(40 + y), 170));
        var panelRelative = Path.GetRelativePath(root, sourceArtwork).Replace('\\', '/');
        File.WriteAllText(panelPath, Panel2DDocumentStorage.Serialize("Source", string.Empty,
        [
            new PanelElementFile { ObjectId = "background", Name = "Background", Kind = "background", X = 0, Y = 0, Width = 48, Height = 32, AssetPath = panelRelative },
            new PanelElementFile { ObjectId = "source-lamp", Name = "Source lamp", Kind = "lamp", X = 8, Y = 8, Width = 10, Height = 10, DisplayNumber = 5, AssetPath = panelRelative }
        ],
        [new PanelFaceSourceShapeFile { Id = "front-shape", Name = "Front", Type = "perspectiveRectangle", Points = [new(0, 0), new(48, 0), new(48, 32), new(0, 32)] }]));
        project.OpenProject(project.ProjectFilePath!);
        project.OpenPanel(panelPath);
        project.Save();
        return (project, root, panelPath, Path.GetRelativePath(root, panelPath).Replace('\\', '/'), sourceArtwork);
    }

    private (AuthoringSession Project, FaceAuthoringSession Faces, string Root, string SourceImage, string AuthoredMask) CreateBuildableFace(string name)
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject(name);
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var faces = new FaceAuthoringSession(project);
        faces.CreateFace(name);
        var artwork = Path.Combine(_workspace, name + "-artwork.png");
        var mask = Path.Combine(_workspace, name + "-mask.png");
        WriteImage(artwork, 32, 18, (x, y) => new SKColor((byte)(x * 5), (byte)(y * 8), 140));
        WriteImage(mask, 9, 7, (x, y) => new SKColor(80, (byte)(x * 10), 180, (byte)(100 + x + y)));
        faces.ImportArtwork(artwork);
        faces.ImportLampMask(mask);
        faces.AddLamp("Test lamp", 5, 5, 12, 10, lampIndex: 1);
        var authoredMask = Path.Combine(root, faces.Document!.MaskLayer!.AuthoredAssetPath!.Replace('/', Path.DirectorySeparatorChar));
        return (project, faces, root, artwork, authoredMask);
    }

    private static string GeneratedFingerprint(string root, string faceName)
    {
        var generatedRoot = Path.Combine(root, "Generated", "Faces", faceName);
        if (!Directory.Exists(generatedRoot)) return string.Empty;
        return string.Join(Environment.NewLine, Directory.EnumerateFiles(generatedRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(generatedRoot, path).Replace('\\', '/') + ":" +
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))));
    }

    private static void WriteImage(string path, int width, int height, Func<int, int, SKColor> color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) bitmap.SetPixel(x, y, color(x, y));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path); encoded.SaveTo(stream);
    }

    private static string ReadPixels(string path)
    {
        using var bitmap = SKBitmap.Decode(path)!;
        var result = new System.Text.StringBuilder();
        for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            result.Append(pixel.Red).Append(',').Append(pixel.Green).Append(',').Append(pixel.Blue).Append(',').Append(pixel.Alpha).Append(';');
        }
        return result.ToString();
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);
}
