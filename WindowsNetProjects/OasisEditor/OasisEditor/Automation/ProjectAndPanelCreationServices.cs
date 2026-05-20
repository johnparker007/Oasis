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
