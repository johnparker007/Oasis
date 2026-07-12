using System;
using System.IO;
using System.Threading;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ProjectAssetPathResolverTests
{
    [Fact]
    public void TryResolveAssetPath_WithBlankPath_ReturnsFalse()
    {
        var previous = ProjectAssetPathResolver.ProjectDirectoryPath;
        try
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = CreateProjectRoot();

            var success = ProjectAssetPathResolver.TryResolveAssetPath("  ", out var resolvedPath);

            Assert.False(success);
            Assert.Equal(string.Empty, resolvedPath);
        }
        finally
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = previous;
        }
    }

    [Fact]
    public void TryResolveAssetPath_WithAbsolutePath_ReturnsAbsolutePathWithoutProjectContext()
    {
        var previous = ProjectAssetPathResolver.ProjectDirectoryPath;
        try
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = null;
            var absolutePath = Path.Combine(CreateProjectRoot(), "Assets", "image.png");

            var success = ProjectAssetPathResolver.TryResolveAssetPath(absolutePath, out var resolvedPath);

            Assert.True(success);
            Assert.Equal(absolutePath, resolvedPath);
        }
        finally
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = previous;
        }
    }

    [Theory]
    [InlineData("Assets/Images/lamp.png")]
    [InlineData("Assets\\Images\\lamp.png")]
    public void TryResolveAssetPath_WithRelativePath_ResolvesAgainstProjectDirectory(string assetPath)
    {
        var previous = ProjectAssetPathResolver.ProjectDirectoryPath;
        try
        {
            var projectRoot = CreateProjectRoot();
            ProjectAssetPathResolver.ProjectDirectoryPath = projectRoot;

            var success = ProjectAssetPathResolver.TryResolveAssetPath(assetPath, out var resolvedPath);

            Assert.True(success);
            Assert.Equal(Path.GetFullPath(Path.Combine(projectRoot, "Assets", "Images", "lamp.png")), resolvedPath);
        }
        finally
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = previous;
        }
    }

    [Fact]
    public void TryResolveAssetPath_WithRelativePathAndNoProjectDirectory_ReturnsFalse()
    {
        var previous = ProjectAssetPathResolver.ProjectDirectoryPath;
        try
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = null;

            var success = ProjectAssetPathResolver.TryResolveAssetPath("Assets/Images/lamp.png", out var resolvedPath);

            Assert.False(success);
            Assert.Equal(string.Empty, resolvedPath);
        }
        finally
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = previous;
        }
    }

    [Fact]
    public void ProjectDirectoryPath_UsesThreadSpecificOverride()
    {
        var previous = ProjectAssetPathResolver.ProjectDirectoryPath;
        try
        {
            var mainThreadProject = Path.Combine(CreateProjectRoot(), "Main");
            var workerThreadProject = Path.Combine(CreateProjectRoot(), "Worker");
            ProjectAssetPathResolver.ProjectDirectoryPath = mainThreadProject;
            string? workerObserved = null;

            var worker = new Thread(() =>
            {
                ProjectAssetPathResolver.ProjectDirectoryPath = workerThreadProject;
                workerObserved = ProjectAssetPathResolver.ProjectDirectoryPath;
            });
            worker.Start();
            worker.Join();

            Assert.Equal(workerThreadProject, workerObserved);
            Assert.Equal(mainThreadProject, ProjectAssetPathResolver.ProjectDirectoryPath);
        }
        finally
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = previous;
        }
    }

    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
    }
}
