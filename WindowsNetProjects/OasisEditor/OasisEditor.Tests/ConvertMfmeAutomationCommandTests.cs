using OasisEditor.Automation;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ConvertMfmeAutomationCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WhenImportSucceeds_SavesPanelDocument()
    {
        var state = new ConvertMfmeAutomationState();
        var saveService = new FakeSaveService();
        var command = new ConvertMfmeAutomationCommand(
            new FakeProjectCreationService(),
            new Panel2DDocumentCreationService(),
            new FakeMfmeImportService(succeeded: true),
            saveService,
            new ConvertMfmeAutomationOptions
            {
                ProjectName = "DemoProject",
                ProjectRootLocation = "C:/Temp",
                PanelDocumentTitle = "mfmeimport.panel2d",
                InputExtractPath = "input.json",
                OutputPanelPath = "output.panel2d"
            },
            state);

        var result = await command.ExecuteAsync(new OasisAutomationCommandContext());

        Assert.True(result.Succeeded);
        Assert.True(saveService.WasCalled);
        Assert.NotNull(state.ProjectDirectory);
        Assert.NotNull(state.PanelDocument);
    }

    [Fact]
    public async Task ExecuteAsync_WhenImportFails_ReturnsFailureAndDoesNotSave()
    {
        var saveService = new FakeSaveService();
        var command = new ConvertMfmeAutomationCommand(
            new FakeProjectCreationService(),
            new Panel2DDocumentCreationService(),
            new FakeMfmeImportService(succeeded: false),
            saveService,
            new ConvertMfmeAutomationOptions
            {
                ProjectName = "DemoProject",
                ProjectRootLocation = "C:/Temp",
                PanelDocumentTitle = "mfmeimport.panel2d",
                InputExtractPath = "input.json",
                OutputPanelPath = "output.panel2d"
            });

        var result = await command.ExecuteAsync(new OasisAutomationCommandContext());

        Assert.False(result.Succeeded);
        Assert.False(saveService.WasCalled);
        Assert.Contains("import failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeProjectCreationService : IProjectContainerCreationService
    {
        public string CreateProjectContainer(string projectName, string rootLocation) => Path.Combine(rootLocation, projectName);
    }

    private sealed class FakeMfmeImportService(bool succeeded) : IMfmeExtractImportService
    {
        public MfmeImportResult ImportFromExtract(string sourceExtractPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true)
        {
            return new MfmeImportResult
            {
                ImportedElements = succeeded ? [PanelElementFactory.CreateDefaultRectangle("rect-1")] : [],
                CopiedAssetRelativePaths = [],
                InputDefinitions = [],
                SkippedLegacyComponentTypes = [],
                Warnings = [],
                Errors = succeeded ? [] : ["import failed"]
            };
        }
    }

    private sealed class FakeSaveService : IDocumentSaveService
    {
        public bool WasCalled { get; private set; }

        public DocumentTabViewModel SaveDocument(DocumentTabViewModel current, string savePath)
        {
            WasCalled = true;
            return current;
        }
    }
}
