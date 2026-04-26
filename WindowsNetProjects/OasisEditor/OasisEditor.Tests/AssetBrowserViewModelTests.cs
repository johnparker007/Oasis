using System.IO;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AssetBrowserViewModelTests
{
    [Fact]
    public void RefreshAssetBrowser_BuildsDirectoryTreeAndSelectedDirectoryContents()
    {
        using var temp = new TempProjectDirectory();
        Directory.CreateDirectory(Path.Combine(temp.AssetsDirectory, "Art"));
        Directory.CreateDirectory(Path.Combine(temp.AssetsDirectory, "Art", "Sub"));
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "readme.txt"), "root");
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "Art", "panel.panel2d"), "{}");

        var viewModel = CreateViewModel(temp.Project, _ => { });

        viewModel.RefreshAssetBrowser();

        var root = Assert.Single(viewModel.AssetDirectoryTree);
        Assert.Equal("Assets", root.DisplayPath);
        Assert.Equal(temp.AssetsDirectory, root.FullPath);
        Assert.Equal(root, viewModel.SelectedDirectory);

        Assert.Contains(viewModel.AssetBrowserItems, item => item.IsDirectory && item.DisplayPath == "Art");
        Assert.Contains(viewModel.AssetBrowserItems, item => !item.IsDirectory && item.DisplayPath == "readme.txt");
        Assert.DoesNotContain(viewModel.AssetBrowserItems, item => item.DisplayPath == "panel.panel2d");
    }

    [Fact]
    public void OpenAssetCommand_WhenDirectorySelected_NavigatesIntoDirectoryWithoutOpeningDocument()
    {
        using var temp = new TempProjectDirectory();
        Directory.CreateDirectory(Path.Combine(temp.AssetsDirectory, "Art"));
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "Art", "panel.panel2d"), "{}");

        AssetBrowserItemViewModel? openedAsset = null;
        var viewModel = CreateViewModel(temp.Project, asset => openedAsset = asset);

        viewModel.RefreshAssetBrowser();
        var artDirectory = Assert.Single(viewModel.AssetBrowserItems, item => item.IsDirectory);

        Assert.True(viewModel.OpenAssetCommand.CanExecute(artDirectory));
        viewModel.OpenAssetCommand.Execute(artDirectory);

        Assert.Equal(Path.Combine(temp.AssetsDirectory, "Art"), viewModel.SelectedDirectory?.FullPath);
        Assert.Null(openedAsset);
        Assert.Contains(viewModel.AssetBrowserItems, item => !item.IsDirectory && item.DisplayPath == "panel.panel2d");
    }

    private static AssetBrowserViewModel CreateViewModel(EditorProject project, Action<AssetBrowserItemViewModel?> openAsset)
    {
        return new AssetBrowserViewModel(
            loadedProjectAccessor: () => project,
            selectionChanged: () => { },
            notifyInspectorChanged: () => { },
            addOutputEntry: (_, _) => { },
            openAsset: openAsset);
    }

    private sealed class TempProjectDirectory : IDisposable
    {
        public TempProjectDirectory()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), $"oasis-tests-{Guid.NewGuid():N}");
            AssetsDirectory = Path.Combine(RootDirectory, "Assets");
            Directory.CreateDirectory(AssetsDirectory);

            Project = new EditorProject
            {
                Name = "TestProject",
                ProjectFilePath = Path.Combine(RootDirectory, "TestProject.oasisproj"),
                ProjectDirectory = RootDirectory,
                AssetsDirectory = AssetsDirectory,
                MachinesDirectory = Path.Combine(RootDirectory, "Machines"),
                GeneratedDirectory = Path.Combine(RootDirectory, "Generated")
            };
        }

        public string RootDirectory { get; }
        public string AssetsDirectory { get; }
        public EditorProject Project { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
    }
}
