using System.Windows;
using OasisEditor.Features.CabinetEditor.Models;
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
    private readonly FaceTrayAutoAuthoringService _trayAutoAuthoringService = new();
    private readonly FaceSemanticElementConversionService _semanticElementConversionService;

    public FaceGenerationService(IMachineObjectReferenceResolver? machineObjectReferenceResolver = null)
    {
        _machineObjectReferenceResolver = machineObjectReferenceResolver ?? MachineObjectReferenceResolver.Instance;
        _semanticElementConversionService = new FaceSemanticElementConversionService(_machineObjectReferenceResolver);
    }



    public FaceGenerationResult GenerateFromPanelFaceSourceShape(
        Panel2DDocumentModel sourcePanel,
        PanelFaceSourceShapeModel sourceShape,
        string title,
        string? sourcePanel2DDocumentId = null,
        string? assignedCabinetFaceTargetId = null,
        string? assignedCabinetAssetPath = null,
        double? targetAspectRatio = null,
        string? projectDirectory = null,
        string? generatedDirectory = null,
        string? faceAssetName = null,
        string? faceAssetDirectory = null,
        FaceGenerationSettingsModel? generationSettings = null,
        IEditorProgressReporter? progress = null,
        string? sourcePanel2DDocumentPath = null,
        IReadOnlyList<InputDefinitionModel>? inputDefinitions = null,
        CabinetDocument? cabinetDocument = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePanel);
        ArgumentNullException.ThrowIfNull(sourceShape);
        cancellationToken.ThrowIfCancellationRequested();
        var output = FaceSourceShapeTransformService.EstimateOutputSize(sourceShape, targetAspectRatio);
        var pathService = new ProjectAssetPathService();
        var resolvedFaceAssetName = pathService.SanitizePathSegment(string.IsNullOrWhiteSpace(faceAssetName) ? title : faceAssetName);
        var faceArtworkPath = ResolveFaceAuthoredAssetPath(projectDirectory, generatedDirectory, faceAssetDirectory, resolvedFaceAssetName, ProjectAssetPathService.FaceArtworkFileName);
        var region = FaceSourceRegionModel.FromRect(new Rect(0, 0, output.Width, output.Height));
        var sourceBackground = sourcePanel.Elements.FirstOrDefault(element => element.Kind == PanelElementKind.Background);
        var artworkState = new FaceArtworkModel
        {
            Source = new FaceArtworkSourceModel
            {
                Kind = FaceArtworkSourceKind.Panel2DFaceSourceShape,
                AssetPath = sourceBackground?.AssetPath,
                Panel2DDocumentId = NormalizeOptional(sourcePanel2DDocumentId),
                Panel2DDocumentPath = NormalizeOptional(sourcePanel2DDocumentPath),
                FaceSourceShapeId = NormalizeOptional(sourceShape.Id)
            },
            ProcessingPipeline = new ImageProcessingPipelineModel(),
            OutputWidth = output.Width,
            OutputHeight = output.Height, FinalOutputWidth=output.Width, FinalOutputHeight=output.Height
        };
        var settings = (generationSettings ?? FaceGenerationSettingsModel.Default).Normalize();
        string? generatedCorrectionInputPath = null;
        string? generatedBasePath = null;
        if (!string.IsNullOrWhiteSpace(faceArtworkPath) && !string.IsNullOrWhiteSpace(projectDirectory))
        {
            var correctionInputPath = FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(faceArtworkPath);
            var basePath = FaceArtworkGeneratedPathService.GetBasePathFromOutput(faceArtworkPath);
            var builder = new FaceArtworkRebuildService();
            generatedCorrectionInputPath = builder.RebuildCorrectionInput(
                artworkState, sourcePanel, sourceShape, projectDirectory, correctionInputPath, settings);
            cancellationToken.ThrowIfCancellationRequested();
            generatedBasePath = string.IsNullOrWhiteSpace(generatedCorrectionInputPath)
                ? null
                : FaceArtworkGeneratedPathService.ToProjectRelative(basePath, projectDirectory);
        }
        var assetPath = string.IsNullOrWhiteSpace(projectDirectory) || string.IsNullOrWhiteSpace(generatedBasePath)
            ? null
            : FaceArtworkGeneratedPathService.ToProjectRelative(faceArtworkPath, projectDirectory);
        artworkState = new FaceArtworkModel
        {
            Id = artworkState.Id,
            Source = artworkState.Source,
            Geometry = artworkState.Geometry,
            ProcessingPipeline = artworkState.ProcessingPipeline,
            CorrectionInputAssetPath = generatedCorrectionInputPath,
            BaseAssetPath = generatedBasePath,
            OutputAssetPath = assetPath,
            OutputWidth = artworkState.OutputWidth,
            OutputHeight = artworkState.OutputHeight, Override=artworkState.Override,
            FinalOutputWidth=artworkState.FinalOutputWidth, FinalOutputHeight=artworkState.FinalOutputHeight
        };
        if (!string.IsNullOrWhiteSpace(projectDirectory)
            && !string.IsNullOrWhiteSpace(artworkState.BaseAssetPath)
            && !string.IsNullOrWhiteSpace(artworkState.OutputAssetPath))
        {
            var builder = new FaceArtworkRebuildService();
            var baseResult = builder.BuildBaseFromCorrectionInput(artworkState, projectDirectory);
            if (!baseResult.Succeeded) throw new InvalidOperationException(baseResult.ErrorMessage);
            var finalized = builder.FinalizeOutput(artworkState, projectDirectory);
            if (!finalized.Succeeded) throw new InvalidOperationException(finalized.ErrorMessage);
            cancellationToken.ThrowIfCancellationRequested();
        }
        var faceDocumentId = Guid.NewGuid().ToString("N");
        progress?.Report(0.2, "Converting source-shape semantic components...");
        var semanticElements = _semanticElementConversionService.ConvertSupportedElements(sourcePanel, sourceShape, output.Width, output.Height, projectDirectory, inputDefinitions, cabinetDocument?.DefaultReelSpecificationId).ToArray();
        var lampWindows = semanticElements.OfType<FaceLampWindowElement>().ToArray();
        var maskLayer = GenerateMaskFromSourceShape(
            sourcePanel,
            sourceShape,
            output.Width,
            output.Height,
            lampWindows,
            faceDocumentId,
            sourcePanel2DDocumentId,
            projectDirectory,
            resolvedFaceAssetName,
            ResolveFaceAuthoredAssetPath(projectDirectory, generatedDirectory, faceAssetDirectory, resolvedFaceAssetName, ProjectAssetPathService.FaceMaskFileName),
            settings.MaskExtractionThreshold,
            ImageProcessingExecutionPolicy.Current.WithCancellation(cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        var artwork = new FaceArtworkElement
        {
            ObjectId = $"face-artwork-{Guid.NewGuid():N}",
            Name = "Perspective-corrected artwork",
            X = 0,
            Y = 0,
            Width = output.Width,
            Height = output.Height,
            IsVisible = true,
            IsTransformLocked = true,
            AssetPath = assetPath,
            SourcePanel2DDocumentId = NormalizeOptional(sourcePanel2DDocumentId),
            SourceRegion = region,
            Provenance = new FaceArtworkProvenanceModel { Generator = "Create Face from Face Source Shape", GeneratedAtUtc = DateTime.UtcNow }
        };
        var elements = new FaceElementModel[] { artwork }.Concat(semanticElements).ToArray();
        progress?.Report(0.9, "Auto-authoring trays/emitters...");
        var autoAuthored = _trayAutoAuthoringService.AutoAuthor(new FaceDocumentModel { GenerationSettings = settings, MaskLayer = maskLayer, Elements = elements }, projectDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        var document = new FaceDocumentModel
        {
            Id = faceDocumentId,
            Title = resolvedFaceAssetName,
            Summary = $"Generated from Face Source Shape '{sourceShape.Name}' ({output.Width} x {output.Height}).",
            SourcePanel2DDocumentId = NormalizeOptional(sourcePanel2DDocumentId),
            SourceFaceShapeId = NormalizeOptional(sourceShape.Id),
            SourcePanel2DDocumentPath = NormalizeOptional(sourcePanel2DDocumentPath),
            AssignedCabinetFaceTargetId = NormalizeOptional(assignedCabinetFaceTargetId),
            AssignedCabinetAssetPath = NormalizeOptional(assignedCabinetAssetPath),
            SourceRegion = region,
            LastRegeneratedAtUtc = DateTime.UtcNow,
            GenerationSettings = settings,
            Provenance = FaceBuildStateFactory.CreateDerivedProvenance(sourcePanel2DDocumentPath),
            BuildState = FaceBuildStateFactory.CreateGeneratedState(artwork: !string.IsNullOrWhiteSpace(artworkState.CorrectionInputAssetPath)
                    && !string.IsNullOrWhiteSpace(artworkState.BaseAssetPath)
                    && !string.IsNullOrWhiteSpace(artworkState.OutputAssetPath),
                mask: maskLayer is not null && lampWindows.Length > 0,
                trays: maskLayer is not null && lampWindows.Length > 0 && autoAuthored.Trays.Count > 0,
                runtimeAssetsCurrent: false,
                runtimeAssetsConfigured: false),
            Artwork = artworkState,
            MaskLayer = maskLayer,
            Trays = autoAuthored.Trays,
            LampEmitters = autoAuthored.Emitters,
            Layers =
            [
                new FaceLayerModel { Id = "layer-artwork", Name = "Artwork", IsVisible = true },
                new FaceLayerModel { Id = "layer-face-mask", Name = "Face Mask", IsVisible = true },
                new FaceLayerModel { Id = "layer-runtime-lamps", Name = "Runtime Lamps", IsVisible = true },
                new FaceLayerModel { Id = "layer-semantic-components", Name = "Semantic Components", IsVisible = true }
            ],
            Elements = elements
        };
        var reelCount = semanticElements.OfType<FaceReelDisplayElement>().Count();
        var sevenSegmentCount = semanticElements.OfType<FaceSevenSegmentDisplayElement>().Count();
        var alphaCount = semanticElements.OfType<FaceAlphaDisplayElement>().Count();
        var buttonCount = semanticElements.OfType<FaceButtonElement>().Count();
        progress?.Report(1.0, $"Face generated: artwork=1, lamps={lampWindows.Length}, reels={reelCount}, sevenSegment={sevenSegmentCount}, alpha={alphaCount}, buttons={buttonCount}");
        return new FaceGenerationResult(document, lampWindows.Length, 1, buttonCount, sevenSegmentCount, alphaCount, reelCount);
    }

    internal FaceMaskLayerModel? GenerateMaskFromSourceShape(
        Panel2DDocumentModel sourcePanel,
        PanelFaceSourceShapeModel sourceShape,
        int faceWidth,
        int faceHeight,
        IReadOnlyList<FaceLampWindowElement> lampWindows,
        string faceDocumentId,
        string? sourcePanel2DDocumentId,
        string? projectDirectory,
        string faceAssetName,
        string? outputPath,
        byte extractionThreshold,
        ImageProcessingExecutionOptions? executionOptions = null)
    {
        using var totalTiming = FaceArtworkPerformanceTrace.Measure("Complete generated lamp-mask build");
        var options = executionOptions ?? ImageProcessingExecutionPolicy.Current;
        if (string.IsNullOrWhiteSpace(projectDirectory) || faceWidth <= 0 || faceHeight <= 0)
        {
            return null;
        }

        var sourceLampsById = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Lamp && !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, StringComparer.Ordinal);
        var maskPixels = new byte[faceWidth * faceHeight];
        var contributions = new List<FaceMaskContributionModel>();
        var decodeElapsed = TimeSpan.Zero;
        var compositeElapsed = TimeSpan.Zero;

        foreach (var lampWindow in lampWindows)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
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

            var decodeStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            using var bitmap = SKBitmap.Decode(sourcePath);
            decodeElapsed += System.Diagnostics.Stopwatch.GetElapsedTime(decodeStarted);
            if (bitmap is null)
            {
                continue;
            }

            var compositeStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var contribution = CompositeSourceShapeLampMask(maskPixels, faceWidth, faceHeight, sourceShape, sourceLamp, bitmap, lampWindow, extractionThreshold, options);
            compositeElapsed += System.Diagnostics.Stopwatch.GetElapsedTime(compositeStarted);
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

        FaceArtworkPerformanceTrace.WriteMeasurement("Source lamp bitmap decode total", decodeElapsed);
        FaceArtworkPerformanceTrace.WriteMeasurement("Source-shape lamp compositing total", compositeElapsed);
        var assetPath = SaveSourceShapeMask(maskPixels, faceWidth, faceHeight, projectDirectory, faceAssetName, outputPath, options);
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

    internal static SourceShapeMaskContribution CompositeSourceShapeLampMask(
        byte[] maskPixels, int faceWidth, int faceHeight, PanelFaceSourceShapeModel sourceShape,
        PanelElementModel sourceLamp, SKBitmap lampBitmap, FaceLampWindowElement lampWindow,
        byte extractionThreshold, ImageProcessingExecutionOptions options)
    {
        var left = Math.Max(0, (int)Math.Floor(lampWindow.X));
        var top = Math.Max(0, (int)Math.Floor(lampWindow.Y));
        var right = Math.Min(faceWidth, (int)Math.Ceiling(lampWindow.X + lampWindow.Width));
        var bottom = Math.Min(faceHeight, (int)Math.Ceiling(lampWindow.Y + lampWindow.Height));
        if (right <= left || bottom <= top) return default;
        if (!FaceSourceShapeTransformService.TryCreateFaceToPanelHomography(sourceShape, faceWidth, faceHeight, out var h))
            return default;
        var sourcePixels = new BitmapPixelBuffer(lampBitmap);
        if (!sourcePixels.IsDirect) options = options with { MaxDegreeOfParallelism = 1 };
        var lampRight = sourceLamp.X + sourceLamp.Width;
        var lampBottom = sourceLamp.Y + sourceLamp.Height;
        var scaleX = lampBitmap.Width / Math.Max(1d, sourceLamp.Width);
        var scaleY = lampBitmap.Height / Math.Max(1d, sourceLamp.Height);
        var rowCounts = new int[bottom - top];
        var rowMinX = new int[bottom - top];
        var rowMaxX = new int[bottom - top];
        ImageProcessingExecutionPolicy.ForEachRow(bottom - top, options, row =>
        {
            var y = top + row;
            var count = 0; var minX = faceWidth; var maxX = -1;
            var destinationX = left + .5d; var destinationY = y + .5d;
            var nx = h[0] * destinationX + h[1] * destinationY + h[2];
            var ny = h[3] * destinationX + h[4] * destinationY + h[5];
            var denominator = h[6] * destinationX + h[7] * destinationY + h[8];
            for (var x = left; x < right; x++)
            {
                if (double.IsFinite(denominator) && Math.Abs(denominator) >= 1e-9)
                {
                    var panelX = nx / denominator; var panelY = ny / denominator;
                    if (double.IsFinite(panelX) && double.IsFinite(panelY) && panelX >= sourceLamp.X && panelY >= sourceLamp.Y
                        && panelX <= lampRight && panelY <= lampBottom)
                    {
                        var sourceX = Math.Clamp((int)Math.Round((panelX - sourceLamp.X) * scaleX), 0, lampBitmap.Width - 1);
                        var sourceY = Math.Clamp((int)Math.Round((panelY - sourceLamp.Y) * scaleY), 0, lampBitmap.Height - 1);
                        var alpha = sourcePixels.ReadAlpha(sourceX, sourceY);
                        if (alpha >= extractionThreshold && alpha != 0)
                        {
                            var index = y * faceWidth + x;
                            if (alpha > maskPixels[index]) maskPixels[index] = alpha;
                            count++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                        }
                    }
                }
                nx += h[0]; ny += h[3]; denominator += h[6];
            }
            rowCounts[row] = count; rowMinX[row] = minX; rowMaxX[row] = maxX;
        });
        var count = 0; var minX = faceWidth; var minY = faceHeight; var maxX = -1; var maxY = -1;
        for (var row = 0; row < rowCounts.Length; row++)
        {
            if (rowCounts[row] == 0) continue;
            count += rowCounts[row]; minX = Math.Min(minX, rowMinX[row]); maxX = Math.Max(maxX, rowMaxX[row]);
            minY = Math.Min(minY, top + row); maxY = Math.Max(maxY, top + row);
        }
        var bounds = count > 0
            ? FaceSourceRegionModel.FromRect(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1))
            : null;
        return new SourceShapeMaskContribution(bounds, count);
    }

    private static string SaveSourceShapeMask(byte[] maskPixels, int width, int height, string projectDirectory,
        string faceAssetName, string? outputPath, ImageProcessingExecutionOptions options)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (FaceArtworkPerformanceTrace.Measure("Final byte-mask to bitmap conversion"))
        {
            var pixels = new BitmapPixelBuffer(bitmap);
            ImageProcessingExecutionPolicy.ForEachRow(height, options, y =>
            {
                for (var x = 0; x < width; x++)
                {
                    var value = maskPixels[y * width + x];
                    pixels.WriteStraight(x, y, value, value, value, value);
                }
            });
        }

        var pathService = new ProjectAssetPathService();
        var project = CreatePathProject(projectDirectory, null);
        var path = string.IsNullOrWhiteSpace(outputPath) ? pathService.GetFaceMaskPath(project, faceAssetName) : outputPath;
        var relative = pathService.ToProjectRelativePath(project, path);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        options.CancellationToken.ThrowIfCancellationRequested();
        using (FaceArtworkPerformanceTrace.Measure("Lamp-mask PNG encode/write"))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = System.IO.File.Create(path)) data.SaveTo(stream);
        return ProjectAssetPathService.NormalizeProjectRelativePath(relative);
    }


    private static string? ResolveFaceAuthoredAssetPath(string? projectDirectory, string? generatedDirectory, string? faceAssetDirectory, string faceAssetName, string fileName)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        var pathService = new ProjectAssetPathService();
        var project = CreatePathProject(projectDirectory, generatedDirectory);
        if (!string.IsNullOrWhiteSpace(faceAssetDirectory)
            && !string.Equals(fileName, ProjectAssetPathService.FaceArtworkFileName, StringComparison.OrdinalIgnoreCase))
        {
            return System.IO.Path.Combine(faceAssetDirectory, fileName);
        }
        return string.Equals(fileName, ProjectAssetPathService.FaceMaskFileName, StringComparison.OrdinalIgnoreCase)
            ? pathService.GetFaceMaskPath(project, faceAssetName)
            : pathService.GetFaceArtworkPath(project, faceAssetName);
    }

    private static EditorProject CreatePathProject(string projectDirectory, string? generatedDirectory)
    {
        var root = System.IO.Path.GetFullPath(projectDirectory);
        return new EditorProject
        {
            Name = System.IO.Path.GetFileName(root),
            ProjectFilePath = System.IO.Path.Combine(root, $"{System.IO.Path.GetFileName(root)}.oasisproj"),
            ProjectDirectory = root,
            AssetsDirectory = System.IO.Path.Combine(root, "Assets"),
            MachinesDirectory = System.IO.Path.Combine(root, "Machines"),
            GeneratedDirectory = string.IsNullOrWhiteSpace(generatedDirectory) ? System.IO.Path.Combine(root, "Generated") : generatedDirectory
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
            IsTransformLocked = sourceElement.IsTransformLocked,
            LinkedMachineObjectReference = machineReference.IsEmpty ? null : machineReference,
            LinkedPanel2DElementId = string.IsNullOrWhiteSpace(sourceElement.ObjectId) ? null : sourceElement.ObjectId,
            BulbMaskAssetPath = bulbMaskAssetPath,
            SourceComponentIndex = sourceElement.SourceComponentIndex,
            SharedSourceSetId = NormalizeOptional(sourceElement.SharedSourceSetId),
            SharedSourceSetCount = sourceElement.SharedSourceSetCount,
            SourceBlend = sourceElement.SourceBlend
        };
    }

    private static string CreateGeneratedElementId(PanelElementModel sourceElement)
    {
        return string.IsNullOrWhiteSpace(sourceElement.ObjectId)
            ? Guid.NewGuid().ToString("N")
            : $"face-{sourceElement.ObjectId.Trim()}";
    }

    private static bool IsCenterInsideSourceShape(PanelElementModel element, PanelFaceSourceShapeModel sourceShape)
    {
        if (element.Width <= 0 || element.Height <= 0)
        {
            return false;
        }

        return FaceSourceShapeTransformService.ContainsPanelPoint(sourceShape, element.X + (element.Width / 2d), element.Y + (element.Height / 2d));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal readonly record struct SourceShapeMaskContribution(FaceSourceRegionModel? Bounds, int PixelCount);
}
