using OasisEditor;
using OasisEditor.FacePipeline;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class FaceWorkflowTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "OasisFaceWorkflowTests", Guid.NewGuid().ToString("N"));

    public FaceWorkflowTests() => Directory.CreateDirectory(_workspace);

    [Fact]
    public void CreateFace_AddLampEditUndoRedoAndSave_PreservesNativeManifest()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Face Project");
        var session = new FaceAuthoringSession(project);

        var path = session.CreateFace("Window");
        Assert.True(File.Exists(path));
        Assert.Equal("Window", session.FaceTitle);
        Assert.Equal(FaceDocumentStorage.DefaultNativeLogicalWidth, session.LogicalWidth);
        Assert.Equal(FaceDocumentStorage.DefaultNativeLogicalHeight, session.LogicalHeight);
        Assert.False(session.IsDirty);
        Assert.True(session.IsProjectCurrent);

        var lampId = session.AddLamp("Start lamp", 20, 30, 24, 24, lampIndex: 12);
        Assert.True(session.IsDirty);
        Assert.True(session.CanUndo);
        var lamp = Assert.Single(session.Elements);
        Assert.Equal(lampId, lamp.Id);
        Assert.Equal("lampWindow", lamp.Kind);
        Assert.Equal(12, lamp.LampId);
        Assert.Equal("Start lamp", lamp.Name);

        Assert.True(session.UpdateElement(lampId, name: "Start button lamp", x: 40, visible: false, lampIndex: 14));
        Assert.Equal(14, Assert.Single(session.Elements).LampId);
        session.Undo();
        Assert.Equal("Start lamp", Assert.Single(session.Elements).Name);
        Assert.Equal(12, Assert.Single(session.Elements).LampId);
        session.Redo();
        Assert.Equal("Start button lamp", Assert.Single(session.Elements).Name);
        Assert.True(session.Save());
        Assert.False(session.IsDirty);

        Assert.True(FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var saved, out var error), error);
        var element = Assert.Single(saved.Elements!);
        Assert.Equal(lampId, element.ObjectId);
        Assert.Equal("lampWindow", element.Kind);
        Assert.Equal("lamp:14", element.LinkedMachineObjectReference);
        Assert.Equal(40, element.X);
        Assert.False(element.IsVisible);

        session.Close();
        Assert.Null(session.FacePath);
    }

    [Fact]
    public void OpenFace_PreservesExistingArtworkAndFaceMetadataAcrossEdit()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Existing Face Project");
        var projectRoot = Path.GetDirectoryName(project.ProjectFilePath)!;
        var path = Path.Combine(projectRoot, "Assets", "Faces", "Existing", ProjectAssetPathService.FaceManifestFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var model = new FaceDocumentModel
        {
            Id = "existing-face-id",
            Title = "Existing",
            SourceRegion = FaceSourceRegionModel.FromBounds(0, 0, 800, 600),
            Artwork = new FaceArtworkModel
            {
                Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.Image, AssetPath = "Assets/Faces/Existing/ArtworkSource/front.png", PixelWidth = 800, PixelHeight = 600 },
                OutputAssetPath = "Generated/Faces/Existing/Artwork/artwork.png",
                OutputWidth = 800,
                OutputHeight = 600
            },
            MaskLayer = new FaceMaskLayerModel { Id = "authored-mask", Name = "Lamp mask", AssetPath = "Assets/Faces/Existing/mask.png", Width = 800, Height = 600, SourceKind = FaceLampMaskSourceKind.AuthoredImage },
            Elements = [new FaceButtonElement { ObjectId = "button-id", Name = "Play", X = 20, Y = 30, Width = 40, Height = 22, LinkedInputReference = MachineInputReference.FromInputId("start") }]
        };
        File.WriteAllText(path, FaceDocumentStorage.Serialize(model));

        var session = new FaceAuthoringSession(project);
        session.OpenFace(path);
        Assert.Equal("Generated/Faces/Existing/Artwork/artwork.png", session.ArtworkPath);
        Assert.Equal(800, session.ArtworkWidth);
        Assert.Equal(600, session.ArtworkHeight);
        Assert.Equal(800, session.LogicalWidth);
        Assert.Equal(600, session.LogicalHeight);
        Assert.Equal("button-id", Assert.Single(session.Elements).Id);
        session.UpdateElement("button-id", x: 28);
        Assert.True(session.Save());

        Assert.True(FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var saved, out var error), error);
        Assert.Equal("Generated/Faces/Existing/Artwork/artwork.png", saved.Artwork?.OutputAssetPath);
        Assert.Equal("Assets/Faces/Existing/mask.png", saved.MaskLayer?.AssetPath);
        Assert.Equal("input:start", Assert.Single(saved.Elements!).LinkedInputReference);
        Assert.Equal(28, Assert.Single(saved.Elements!).X);
    }

    [Fact]
    public void AddLamp_EnforcesRuntimeLogicalIndexRangeAndCloseProtectsDirtyWork()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Validation Project");
        var session = new FaceAuthoringSession(project);
        session.CreateFace("Valid");
        Assert.Throws<ArgumentOutOfRangeException>(() => session.AddLamp("Invalid", 0, 0, 10, 10, lampIndex: 255));
        var id = session.AddLamp("Valid", 0, 0, 10, 10, lampIndex: 254);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateElement(id, lampIndex: -1));
        Assert.True(session.IsDirty);
        Assert.Throws<InvalidOperationException>(() => session.Close());
        Assert.True(session.DeleteSelected());
    }

    [Fact]
    public void Save_RejectsExternalAuthoredPackageChangesAndOpenRejectsUnknownData()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Conflict Project");
        var session = new FaceAuthoringSession(project);
        var path = session.CreateFace("Conflict");
        session.AddLamp("Lamp", 0, 0, 10, 10);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "authored-note.txt"), "external");
        Assert.Throws<IOException>(() => session.Save());

        session.Undo();
        session.Undo();
        session.Close();
        File.WriteAllText(path, "{\"SchemaVersion\":24,\"Title\":\"Unknown\",\"UnrecognizedFaceField\":true}");
        Assert.Throws<InvalidDataException>(() => session.OpenFace(path));
    }

    [Fact]
    public void InvalidGeometry_IsRejectedWithoutChangingDocumentOrHistory()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Geometry Project");
        var session = new FaceAuthoringSession(project);
        session.CreateFace("Geometry");
        var id = session.AddLamp("Lamp", 12, 18, 20, 24, lampIndex: 4);
        var before = Assert.Single(session.Elements);
        var undoAvailableBefore = session.CanUndo;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateElement(id, x: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateElement(id, width: 0));

        Assert.Equal(before, Assert.Single(session.Elements));
        Assert.Equal(undoAvailableBefore, session.CanUndo);
    }

    [Fact]
    public void GenerateFaceFromSourceShape_CreatesNativeDocumentArtworkMaskAndSemanticLamp()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Generated Face Project");
        var panelPath = project.CreatePanel("Source Panel");
        var panelDirectory = Path.GetDirectoryName(panelPath)!;
        var imagePath = Path.Combine(panelDirectory, "source.png");
        WriteImage(imagePath, 160, 120, (x, y) => x > 25 && x < 135 && y > 15 && y < 105 ? SKColors.OrangeRed : SKColors.Transparent);
        var relativeImage = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(Path.GetDirectoryName(project.ProjectFilePath!)!, imagePath));
        File.WriteAllText(panelPath, Panel2DDocumentStorage.Serialize("Source Panel", string.Empty,
        [
            new PanelElementFile { ObjectId = "background", Name = "Background", Kind = "background", X = 0, Y = 0,
                Width = 160, Height = 120, AssetPath = relativeImage },
            new PanelElementFile { ObjectId = "lamp-source", Name = "Start", Kind = "lamp", X = 55, Y = 35,
                Width = 40, Height = 35, DisplayNumber = 4, AssetPath = relativeImage }
        ],
        [new PanelFaceSourceShapeFile { Id = "shape-front", Name = "Front", Points =
        [new(0, 0), new(160, 0), new(160, 120), new(0, 120)] }]));
        project.OpenProject(project.ProjectFilePath!);
        project.OpenPanel(panelPath);
        var faces = new FaceAuthoringSession(project);

        var facePath = faces.CreateFromPanelFaceSourceShape("shape-front", "Generated Front");

        Assert.True(File.Exists(facePath));
        Assert.Equal("Generated Front", faces.FaceTitle);
        Assert.Equal("shape-front", faces.CurrentDocument!.SourceFaceShapeId);
        Assert.NotNull(faces.CurrentDocument.Artwork?.CorrectionInputAssetPath);
        Assert.NotNull(faces.CurrentDocument.Artwork?.BaseAssetPath);
        Assert.NotNull(faces.CurrentDocument.Artwork?.OutputAssetPath);
        Assert.Contains(faces.CurrentDocument.Elements, element => element is FaceLampWindowElement lamp && lamp.LinkedPanel2DElementId == "lamp-source");
        Assert.NotNull(faces.CurrentDocument.MaskLayer?.AssetPath);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(project.ProjectFilePath!)!, faces.CurrentDocument.MaskLayer!.AssetPath!)));
        Assert.True(FaceDocumentStorage.TryReadValidated(File.ReadAllText(facePath), out var saved, out var error), error);
        Assert.Equal("Generated Front", saved.Title);
    }

    [Fact]
    public void GenerateFaceFromSourceShape_RequiresUnchangedSavedSourcePanel()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Source Panel Conflict Project");
        var panelPath = project.CreatePanel("Source");
        File.WriteAllText(panelPath, Panel2DDocumentStorage.Serialize("Source", string.Empty,
        [new PanelElementFile { ObjectId = "box", Name = "Box", Kind = "rectangle", X = 0, Y = 0, Width = 5, Height = 5 }],
        [new PanelFaceSourceShapeFile { Id = "front", Name = "Front", Points = [new(0, 0), new(40, 0), new(40, 30), new(0, 30)] }]));
        project.OpenProject(project.ProjectFilePath!);
        project.OpenPanel(panelPath);
        var faces = new FaceAuthoringSession(project);

        project.UpdateElement("box", x: 2);
        Assert.Throws<InvalidOperationException>(() => faces.CreateFromPanelFaceSourceShape("front", "Unsaved"));
        project.Undo();
        File.AppendAllText(panelPath, " ");
        Assert.Throws<IOException>(() => faces.CreateFromPanelFaceSourceShape("front", "Conflict"));
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(project.ProjectFilePath!)!, "Assets", "Faces", "Conflict")));
    }

    [Fact]
    public void ImageImportRegistrationCalibrationAndOverride_BuildsStagesAndRoundTripsRecipe()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Artwork Project");
        var faces = new FaceAuthoringSession(project);
        faces.CreateFace("Front");
        var source = Path.Combine(_workspace, "external-source.png");
        WriteImage(source, 80, 60, (x, y) => x < 40 ? new SKColor(40, 40, 40) : new SKColor(220, 220, 220));

        faces.ImportArtwork(source);
        var artworkElement = Assert.IsType<FaceArtworkElement>(Assert.Single(faces.CurrentDocument!.Elements));
        Assert.Equal(faces.CurrentArtwork!.OutputAssetPath, artworkElement.AssetPath);
        Assert.Equal(faces.CurrentArtwork.OutputWidth, artworkElement.Width);
        Assert.Equal(faces.CurrentArtwork.OutputHeight, artworkElement.Height);
        var registration = new FacePerspectiveRegistrationModel
        {
            TopLeft = new() { X = .05, Y = .08 }, TopRight = new() { X = .95, Y = .02 },
            BottomRight = new() { X = .9, Y = .92 }, BottomLeft = new() { X = .1, Y = .96 }
        };
        faces.SetArtworkRegistration(registration);
        faces.SetArtworkCalibration(new ArtworkCalibrationOperationModel
        {
            Strength = 72,
            BlackReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF282828" },
            WhiteReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FFDCDCDC" },
            CorrectSpatialBrightness = false
        });
        var overrideSource = Path.Combine(_workspace, "override.png");
        WriteImage(overrideSource, 12, 8, (_, _) => new SKColor(40, 150, 240, 180));
        faces.ImportArtworkOverride(overrideSource);
        faces.SetArtworkOverrideAlignment(.2, .3, .6, .4, FaceArtworkOverrideAlphaSource.OverrideImage, FacePerspectiveRegistrationModel.FullImage);
        faces.RebuildArtwork();

        var artwork = faces.CurrentArtwork!;
        Assert.Equal(72, Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(artwork.ProcessingPipeline.Operations)).Strength);
        Assert.Equal(.2, artwork.Override!.X);
        Assert.Equal(FaceArtworkOverrideAlphaSource.OverrideImage, artwork.Override.AlphaSource);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(project.ProjectFilePath!)!, artwork.Source.AssetPath!)));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(project.ProjectFilePath!)!, artwork.OutputAssetPath!)));
        faces.Save();
        var savedPath = faces.FacePath!;
        faces.Close(); faces.OpenFace(savedPath);
        Assert.Equal(registration.TopLeft.X, faces.CurrentArtwork!.Geometry.PerspectiveRegistration.TopLeft.X, 6);
        Assert.Equal("#FF282828", Assert.IsType<ArtworkCalibrationOperationModel>(faces.CurrentArtwork.ProcessingPipeline.Operations[0]).BlackReference.ManualColor);
        Assert.Equal(.6, faces.CurrentArtwork.Override!.Width, 6);
    }

    [Fact]
    public void ImportArtwork_RejectsExternalPackageChangeAndInvalidRecipeWritesNothing()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Artwork Conflict Project");
        var faces = new FaceAuthoringSession(project);
        var manifest = faces.CreateFace("Protected");
        var source = Path.Combine(_workspace, "source.png");
        WriteImage(source, 24, 16, (x, _) => x < 12 ? SKColors.Black : SKColors.White);
        File.AppendAllText(manifest, " ");

        Assert.Throws<IOException>(() => faces.ImportArtwork(source));
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(manifest)!, "ArtworkSource")));

        faces.Close();
        var reopened = new FaceAuthoringSession(project);
        reopened.OpenFace(manifest);
        reopened.ImportArtwork(source);
        var artwork = reopened.CurrentArtwork!;
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var before = Fingerprint(root, artwork);
        var invalid = new ArtworkCalibrationOperationModel { Strength = 101 };
        Assert.Throws<ArgumentOutOfRangeException>(() => reopened.UpdateArtworkRecipe(new FaceArtworkModel
        {
            Source = artwork.Source, Geometry = artwork.Geometry, ProcessingPipeline = new ImageProcessingPipelineModel { Operations = [invalid] },
            CorrectionInputAssetPath = artwork.CorrectionInputAssetPath, BaseAssetPath = artwork.BaseAssetPath,
            OutputAssetPath = artwork.OutputAssetPath, OutputWidth = artwork.OutputWidth, OutputHeight = artwork.OutputHeight
        }));
        Assert.Equal(before, Fingerprint(root, artwork));
    }

    [Fact]
    public void ArtworkRecipeUndo_RebuildsPreviousPixels()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Artwork History Project");
        var faces = new FaceAuthoringSession(project);
        faces.CreateFace("History");
        var source = Path.Combine(_workspace, "history.png");
        WriteImage(source, 32, 16, (x, _) => x < 16 ? new SKColor(30, 30, 30) : new SKColor(220, 220, 220));
        faces.ImportArtwork(source);
        var artwork = faces.CurrentArtwork!;
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var before = ReadPixels(Path.Combine(root, artwork.OutputAssetPath!));

        faces.SetArtworkCalibration(new ArtworkCalibrationOperationModel
        {
            Strength = 100, CorrectSpatialBrightness = false, CorrectSpatialColor = false,
            BlackReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF646464" },
            WhiteReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF969696" }
        });
        faces.RebuildArtwork();
        Assert.NotEqual(before, ReadPixels(Path.Combine(root, artwork.OutputAssetPath!)));

        faces.Undo();
        Assert.Equal(before, ReadPixels(Path.Combine(root, artwork.OutputAssetPath!)));
    }

    private static void WriteImage(string path, int width, int height, Func<int, int, SKColor> color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) bitmap.SetPixel(x, y, color(x, y));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path); encoded.SaveTo(stream);
    }

    private static string Fingerprint(string root, FaceArtworkModel artwork)
        => string.Join("|", new[] { artwork.CorrectionInputAssetPath, artwork.BaseAssetPath, artwork.OutputAssetPath }
            .Select(path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path!))))));

    private static string ReadPixels(string path)
    {
        using var bitmap = SKBitmap.Decode(path)!;
        var pixels = new System.Text.StringBuilder();
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                pixels.Append(pixel.Red).Append(',').Append(pixel.Green).Append(',').Append(pixel.Blue).Append(',').Append(pixel.Alpha).Append(';');
            }
        return pixels.ToString();
    }

    [Fact]
    public void OpenFace_RejectsOutsidePackageAndSymbolicLinkManifest()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Path Project");
        var projectRoot = Path.GetDirectoryName(project.ProjectFilePath)!;
        var outsideDirectory = Path.Combine(Path.GetTempPath(), "outside-face-" + Guid.NewGuid().ToString("N"));
        var outsideManifest = Path.Combine(outsideDirectory, ProjectAssetPathService.FaceManifestFileName);
        Directory.CreateDirectory(outsideDirectory);
        File.WriteAllText(outsideManifest, FaceDocumentStorage.Serialize(FaceDocumentStorage.CreateEmpty("Outside")));
        var session = new FaceAuthoringSession(project);
        try
        {
            Assert.Throws<InvalidDataException>(() => session.OpenFace(outsideManifest));

            var linkPath = Path.Combine(projectRoot, "Assets", "Faces", "Linked", ProjectAssetPathService.FaceManifestFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
            try
            {
                File.CreateSymbolicLink(linkPath, outsideManifest);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                return;
            }

            Assert.Throws<InvalidDataException>(() => session.OpenFace(linkPath));
        }
        finally
        {
            Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);
}
