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
    public FaceGenerationResult(FaceDocumentModel document, int convertedLampCount, int artworkElementCount, int convertedButtonCount)
    {
        Document = document;
        ConvertedLampCount = convertedLampCount;
        ArtworkElementCount = artworkElementCount;
        ConvertedButtonCount = convertedButtonCount;
    }

    public FaceDocumentModel Document { get; }
    public int ConvertedLampCount { get; }
    public int ArtworkElementCount { get; }
    public int ConvertedButtonCount { get; }
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
        string? sourcePanel2DDocumentId = null,
        IReadOnlyList<InputDefinitionModel>? inputDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePanel);
        ArgumentNullException.ThrowIfNull(sourceRegion);

        if (!sourceRegion.IsValid)
        {
            throw new ArgumentException("Face source region must be a non-empty finite rectangle.", nameof(sourceRegion));
        }

        var region = sourceRegion.ToRect();
        var artworkElements = CreateArtworkElements(sourcePanel, region, sourcePanel2DDocumentId);
        var lampWindows = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Lamp && IsContainedBy(element, region))
            .Select(element => CreateLampWindow(element, region))
            .ToArray();
        var buttons = CreateButtonElements(sourcePanel, region, inputDefinitions ?? []);

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
                    Id = "layer-artwork",
                    Name = "Artwork",
                    IsVisible = true
                },
                new FaceLayerModel
                {
                    Id = "layer-lamp-windows",
                    Name = "Lamp Windows",
                    IsVisible = true
                },
                new FaceLayerModel
                {
                    Id = "layer-buttons",
                    Name = "Buttons",
                    IsVisible = true
                }
            ],
            Elements = artworkElements.Cast<FaceElementModel>().Concat(lampWindows).Concat(buttons).ToArray()
        };

        return new FaceGenerationResult(document, lampWindows.Length, artworkElements.Length, buttons.Length);
    }

    private static FaceArtworkElement[] CreateArtworkElements(
        Panel2DDocumentModel sourcePanel,
        Rect region,
        string? sourcePanel2DDocumentId)
    {
        var backgroundArtwork = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Background && Intersects(element, region))
            .Select(element => CreateArtworkElement(element, region, sourcePanel2DDocumentId))
            .ToArray();

        if (backgroundArtwork.Length > 0)
        {
            return backgroundArtwork;
        }

        return
        [
            new FaceArtworkElement
            {
                ObjectId = $"face-artwork-{Guid.NewGuid():N}",
                Name = "Artwork Region",
                X = 0,
                Y = 0,
                Width = Math.Round(region.Width, 2),
                Height = Math.Round(region.Height, 2),
                IsVisible = true,
                SourcePanel2DDocumentId = NormalizeOptional(sourcePanel2DDocumentId),
                SourceRegion = FaceSourceRegionModel.FromRect(region),
                Provenance = new FaceArtworkProvenanceModel
                {
                    Generator = "Generate Face From Region",
                    GeneratedAtUtc = DateTime.UtcNow
                }
            }
        ];
    }

    private static FaceArtworkElement CreateArtworkElement(PanelElementModel sourceElement, Rect region, string? sourcePanel2DDocumentId)
    {
        var sourceBounds = new Rect(sourceElement.X, sourceElement.Y, sourceElement.Width, sourceElement.Height);
        var intersection = Rect.Intersect(sourceBounds, region);
        return new FaceArtworkElement
        {
            ObjectId = CreateGeneratedArtworkElementId(sourceElement),
            Name = string.IsNullOrWhiteSpace(sourceElement.Name) ? "Artwork" : sourceElement.Name.Trim(),
            X = Math.Round(intersection.X - region.X, 2),
            Y = Math.Round(intersection.Y - region.Y, 2),
            Width = Math.Round(intersection.Width, 2),
            Height = Math.Round(intersection.Height, 2),
            IsVisible = sourceElement.IsVisible,
            IsLocked = sourceElement.IsLocked,
            LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId,
            AssetPath = sourceElement.AssetPath,
            SourcePanel2DDocumentId = NormalizeOptional(sourcePanel2DDocumentId),
            SourceRegion = FaceSourceRegionModel.FromRect(intersection),
            Provenance = new FaceArtworkProvenanceModel
            {
                Generator = "Generate Face From Region",
                GeneratedAtUtc = DateTime.UtcNow,
                SourcePanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId,
                SourcePanel2DElementKind = Panel2DDocumentStorage.SerializeElementKind(sourceElement.Kind),
                SourceAssetPath = sourceElement.AssetPath,
                SourceElementBounds = FaceSourceRegionModel.FromRect(sourceBounds)
            }
        };
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

    private FaceButtonElement[] CreateButtonElements(Panel2DDocumentModel sourcePanel, Rect region, IReadOnlyList<InputDefinitionModel> inputDefinitions)
    {
        if (inputDefinitions.Count == 0)
        {
            return [];
        }

        var elementsByGuid = sourcePanel.Elements
            .Where(element => Guid.TryParse(element.ObjectId, out _))
            .GroupBy(element => Guid.Parse(element.ObjectId), element => element)
            .ToDictionary(group => group.Key, group => group.First());

        var buttons = new List<FaceButtonElement>();
        foreach (var input in inputDefinitions)
        {
            if (input is null)
            {
                continue;
            }

            if (input.LinkedVisualElementId is not Guid visualElementId
                || !elementsByGuid.TryGetValue(visualElementId, out var sourceElement)
                || !IsContainedBy(sourceElement, region))
            {
                continue;
            }

            _machineObjectReferenceResolver.TryGetReference(input, out var inputReference);
            if (inputReference.IsEmpty || inputReference.Kind != MachineObjectKind.Input)
            {
                continue;
            }

            buttons.Add(new FaceButtonElement
            {
                ObjectId = CreateGeneratedButtonElementId(sourceElement, input),
                Name = string.IsNullOrWhiteSpace(input.Name) ? sourceElement.Name ?? string.Empty : input.Name.Trim(),
                X = Math.Round(sourceElement.X - region.X, 2),
                Y = Math.Round(sourceElement.Y - region.Y, 2),
                Width = Math.Round(sourceElement.Width, 2),
                Height = Math.Round(sourceElement.Height, 2),
                IsVisible = sourceElement.IsVisible,
                IsLocked = sourceElement.IsLocked,
                LinkedMachineObjectReference = inputReference,
                LinkedInputReference = new MachineInputReference(inputReference),
                LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId
            });
        }

        return buttons.ToArray();
    }

    private static string CreateGeneratedElementId(PanelElementModel sourceElement)
    {
        return string.IsNullOrWhiteSpace(sourceElement.ObjectId)
            ? Guid.NewGuid().ToString("N")
            : $"face-{sourceElement.ObjectId.Trim()}";
    }

    private static string CreateGeneratedButtonElementId(PanelElementModel sourceElement, InputDefinitionModel inputDefinition)
    {
        var sourcePart = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? Guid.NewGuid().ToString("N") : sourceElement.ObjectId.Trim();
        var inputPart = string.IsNullOrWhiteSpace(inputDefinition.Id) ? "input" : inputDefinition.Id.Trim();
        return $"face-button-{inputPart}-{sourcePart}";
    }

    private static string CreateGeneratedArtworkElementId(PanelElementModel sourceElement)
    {
        return string.IsNullOrWhiteSpace(sourceElement.ObjectId)
            ? $"face-artwork-{Guid.NewGuid():N}"
            : $"face-artwork-{sourceElement.ObjectId.Trim()}";
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

    private static bool Intersects(PanelElementModel element, Rect region)
    {
        if (element.Width <= 0 || element.Height <= 0)
        {
            return false;
        }

        return new Rect(element.X, element.Y, element.Width, element.Height).IntersectsWith(region);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Format(double value) => Math.Round(value, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
