using System.IO;
using System.Linq;

namespace OasisEditor;

public enum FaceValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record FaceValidationDiagnostic(FaceValidationSeverity Severity, string Code, string Message);

public sealed class FaceValidationService
{
    public IReadOnlyList<FaceValidationDiagnostic> Validate(
        FaceDocumentModel faceDocument,
        EditorProject? project,
        IReadOnlyList<DocumentTabViewModel> openDocuments)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(openDocuments);

        var diagnostics = new List<FaceValidationDiagnostic>();
        ValidateSourcePanel(faceDocument, openDocuments, diagnostics);
        ValidateArtworkAssets(faceDocument, project, diagnostics);
        ValidateMachineReferences(faceDocument, diagnostics);
        return diagnostics;
    }

    private static void ValidateSourcePanel(
        FaceDocumentModel faceDocument,
        IReadOnlyList<DocumentTabViewModel> openDocuments,
        List<FaceValidationDiagnostic> diagnostics)
    {
        var sourceId = faceDocument.SourcePanel2DDocumentId?.Trim();
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.SourcePanel2D.Missing",
                "Face does not contain Source Panel2D metadata, so regeneration cannot locate its source document."));
            return;
        }

        if (faceDocument.SourceRegion is not { IsValid: true })
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.SourceRegion.Missing",
                "Face does not contain a valid source region, so regeneration cannot be replayed."));
        }

        var sourceIsOpen = openDocuments.Any(document =>
            document.Document.DocumentType == EditorDocumentType.Panel2D
            && (string.Equals(document.DocumentId.ToString("N"), sourceId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(document.DocumentId.ToString("D"), sourceId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(document.FilePath, sourceId, StringComparison.OrdinalIgnoreCase)));
        if (!sourceIsOpen)
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.SourcePanel2D.NotOpen",
                $"Source Panel2D document '{sourceId}' is not currently open."));
        }
    }

    private static void ValidateArtworkAssets(
        FaceDocumentModel faceDocument,
        EditorProject? project,
        List<FaceValidationDiagnostic> diagnostics)
    {
        foreach (var artwork in faceDocument.Elements.OfType<FaceArtworkElement>())
        {
            if (string.IsNullOrWhiteSpace(artwork.AssetPath))
            {
                continue;
            }

            var resolvedPath = ResolveProjectPath(project, artwork.AssetPath.Trim());
            if (resolvedPath is null || !File.Exists(resolvedPath))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.ArtworkAsset.Missing",
                    $"Artwork element '{DisplayName(artwork)}' references missing asset '{artwork.AssetPath}'."));
            }
        }

        foreach (var reel in faceDocument.Elements.OfType<FaceReelDisplayElement>())
        {
            if (string.IsNullOrWhiteSpace(reel.AssetPath))
            {
                continue;
            }

            var resolvedPath = ResolveProjectPath(project, reel.AssetPath.Trim());
            if (resolvedPath is null || !File.Exists(resolvedPath))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.ReelAsset.Missing",
                    $"Reel display '{DisplayName(reel)}' references missing asset '{reel.AssetPath}'."));
            }
        }
    }

    private static void ValidateMachineReferences(FaceDocumentModel faceDocument, List<FaceValidationDiagnostic> diagnostics)
    {
        foreach (var element in faceDocument.Elements)
        {
            var expectedKind = element switch
            {
                FaceLampWindowElement => MachineObjectKind.Lamp,
                FaceReelDisplayElement => MachineObjectKind.Reel,
                FaceSevenSegmentDisplayElement => MachineObjectKind.SevenSegmentDisplay,
                FaceAlphaDisplayElement => MachineObjectKind.AlphaDisplay,
                FaceButtonElement => MachineObjectKind.Input,
                _ => (MachineObjectKind?)null
            };

            if (expectedKind is null)
            {
                continue;
            }

            if (element.LinkedMachineObjectReference is not MachineObjectReference reference || reference.IsEmpty)
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.MachineReference.Missing",
                    $"{NicifyElementKind(element)} '{DisplayName(element)}' does not have a machine reference."));
                continue;
            }

            if (reference.Kind != expectedKind.Value)
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.MachineReference.KindMismatch",
                    $"{NicifyElementKind(element)} '{DisplayName(element)}' references '{reference}', but expected a {expectedKind.Value} reference."));
            }
        }
    }

    private static string? ResolveProjectPath(EditorProject? project, string assetPath)
    {
        if (Path.IsPathRooted(assetPath))
        {
            return assetPath;
        }

        if (project is null)
        {
            return null;
        }

        var projectRelative = Path.GetFullPath(Path.Combine(project.ProjectDirectory, assetPath));
        if (File.Exists(projectRelative))
        {
            return projectRelative;
        }

        return Path.GetFullPath(Path.Combine(project.AssetsDirectory, assetPath));
    }

    private static string DisplayName(FaceElementModel element)
    {
        return string.IsNullOrWhiteSpace(element.Name) ? element.ObjectId : element.Name.Trim();
    }

    private static string NicifyElementKind(FaceElementModel element)
    {
        return element switch
        {
            FaceReelDisplayElement => "Reel display",
            FaceSevenSegmentDisplayElement => "Seven-segment display",
            FaceAlphaDisplayElement => "Alpha display",
            FaceButtonElement => "Button",
            FaceLampWindowElement => "Lamp window",
            _ => "Face element"
        };
    }
}
