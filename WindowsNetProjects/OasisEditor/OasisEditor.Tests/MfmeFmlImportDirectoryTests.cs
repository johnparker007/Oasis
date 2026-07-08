using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeFmlImportDirectoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolveMfmeFmlImportInitialDirectory_UsesProjectDirectory_WhenSavedDirectoryIsEmpty()
    {
        var projectDirectory = CreateDirectory("project");

        var initialDirectory = MainWindowViewModel.ResolveMfmeFmlImportInitialDirectory(
            string.Empty,
            projectDirectory);

        Assert.Equal(projectDirectory, initialDirectory);
    }

    [Fact]
    public void ResolveMfmeFmlImportInitialDirectory_UsesSavedDirectory_WhenSavedDirectoryExists()
    {
        var projectDirectory = CreateDirectory("project");
        var savedDirectory = CreateDirectory("fml-layouts");

        var initialDirectory = MainWindowViewModel.ResolveMfmeFmlImportInitialDirectory(
            savedDirectory,
            projectDirectory);

        Assert.Equal(savedDirectory, initialDirectory);
    }

    [Fact]
    public void ResolveMfmeFmlImportInitialDirectory_UsesProjectDirectory_WhenSavedDirectoryIsMissing()
    {
        var projectDirectory = CreateDirectory("project");
        var missingSavedDirectory = Path.Combine(_tempRoot, "deleted-layouts");

        var initialDirectory = MainWindowViewModel.ResolveMfmeFmlImportInitialDirectory(
            missingSavedDirectory,
            projectDirectory);

        Assert.Equal(projectDirectory, initialDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
