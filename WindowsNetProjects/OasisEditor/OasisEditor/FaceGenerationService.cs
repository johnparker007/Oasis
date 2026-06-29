using System.Windows;
using OasisEditor.Progress;

using SkiaSharp;

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
    public FaceGenerationResult(FaceDocumentModel document, int convertedLampCount, int artworkElementCount, int convertedButtonCount, int convertedSevenSegmentDisplayCount, int convertedAlphaDisplayCount, int convertedReelDisplayCount)
    {
        Document = document;
        ConvertedLampCount = convertedLampCount;
        ArtworkElementCount = artworkElementCount;
        ConvertedButtonCount = convertedButtonCount;
        ConvertedSevenSegmentDisplayCount = convertedSevenSegmentDisplayCount;
        ConvertedAlphaDisplayCount = convertedAlphaDisplayCount;
        ConvertedReelDisplayCount = convertedReelDisplayCount;
    }

    public FaceDocumentModel Document { get; }
    public int ConvertedLampCount { get; }
    public int ArtworkElementCount { get; }
    public int ConvertedButtonCount { get; }
    public int ConvertedSevenSegmentDisplayCount { get; }
    public int ConvertedAlphaDisplayCount { get; }
    public int ConvertedReelDisplayCount { get; }
}

internal sealed class FaceGenerationService
{
    private readonly IMachineObjectReferenceResolver _machineObjectReferenceResolver;
    private readonly FaceMaskLayerExtractionService _maskLayerExtractionService;
    private readonly FaceTrayAutoAuthoringService _trayAutoAuthoringService = new();

    public FaceGenerationService(IMachineObjectReferenceResolver? machineObjectReferenceResolver = null)
    {
        _machineObjectReferenceResolver = machineObjectReferenceResolver ?? MachineObjectReferenceResolver.Instance;
        _maskLayerExtractionService = new FaceMaskLayerExtractionService(_machineObjectReferenceResolver);
    }



    public FaceGenerationResult GenerateFromPanelFaceSourceShape(
        Panel2DDocumentModel sourcePanel,
        PanelFaceSourceShapeModel sourceShape,
        string title,
        string? sourcePanel2DDocumentId = null,
        string? assignedCabinetFaceTargetId = null,
        double? targetAspectRatio = null,
        string? projectDirectory = null,
        string? generatedDirectory = null,
        FaceGenerationSettingsModel? generationSettings = null,
        IEditorProgressReporter? progress = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePanel);
        ArgumentNullException.ThrowIfNull(sourceShape);
        var output = FaceSourceShapeTransformService.EstimateOutputSize(sourceShape, targetAspectRatio);
        var region = FaceSourceRegionModel.FromRect(new Rect(0, 0, output.Width, output.Height));
        var assetPath = FaceSourceShapeTransformService.TryGenerateBackground(sourcePanel, sourceShape, output.Width, output.Height, projectDirectory, generatedDirectory);
        var settings = (generationSettings ?? FaceGenerationSettingsModel.Default).Normalize();
        var faceDocumentId = Guid.NewGuid().ToString("N");
        progress?.Report(0.2, "Converting source-shape lamps...");
        var lampWindows = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Lamp && IsCenterInsideSourceShape(element, sourceShape))
            .Select(element => CreateLampWindowFromSourceShape(element, sourceShape, output.Width, output.Height, projectDirectory))
            .OfType<FaceLampWindowElement>()
            .ToArray();
        var maskLayer = CreateMaskLayerFromSourceShape(
            sourcePanel,
            sourceShape,
            output.Width,
            output.Height,
            lampWindows,
            faceDocumentId,
            sourcePanel2DDocumentId,
            projectDirectory,
            settings.MaskExtractionThreshold);
        var artwork = new FaceArtworkElement
        {
            ObjectId = $"face-artwork-{Guid.NewGuid():N}",
            Name = "Perspective-corrected artwork",
            X = 0,
            Y = 0,
            Width = output.Width,
            Height = output.Height,
            IsVisible = true,
            AssetPath = assetPath,
            SourcePanel2DDocumentId = NormalizeOptional(sourcePanel2DDocumentId),
            SourceRegion = region,
            Provenance = new FaceArtworkProvenanceModel { Generator = "Generate Face From Face Source Shape", GeneratedAtUtc = DateTime.UtcNow }
        };
        var elements = new FaceElementModel[] { artwork }.Concat(lampWindows).ToArray();
        progress?.Report(0.9, "Auto-authoring trays/emitters...");
        var autoAuthored = _trayAutoAuthoringService.AutoAuthor(new FaceDocumentModel { GenerationSettings = settings, MaskLayer = maskLayer, Elements = elements }, projectDirectory);
        var document = new FaceDocumentModel
        {
            Id = faceDocumentId,
            Title = string.IsNullOrWhiteSpace(title) ? "Generated Face" : title.Trim(),
            Summary = $"Generated from Face Source Shape '{sourceShape.Name}' ({output.Width} x {output.Height}).",
            SourcePanel2DDocumentId = NormalizeOptional(sourcePanel2DDocumentId),
            SourceFaceShapeId = NormalizeOptional(sourceShape.Id),
            AssignedCabinetFaceTargetId = NormalizeOptional(assignedCabinetFaceTargetId),
            SourceRegion = region,
            LastRegeneratedAtUtc = DateTime.UtcNow,
            GenerationSettings = settings,
            MaskLayer = maskLayer,
            Trays = autoAuthored.Trays,
            LampEmitters = autoAuthored.Emitters,
            Layers =
            [
                new FaceLayerModel { Id = "layer-artwork", Name = "Artwork", IsVisible = true },
                new FaceLayerModel { Id = "layer-face-mask", Name = "Face Mask", IsVisible = true },
                new FaceLayerModel { Id = "layer-runtime-lamps", Name = "Runtime Lamps", IsVisible = true }
            ],
            Elements = elements
        };
        progress?.Report(1.0, "Face generation complete.");
        return new FaceGenerationResult(document, lampWindows.Length, 1, 0, 0, 0, 0);
    }

    private FaceMaskLayerModel? CreateMaskLayerFromSourceShape(
        Panel2DDocumentModel sourcePanel,
        PanelFaceSourceShapeModel sourceShape,
        int faceWidth,
        int faceHeight,
        IReadOnlyList<FaceLampWindowElement> lampWindows,
        string faceDocumentId,
        string? sourcePanel2DDocumentId,
        string? projectDirectory,
        byte extractionThreshold)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory) || faceWidth <= 0 || faceHeight <= 0)
        {
            return null;
        }

        var sourceLampsById = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Lamp && !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, StringComparer.Ordinal);
        var maskPixels = new byte[faceWidth * faceHeight];
        var contributions = new List<FaceMaskContributionModel>();

        foreach (var lampWindow in lampWindows)
        {
            if (string.IsNullOrWhiteSpace(lampWindow.LinkedPanel2DElementId)
                || !sourceLampsById.TryGetValue(lampWindow.LinkedPanel2DElementId, out var sourceLamp)
                || string.IsNullOrWhiteSpace(sourceLamp.AssetPath))
            {
                continue;
            }

            var sourcePath = System.IO.Path.IsPathRooted(sourceLamp.AssetPath!)
                ? sourceLamp.AssetPath!
                : System.IO.Path.Combine(projectDirectory, sourceLamp.AssetPath!);
            if (!System.IO.File.Exists(sourcePath))
            {
                continue;
            }

            using var bitmap = SKBitmap.Decode(sourcePath);
            if (bitmap is null)
            {
                continue;
            }

            var contribution = CompositeSourceShapeLampMask(maskPixels, faceWidth, faceHeight, sourceShape, sourceLamp, bitmap, lampWindow, extractionThreshold);
            if (contribution.PixelCount <= 0 || contribution.Bounds is null)
            {
                continue;
            }

            contributions.Add(new FaceMaskContributionModel
            {
                SourcePanel2DElementId = lampWindow.LinkedPanel2DElementId,
                LinkedMachineObjectReference = lampWindow.LinkedMachineObjectReference,
                Bounds = contribution.Bounds,
                PixelCount = contribution.PixelCount
            });
        }

        var assetPath = SaveSourceShapeMask(maskPixels, faceWidth, faceHeight, faceDocumentId, projectDirectory);
        return new FaceMaskLayerModel
        {
            Id = "face-mask-layer",
            Name = "Face Mask",
            AssetPath = assetPath,
            SourcePanel2DDocumentId = NormalizeOptional(sourcePanel2DDocumentId),
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(0, 0, faceWidth, faceHeight)),
            ExtractionThreshold = extractionThreshold,
            GeneratedUtc = DateTime.UtcNow,
            Width = faceWidth,
            Height = faceHeight,
            Contributions = contributions.ToArray()
        };
    }

    public FaceGenerationResult GenerateFromPanelRegion(
        Panel2DDocumentModel sourcePanel,
        FaceSourceRegionModel sourceRegion,
        string title,
        string? sourcePanel2DDocumentId = null,
        IReadOnlyList<InputDefinitionModel>? inputDefinitions = null,
        string? projectDirectory = null,
        string? generatedDirectory = null,
        FaceGenerationSettingsModel? generationSettings = null,
        IEditorProgressReporter? progress = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePanel);
        ArgumentNullException.ThrowIfNull(sourceRegion);
        progress ??= NoOpEditorProgressReporter.Instance;
        progress.Report(0.0, "Validating source region...");

        if (!sourceRegion.IsValid)
        {
            throw new ArgumentException("Face source region must be a non-empty finite rectangle.", nameof(sourceRegion));
        }

        var settings = (generationSettings ?? FaceGenerationSettingsModel.Default).Normalize();
        var region = sourceRegion.ToRect();
        progress.Report(0.1, "Creating artwork elements...");
        var artworkElements = CreateArtworkElements(sourcePanel, region, sourcePanel2DDocumentId);
        progress.Report(0.2, "Converting lamps...");
        var lampWindows = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Lamp && IsContainedBy(element, region))
            .Select(element => CreateLampWindow(element, region))
            .ToArray();
        progress.Report(0.3, "Converting reels...");
        var reelDisplays = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Reel && IsContainedBy(element, region))
            .Select(element => CreateReelDisplay(element, region))
            .ToArray();
        progress.Report(0.4, "Converting seven-segment displays...");
        var sevenSegmentDisplays = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.SevenSegment && IsContainedBy(element, region))
            .Select(element => CreateSevenSegmentDisplay(element, region))
            .ToArray();
        progress.Report(0.5, "Converting alpha displays...");
        var alphaDisplays = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Alpha && IsContainedBy(element, region))
            .Select(element => CreateAlphaDisplay(element, region))
            .ToArray();
        progress.Report(0.6, "Creating button/input elements...");
        var buttons = CreateButtonElements(sourcePanel, region, inputDefinitions ?? []);

        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "Generated Face" : title.Trim();
        var faceDocumentId = Guid.NewGuid().ToString("N");
        progress.Report(0.75, "Generating mask layer...");
        var maskLayer = _maskLayerExtractionService.GenerateMaskLayer(sourcePanel, region, faceDocumentId, sourcePanel2DDocumentId, projectDirectory, generatedDirectory, settings.MaskExtractionThreshold);
        var elements = artworkElements.Cast<FaceElementModel>().Concat(lampWindows).Concat(reelDisplays).Concat(sevenSegmentDisplays).Concat(alphaDisplays).Concat(buttons).ToArray();
        progress.Report(0.9, "Auto-authoring trays/emitters...");
        var autoAuthored = _trayAutoAuthoringService.AutoAuthor(new FaceDocumentModel { GenerationSettings = settings, MaskLayer = maskLayer, Elements = elements }, projectDirectory);
        var document = new FaceDocumentModel
        {
            Id = faceDocumentId,
            Title = resolvedTitle,
            Summary = $"Generated from Panel2D source region ({Format(region.X)}, {Format(region.Y)}, {Format(region.Width)}, {Format(region.Height)}).",
            SourcePanel2DDocumentId = string.IsNullOrWhiteSpace(sourcePanel2DDocumentId) ? null : sourcePanel2DDocumentId.Trim(),
            SourceRegion = sourceRegion,
            LastRegeneratedAtUtc = DateTime.UtcNow,
            GenerationSettings = settings,
            MaskLayer = maskLayer,
            Trays = autoAuthored.Trays,
            LampEmitters = autoAuthored.Emitters,
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
                    Id = "layer-face-mask",
                    Name = "Face Mask",
                    IsVisible = true
                },
                new FaceLayerModel
                {
                    Id = "layer-runtime-lamps",
                    Name = "Runtime Lamps",
                    IsVisible = true
                },
                new FaceLayerModel
                {
                    Id = "layer-displays",
                    Name = "Displays",
                    IsVisible = true
                },
                new FaceLayerModel
                {
                    Id = "layer-buttons",
                    Name = "Buttons",
                    IsVisible = true
                }
            ],
            Elements = elements
        };

        progress.Report(1.0, "Face generation complete.");
        return new FaceGenerationResult(document, lampWindows.Length, artworkElements.Length, buttons.Length, sevenSegmentDisplays.Length, alphaDisplays.Length, reelDisplays.Length);
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


    private static SourceShapeMaskContribution CompositeSourceShapeLampMask(
        byte[] maskPixels,
        int faceWidth,
        int faceHeight,
        PanelFaceSourceShapeModel sourceShape,
        PanelElementModel sourceLamp,
        SKBitmap lampBitmap,
        FaceLampWindowElement lampWindow,
        byte extractionThreshold)
    {
        var left = Math.Max(0, (int)Math.Floor(lampWindow.X));
        var top = Math.Max(0, (int)Math.Floor(lampWindow.Y));
        var right = Math.Min(faceWidth, (int)Math.Ceiling(lampWindow.X + lampWindow.Width));
        var bottom = Math.Min(faceHeight, (int)Math.Ceiling(lampWindow.Y + lampWindow.Height));
        var count = 0;
        var minX = faceWidth;
        var minY = faceHeight;
        var maxX = -1;
        var maxY = -1;

        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            if (!FaceSourceShapeTransformService.TryTransformFacePointToPanel(sourceShape, faceWidth, faceHeight, x + 0.5d, y + 0.5d, out var panelPoint)
                || panelPoint.X < sourceLamp.X
                || panelPoint.Y < sourceLamp.Y
                || panelPoint.X > sourceLamp.X + sourceLamp.Width
                || panelPoint.Y > sourceLamp.Y + sourceLamp.Height)
            {
                continue;
            }

            var sourceX = (panelPoint.X - sourceLamp.X) / Math.Max(1d, sourceLamp.Width) * lampBitmap.Width;
            var sourceY = (panelPoint.Y - sourceLamp.Y) / Math.Max(1d, sourceLamp.Height) * lampBitmap.Height;
            var color = lampBitmap.GetPixel(
                Math.Clamp((int)Math.Round(sourceX), 0, lampBitmap.Width - 1),
                Math.Clamp((int)Math.Round(sourceY), 0, lampBitmap.Height - 1));
            var mask = color.Alpha >= extractionThreshold ? color.Alpha : (byte)0;
            if (mask == 0)
            {
                continue;
            }

            var index = (y * faceWidth) + x;
            if (mask > maskPixels[index])
            {
                maskPixels[index] = mask;
            }

            count++;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        var bounds = count > 0
            ? FaceSourceRegionModel.FromRect(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1))
            : null;
        return new SourceShapeMaskContribution(bounds, count);
    }

    private static string SaveSourceShapeMask(byte[] maskPixels, int width, int height, string faceDocumentId, string projectDirectory)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var value = maskPixels[(y * width) + x];
            bitmap.SetPixel(x, y, new SKColor(value, value, value, value));
        }

        var relative = System.IO.Path.Combine("Generated", "Faces", $"{faceDocumentId}-face-source-shape-mask.png");
        var path = System.IO.Path.Combine(projectDirectory, relative);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = System.IO.File.Create(path);
        data.SaveTo(stream);
        return relative.Replace(System.IO.Path.DirectorySeparatorChar, '/');
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

    private FaceLampWindowElement? CreateLampWindowFromSourceShape(PanelElementModel sourceElement, PanelFaceSourceShapeModel sourceShape, int faceWidth, int faceHeight, string? projectDirectory)
    {
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

        var minX = transformed.Min(point => point.X);
        var minY = transformed.Min(point => point.Y);
        var maxX = transformed.Max(point => point.X);
        var maxY = transformed.Max(point => point.Y);
        if (!PanelElementValidation.IsFinite(minX) || !PanelElementValidation.IsFinite(minY) || !PanelElementValidation.IsFinite(maxX) || !PanelElementValidation.IsFinite(maxY) || maxX <= minX || maxY <= minY)
        {
            return null;
        }

        var faceBounds = FaceSourceRegionModel.FromRect(new Rect(minX, minY, maxX - minX, maxY - minY));
        var bulbMaskAssetPath = FaceSourceShapeTransformService.TryGenerateTransformedElementAsset(
            sourceElement,
            sourceElement.SecondaryAssetPath,
            sourceShape,
            faceWidth,
            faceHeight,
            faceBounds,
            projectDirectory,
            "face-source-shape-lamp-mask");

        _machineObjectReferenceResolver.TryGetReference(sourceElement, out var machineReference);
        return new FaceLampWindowElement
        {
            ObjectId = CreateGeneratedElementId(sourceElement),
            Name = sourceElement.Name ?? string.Empty,
            X = Math.Round(minX, 2),
            Y = Math.Round(minY, 2),
            Width = Math.Round(maxX - minX, 2),
            Height = Math.Round(maxY - minY, 2),
            IsVisible = sourceElement.IsVisible,
            IsLocked = sourceElement.IsLocked,
            LinkedMachineObjectReference = machineReference.IsEmpty ? null : machineReference,
            LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId,
            BulbMaskAssetPath = bulbMaskAssetPath,
            SourceComponentIndex = sourceElement.SourceComponentIndex,
            SharedSourceSetId = NormalizeOptional(sourceElement.SharedSourceSetId),
            SharedSourceSetCount = sourceElement.SharedSourceSetCount,
            SourceBlend = sourceElement.SourceBlend
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
            LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId,
            BulbMaskAssetPath = sourceElement.SecondaryAssetPath,
            SourceComponentIndex = sourceElement.SourceComponentIndex,
            SharedSourceSetId = NormalizeOptional(sourceElement.SharedSourceSetId),
            SharedSourceSetCount = sourceElement.SharedSourceSetCount,
            SourceBlend = sourceElement.SourceBlend
        };
    }

    private FaceReelDisplayElement CreateReelDisplay(PanelElementModel sourceElement, Rect region)
    {
        _machineObjectReferenceResolver.TryGetReference(sourceElement, out var machineReference);

        return new FaceReelDisplayElement
        {
            ObjectId = CreateGeneratedElementId(sourceElement),
            Name = string.IsNullOrWhiteSpace(sourceElement.Name) ? "Reel Display" : sourceElement.Name.Trim(),
            X = Math.Round(sourceElement.X - region.X, 2),
            Y = Math.Round(sourceElement.Y - region.Y, 2),
            Width = Math.Round(sourceElement.Width, 2),
            Height = Math.Round(sourceElement.Height, 2),
            IsVisible = sourceElement.IsVisible,
            IsLocked = sourceElement.IsLocked,
            LinkedMachineObjectReference = machineReference.IsEmpty ? null : machineReference,
            LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId,
            AssetPath = sourceElement.AssetPath,
            Stops = sourceElement.Stops,
            VisibleScale = sourceElement.VisibleScale,
            BandOffset = sourceElement.BandOffset,
            IsReversed = sourceElement.IsReversed == true
        };
    }

    private FaceSevenSegmentDisplayElement CreateSevenSegmentDisplay(PanelElementModel sourceElement, Rect region)
    {
        _machineObjectReferenceResolver.TryGetReference(sourceElement, out var machineReference);

        return new FaceSevenSegmentDisplayElement
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
            LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId,
            OnColorHex = sourceElement.OnColorHex,
            OffColorHex = sourceElement.OffColorHex,
            ShowDecimalPoint = sourceElement.ShowDecimalPoint
        };
    }

    private FaceAlphaDisplayElement CreateAlphaDisplay(PanelElementModel sourceElement, Rect region)
    {
        _machineObjectReferenceResolver.TryGetReference(sourceElement, out var machineReference);

        return new FaceAlphaDisplayElement
        {
            ObjectId = CreateGeneratedElementId(sourceElement),
            Name = string.IsNullOrWhiteSpace(sourceElement.Name) ? "Alpha Display" : sourceElement.Name.Trim(),
            X = Math.Round(sourceElement.X - region.X, 2),
            Y = Math.Round(sourceElement.Y - region.Y, 2),
            Width = Math.Round(sourceElement.Width, 2),
            Height = Math.Round(sourceElement.Height, 2),
            IsVisible = sourceElement.IsVisible,
            IsLocked = sourceElement.IsLocked,
            LinkedMachineObjectReference = machineReference.IsEmpty ? null : machineReference,
            LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId,
            SegmentDisplayType = sourceElement.SegmentDisplayType,
            OnColorHex = sourceElement.OnColorHex,
            OffColorHex = sourceElement.OffColorHex,
            ShowDecimalPoint = sourceElement.ShowDecimalPoint,
            ShowCommaTail = sourceElement.ShowCommaTail,
            IsReversed = sourceElement.IsReversed == true
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

    private static bool IsCenterInsideSourceShape(PanelElementModel element, PanelFaceSourceShapeModel sourceShape)
    {
        if (element.Width <= 0 || element.Height <= 0)
        {
            return false;
        }

        return FaceSourceShapeTransformService.ContainsPanelPoint(sourceShape, element.X + (element.Width / 2d), element.Y + (element.Height / 2d));
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

    private readonly record struct SourceShapeMaskContribution(FaceSourceRegionModel? Bounds, int PixelCount);
}
