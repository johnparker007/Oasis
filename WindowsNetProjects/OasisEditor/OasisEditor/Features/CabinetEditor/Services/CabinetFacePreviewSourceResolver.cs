using System.IO;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed record CabinetFacePreviewSource(
    string AssetPath,
    FaceDocumentModel FaceDocument,
    MachineRuntimeState RuntimeState,
    string CacheIdentity,
    string ContentIdentity,
    DocumentTabViewModel? OpenDocument)
{
    public bool IsLive => OpenDocument is not null;
}

public sealed class CabinetFacePreviewSourceResolver
{
    private readonly ProjectAssetPathService _pathService = new();

    public bool TryResolve(
        EditorProject project,
        string faceAssetPath,
        IReadOnlyList<DocumentTabViewModel> openDocuments,
        out CabinetFacePreviewSource source,
        out string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(openDocuments);
        source = default!;
        diagnostic = null;
        if (string.IsNullOrWhiteSpace(faceAssetPath))
        {
            diagnostic = "The Machine Face assignment has no asset path.";
            return false;
        }

        var normalizedAssetPath = ProjectAssetPathService.NormalizeProjectRelativePath(faceAssetPath);
        var manifestPath = _pathService.ResolveProjectRelativePath(project, normalizedAssetPath);
        var openDocument = openDocuments.FirstOrDefault(candidate =>
            candidate.Document.DocumentType == EditorDocumentType.Face
            && !candidate.Document.IsUntitled
            && string.Equals(Path.GetFullPath(candidate.FilePath), Path.GetFullPath(manifestPath), StringComparison.OrdinalIgnoreCase));
        if (openDocument is not null)
        {
            source = new CabinetFacePreviewSource(
                normalizedAssetPath,
                openDocument.GetFaceDocument(),
                openDocument.RuntimeState,
                $"open:{openDocument.DocumentId:D}",
                openDocument.GetFaceDocumentJson(),
                openDocument);
            return true;
        }

        if (!File.Exists(manifestPath))
        {
            diagnostic = $"Assigned Face asset was not found: {normalizedAssetPath}";
            return false;
        }

        string json;
        try { json = File.ReadAllText(manifestPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostic = $"Assigned Face asset could not be read: {normalizedAssetPath} ({exception.Message})";
            return false;
        }
        if (!FaceDocumentStorage.TryReadValidated(json, out var file, out var error))
        {
            diagnostic = $"Assigned Face asset is invalid: {normalizedAssetPath} ({error})";
            return false;
        }

        source = new CabinetFacePreviewSource(
            normalizedAssetPath,
            FaceDocumentStorage.ToModel(file),
            new MachineRuntimeState(),
            $"saved:{normalizedAssetPath.ToUpperInvariant()}",
            json,
            null);
        return true;
    }
}
