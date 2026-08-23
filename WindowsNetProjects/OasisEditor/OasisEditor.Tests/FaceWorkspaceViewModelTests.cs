using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceWorkspaceViewModelTests
{
    [Fact]
    public void FaceWorkspace_InitializesToOverview()
    {
        var (_, workspace) = CreateWorkspace();
        Assert.Equal(FaceWorkspaceDestination.Overview, workspace.Destination);
        Assert.Equal(["Upper Glass"], workspace.Breadcrumbs.Select(item => item.Label));
    }

    [Theory]
    [InlineData(FaceWorkspaceDestination.Artwork)]
    [InlineData(FaceWorkspaceDestination.Components)]
    [InlineData(FaceWorkspaceDestination.Illumination)]
    [InlineData(FaceWorkspaceDestination.FaceEditor)]
    public void NavigateFromOverview_ChangesDestination(FaceWorkspaceDestination destination)
    {
        var (_, workspace) = CreateWorkspace();
        workspace.NavigateTo(destination);
        Assert.Equal(destination, workspace.Destination);
    }

    [Fact]
    public void ArtworkCalibration_HasThreeLevelBreadcrumbAndArtworkParent()
    {
        var (_, workspace) = CreateWorkspace();
        workspace.NavigateToArtworkCommand.Execute(null);
        workspace.NavigateToArtworkCalibrationCommand.Execute(null);

        Assert.Equal(FaceWorkspaceDestination.ArtworkCalibration, workspace.Destination);
        Assert.Equal(["Upper Glass", "Artwork", "Calibration"], workspace.Breadcrumbs.Select(item => item.Label));

        workspace.Breadcrumbs[1].Command!.Execute(null);
        Assert.Equal(FaceWorkspaceDestination.Artwork, workspace.Destination);
    }

    [Fact]
    public void Calibration_CanReturnDirectlyToOverview()
    {
        var (_, workspace) = CreateWorkspace();
        workspace.NavigateTo(FaceWorkspaceDestination.ArtworkCalibration);
        workspace.NavigateToOverviewCommand.Execute(null);
        Assert.Equal(FaceWorkspaceDestination.Overview, workspace.Destination);
    }

    [Fact]
    public void ComponentsEditor_HasExplicitRoute()
    {
        var (_, workspace) = CreateWorkspace();
        workspace.NavigateToComponentsCommand.Execute(null);
        workspace.NavigateToComponentsEditorCommand.Execute(null);
        Assert.Equal(FaceWorkspaceDestination.ComponentsEditor, workspace.Destination);
        Assert.Equal(["Upper Glass", "Components", "Edit"], workspace.Breadcrumbs.Select(item => item.Label));
    }

    [Fact]
    public void FaceEditor_FallbackRemainsAvailable()
    {
        var (_, workspace) = CreateWorkspace();
        workspace.NavigateToFaceEditorCommand.Execute(null);
        Assert.Equal(FaceWorkspaceDestination.FaceEditor, workspace.Destination);
        Assert.Equal(["Upper Glass", "Face Editor"], workspace.Breadcrumbs.Select(item => item.Label));
    }

    [Fact]
    public void Workspaces_AreDocumentLocal()
    {
        var (_, first) = CreateWorkspace();
        var (_, second) = CreateWorkspace("Lower Glass");
        first.NavigateTo(FaceWorkspaceDestination.ComponentsEditor);
        Assert.Equal(FaceWorkspaceDestination.ComponentsEditor, first.Destination);
        Assert.Equal(FaceWorkspaceDestination.Overview, second.Destination);
    }

    [Fact]
    public void LeavingCalibration_CancelsTransientPlacement()
    {
        var (document, workspace) = CreateWorkspace();
        workspace.NavigateTo(FaceWorkspaceDestination.ArtworkCalibration);
        document.CalibrationPlacement = Placement();

        workspace.NavigateTo(FaceWorkspaceDestination.ComponentsEditor);

        Assert.Null(document.CalibrationPlacement);
        Assert.Equal(FaceWorkspaceDestination.ComponentsEditor, workspace.Destination);
    }

    [Fact]
    public void InspectorStylePlacementEntry_RoutesToCalibrationBeforeStartingTool()
    {
        var (document, workspace) = CreateWorkspace();
        workspace.NavigateTo(FaceWorkspaceDestination.ComponentsEditor);

        document.BeginCalibrationPlacement(Placement());

        Assert.Equal(FaceWorkspaceDestination.ArtworkCalibration, workspace.Destination);
        Assert.NotNull(document.CalibrationPlacement);
    }

    [Fact]
    public void NavigatingWithinCalibration_DoesNotCancelPlacement()
    {
        var (document, workspace) = CreateWorkspace();
        document.BeginCalibrationPlacement(Placement());
        workspace.NavigateTo(FaceWorkspaceDestination.ArtworkCalibration);
        Assert.NotNull(document.CalibrationPlacement);
    }

    [Fact]
    public void BuildSummary_IsDrivenByCentralStateAndRefreshesAfterInvalidation()
    {
        var model = new FaceDocumentModel
        {
            Title = "Upper Glass",
            BuildState = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false)
        };
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Upper Glass"),
            faceDocumentJson: FaceDocumentStorage.Serialize(model));
        var workspace = Assert.IsType<FaceWorkspaceViewModel>(document.FaceWorkspace);
        Assert.Equal("Build status: Current", workspace.BuildStatusSummary);

        document.InvalidateFaceBuild(FaceBuildInput.ArtworkCorrection);

        Assert.Equal("Build status: 2 outputs need building", workspace.BuildStatusSummary);
        Assert.Contains("Base: Stale", workspace.ArtworkBuildSummary);
        Assert.Contains("Output: Stale", workspace.ArtworkBuildSummary);
    }

    [Fact]
    public void BuildErrorMessage_IsAvailableInWorkspaceSummary()
    {
        var model = new FaceDocumentModel { Title = "Upper Glass" };
        var node = model.BuildState.Get(FaceGeneratedProduct.LampMask);
        node.Status = FaceBuildStatus.Error;
        node.ErrorMessage = "Source Panel2D is unavailable.";
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Upper Glass"),
            faceDocumentJson: FaceDocumentStorage.Serialize(model));

        var workspace = Assert.IsType<FaceWorkspaceViewModel>(document.FaceWorkspace);

        Assert.Contains("Lamp Mask: Source Panel2D is unavailable.", workspace.BuildErrorSummary);
        Assert.Contains("Mask: Error", workspace.IlluminationBuildSummary);
    }

    private static CalibrationPlacementState Placement() =>
        new("calibration", CalibrationPlacementTargetKind.BlackReference, string.Empty, CalibrationSamplingMode.Pixel, .01);

    private static (DocumentTabViewModel Document, FaceWorkspaceViewModel Workspace) CreateWorkspace(string title = "Upper Glass")
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub(title));
        return (document, Assert.IsType<FaceWorkspaceViewModel>(document.FaceWorkspace));
    }
}
