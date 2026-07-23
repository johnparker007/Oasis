using System.Windows;

namespace OasisEditor;

internal sealed class FaceSemanticElementConversionService
{
    private const double DriftTolerance = 1e-9;
    private readonly IMachineObjectReferenceResolver _machineObjectReferenceResolver;

    public FaceSemanticElementConversionService(IMachineObjectReferenceResolver? machineObjectReferenceResolver = null)
    {
        _machineObjectReferenceResolver = machineObjectReferenceResolver ?? MachineObjectReferenceResolver.Instance;
    }

    public IReadOnlyList<FaceElementModel> ConvertSupportedElements(
        Panel2DDocumentModel sourcePanel,
        PanelFaceSourceShapeModel sourceShape,
        int faceWidth,
        int faceHeight,
        string? projectDirectory,
        IReadOnlyList<InputDefinitionModel>? inputDefinitions = null,
        string? defaultReelSpecificationId = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePanel);
        ArgumentNullException.ThrowIfNull(sourceShape);

        var buttonsByVisualId = CreateButtonsByVisualId(inputDefinitions);
        var converted = new List<FaceElementModel>();
        foreach (var sourceElement in sourcePanel.Elements)
        {
            var element = ConvertSupportedElement(sourceElement, sourceShape, faceWidth, faceHeight, projectDirectory, buttonsByVisualId);
            if (element is FaceReelDisplayElement reel && !string.IsNullOrWhiteSpace(defaultReelSpecificationId))
            {
                element = CopyReelWithSpecification(reel, defaultReelSpecificationId.Trim());
            }
            if (element is not null)
            {
                converted.Add(element);
            }
        }

        return converted;
    }

    public FaceSourceRegionModel? TryTransformBounds(PanelElementModel sourceElement, PanelFaceSourceShapeModel sourceShape, int faceWidth, int faceHeight)
    {
        ArgumentNullException.ThrowIfNull(sourceElement);
        ArgumentNullException.ThrowIfNull(sourceShape);

        var sourceCorners = new[]
        {
            (X: sourceElement.X, Y: sourceElement.Y),
            (X: sourceElement.X + sourceElement.Width, Y: sourceElement.Y),
            (X: sourceElement.X + sourceElement.Width, Y: sourceElement.Y + sourceElement.Height),
            (X: sourceElement.X, Y: sourceElement.Y + sourceElement.Height)
        };
        var transformed = new List<FacePointModel>(4);
        foreach (var corner in sourceCorners)
        {
            if (!FaceSourceShapeTransformService.TryTransformPanelPointToFace(sourceShape, faceWidth, faceHeight, corner.X, corner.Y, out var point))
            {
                return null;
            }

            transformed.Add(point);
        }

        var minX = ClampMinorDrift(transformed.Min(point => point.X), 0d, faceWidth);
        var minY = ClampMinorDrift(transformed.Min(point => point.Y), 0d, faceHeight);
        var maxX = ClampMinorDrift(transformed.Max(point => point.X), 0d, faceWidth);
        var maxY = ClampMinorDrift(transformed.Max(point => point.Y), 0d, faceHeight);
        if (!PanelElementValidation.IsFinite(minX) || !PanelElementValidation.IsFinite(minY) || !PanelElementValidation.IsFinite(maxX) || !PanelElementValidation.IsFinite(maxY) || maxX <= minX || maxY <= minY)
        {
            return null;
        }

        return FaceSourceRegionModel.FromRect(new Rect(minX, minY, maxX - minX, maxY - minY));
    }

    public static bool IsCenterInsideSourceShape(PanelElementModel element, PanelFaceSourceShapeModel sourceShape)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(sourceShape);
        if (element.Width <= 0 || element.Height <= 0)
        {
            return false;
        }

        return FaceSourceShapeTransformService.ContainsPanelPoint(sourceShape, element.X + (element.Width / 2d), element.Y + (element.Height / 2d));
    }

    private FaceElementModel? ConvertSupportedElement(
        PanelElementModel sourceElement,
        PanelFaceSourceShapeModel sourceShape,
        int faceWidth,
        int faceHeight,
        string? projectDirectory,
        IReadOnlyDictionary<string, InputDefinitionModel> buttonsByVisualId)
    {
        if (!IsSupportedKind(sourceElement.Kind) || !IsCenterInsideSourceShape(sourceElement, sourceShape))
        {
            return null;
        }

        var bounds = TryTransformBounds(sourceElement, sourceShape, faceWidth, faceHeight);
        if (bounds is not { IsValid: true })
        {
            throw new InvalidOperationException($"Could not convert Panel2D element '{sourceElement.ObjectId}' ('{sourceElement.Name}', {sourceElement.Kind}) to Face semantic element: transformed bounds are invalid.");
        }

        _machineObjectReferenceResolver.TryGetReference(sourceElement, out var machineReference);
        machineReference = machineReference.IsEmpty ? default : machineReference;
        return sourceElement.Kind switch
        {
            PanelElementKind.Lamp => CreateLamp(sourceElement, sourceShape, faceWidth, faceHeight, bounds, projectDirectory, machineReference),
            PanelElementKind.Reel => CreateReel(sourceElement, bounds, machineReference),
            PanelElementKind.SevenSegment => CreateSevenSegment(sourceElement, bounds, machineReference),
            PanelElementKind.Alpha => CreateAlpha(sourceElement, bounds, machineReference),
            PanelElementKind.Image or PanelElementKind.Rectangle or PanelElementKind.Label => CreateButtonIfLinked(sourceElement, bounds, buttonsByVisualId),
            _ => null
        };
    }

    private static bool IsSupportedKind(PanelElementKind kind) => kind is PanelElementKind.Lamp or PanelElementKind.Reel or PanelElementKind.SevenSegment or PanelElementKind.Alpha or PanelElementKind.Image or PanelElementKind.Rectangle or PanelElementKind.Label;

    private FaceLampWindowElement CreateLamp(PanelElementModel sourceElement, PanelFaceSourceShapeModel sourceShape, int faceWidth, int faceHeight, FaceSourceRegionModel bounds, string? projectDirectory, MachineObjectReference machineReference)
    {
        var bulbMaskAssetPath = FaceSourceShapeTransformService.TryGenerateTransformedElementAsset(sourceElement, sourceElement.SecondaryAssetPath, sourceShape, faceWidth, faceHeight, bounds, projectDirectory, "face-source-shape-lamp-mask");
        return new FaceLampWindowElement { ObjectId = CreateGeneratedElementId(sourceElement), Name = sourceElement.Name ?? string.Empty, X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height, IsVisible = sourceElement.IsVisible, IsTransformLocked = sourceElement.IsTransformLocked, LinkedMachineObjectReference = machineReference.IsEmpty ? null : machineReference, LinkedPanel2DElementId = NormalizeOptional(sourceElement.ObjectId), BulbMaskAssetPath = bulbMaskAssetPath, SourceComponentIndex = sourceElement.SourceComponentIndex, SharedSourceSetId = NormalizeOptional(sourceElement.SharedSourceSetId), SharedSourceSetCount = sourceElement.SharedSourceSetCount, SourceBlend = sourceElement.SourceBlend };
    }

    private static FaceReelDisplayElement CopyReelWithSpecification(FaceReelDisplayElement reel, string reelSpecificationId) => new() { ObjectId = reel.ObjectId, Name = reel.Name, X = reel.X, Y = reel.Y, Width = reel.Width, Height = reel.Height, IsVisible = reel.IsVisible, IsTransformLocked = reel.IsTransformLocked, LinkedMachineObjectReference = reel.LinkedMachineObjectReference, LinkedPanel2DElementId = reel.LinkedPanel2DElementId, ReelSpecificationId = reelSpecificationId, AssetPath = reel.AssetPath, Stops = reel.Stops, VisibleScale = reel.VisibleScale, BandOffset = reel.BandOffset, IsReversed = reel.IsReversed };

    private static FaceReelDisplayElement CreateReel(PanelElementModel e, FaceSourceRegionModel b, MachineObjectReference r) => new() { ObjectId = CreateGeneratedElementId(e), Name = e.Name ?? string.Empty, X = b.X, Y = b.Y, Width = b.Width, Height = b.Height, IsVisible = e.IsVisible, IsTransformLocked = e.IsTransformLocked, LinkedMachineObjectReference = r.IsEmpty ? null : r, LinkedPanel2DElementId = NormalizeOptional(e.ObjectId), AssetPath = NormalizeOptional(e.AssetPath), Stops = e.Stops, VisibleScale = e.VisibleScale, BandOffset = e.BandOffset, IsReversed = e.IsReversed == true };
    private static FaceSevenSegmentDisplayElement CreateSevenSegment(PanelElementModel e, FaceSourceRegionModel b, MachineObjectReference r) => new() { ObjectId = CreateGeneratedElementId(e), Name = e.Name ?? string.Empty, X = b.X, Y = b.Y, Width = b.Width, Height = b.Height, IsVisible = e.IsVisible, IsTransformLocked = e.IsTransformLocked, LinkedMachineObjectReference = r.IsEmpty ? null : r, LinkedPanel2DElementId = NormalizeOptional(e.ObjectId), OnColorHex = NormalizeOptional(e.OnColorHex), OffColorHex = NormalizeOptional(e.OffColorHex), DigitCount = Math.Max(1, e.DigitCount ?? FaceSevenSegmentDisplayElement.DefaultDigitCount), ShowDecimalPoint = e.ShowDecimalPoint };
    private static FaceAlphaDisplayElement CreateAlpha(PanelElementModel e, FaceSourceRegionModel b, MachineObjectReference r) => new() { ObjectId = CreateGeneratedElementId(e), Name = e.Name ?? string.Empty, X = b.X, Y = b.Y, Width = b.Width, Height = b.Height, IsVisible = e.IsVisible, IsTransformLocked = e.IsTransformLocked, LinkedMachineObjectReference = r.IsEmpty ? null : r, LinkedPanel2DElementId = NormalizeOptional(e.ObjectId), SegmentDisplayType = NormalizeOptional(e.SegmentDisplayType), OnColorHex = NormalizeOptional(e.OnColorHex), OffColorHex = NormalizeOptional(e.OffColorHex), DigitCount = Math.Max(1, e.DigitCount ?? 16), ShowDecimalPoint = e.ShowDecimalPoint, ShowCommaTail = e.ShowCommaTail, IsReversed = e.IsReversed == true };

    private static FaceButtonElement? CreateButtonIfLinked(PanelElementModel e, FaceSourceRegionModel b, IReadOnlyDictionary<string, InputDefinitionModel> buttonsByVisualId)
    {
        if (string.IsNullOrWhiteSpace(e.ObjectId) || !buttonsByVisualId.TryGetValue(e.ObjectId.Trim(), out var input)) return null;
        var inputReference = MachineInputReference.FromInputId(input.Id);
        return new FaceButtonElement { ObjectId = CreateGeneratedElementId(e), Name = e.Name ?? input.Name ?? string.Empty, X = b.X, Y = b.Y, Width = b.Width, Height = b.Height, IsVisible = e.IsVisible, IsTransformLocked = e.IsTransformLocked, LinkedMachineObjectReference = inputReference.Reference, LinkedPanel2DElementId = e.ObjectId.Trim(), LinkedInputReference = inputReference };
    }

    private static IReadOnlyDictionary<string, InputDefinitionModel> CreateButtonsByVisualId(IReadOnlyList<InputDefinitionModel>? inputDefinitions)
    {
        if (inputDefinitions is null || inputDefinitions.Count == 0) return new Dictionary<string, InputDefinitionModel>(StringComparer.Ordinal);
        return inputDefinitions.Where(input => input.LinkedVisualElementId.HasValue && (input.Kind is InputDefinitionKind.Button or InputDefinitionKind.Coin) && !string.IsNullOrWhiteSpace(input.Id)).GroupBy(input => input.LinkedVisualElementId!.Value.ToString("N"), StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static string CreateGeneratedElementId(PanelElementModel sourceElement) => string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? Guid.NewGuid().ToString("N") : $"face-{sourceElement.ObjectId.Trim()}";
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static double ClampMinorDrift(double value, double min, double max) => value < min && min - value <= DriftTolerance ? min : value > max && value - max <= DriftTolerance ? max : value;
}
