using System.Windows;

namespace OasisEditor;

public enum FaceSourceRegionKind
{
    Rect
}

public sealed class FaceSourceRegionModel
{
    public FaceSourceRegionKind Kind { get; init; } = FaceSourceRegionKind.Rect;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public static FaceSourceRegionModel FromRect(Rect rect)
    {
        var normalized = Normalize(rect);
        return new FaceSourceRegionModel
        {
            Kind = FaceSourceRegionKind.Rect,
            X = normalized.X,
            Y = normalized.Y,
            Width = normalized.Width,
            Height = normalized.Height
        };
    }

    public Rect ToRect() => new(X, Y, Width, Height);

    public bool IsValid => Kind == FaceSourceRegionKind.Rect
        && PanelElementValidation.IsFinite(X)
        && PanelElementValidation.IsFinite(Y)
        && PanelElementValidation.IsFinite(Width)
        && PanelElementValidation.IsFinite(Height)
        && Width > 0
        && Height > 0;

    private static Rect Normalize(Rect rect)
    {
        var left = Math.Min(rect.Left, rect.Right);
        var top = Math.Min(rect.Top, rect.Bottom);
        var right = Math.Max(rect.Left, rect.Right);
        var bottom = Math.Max(rect.Top, rect.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }
}

internal sealed class FaceGenerationResult
{
    public FaceGenerationResult(FaceDocumentModel document, int convertedLampCount)
    {
        Document = document;
        ConvertedLampCount = convertedLampCount;
    }

    public FaceDocumentModel Document { get; }
    public int ConvertedLampCount { get; }
}

internal sealed class FaceGenerationService
{
    private readonly IMachineObjectReferenceResolver _machineObjectReferenceResolver;

    public FaceGenerationService(IMachineObjectReferenceResolver? machineObjectReferenceResolver = null)
    {
        _machineObjectReferenceResolver = machineObjectReferenceResolver ?? MachineObjectReferenceResolver.Instance;
    }

    public FaceGenerationResult GenerateFromPanelRegion(
        Panel2DDocumentModel sourcePanel,
        FaceSourceRegionModel sourceRegion,
        string title,
        string? sourcePanel2DDocumentId = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePanel);
        ArgumentNullException.ThrowIfNull(sourceRegion);

        if (!sourceRegion.IsValid)
        {
            throw new ArgumentException("Face source region must be a non-empty finite rectangle.", nameof(sourceRegion));
        }

        var region = sourceRegion.ToRect();
        var lampWindows = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Lamp && IsContainedBy(element, region))
            .Select(element => CreateLampWindow(element, region))
            .ToArray();

        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "Generated Face" : title.Trim();
        var document = new FaceDocumentModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = resolvedTitle,
            Summary = $"Generated from Panel2D source region ({Format(region.X)}, {Format(region.Y)}, {Format(region.Width)}, {Format(region.Height)}).",
            SourcePanel2DDocumentId = string.IsNullOrWhiteSpace(sourcePanel2DDocumentId) ? null : sourcePanel2DDocumentId.Trim(),
            SourceRegion = sourceRegion,
            Layers =
            [
                new FaceLayerModel
                {
                    Id = "layer-lamp-windows",
                    Name = "Lamp Windows",
                    IsVisible = true
                }
            ],
            Elements = lampWindows
        };

        return new FaceGenerationResult(document, lampWindows.Length);
    }

    private FaceLampWindowElement CreateLampWindow(PanelElementModel sourceElement, Rect region)
    {
        _machineObjectReferenceResolver.TryGetReference(sourceElement, out var machineReference);

        return new FaceLampWindowElement
        {
            ObjectId = CreateGeneratedElementId(sourceElement),
            Name = sourceElement.Name ?? string.Empty,
            X = Math.Round(sourceElement.X - region.X, 2),
            Y = Math.Round(sourceElement.Y - region.Y, 2),
            Width = Math.Round(sourceElement.Width, 2),
            Height = Math.Round(sourceElement.Height, 2),
            IsVisible = sourceElement.IsVisible,
            IsLocked = sourceElement.IsLocked,
            LinkedMachineObjectReference = machineReference.IsEmpty ? null : machineReference,
            LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId
        };
    }

    private static string CreateGeneratedElementId(PanelElementModel sourceElement)
    {
        return string.IsNullOrWhiteSpace(sourceElement.ObjectId)
            ? Guid.NewGuid().ToString("N")
            : $"face-{sourceElement.ObjectId.Trim()}";
    }

    private static bool IsContainedBy(PanelElementModel element, Rect region)
    {
        var left = element.X;
        var top = element.Y;
        var right = element.X + element.Width;
        var bottom = element.Y + element.Height;
        return left >= region.Left
            && top >= region.Top
            && right <= region.Right
            && bottom <= region.Bottom;
    }

    private static string Format(double value) => Math.Round(value, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
