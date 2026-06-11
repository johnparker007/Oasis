using System.IO;
using System.Linq;
using SkiaSharp;

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
        ValidateMaskLayer(faceDocument, project, diagnostics);
        ValidateMachineReferences(faceDocument, diagnostics);
        diagnostics.AddRange(new FaceTrayAutoAuthoringService().Validate(faceDocument));
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

    private static void ValidateMaskLayer(
        FaceDocumentModel faceDocument,
        EditorProject? project,
        List<FaceValidationDiagnostic> diagnostics)
    {
        var maskLayer = faceDocument.MaskLayer;
        if (maskLayer is null)
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.MaskLayer.Missing",
                "Face does not contain FaceMaskLayer metadata, so future mask-aware renderers cannot consume an aligned mask asset."));
            return;
        }

        if (string.IsNullOrWhiteSpace(maskLayer.AssetPath))
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.MaskLayer.AssetPath.Missing",
                "FaceMaskLayer does not contain an asset path."));
        }
        else
        {
            var resolvedPath = ResolveProjectPath(project, maskLayer.AssetPath.Trim());
            if (resolvedPath is null || !File.Exists(resolvedPath))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.MaskLayer.Asset.Missing",
                    $"FaceMaskLayer references missing asset '{maskLayer.AssetPath}'."));
            }
            else
            {
                ValidateMaskAssetDimensions(maskLayer, resolvedPath, diagnostics);
            }
        }

        if (maskLayer.Width <= 0 || maskLayer.Height <= 0)
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.MaskLayer.Dimensions.Invalid",
                $"FaceMaskLayer has invalid dimensions {maskLayer.Width}x{maskLayer.Height}."));
        }

        var expectedRegion = maskLayer.SourceRegion ?? faceDocument.SourceRegion;
        if (expectedRegion is { IsValid: true })
        {
            var expectedWidth = Math.Max(1, (int)Math.Ceiling(expectedRegion.Width));
            var expectedHeight = Math.Max(1, (int)Math.Ceiling(expectedRegion.Height));
            if (maskLayer.Width > 0
                && maskLayer.Height > 0
                && (maskLayer.Width != expectedWidth || maskLayer.Height != expectedHeight))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.MaskLayer.Dimensions.SourceRegionMismatch",
                    $"FaceMaskLayer dimensions {maskLayer.Width}x{maskLayer.Height} do not match source region dimensions {expectedWidth}x{expectedHeight}."));
            }
        }

        if (maskLayer.Contributions.Count == 0)
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.MaskLayer.Contributions.Missing",
                "FaceMaskLayer does not contain contribution metadata."));
            return;
        }

        var incompleteContributionCount = maskLayer.Contributions.Count(contribution =>
            contribution.PixelCount <= 0
            || contribution.Bounds is not { IsValid: true }
            || (string.IsNullOrWhiteSpace(contribution.SourcePanel2DElementId)
                && (contribution.LinkedMachineObjectReference is not MachineObjectReference reference || reference.IsEmpty)));
        if (incompleteContributionCount > 0)
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.MaskLayer.Contributions.Incomplete",
                $"FaceMaskLayer contains {incompleteContributionCount} incomplete contribution metadata entr{(incompleteContributionCount == 1 ? "y" : "ies")}."));
        }
    }

    private static void ValidateMaskAssetDimensions(
        FaceMaskLayerModel maskLayer,
        string resolvedPath,
        List<FaceValidationDiagnostic> diagnostics)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(resolvedPath);
            if (bitmap is null)
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.MaskLayer.Asset.Unreadable",
                    $"FaceMaskLayer asset '{maskLayer.AssetPath}' could not be read as an image."));
                return;
            }

            if (maskLayer.Width > 0
                && maskLayer.Height > 0
                && (bitmap.Width != maskLayer.Width || bitmap.Height != maskLayer.Height))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.MaskLayer.Dimensions.AssetMismatch",
                    $"FaceMaskLayer metadata dimensions {maskLayer.Width}x{maskLayer.Height} do not match asset dimensions {bitmap.Width}x{bitmap.Height}."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                "Face.MaskLayer.Asset.Unreadable",
                $"FaceMaskLayer asset '{maskLayer.AssetPath}' could not be read: {ex.Message}"));
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
