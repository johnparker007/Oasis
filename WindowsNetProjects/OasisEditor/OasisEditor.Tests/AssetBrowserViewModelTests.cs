using System.Collections.Generic;
using System.IO;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AssetBrowserViewModelTests
{
    [Fact]
    public void RefreshAssetBrowser_RaisesSingleAssetCatalogChangedNotification()
    {
        using var temp = new TempProjectDirectory();
        var viewModel = CreateViewModel(temp.Project, _ => { });
        var changes = 0; viewModel.AssetCatalogChanged += () => changes++;
        viewModel.RefreshAssetBrowser();
        Assert.Equal(1, changes);
        viewModel.Dispose();
    }

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
            requestAssetRename: _ => "new-name.panel2d",
            confirmAssetDelete: _ => true);

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
            requestAssetRename: _ => null,
            confirmAssetDelete: _ => true);

        viewModel.RefreshAssetBrowser();
        viewModel.SelectedDirectory = new AssetDirectoryNodeViewModel("Outside", outsideDirectory);

        Assert.Empty(viewModel.AssetBrowserItems);
        Assert.Contains(outputEntries, message => message.Contains("outside the Assets root", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OpenAssetCommand_WithMultipleSelectedFiles_OpensEveryFile()
    {
        using var temp = new TempProjectDirectory();
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "one.panel2d"), "{}");
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "two.png"), "png");
        var openedPaths = new List<string>();
        var viewModel = CreateViewModel(temp.Project, asset => openedPaths.Add(asset!.FullPath));
        viewModel.RefreshAssetBrowser();
        viewModel.SetSelectedAssets(viewModel.AssetBrowserItems);

        viewModel.OpenAssetCommand.Execute(null);

        Assert.Equal(2, openedPaths.Count);
        Assert.Contains(Path.Combine(temp.AssetsDirectory, "one.panel2d"), openedPaths);
        Assert.Contains(Path.Combine(temp.AssetsDirectory, "two.png"), openedPaths);
    }

    [Fact]
    public void DeleteAssetCommand_WithMixedSelection_ConfirmsOnceAndDeletesEverything()
    {
        using var temp = new TempProjectDirectory();
        var filePath = Path.Combine(temp.AssetsDirectory, "delete.txt");
        var folderPath = Path.Combine(temp.AssetsDirectory, "Folder");
        File.WriteAllText(filePath, "delete");
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, "child.txt"), "delete");
        var confirmationCount = 0;
        var viewModel = new AssetBrowserViewModel(
            () => temp.Project, () => { }, () => { }, (_, _) => { }, _ => { }, _ => null,
            assets =>
            {
                confirmationCount++;
                Assert.Equal(2, assets.Count);
                return true;
            });
        viewModel.RefreshAssetBrowser();
        viewModel.SetSelectedAssets(viewModel.AssetBrowserItems);

        viewModel.DeleteAssetCommand.Execute(null);

        Assert.Equal(1, confirmationCount);
        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(folderPath));
        Assert.Empty(viewModel.SelectedAssets);
    }

    [Fact]
    public void RenameAssetCommand_IsDisabledForMultipleSelection()
    {
        using var temp = new TempProjectDirectory();
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "one.txt"), "one");
        File.WriteAllText(Path.Combine(temp.AssetsDirectory, "two.txt"), "two");
        var viewModel = CreateViewModel(temp.Project, _ => { });
        viewModel.RefreshAssetBrowser();
        viewModel.SetSelectedAssets(viewModel.AssetBrowserItems);

        Assert.False(viewModel.RenameAssetCommand.CanExecute(null));
    }

    [Fact]
    public void CopyCabinetPackageToLibraryCopiesDependenciesAndLibraryCanBrowseAndOpenIt()
    {
        using var temp = new TempProjectDirectory();
        var library = Path.Combine(temp.RootDirectory, "Library");
        var package = Path.Combine(temp.AssetsDirectory, "Cabinet3D", "Vogue");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "asset.cabinet3d"), "{}");
        File.WriteAllText(Path.Combine(package, "cabinet.glb"), "glb");
        var reelPackage = Path.Combine(temp.AssetsDirectory, "Reels", "Standard");
        Directory.CreateDirectory(reelPackage);
        File.WriteAllText(Path.Combine(reelPackage, "asset.reel"), "{}");
        string? opened = null;
        var viewModel = new AssetBrowserViewModel(() => temp.Project, () => { }, () => { }, (_, _) => { }, item => opened = item?.FullPath, _ => null, _ => true, () => library);
        viewModel.RefreshAssetBrowser();
        viewModel.SelectedDirectory = Find(viewModel.AssetDirectoryTree[0], package)!;
        var manifest = viewModel.AssetBrowserItems.Single(item => item.DisplayPath == "asset.cabinet3d");

        Assert.True(viewModel.CopyToLibraryCommand.CanExecute(manifest));
        viewModel.CopyToLibraryCommand.Execute(manifest);
        Assert.True(File.Exists(Path.Combine(library, "Cabinets", "Vogue", "cabinet.glb")));
        viewModel.SelectedDirectory = Find(viewModel.AssetDirectoryTree[0], reelPackage)!;
        viewModel.CopyToLibraryCommand.Execute(viewModel.AssetBrowserItems.Single(item => item.DisplayPath == "asset.reel"));
        Assert.True(File.Exists(Path.Combine(library, "Reels", "Standard", "asset.reel")));
        var libraryRoot = viewModel.AssetDirectoryTree.Single(node => node.DisplayPath == "Library");
        var libraryPackage = Find(libraryRoot, Path.Combine(library, "Cabinets", "Vogue"))!;
        viewModel.SelectedDirectory = libraryPackage;
        var libraryManifest = viewModel.AssetBrowserItems.Single(item => item.DisplayPath == "asset.cabinet3d");
        Assert.False(viewModel.RenameAssetCommand.CanExecute(libraryManifest));
        Assert.False(viewModel.DeleteAssetCommand.CanExecute(libraryManifest));
        viewModel.OpenAssetCommand.Execute(libraryManifest);
        Assert.Equal(libraryManifest.FullPath, opened);
        viewModel.Dispose();
    }

    private static AssetDirectoryNodeViewModel? Find(IEnumerable<AssetDirectoryNodeViewModel> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase)) return node;
            var child = Find(node.Children, path); if (child is not null) return child;
        }
        return null;
    }

    private static AssetDirectoryNodeViewModel? Find(AssetDirectoryNodeViewModel node, string path)
        => Find([node], path);

    private static AssetBrowserViewModel CreateViewModel(EditorProject project, Action<AssetBrowserItemViewModel?> openAsset)
    {
        return new AssetBrowserViewModel(
            loadedProjectAccessor: () => project,
            selectionChanged: () => { },
            notifyInspectorChanged: () => { },
            addOutputEntry: (_, _) => { },
            openAsset: openAsset,
            requestAssetRename: _ => null,
            confirmAssetDelete: _ => true);
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
