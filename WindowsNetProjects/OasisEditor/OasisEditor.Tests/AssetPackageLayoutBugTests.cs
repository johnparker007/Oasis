using OasisEditor;
using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AssetPackageLayoutBugTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"OasisAssetPackageBugTests-{Guid.NewGuid():N}");

    [Fact]
    public void CreateProject_DoesNotCreateLegacyAssetPlaceholderFolders()
    {
        var projectDirectory = new ProjectScaffolder().CreateProject("PackageProject", _root);

        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Audio")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Fonts")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Images")));
        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Panel2D")));
        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Faces")));
        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Cabinet3D")));
    }

    [Theory]
    [InlineData("Assets/Panel2D/Main Panel/asset.panel2d", "Main Panel")]
    [InlineData("Assets/Faces/Top Glass/asset.face", "Top Glass")]
    [InlineData("Assets/Cabinet3D/Vogue/asset.cabinet3d", "Vogue")]
    public void CreateFromFile_ForPackageManifest_UsesEnclosingFolderAsTitle(string relativePath, string expectedTitle)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        var document = EditorDocument.CreateFromFile(path, "Opened.");

        Assert.Equal(expectedTitle, document.Title);
    }

    [Fact]
    public void BuildOpenDocumentData_ForPackageManifests_UsesEnclosingFolderAsTitle()
    {
        var panelJson = Panel2DDocumentStorage.Serialize("Internal Title", "Panel summary", [], []);
        var faceJson = FaceDocumentStorage.Serialize(FaceDocumentStorage.CreateEmpty("Internal Face"));
        var cabinetJson = CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("model.glb"));

        Assert.Equal("Main Panel", DocumentWorkspaceViewModel.BuildOpenDocumentData(Path.Combine(_root, "Assets", "Panel2D", "Main Panel", "asset.panel2d"), panelJson).PanelTitle);
        Assert.Equal("Top Glass", DocumentWorkspaceViewModel.BuildOpenDocumentData(Path.Combine(_root, "Assets", "Faces", "Top Glass", "asset.face"), faceJson).PanelTitle);
        Assert.Equal("Vogue", DocumentWorkspaceViewModel.BuildOpenDocumentData(Path.Combine(_root, "Assets", "Cabinet3D", "Vogue", "asset.cabinet3d"), cabinetJson).PanelTitle);
    }

    [Fact]
    public void SaveDocument_ForUnsavedGeneratedFace_CreatesFacePackageFilesAndUsesPackageTitle()
    {
        var project = CreateProject();
        var faceDocument = new FaceDocumentModel
        {
            Title = "Temporary Face",
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 2, Height = 2 },
            Elements =
            [
                new FaceArtworkElement { ObjectId = "art", Name = "Artwork", Width = 2, Height = 2 }
            ]
        };
        var current = new DocumentTabViewModel(
            EditorDocument.CreateFaceStub("Temporary Face").MarkDirty(),
            faceDocumentJson: FaceDocumentStorage.Serialize(faceDocument));
        var savePath = Path.Combine(project.AssetsDirectory, "Faces", "Saved Face", "asset.face");

        var saved = new DocumentSaveService().SaveDocument(current, savePath, project);

        Assert.Equal("Saved Face", saved.Title);
        Assert.False(saved.Document.IsUntitled);
        Assert.True(File.Exists(Path.Combine(project.AssetsDirectory, "Faces", "Saved Face", "asset.face")));
        Assert.True(File.Exists(Path.Combine(project.AssetsDirectory, "Faces", "Saved Face", "artwork.png")));
        Assert.True(File.Exists(Path.Combine(project.AssetsDirectory, "Faces", "Saved Face", "mask.png")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private EditorProject CreateProject()
    {
        Directory.CreateDirectory(_root);
        var assets = Path.Combine(_root, "Assets");
        var generated = Path.Combine(_root, "Generated");
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(generated);
        return new EditorProject
        {
            Name = "Test",
            ProjectFilePath = Path.Combine(_root, "Test.oasisproj"),
            ProjectDirectory = _root,
            AssetsDirectory = assets,
            MachinesDirectory = Path.Combine(_root, "Machines"),
            GeneratedDirectory = generated
        };
    }
}
