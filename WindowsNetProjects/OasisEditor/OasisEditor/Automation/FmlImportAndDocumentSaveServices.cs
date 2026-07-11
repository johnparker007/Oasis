using System.IO;
using OasisEditor.Features.LayoutImport;
using OasisEditor.Features.FmlImport;
using OasisEditor.Progress;
using SkiaSharp;

namespace OasisEditor.Automation;

internal interface IFmlAutomationImportService
{
    LayoutImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true);
}

internal sealed class FmlAutomationImportService : IFmlAutomationImportService
{
    private readonly IFmlImportService _fmlImportService;

    public FmlAutomationImportService(IFmlImportService? fmlImportService = null)
    {
        _fmlImportService = fmlImportService ?? new FmlImportService();
    }

    public LayoutImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true)
        => _fmlImportService.ImportFromFml(fmlPath, projectRootPath, projectAssetsPath, copyAssets);
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

        if (current.Document.DocumentType == EditorDocumentType.Cabinet3D
            && string.Equals(Path.GetExtension(savePath), ".glb", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cabinet3D documents must be saved as .cabinet3d metadata files, never as .glb model assets.");
        }

        var faceDocumentJson = current.FaceDocumentJson;
        var contentSource = current;
        progress.Report(0.1, "Collecting document content...");
        if (current.Document.DocumentType == EditorDocumentType.Face && project is not null)
        {
            progress.Report(0.15, "Exporting Face runtime assets...");
            var faceWithAuthoredAssets = EnsureFaceAuthoredPackageAssets(current.GetFaceDocument(), project, savePath);
            var exportResult = _faceRuntimeExportService.Export(faceWithAuthoredAssets, project, savePath, progress.CreateChild(0.15, 0.75));
            faceDocumentJson = FaceDocumentStorage.Serialize(exportResult.Document);
            contentSource = new DocumentTabViewModel(
                current.Document,
                current.PanelLayoutJson,
                current.DocumentId,
                current.CommandService,
                current.RuntimeState,
                faceDocumentJson,
                current.CabinetDocumentJson)
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
            faceDocumentJson,
            current.CabinetDocumentJson)
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

    private static FaceDocumentModel EnsureFaceAuthoredPackageAssets(FaceDocumentModel faceDocument, EditorProject project, string savePath)
    {
        var assetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(savePath, EditorAssetType.Face);
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return faceDocument;
        }

        var pathService = new ProjectAssetPathService();
        var artworkPath = pathService.GetFaceArtworkPath(project, assetName);
        var maskPath = pathService.GetFaceMaskPath(project, assetName);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(artworkPath)!);

        CopyOrCreatePng(project, faceDocument.Elements.OfType<FaceArtworkElement>().FirstOrDefault()?.AssetPath, artworkPath, faceDocument);
        CopyOrCreatePng(project, faceDocument.MaskLayer?.AssetPath, maskPath, faceDocument);

        var artworkRelative = pathService.ToProjectRelativePath(project, artworkPath);
        var maskRelative = pathService.ToProjectRelativePath(project, maskPath);
        var elements = faceDocument.Elements.Select(element => element is FaceArtworkElement artwork
            ? new FaceArtworkElement
            {
                ObjectId = artwork.ObjectId,
                Name = artwork.Name,
                X = artwork.X,
                Y = artwork.Y,
                Width = artwork.Width,
                Height = artwork.Height,
                IsVisible = artwork.IsVisible,
                IsLocked = artwork.IsLocked,
                LinkedMachineObjectReference = artwork.LinkedMachineObjectReference,
                LinkedPanel2DElementId = artwork.LinkedPanel2DElementId,
                AssetPath = artworkRelative,
                SourcePanel2DDocumentId = artwork.SourcePanel2DDocumentId,
                SourceRegion = artwork.SourceRegion,
                Provenance = artwork.Provenance
            }
            : element).ToArray();
        var maskLayer = faceDocument.MaskLayer is null
            ? new FaceMaskLayerModel
            {
                Id = "face-mask-layer",
                Name = "Face Mask",
                AssetPath = maskRelative,
                SourcePanel2DDocumentId = faceDocument.SourcePanel2DDocumentId,
                SourceRegion = faceDocument.SourceRegion,
                Width = Math.Max(1, (int)Math.Ceiling(faceDocument.SourceRegion?.Width ?? 1)),
                Height = Math.Max(1, (int)Math.Ceiling(faceDocument.SourceRegion?.Height ?? 1)),
                GeneratedUtc = DateTime.UtcNow,
                Contributions = []
            }
            : new FaceMaskLayerModel
            {
                Id = faceDocument.MaskLayer.Id,
                Name = faceDocument.MaskLayer.Name,
                AssetPath = maskRelative,
                SourcePanel2DDocumentId = faceDocument.MaskLayer.SourcePanel2DDocumentId,
                SourceRegion = faceDocument.MaskLayer.SourceRegion,
                ExtractionThreshold = faceDocument.MaskLayer.ExtractionThreshold,
                GeneratedUtc = faceDocument.MaskLayer.GeneratedUtc,
                Width = faceDocument.MaskLayer.Width,
                Height = faceDocument.MaskLayer.Height,
                Contributions = faceDocument.MaskLayer.Contributions
            };

        return new FaceDocumentModel
        {
            Id = faceDocument.Id,
            Title = assetName,
            Summary = faceDocument.Summary,
            SourcePanel2DDocumentId = faceDocument.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath = faceDocument.SourcePanel2DDocumentPath,
            SourceFaceShapeId = faceDocument.SourceFaceShapeId,
            AssignedCabinetFaceTargetId = faceDocument.AssignedCabinetFaceTargetId,
            SourceRegion = faceDocument.SourceRegion,
            LastRegeneratedAtUtc = faceDocument.LastRegeneratedAtUtc,
            GenerationSettings = faceDocument.GenerationSettings,
            RuntimeRenderAssets = faceDocument.RuntimeRenderAssets,
            MaskLayer = maskLayer,
            Trays = faceDocument.Trays,
            LampEmitters = faceDocument.LampEmitters,
            Layers = faceDocument.Layers,
            Elements = elements
        };
    }

    private static void CopyOrCreatePng(EditorProject project, string? sourceAssetPath, string destinationPath, FaceDocumentModel faceDocument)
    {
        var sourcePath = ResolveExistingProjectPath(project, sourceAssetPath);
        if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
        {
            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            return;
        }

        var width = Math.Max(1, (int)Math.Ceiling(faceDocument.SourceRegion?.Width ?? 1));
        var height = Math.Max(1, (int)Math.Ceiling(faceDocument.SourceRegion?.Height ?? 1));
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(destinationPath);
        data.SaveTo(stream);
    }

    private static string? ResolveExistingProjectPath(EditorProject project, string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return null;
        return Path.IsPathRooted(assetPath)
            ? assetPath
            : Path.GetFullPath(Path.Combine(project.ProjectDirectory, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

}
