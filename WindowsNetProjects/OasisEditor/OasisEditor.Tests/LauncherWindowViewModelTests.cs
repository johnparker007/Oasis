using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class LauncherWindowViewModelTests
{
    [Fact]
    public void GetDefaultProjectLocation_PlacesProjectsUnderOasisEditorProjectsInDocuments()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var expectedPath = Path.Combine(documentsPath, "Oasis", "Editor", "Projects");

        var defaultProjectLocation = LauncherWindowViewModel.GetDefaultProjectLocation();

        Assert.Equal(expectedPath, defaultProjectLocation);
    }
}
