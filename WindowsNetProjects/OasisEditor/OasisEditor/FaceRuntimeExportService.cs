using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Progress;

namespace OasisEditor;

public sealed class FaceRuntimeExportService
{
    public const int RuntimeManifestSchemaVersion = 2;
    public const string RuntimeDirectoryName = "runtime";
    public const string ManifestFileName = "face.runtime.json";
    public const string ArtworkFileName = "artwork.png";
    public const string MaskFileName = "mask.png";
    public const string ReelBandDirectoryName = "reels";

    private readonly FaceRuntimeTextureGenerator _runtimeTextureGenerator;

    public FaceRuntimeExportService(FaceRuntimeTextureGenerator? runtimeTextureGenerator = null)
    {
        _runtimeTextureGenerator = runtimeTextureGenerator ?? new FaceRuntimeTextureGenerator();
    }

    private static readonly JsonSerializerOptions s_manifestJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public FaceRuntimeExportResult Export(FaceDocumentModel faceDocument, EditorProject project, string? documentPath = null, IEditorProgressReporter? progress = null)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(project);
        progress ??= NoOpEditorProgressReporter.Instance;
        progress.Report(0.0, "Resolving dimensions/output directory...");

        if (string.IsNullOrWhiteSpace(project.ProjectDirectory))
        {
            throw new InvalidOperationException("Project directory is not configured.");
        }

        var width = ResolveRuntimeWidth(faceDocument);
        var height = ResolveRuntimeHeight(faceDocument);
        var outputDirectory = ResolveRuntimeOutputDirectory(project, documentPath);
        Directory.CreateDirectory(outputDirectory);

        if (!Directory.Exists(outputDirectory))
        {
            throw new IOException($"Face runtime output directory could not be created: {outputDirectory}");
        }

        var artworkPath = Path.Combine(outputDirectory, ArtworkFileName);
        var maskPath = Path.Combine(outputDirectory, MaskFileName);
        progress.Report(0.15, "Exporting artwork...");
        ExportArtwork(faceDocument, project, width, height, artworkPath);
        progress.Report(0.3, "Copying mask...");
        CopyMask(faceDocument, project, maskPath);
        progress.Report(0.45, "Creating runtime texture plan...");
        var textureResult = _runtimeTextureGenerator.Generate(faceDocument, width, height, outputDirectory, progress.CreateChild(0.45, 0.75));
        CopyReelBands(faceDocument, project, outputDirectory);

        var generatedUtc = DateTime.UtcNow;
        progress.Report(0.8, "Writing manifest...");
        var cabinetContext = new FaceCabinetContextResolver().ResolveForFace(project, Array.Empty<DocumentTabViewModel>(), faceDocument);
        var manifest = CreateManifest(faceDocument, width, height, textureResult.Plan, cabinetContext);
        var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, s_manifestJsonOptions));

        progress.Report(0.9, "Updating runtime asset references...");
        var runtimeAssets = new FaceRuntimeRenderAssetsModel
        {
            ManifestPath = ToProjectRelativePath(project, manifestPath),
            ArtworkPath = ToProjectRelativePath(project, artworkPath),
            MaskPath = ToProjectRelativePath(project, maskPath),
            TrayIdPath = ToProjectRelativePath(project, textureResult.TrayIdPath),
            LampIds0Path = ToProjectRelativePath(project, textureResult.LampIds0Path),
            LampWeights0Path = ToProjectRelativePath(project, textureResult.LampWeights0Path),
            LampIds1Path = null,
            LampWeights1Path = null,
            TrayIdDebugPath = ToProjectRelativePath(project, textureResult.TrayIdDebugPath),
            LampWeightsDebugPath = ToProjectRelativePath(project, textureResult.LampWeightsDebugPath),
            Width = width,
            Height = height,
            GeneratedUtc = generatedUtc
        };

        var updatedDocument = new FaceDocumentModel
        {
            Id = faceDocument.Id,
            Title = faceDocument.Title,
            Summary = faceDocument.Summary,
            SourcePanel2DDocumentId = faceDocument.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath = faceDocument.SourcePanel2DDocumentPath,
            SourceFaceShapeId = faceDocument.SourceFaceShapeId,
            AssignedCabinetFaceTargetId = faceDocument.AssignedCabinetFaceTargetId,
                AssignedCabinetAssetPath = faceDocument.AssignedCabinetAssetPath,
            SourceRegion = faceDocument.SourceRegion,
            LastRegeneratedAtUtc = faceDocument.LastRegeneratedAtUtc,
            GenerationSettings = faceDocument.GenerationSettings,
            RuntimeRenderAssets = runtimeAssets,
            MaskLayer = faceDocument.MaskLayer,
            Trays = faceDocument.Trays,
            LampEmitters = faceDocument.LampEmitters,
            Layers = faceDocument.Layers,
            Elements = faceDocument.Elements
        };

        progress.Report(1.0, "Runtime export complete.");
        return new FaceRuntimeExportResult(updatedDocument, manifest, outputDirectory, manifestPath, artworkPath, maskPath);
    }

    public FaceRuntimeManifest CreateManifest(FaceDocumentModel faceDocument, int width, int height, FaceCabinetContext? cabinetContext = null)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        var texturePlan = _runtimeTextureGenerator.CreatePlan(faceDocument, width, height);
        return CreateManifest(faceDocument, width, height, texturePlan, cabinetContext);
    }

    public FaceRuntimeManifest CreateManifest(FaceDocumentModel faceDocument, int width, int height, FaceRuntimeTextureGenerationPlan texturePlan, FaceCabinetContext? cabinetContext = null)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(texturePlan);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Runtime manifest width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Runtime manifest height must be positive.");
        }

        return new FaceRuntimeManifest
        {
            SchemaVersion = RuntimeManifestSchemaVersion,
            FaceId = faceDocument.Id,
            Width = width,
            Height = height,
            Artwork = ArtworkFileName,
            Mask = MaskFileName,
            TrayId = FaceRuntimeTextureGenerator.TrayIdFileName,
            LampIds0 = FaceRuntimeTextureGenerator.LampIds0FileName,
            LampWeights0 = FaceRuntimeTextureGenerator.LampWeights0FileName,
            LampIds1 = null,
            LampWeights1 = null,
            TrayIdDebug = FaceRuntimeTextureGenerator.TrayIdDebugFileName,
            LampWeightsDebug = FaceRuntimeTextureGenerator.LampWeightsDebugFileName,
            Lamps = texturePlan.Emitters.Select(CreateLampManifestEntry).ToArray(),
            Trays = texturePlan.Trays.Select(CreateTrayManifestEntry).ToArray(),
            Reels = faceDocument.Elements.OfType<FaceReelDisplayElement>().Select(reel => CreateReelManifestEntry(faceDocument, reel, cabinetContext)).ToArray(),
            SevenSegmentDisplays = faceDocument.Elements.OfType<FaceSevenSegmentDisplayElement>().Select(CreateDisplayManifestEntry).ToArray(),
            AlphaDisplays = faceDocument.Elements.OfType<FaceAlphaDisplayElement>().Select(CreateDisplayManifestEntry).ToArray(),
            Buttons = faceDocument.Elements.OfType<FaceButtonElement>().Select(CreateButtonManifestEntry).ToArray()
        };
    }

    private static void ExportArtwork(FaceDocumentModel faceDocument, EditorProject project, int width, int height, string outputPath)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        foreach (var artwork in faceDocument.Elements.OfType<FaceArtworkElement>())
        {
            if (!artwork.IsVisible || artwork.Width <= 0d || artwork.Height <= 0d)
            {
                continue;
            }

            var sourcePath = ResolveExistingProjectPath(project, artwork.AssetPath, $"Artwork element '{DisplayName(artwork)}'");
            using var image = LoadImage(sourcePath, $"Artwork element '{DisplayName(artwork)}'");
            var destination = SKRect.Create((float)artwork.X, (float)artwork.Y, (float)artwork.Width, (float)artwork.Height);
            var source = ResolveArtworkSourceRect(artwork, image.Width, image.Height);
            canvas.DrawImage(image, source, destination);
        }

        using var flattenedImage = SKImage.FromBitmap(bitmap);
        using var data = flattenedImage.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            throw new IOException("Flattened face artwork could not be encoded as PNG.");
        }

        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }


    private static void CopyReelBands(FaceDocumentModel faceDocument, EditorProject project, string outputDirectory)
    {
        var reels = faceDocument.Elements.OfType<FaceReelDisplayElement>().Where(reel => !string.IsNullOrWhiteSpace(reel.AssetPath)).ToArray();
        if (reels.Length == 0) return;
        var reelDirectory = Path.Combine(outputDirectory, ReelBandDirectoryName);
        Directory.CreateDirectory(reelDirectory);
        foreach (var reel in reels)
        {
            var sourcePath = ResolveExistingProjectPath(project, reel.AssetPath, $"Reel '{DisplayName(reel)}' band");
            ExportReelBandPng(sourcePath, Path.Combine(reelDirectory, CreateReelBandFileName(reel)), reel);
        }
    }

    private static string CreateReelBandFileName(FaceReelDisplayElement reel)
    {
        var objectId = string.IsNullOrWhiteSpace(reel.ObjectId) ? "reel" : reel.ObjectId.Trim();
        var safe = string.Concat(objectId.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_'));
        return $"{safe}.png";
    }

    private static void ExportReelBandPng(string sourcePath, string outputPath, FaceReelDisplayElement reel)
    {
        using var image = LoadImage(sourcePath, $"Reel '{DisplayName(reel)}' band");
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            throw new IOException($"Reel '{DisplayName(reel)}' band could not be encoded as PNG.");
        }

        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static void CopyMask(FaceDocumentModel faceDocument, EditorProject project, string outputPath)
    {
        if (faceDocument.MaskLayer is null)
        {
            throw new InvalidOperationException("Face document does not contain a mask layer to export as runtime mask.png.");
        }

        var sourcePath = ResolveExistingProjectPath(project, faceDocument.MaskLayer.AssetPath, "Face mask layer");
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Copy(sourcePath, outputPath, overwrite: true);
    }

    private static string ResolveRuntimeOutputDirectory(EditorProject project, string? documentPath)
    {
        if (string.IsNullOrWhiteSpace(project.GeneratedDirectory))
        {
            throw new InvalidOperationException("Project Generated directory is not configured.");
        }

        var generatedDirectory = Path.GetFullPath(project.GeneratedDirectory);
        if (File.Exists(generatedDirectory))
        {
            throw new IOException($"Project Generated directory points to a file: {generatedDirectory}");
        }

        var faceName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(documentPath ?? string.Empty, EditorAssetType.Face);
        if (string.IsNullOrWhiteSpace(faceName))
        {
            throw new InvalidOperationException("Face runtime export requires a Face package manifest path: Assets/Faces/<AssetName>/asset.face.");
        }

        return new ProjectAssetPathService().GetFaceRuntimeDirectory(project, faceName);
    }

    private static string ResolveExistingProjectPath(EditorProject project, string? projectPath, string description)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new FileNotFoundException($"{description} does not specify an asset path.");
        }

        var candidate = projectPath.Trim();
        var paths = new List<string>();
        if (Path.IsPathRooted(candidate))
        {
            paths.Add(candidate);
        }
        else
        {
            var normalizedRelative = candidate.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            paths.Add(Path.GetFullPath(Path.Combine(project.ProjectDirectory, normalizedRelative)));
            paths.Add(Path.GetFullPath(Path.Combine(project.AssetsDirectory, normalizedRelative)));
        }

        var resolved = paths.FirstOrDefault(File.Exists);
        if (resolved is not null)
        {
            return resolved;
        }

        throw new FileNotFoundException($"{description} references missing asset '{projectPath}'.", paths.FirstOrDefault() ?? candidate);
    }

    private static SKImage LoadImage(string path, string description)
    {
        using var data = SKData.Create(path);
        if (data is null)
        {
            throw new IOException($"{description} asset '{path}' could not be opened.");
        }

        var image = SKImage.FromEncodedData(data);
        if (image is null)
        {
            throw new InvalidOperationException($"{description} asset '{path}' could not be read as an image.");
        }

        return image;
    }

    private static FaceRuntimeLampManifestEntry CreateLampManifestEntry(FaceLampEmitterElement element)
    {
        var reference = element.LinkedMachineObjectReference?.ToString();
        return new FaceRuntimeLampManifestEntry
        {
            ObjectId = element.ObjectId,
            SourceLampWindowObjectId = element.SourceLampWindowObjectId,
            LampId = element.LampId,
            MachineReference = string.IsNullOrWhiteSpace(reference) ? null : reference,
            Name = element.Name,
            TrayId = element.TrayId,
            X = element.CenterX,
            Y = element.CenterY,
            Width = element.Width,
            Height = element.Height
        };
    }

    private static FaceRuntimeTrayManifestEntry CreateTrayManifestEntry(FaceRuntimeTrayElement element)
    {
        return new FaceRuntimeTrayManifestEntry
        {
            ObjectId = element.ObjectId,
            SourceLampWindowObjectId = element.SourceLampWindowObjectId,
            Name = element.Name,
            TrayId = element.TrayId,
            LampEmitterObjectId = element.LampEmitterObjectId,
            LampId = element.LampId,
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height
        };
    }


    private static FaceRuntimeReelManifestEntry CreateReelManifestEntry(FaceDocumentModel faceDocument, FaceReelDisplayElement element, FaceCabinetContext? cabinetContext)
    {
        var dimensions = ResolveReelPhysicalDimensions(faceDocument, element, cabinetContext);
        return new FaceRuntimeReelManifestEntry
        {
            ObjectId = element.ObjectId,
            MachineReference = element.LinkedMachineObjectReference?.ToString(),
            CabinetReelTargetId = ResolveCabinetReelTargetId(element),
            Name = element.Name,
            ReelBand = ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(ReelBandDirectoryName, CreateReelBandFileName(element))),
            Stops = element.Stops.GetValueOrDefault(0),
            IsReversed = element.IsReversed,
            BandOffset = element.BandOffset.GetValueOrDefault(0d),
            PhysicalWidth = dimensions.WidthMm,
            PhysicalRadius = dimensions.RadiusMm,
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height
        };
    }

    private static ResolvedReelPhysicalDimensions ResolveReelPhysicalDimensions(FaceDocumentModel faceDocument, FaceReelDisplayElement reel, FaceCabinetContext? cabinetContext)
    {
        var faceAsset = string.IsNullOrWhiteSpace(faceDocument.Title) ? faceDocument.Id : faceDocument.Title;
        var reelName = DisplayName(reel);
        var requestedId = string.IsNullOrWhiteSpace(reel.ReelSpecificationId) ? string.Empty : reel.ReelSpecificationId.Trim();
        var cabinetAsset = cabinetContext?.CabinetAssetPath ?? faceDocument.AssignedCabinetAssetPath ?? string.Empty;

        InvalidOperationException Fail(string reason) => new($"Unable to resolve physical reel dimensions. Face asset '{faceAsset}', reel '{reelName}' (objectId '{reel.ObjectId}'), Cabinet asset '{cabinetAsset}', requested specification ID '{requestedId}': {reason}");

        if (cabinetContext is null || cabinetContext.CabinetDocument is null)
        {
            var reason = cabinetContext?.DiagnosticMessage ?? "Face has no assigned Cabinet.";
            throw Fail(reason);
        }

        if (string.IsNullOrWhiteSpace(requestedId))
        {
            throw Fail("Face reel has no ReelSpecificationId.");
        }

        var matches = (cabinetContext.CabinetDocument.ReelSpecifications ?? [])
            .Where(specification => string.Equals(specification.Id?.Trim(), requestedId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            throw Fail("Referenced Cabinet reel specification does not exist.");
        }

        if (matches.Length > 1)
        {
            throw Fail("Cabinet contains duplicate matching reel specification IDs.");
        }

        var specification = matches[0];
        if (!PanelElementValidation.IsFinite(specification.DiameterMm) || specification.DiameterMm <= 0d)
        {
            throw Fail($"Cabinet reel specification diameter '{specification.DiameterMm}' is not a positive finite millimetre value.");
        }

        if (!PanelElementValidation.IsFinite(specification.WidthMm) || specification.WidthMm <= 0d)
        {
            throw Fail($"Cabinet reel specification width '{specification.WidthMm}' is not a positive finite millimetre value.");
        }

        return new ResolvedReelPhysicalDimensions(specification.WidthMm, specification.DiameterMm / 2d);
    }

    private readonly record struct ResolvedReelPhysicalDimensions(double WidthMm, double RadiusMm);

    private static string ResolveCabinetReelTargetId(FaceReelDisplayElement element)
    {
        var reference = element.LinkedMachineObjectReference?.ToString();
        if (!string.IsNullOrWhiteSpace(reference)) return reference.Trim().Replace(":", string.Empty);
        return element.ObjectId;
    }

    private static FaceRuntimeElementManifestEntry CreateDisplayManifestEntry(FaceElementModel element)
    {
        return new FaceRuntimeElementManifestEntry
        {
            ObjectId = element.ObjectId,
            MachineReference = element.LinkedMachineObjectReference?.ToString(),
            Name = element.Name,
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height
        };
    }

    private static FaceRuntimeButtonManifestEntry CreateButtonManifestEntry(FaceButtonElement element)
    {
        return new FaceRuntimeButtonManifestEntry
        {
            ObjectId = element.ObjectId,
            MachineReference = element.LinkedMachineObjectReference?.ToString(),
            InputReference = element.LinkedInputReference?.ToString(),
            Name = element.Name,
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height
        };
    }

    private static int ResolveRuntimeWidth(FaceDocumentModel faceDocument)
    {
        if (faceDocument.SourceRegion is { IsValid: true } sourceRegion)
        {
            return Math.Max(1, (int)Math.Ceiling(sourceRegion.Width));
        }

        if (faceDocument.MaskLayer is { Width: > 0 } maskLayer)
        {
            return maskLayer.Width;
        }

        return Math.Max(1, (int)Math.Ceiling(faceDocument.Elements.Select(element => element.X + element.Width).DefaultIfEmpty(1d).Max()));
    }

    private static int ResolveRuntimeHeight(FaceDocumentModel faceDocument)
    {
        if (faceDocument.SourceRegion is { IsValid: true } sourceRegion)
        {
            return Math.Max(1, (int)Math.Ceiling(sourceRegion.Height));
        }

        if (faceDocument.MaskLayer is { Height: > 0 } maskLayer)
        {
            return maskLayer.Height;
        }

        return Math.Max(1, (int)Math.Ceiling(faceDocument.Elements.Select(element => element.Y + element.Height).DefaultIfEmpty(1d).Max()));
    }

    private static SKRect ResolveArtworkSourceRect(FaceArtworkElement element, int imageWidth, int imageHeight)
    {
        var sourceRegion = element.SourceRegion;
        var sourceBounds = element.Provenance?.SourceElementBounds;
        if (sourceRegion is null || sourceBounds is null || sourceBounds.Width <= 0d || sourceBounds.Height <= 0d)
        {
            return SKRect.Create(0f, 0f, imageWidth, imageHeight);
        }

        var scaleX = imageWidth / sourceBounds.Width;
        var scaleY = imageHeight / sourceBounds.Height;
        var x = (sourceRegion.X - sourceBounds.X) * scaleX;
        var y = (sourceRegion.Y - sourceBounds.Y) * scaleY;
        var width = sourceRegion.Width * scaleX;
        var height = sourceRegion.Height * scaleY;
        var left = (float)Math.Clamp(x, 0d, imageWidth);
        var top = (float)Math.Clamp(y, 0d, imageHeight);
        var right = (float)Math.Clamp(x + width, left, imageWidth);
        var bottom = (float)Math.Clamp(y + height, top, imageHeight);
        return new SKRect(left, top, right, bottom);
    }

    private static string ToProjectRelativePath(EditorProject project, string fullPath)
    {
        return Path.GetRelativePath(Path.GetFullPath(project.ProjectDirectory), Path.GetFullPath(fullPath)).Replace('\\', '/');
    }

    private static string DisplayName(FaceElementModel element)
    {
        return string.IsNullOrWhiteSpace(element.Name) ? element.ObjectId : element.Name.Trim();
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }
}

public sealed record FaceRuntimeExportResult(
    FaceDocumentModel Document,
    FaceRuntimeManifest Manifest,
    string OutputDirectory,
    string ManifestPath,
    string ArtworkPath,
    string MaskPath);

public sealed class FaceRuntimeManifest
{
    public int SchemaVersion { get; init; }
    public string FaceId { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public string Artwork { get; init; } = string.Empty;
    public string Mask { get; init; } = string.Empty;
    public string? TrayId { get; init; }
    public string? LampIds0 { get; init; }
    public string? LampWeights0 { get; init; }
    public string? LampIds1 { get; init; }
    public string? LampWeights1 { get; init; }
    public string? TrayIdDebug { get; init; }
    public string? LampWeightsDebug { get; init; }
    public IReadOnlyList<FaceRuntimeLampManifestEntry> Lamps { get; init; } = [];
    public IReadOnlyList<FaceRuntimeTrayManifestEntry> Trays { get; init; } = [];
    public IReadOnlyList<FaceRuntimeReelManifestEntry> Reels { get; init; } = [];
    public IReadOnlyList<FaceRuntimeElementManifestEntry> SevenSegmentDisplays { get; init; } = [];
    public IReadOnlyList<FaceRuntimeElementManifestEntry> AlphaDisplays { get; init; } = [];
    public IReadOnlyList<FaceRuntimeButtonManifestEntry> Buttons { get; init; } = [];
}

public sealed class FaceRuntimeLampManifestEntry
{
    public string ObjectId { get; init; } = string.Empty;
    public string SourceLampWindowObjectId { get; init; } = string.Empty;
    public int? LampId { get; init; }
    public string? MachineReference { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? TrayId { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

public sealed class FaceRuntimeTrayManifestEntry : FaceRuntimeElementManifestEntry
{
    public int TrayId { get; init; }
    public string SourceLampWindowObjectId { get; init; } = string.Empty;
    public string LampEmitterObjectId { get; init; } = string.Empty;
    public int? LampId { get; init; }
}

public class FaceRuntimeElementManifestEntry
{
    public string ObjectId { get; init; } = string.Empty;
    public string? MachineReference { get; init; }
    public string Name { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}


public sealed class FaceRuntimeReelManifestEntry : FaceRuntimeElementManifestEntry
{
    public string CabinetReelTargetId { get; init; } = string.Empty;
    public string ReelBand { get; init; } = string.Empty;
    public int Stops { get; init; }
    public bool IsReversed { get; init; }
    public double BandOffset { get; init; }
    public double PhysicalWidth { get; init; }
    public double PhysicalRadius { get; init; }
}

public sealed class FaceRuntimeButtonManifestEntry : FaceRuntimeElementManifestEntry
{
    public string? InputReference { get; init; }
}
