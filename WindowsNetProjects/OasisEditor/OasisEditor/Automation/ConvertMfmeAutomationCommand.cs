using System.IO;
namespace OasisEditor.Automation;

internal sealed class ConvertMfmeAutomationState
{
    public string? ProjectDirectory { get; set; }
    public DocumentTabViewModel? PanelDocument { get; set; }
}

internal sealed class ConvertMfmeAutomationOptions
{
    public required string ProjectName { get; init; }
    public required string ProjectRootLocation { get; init; }
    public required string PanelDocumentTitle { get; init; }
    public required string InputExtractPath { get; init; }
    public required string OutputPanelPath { get; init; }
}

internal sealed class ConvertMfmeAutomationCommand : IOasisAutomationCommand
{
    private readonly IProjectContainerCreationService _projectCreationService;
    private readonly IPanel2DDocumentCreationService _panelCreationService;
    private readonly IMfmeExtractImportService _mfmeImportService;
    private readonly IDocumentSaveService _documentSaveService;
    private readonly ConvertMfmeAutomationOptions _options;
    private readonly ConvertMfmeAutomationState _state;

    public ConvertMfmeAutomationCommand(
        IProjectContainerCreationService projectCreationService,
        IPanel2DDocumentCreationService panelCreationService,
        IMfmeExtractImportService mfmeImportService,
        IDocumentSaveService documentSaveService,
        ConvertMfmeAutomationOptions options,
        ConvertMfmeAutomationState? state = null)
    {
        _projectCreationService = projectCreationService;
        _panelCreationService = panelCreationService;
        _mfmeImportService = mfmeImportService;
        _documentSaveService = documentSaveService;
        _options = options;
        _state = state ?? new ConvertMfmeAutomationState();
    }

    public string Name => "ConvertMfme";

    public Task<OasisAutomationCommandResult> ExecuteAsync(OasisAutomationCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var projectDirectory = _projectCreationService.CreateProjectContainer(_options.ProjectName, _options.ProjectRootLocation);
            _state.ProjectDirectory = projectDirectory;
            context.Logger.Info($"Created project container: {projectDirectory}");

            var panel = _panelCreationService.CreatePanel2DStubDocument(_options.PanelDocumentTitle, 1);
            _state.PanelDocument = panel;
            context.Logger.Info($"Created Panel2D document: {panel.Title}");

            var importResult = _mfmeImportService.ImportFromExtract(
                _options.InputExtractPath,
                projectDirectory,
                Path.Combine(projectDirectory, "Assets"),
                copyAssets: true);

            if (!importResult.Succeeded)
            {
                var message = importResult.Errors.Count > 0
                    ? string.Join("; ", importResult.Errors)
                    : "MFME import failed.";
                return Task.FromResult(OasisAutomationCommandResult.Failure(message));
            }

            var importCommand = new Features.MfmeImport.ImportMfmeExtractCommand(panel.DocumentId, panel, importResult.ImportedElements);
            panel.CommandService.Execute(importCommand);
            context.Logger.Info($"Imported MFME extract elements: {importResult.ImportedElements.Count}");

            _state.PanelDocument = _documentSaveService.SaveDocument(panel, _options.OutputPanelPath);
            context.Logger.Info($"Saved Panel2D document: {_options.OutputPanelPath}");

            return Task.FromResult(OasisAutomationCommandResult.Success("MFME conversion automation completed."));
        }
        catch (Exception ex)
        {
            context.Logger.Error("MFME conversion automation failed.", ex);
            return Task.FromResult(OasisAutomationCommandResult.Failure(ex.Message));
        }
    }
}
