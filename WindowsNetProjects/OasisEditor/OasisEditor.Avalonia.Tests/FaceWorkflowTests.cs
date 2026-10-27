using OasisEditor;
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
