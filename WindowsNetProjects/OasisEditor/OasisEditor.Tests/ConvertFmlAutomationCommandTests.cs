using OasisEditor.Automation;
using OasisEditor.Features.LayoutImport;
using OasisEditor.Progress;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ConvertFmlAutomationCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WhenImportSucceeds_SavesPanelDocument()
    {
        var state = new ConvertFmlAutomationState();
        var saveService = new FakeSaveService();
        var command = new ConvertFmlAutomationCommand(
            new FakeProjectCreationService(),
            new Panel2DDocumentCreationService(),
            new FakeFmlImportService(succeeded: true),
            saveService,
            new FakeMameLayExportService(),
            new ConvertFmlAutomationOptions
            {
                ProjectName = "DemoProject",
                ProjectRootLocation = "C:/Temp",
                PanelDocumentTitle = "fmlimport.panel2d",
                InputFmlPath = "input.fml",
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
        var command = new ConvertFmlAutomationCommand(
            new FakeProjectCreationService(),
            new Panel2DDocumentCreationService(),
            new FakeFmlImportService(succeeded: false),
            saveService,
            new FakeMameLayExportService(),
            new ConvertFmlAutomationOptions
            {
                ProjectName = "DemoProject",
                ProjectRootLocation = "C:/Temp",
                PanelDocumentTitle = "fmlimport.panel2d",
                InputFmlPath = "input.fml",
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

    private sealed class FakeFmlImportService(bool succeeded) : IFmlAutomationImportService
    {
        public LayoutImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true)
        {
            return new LayoutImportResult
            {
                ImportedElements = succeeded ? [new PanelElementModel
                {
                    ObjectId = "rect-1",
                    Name = "Rect 1",
                    Kind = PanelElementKind.Rectangle,
                    Width = 10,
                    Height = 10
                }] : [],
                CopiedAssetRelativePaths = [],
                InputDefinitions = [],
                UnsupportedComponentTypes = [],
                Warnings = [],
                Errors = succeeded ? [] : ["import failed"]
            };
        }
    }

    private sealed class FakeMameLayExportService : IMameLayExportService
    {
        public OasisAutomationCommandResult Export(DocumentTabViewModel panelDocument, string outputLayPath)
        {
            return OasisAutomationCommandResult.Success("ok");
        }
    }

    private sealed class FakeSaveService : IDocumentSaveService
    {
        public bool WasCalled { get; private set; }

        public DocumentTabViewModel SaveDocument(DocumentTabViewModel current, string savePath, EditorProject? project = null, IEditorProgressReporter? progress = null)
        {
            WasCalled = true;
            return current;
        }
    }
}
