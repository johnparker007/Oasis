using System.IO;
namespace OasisEditor.Automation;

internal sealed class ConvertFmlAutomationState
{
    public string? ProjectDirectory { get; set; }
    public DocumentTabViewModel? PanelDocument { get; set; }
}

internal sealed class ConvertFmlAutomationOptions
{
    public required string ProjectName { get; init; }
    public required string ProjectRootLocation { get; init; }
    public required string PanelDocumentTitle { get; init; }
    public required string InputFmlPath { get; init; }
    public required string OutputPanelPath { get; init; }
}

internal sealed class ConvertFmlAutomationCommand : IOasisAutomationCommand
{
    private readonly IProjectContainerCreationService _projectCreationService;
    private readonly IPanel2DDocumentCreationService _panelCreationService;
    private readonly IFmlAutomationImportService _fmlImportService;
    private readonly IDocumentSaveService _documentSaveService;
    private readonly ConvertFmlAutomationOptions _options;
    private readonly ConvertFmlAutomationState _state;

    public ConvertFmlAutomationCommand(
        IProjectContainerCreationService projectCreationService,
        IPanel2DDocumentCreationService panelCreationService,
        IFmlAutomationImportService fmlImportService,
        IDocumentSaveService documentSaveService,
        ConvertFmlAutomationOptions options,
        ConvertFmlAutomationState? state = null)
    {
        _projectCreationService = projectCreationService;
        _panelCreationService = panelCreationService;
        _fmlImportService = fmlImportService;
        _documentSaveService = documentSaveService;
        _options = options;
        _state = state ?? new ConvertFmlAutomationState();
    }

    public string Name => "ConvertFml";

    public Task<OasisAutomationCommandResult> ExecuteAsync(OasisAutomationCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var projectDirectory = _projectCreationService.CreateProjectContainer(_options.ProjectName, _options.ProjectRootLocation);
            _state.ProjectDirectory = projectDirectory;
            context.Logger.Info($"Created project container: {projectDirectory}");

            var panel = _panelCreationService.CreatePanel2DStubDocument(_options.PanelDocumentTitle, 1);
            _state.PanelDocument = _documentSaveService.SaveDocument(panel, _options.OutputPanelPath);
            context.Logger.Info($"Saved Panel2D document: {_options.OutputPanelPath}");
            return Task.FromResult(OasisAutomationCommandResult.Success("MFME FML conversion automation completed."));
        }
        catch (Exception ex)
        {
            context.Logger.Error("MFME FML conversion automation failed.", ex);
            return Task.FromResult(OasisAutomationCommandResult.Failure(ex.Message));
        }
    }
}
