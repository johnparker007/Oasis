using System.Collections.ObjectModel;
using System.Windows.Input;
using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ReelCreationWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"OasisReelCreation-{Guid.NewGuid():N}");

    [Fact]
    public void ReelCreationCommandExistsAndWorkspaceUsesNormalNewAssetAvailability()
    {
        var commandProperty = typeof(MainWindowViewModel).GetProperty(nameof(MainWindowViewModel.OpenReelStubCommand));
        Assert.NotNull(commandProperty);
        Assert.Equal(typeof(ICommand), commandProperty!.PropertyType);

        EditorProject? project = null;
        var openDocuments = new ObservableCollection<DocumentTabViewModel>();
        DocumentTabViewModel? selected = null;
        var workspace = CreateWorkspace(() => project, openDocuments, document => selected = document, () => selected);
        Assert.False(workspace.CanOpenUntitledDocument());
        workspace.OpenReelStubDocument();
        Assert.Empty(openDocuments);

        project = CreateProject();
        Assert.True(workspace.CanOpenUntitledDocument());
        workspace.OpenReelStubDocument();
        var reel = Assert.Single(openDocuments);
        Assert.Same(reel, selected);
        Assert.Equal(EditorDocumentType.Reel, reel.Document.DocumentType);
    }

    [Fact]
    public void ReelStubSavesAndReopensAtCanonicalPackagePathAndEntersMachineCatalog()
    {
        var project = CreateProject();
        var openDocuments = new ObservableCollection<DocumentTabViewModel>();
        DocumentTabViewModel? selected = null;
        var workspace = CreateWorkspace(() => project, openDocuments, document => selected = document, () => selected);
        workspace.OpenReelStubDocument();
        var reel = Assert.Single(openDocuments);
        reel.ReelDisplayName = "JPM Standard Reel";
        reel.ReelDiameterMm = 290;
        reel.ReelWidthMm = 70;

        var paths = new ProjectAssetPathService();
        var savePath = paths.GetReelManifestPath(project, "JPM Standard Reel");
        paths.CreateAssetPackageDirectory(project, EditorAssetType.Reel, "JPM Standard Reel");
        new DocumentSaveService().SaveDocument(reel, savePath, project).ApplyTo(reel);

        Assert.Equal(Path.Combine(project.AssetsDirectory, "Reels", "JPM Standard Reel", "asset.reel"), savePath);
        Assert.True(File.Exists(savePath));
        var opened = DocumentWorkspaceViewModel.BuildOpenDocumentData(savePath, File.ReadAllText(savePath));
        Assert.Equal("JPM Standard Reel", opened.PanelTitle);
        Assert.NotNull(opened.ReelDocumentJson);
        var reopened = new DocumentTabViewModel(EditorDocument.CreateFromFile(savePath, opened.Summary, opened.PanelTitle), reelDocumentJson: opened.ReelDocumentJson);
        Assert.Equal(EditorDocumentType.Reel, reopened.Document.DocumentType);
        Assert.Equal(290, reopened.GetReelDocument().DiameterMm);
        Assert.Equal(70, reopened.GetReelDocument().WidthMm);

        var choice = Assert.Single(DocumentTabViewModel.DiscoverProjectAssetChoices(project, EditorAssetType.Reel));
        Assert.Equal("JPM Standard Reel", choice.DisplayName);
        Assert.Equal(AssetReference.Project("Assets/Reels/JPM Standard Reel/asset.reel"), choice.AssetPath);
    }

    private EditorProject CreateProject()
    {
        var directory = new ProjectScaffolder().CreateProject("Reel Project", _root);
        return new EditorProject
        {
            Name = "Reel Project",
            ProjectFilePath = Path.Combine(directory, "Reel Project.oasisproj"),
            ProjectDirectory = directory,
            AssetsDirectory = Path.Combine(directory, "Assets"),
            GeneratedDirectory = Path.Combine(directory, "Generated")
        };
    }

    private static DocumentWorkspaceViewModel CreateWorkspace(
        Func<EditorProject?> project,
        ObservableCollection<DocumentTabViewModel> documents,
        Action<DocumentTabViewModel?> setSelected,
        Func<DocumentTabViewModel?> getSelected)
        => new(project, _ => { }, documents, getSelected, setSelected, () => { }, _ => { }, (_, _) => { });

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
