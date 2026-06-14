using System.IO;
using OasisEditor.Features.MfmeImport;
using OasisEditor.Progress;

namespace OasisEditor.Automation;

internal interface IMfmeExtractImportService
{
    MfmeImportResult ImportFromExtract(string sourceExtractPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true);
}

internal sealed class MfmeExtractImportService : IMfmeExtractImportService
{
    private readonly MfmeImportService _mfmeImportService;

    public MfmeExtractImportService(MfmeImportService? mfmeImportService = null)
    {
        _mfmeImportService = mfmeImportService ?? new MfmeImportService();
    }

    public MfmeImportResult ImportFromExtract(string sourceExtractPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true)
    {
        var context = new MfmeImportContext
        {
            SourceExtractPath = sourceExtractPath,
            ProjectRootPath = projectRootPath,
            ProjectAssetsPath = projectAssetsPath,
            CopyAssets = copyAssets
        };

        return _mfmeImportService.Import(context);
    }
}

public interface IDocumentSaveService
{
    DocumentTabViewModel SaveDocument(DocumentTabViewModel current, string savePath, EditorProject? project = null, IEditorProgressReporter? progress = null);
}

public sealed class DocumentSaveService : IDocumentSaveService
{
    private readonly FaceRuntimeExportService _faceRuntimeExportService;

    public DocumentSaveService(FaceRuntimeExportService? faceRuntimeExportService = null)
    {
        _faceRuntimeExportService = faceRuntimeExportService ?? new FaceRuntimeExportService();
    }

    public DocumentTabViewModel SaveDocument(DocumentTabViewModel current, string savePath, EditorProject? project = null, IEditorProgressReporter? progress = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        progress ??= NoOpEditorProgressReporter.Instance;
        progress.Report(0.0, "Preparing document save...");

        if (string.IsNullOrWhiteSpace(savePath))
        {
            throw new ArgumentException("Save path is required.", nameof(savePath));
        }

        var faceDocumentJson = current.FaceDocumentJson;
        var contentSource = current;
        progress.Report(0.1, "Collecting document content...");
        if (current.Document.DocumentType == EditorDocumentType.Face && project is not null)
        {
            progress.Report(0.15, "Exporting Face runtime assets...");
            var exportResult = _faceRuntimeExportService.Export(current.GetFaceDocument(), project, progress.CreateChild(0.15, 0.75));
            faceDocumentJson = FaceDocumentStorage.Serialize(exportResult.Document);
            contentSource = new DocumentTabViewModel(
                current.Document,
                current.PanelLayoutJson,
                current.DocumentId,
                current.CommandService,
                current.RuntimeState,
                faceDocumentJson)
            {
                PanelZoom = current.PanelZoom,
                PanelPanX = current.PanelPanX,
                PanelPanY = current.PanelPanY,
                FaceZoom = current.FaceZoom,
                FacePanX = current.FacePanX,
                FacePanY = current.FacePanY
            };
        }

        progress.Report(0.8, "Serializing document...");
        var content = DocumentWorkspaceViewModel.BuildDocumentContent(contentSource);
        progress.Report(0.9, "Writing document file...");
        File.WriteAllText(savePath, content);
        progress.Report(0.95, "Updating document state...");

        var savedDocument = new DocumentTabViewModel(
            current.Document.SaveAs(savePath, current.ContentSummary).MarkClean(),
            current.PanelLayoutJson,
            current.DocumentId,
            current.CommandService,
            current.RuntimeState,
            faceDocumentJson)
        {
            PanelZoom = current.PanelZoom,
            PanelPanX = current.PanelPanX,
            PanelPanY = current.PanelPanY,
            FaceZoom = current.FaceZoom,
            FacePanX = current.FacePanX,
            FacePanY = current.FacePanY
        };

        progress.Report(1.0, "Document saved.");
        return savedDocument;
    }
}
