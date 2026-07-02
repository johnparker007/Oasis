using System.Collections.Generic;
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

    [Fact]
    public void RefreshAssetBrowser_RestoresSelectedDirectoryAndAssetWhenStillPresent()
    {
        using var temp = new TempProjectDirectory();
        Directory.CreateDirectory(Path.Combine(temp.AssetsDirectory, "Art"));
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "Art", "panel.panel2d"), "{}");

        var viewModel = CreateViewModel(temp.Project, _ => { });
        viewModel.RefreshAssetBrowser();

        var artDirectory = Assert.Single(viewModel.AssetBrowserItems, item => item.IsDirectory && item.DisplayPath == "Art");
        viewModel.OpenAssetCommand.Execute(artDirectory);

        var panelFile = Assert.Single(viewModel.AssetBrowserItems, item => !item.IsDirectory && item.DisplayPath == "panel.panel2d");
        viewModel.SelectedAsset = panelFile;

        viewModel.RefreshAssetBrowser();

        Assert.Equal(Path.Combine(temp.AssetsDirectory, "Art"), viewModel.SelectedDirectory?.FullPath);
        Assert.Equal("panel.panel2d", viewModel.SelectedAsset?.DisplayPath);
    }

    [Fact]
    public void RefreshAssetBrowser_WithEmptyAssetsDirectory_ShowsRootAndNoItems()
    {
        using var temp = new TempProjectDirectory();
        var viewModel = CreateViewModel(temp.Project, _ => { });

        viewModel.RefreshAssetBrowser();

        var root = Assert.Single(viewModel.AssetDirectoryTree);
        Assert.Equal("Assets", root.DisplayPath);
        Assert.Equal(temp.AssetsDirectory, root.FullPath);
        Assert.Empty(viewModel.AssetBrowserItems);
    }

    [Fact]
    public void DeleteAssetCommand_WhenFileSelected_DeletesFileAndRefreshes()
    {
        using var temp = new TempProjectDirectory();
        var filePath = Path.Combine(temp.AssetsDirectory, "delete-me.txt");
        File.WriteAllText(filePath, "test");

        var viewModel = CreateViewModel(temp.Project, _ => { });
        viewModel.RefreshAssetBrowser();
        var fileItem = Assert.Single(viewModel.AssetBrowserItems, item => !item.IsDirectory && item.DisplayPath == "delete-me.txt");

        Assert.True(viewModel.DeleteAssetCommand.CanExecute(fileItem));
        viewModel.DeleteAssetCommand.Execute(fileItem);

        Assert.False(File.Exists(filePath));
        Assert.DoesNotContain(viewModel.AssetBrowserItems, item => item.DisplayPath == "delete-me.txt");
    }

    [Fact]
    public void DeleteAssetCommand_WhenFolderNotEmpty_DeletesFolderAndContents()
    {
        using var temp = new TempProjectDirectory();
        var folderPath = Path.Combine(temp.AssetsDirectory, "Art");
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, "panel.panel2d"), "{}");

        var viewModel = CreateViewModel(temp.Project, _ => { });
        viewModel.RefreshAssetBrowser();
        var folderItem = Assert.Single(viewModel.AssetBrowserItems, item => item.IsDirectory && item.DisplayPath == "Art");

        Assert.True(viewModel.DeleteAssetCommand.CanExecute(folderItem));
        viewModel.DeleteAssetCommand.Execute(folderItem);

        Assert.False(Directory.Exists(folderPath));
        Assert.DoesNotContain(viewModel.AssetBrowserItems, item => item.IsDirectory && item.DisplayPath == "Art");
    }

    [Fact]
    public void RenameAssetCommand_WhenFileSelected_RenamesAndRefreshesSelection()
    {
        using var temp = new TempProjectDirectory();
        var filePath = Path.Combine(temp.AssetsDirectory, "old-name.panel2d");
        File.WriteAllText(filePath, "{}");
        var expectedPath = Path.Combine(temp.AssetsDirectory, "new-name.panel2d");

        var viewModel = new AssetBrowserViewModel(
            loadedProjectAccessor: () => temp.Project,
            selectionChanged: () => { },
            notifyInspectorChanged: () => { },
            addOutputEntry: (_, _) => { },
            openAsset: _ => { },
            requestAssetRename: _ => "new-name.panel2d");

        viewModel.RefreshAssetBrowser();
        var fileItem = Assert.Single(viewModel.AssetBrowserItems, item => !item.IsDirectory && item.DisplayPath == "old-name.panel2d");

        Assert.True(viewModel.RenameAssetCommand.CanExecute(fileItem));
        viewModel.RenameAssetCommand.Execute(fileItem);

        Assert.False(File.Exists(filePath));
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("new-name.panel2d", viewModel.SelectedAsset?.DisplayPath);
        Assert.Contains(viewModel.AssetBrowserItems, item => !item.IsDirectory && item.DisplayPath == "new-name.panel2d");
    }

    [Fact]
    public void SelectedDirectory_OutsideAssetsRoot_IsIgnoredForContents()
    {
        using var temp = new TempProjectDirectory();
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "inside.txt"), "ok");
        var outsideDirectory = Path.Combine(temp.RootDirectory, "Outside");
        Directory.CreateDirectory(outsideDirectory);
        File.WriteAllText(Path.Combine(outsideDirectory, "outside.txt"), "nope");

        var outputEntries = new List<string>();
        var viewModel = new AssetBrowserViewModel(
            loadedProjectAccessor: () => temp.Project,
            selectionChanged: () => { },
            notifyInspectorChanged: () => { },
            addOutputEntry: (message, _) => outputEntries.Add(message),
            openAsset: _ => { },
            requestAssetRename: _ => null);

        viewModel.RefreshAssetBrowser();
        viewModel.SelectedDirectory = new AssetDirectoryNodeViewModel("Outside", outsideDirectory);

        Assert.Empty(viewModel.AssetBrowserItems);
        Assert.Contains(outputEntries, message => message.Contains("outside the Assets root", StringComparison.OrdinalIgnoreCase));
    }

    private static AssetBrowserViewModel CreateViewModel(EditorProject project, Action<AssetBrowserItemViewModel?> openAsset)
    {
        return new AssetBrowserViewModel(
            loadedProjectAccessor: () => project,
            selectionChanged: () => { },
            notifyInspectorChanged: () => { },
            addOutputEntry: (_, _) => { },
            openAsset: openAsset,
            requestAssetRename: _ => null);
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
