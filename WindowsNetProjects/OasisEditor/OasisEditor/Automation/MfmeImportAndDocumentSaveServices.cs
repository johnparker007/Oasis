using System.IO;
using OasisEditor.Features.MfmeImport;

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
    DocumentTabViewModel SaveDocument(DocumentTabViewModel current, string savePath, EditorProject? project = null);
}

public sealed class DocumentSaveService : IDocumentSaveService
{
    private readonly FaceRuntimeExportService _faceRuntimeExportService;

    public DocumentSaveService(FaceRuntimeExportService? faceRuntimeExportService = null)
    {
        _faceRuntimeExportService = faceRuntimeExportService ?? new FaceRuntimeExportService();
    }

    public DocumentTabViewModel SaveDocument(DocumentTabViewModel current, string savePath, EditorProject? project = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            throw new ArgumentException("Save path is required.", nameof(savePath));
        }

        var faceDocumentJson = current.FaceDocumentJson;
        var contentSource = current;
        if (current.Document.DocumentType == EditorDocumentType.Face && project is not null)
        {
            var exportResult = _faceRuntimeExportService.Export(current.GetFaceDocument(), project);
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

        var content = DocumentWorkspaceViewModel.BuildDocumentContent(contentSource);
        File.WriteAllText(savePath, content);

        return new DocumentTabViewModel(
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
    }
}
