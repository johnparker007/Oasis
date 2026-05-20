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
    DocumentTabViewModel SaveDocument(DocumentTabViewModel current, string savePath);
}

public sealed class DocumentSaveService : IDocumentSaveService
{
    public DocumentTabViewModel SaveDocument(DocumentTabViewModel current, string savePath)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            throw new ArgumentException("Save path is required.", nameof(savePath));
        }

        var content = DocumentWorkspaceViewModel.BuildDocumentContent(current);
        File.WriteAllText(savePath, content);

        return new DocumentTabViewModel(
            current.Document.SaveAs(savePath, current.ContentSummary).MarkClean(),
            current.PanelLayoutJson,
            current.DocumentId,
            current.CommandService)
        {
            PanelZoom = current.PanelZoom,
            PanelPanX = current.PanelPanX,
            PanelPanY = current.PanelPanY
        };
    }
}
