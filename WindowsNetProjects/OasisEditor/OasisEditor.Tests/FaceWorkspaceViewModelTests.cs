using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceWorkspaceViewModelTests
{
    [Fact]
    public void FaceWorkspace_InitializesToOverview()
    {
        var workspace = CreateWorkspace();

        Assert.Equal(FaceWorkspaceDestination.Overview, workspace.Destination);
        Assert.Equal("Overview", workspace.DestinationName);
    }

    [Theory]
    [InlineData(FaceWorkspaceDestination.Artwork)]
    [InlineData(FaceWorkspaceDestination.Components)]
    [InlineData(FaceWorkspaceDestination.Illumination)]
    [InlineData(FaceWorkspaceDestination.FaceEditor)]
    public void NavigateFromOverview_ChangesDestination(FaceWorkspaceDestination destination)
    {
        var workspace = CreateWorkspace();

        workspace.NavigateTo(destination);

        Assert.Equal(destination, workspace.Destination);
    }

    [Fact]
    public void OverviewCommand_ReturnsFromDetailAndActsAsParentBreadcrumb()
    {
        var workspace = CreateWorkspace();
        workspace.NavigateTo(FaceWorkspaceDestination.Artwork);

        workspace.NavigateToOverviewCommand.Execute(null);

        Assert.Equal(FaceWorkspaceDestination.Overview, workspace.Destination);
    }

    [Fact]
    public void FaceEditor_HasFriendlyBreadcrumbNameAndCanReturnToOverview()
    {
        var workspace = CreateWorkspace();

        workspace.NavigateToFaceEditorCommand.Execute(null);
        Assert.Equal(FaceWorkspaceDestination.FaceEditor, workspace.Destination);
        Assert.Equal("Face Editor", workspace.DestinationName);

        workspace.NavigateToOverviewCommand.Execute(null);
        Assert.Equal(FaceWorkspaceDestination.Overview, workspace.Destination);
    }

    private static FaceWorkspaceViewModel CreateWorkspace()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Upper Glass"));
        return Assert.IsType<FaceWorkspaceViewModel>(document.FaceWorkspace);
    }
}
