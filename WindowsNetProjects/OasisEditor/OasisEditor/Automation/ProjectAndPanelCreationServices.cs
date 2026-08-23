namespace OasisEditor.Automation;

public interface IProjectContainerCreationService
{
    string CreateProjectContainer(string projectName, string rootLocation);
}

public sealed class ProjectContainerCreationService : IProjectContainerCreationService
{
    private readonly ProjectScaffolder _projectScaffolder;

    public ProjectContainerCreationService(ProjectScaffolder? projectScaffolder = null)
    {
        _projectScaffolder = projectScaffolder ?? new ProjectScaffolder();
    }

    public string CreateProjectContainer(string projectName, string rootLocation)
    {
        return _projectScaffolder.CreateProject(projectName, rootLocation);
    }
}

public interface IPanel2DDocumentCreationService
{
    DocumentTabViewModel CreatePanel2DStubDocument(string title, int panelIndex);
}

public interface IFaceDocumentCreationService
{
    FaceDocumentCreationResult CreateFaceDocument(FaceDocumentCreationOptions options, EditorProject project);
    DocumentTabViewModel CreateFaceStubDocument(string title, int faceIndex);
}

public enum FaceStartingArtworkKind { Blank, Image }
public sealed record FaceDocumentCreationOptions(string? Title, int FaceIndex, FaceStartingArtworkKind StartingArtworkKind, string? ImagePath = null);
public sealed record FaceDocumentCreationResult(DocumentTabViewModel? Document, string? ErrorMessage)
{
    public bool Succeeded => Document is not null;
    public static FaceDocumentCreationResult Failure(string message) => new(null, message);
}

public sealed class Panel2DDocumentCreationService : IPanel2DDocumentCreationService
{
    public DocumentTabViewModel CreatePanel2DStubDocument(string title, int panelIndex)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? $"Panel {panelIndex}"
            : title.Trim();

        return new DocumentTabViewModel(
            EditorDocument.CreatePanel2DStub(resolvedTitle),
            panelLayoutJson: Panel2DDocumentStorage.SerializeLayout([]));
    }
}

public sealed class FaceDocumentCreationService : IFaceDocumentCreationService
{
    public FaceDocumentCreationResult CreateFaceDocument(FaceDocumentCreationOptions options, EditorProject project)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(project);
        var title = string.IsNullOrWhiteSpace(options.Title) ? $"Face {options.FaceIndex}" : options.Title.Trim();
        try
        {
            _ = new ProjectAssetPathService().SanitizePathSegment(title);
            var file = FaceDocumentStorage.CreateEmpty(title);
            if (options.StartingArtworkKind == FaceStartingArtworkKind.Image)
            {
                if (string.IsNullOrWhiteSpace(options.ImagePath)) return FaceDocumentCreationResult.Failure("Choose an image for the Face artwork.");
                var imported = FaceArtworkImageImportService.Import(options.ImagePath, project, title);
                var artwork = FaceArtworkImageImportService.CreateArtwork(imported, title);
                file = file with
                {
                    Artwork = FaceDocumentStorage.ToFile(artwork),
                    Provenance = new FaceProvenanceModel(),
                    BuildState = CreateImageBuildState(),
                    Elements =
                    [
                        new FaceElementFile
                        {
                            ObjectId = $"face-artwork-{Guid.NewGuid():N}", Kind = "artwork", Name = "Face artwork",
                            X = 0, Y = 0, Width = FaceDocumentStorage.DefaultNativeLogicalWidth,
                            Height = FaceDocumentStorage.DefaultNativeLogicalHeight, IsVisible = true,
                            LockTransform = true, AssetPath = artwork.OutputAssetPath
                        }
                    ]
                };
            }
            var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub(title).MarkDirty(), faceDocumentJson: FaceDocumentStorage.Serialize(file));
            return new FaceDocumentCreationResult(document, null);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return FaceDocumentCreationResult.Failure(exception.Message);
        }
    }

    public DocumentTabViewModel CreateFaceStubDocument(string title, int faceIndex)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? $"Face {faceIndex}"
            : title.Trim();

        return new DocumentTabViewModel(
            EditorDocument.CreateFaceStub(resolvedTitle),
            faceDocumentJson: FaceDocumentStorage.Serialize(FaceDocumentStorage.CreateEmpty(resolvedTitle)));
    }

    private static FaceBuildStateModel CreateImageBuildState()
    {
        var state = new FaceBuildStateModel();
        state.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status = FaceBuildStatus.Stale;
        state.Get(FaceGeneratedProduct.BaseArtwork).Status = FaceBuildStatus.Stale;
        state.Get(FaceGeneratedProduct.ArtworkOutput).Status = FaceBuildStatus.Stale;
        return state;
    }
}
