using System.IO;
using SkiaSharp;

namespace OasisEditor;

public sealed class FaceRuntimeTextureGenerator
{
    public const string TrayIdFileName = "trayId.png";
    public const string LampIds0FileName = "lampIds0.png";
    public const string LampWeights0FileName = "lampWeights0.png";
    public const string TrayIdDebugFileName = "trayId_debug.png";
    public const string LampWeightsDebugFileName = "lampWeights_debug.png";

    private readonly TrayIdTextureGenerator _trayIdTextureGenerator;
    private readonly LampInfluenceTextureGenerator _lampInfluenceTextureGenerator;

    public FaceRuntimeTextureGenerator(
        TrayIdTextureGenerator? trayIdTextureGenerator = null,
        LampInfluenceTextureGenerator? lampInfluenceTextureGenerator = null)
    {
        _trayIdTextureGenerator = trayIdTextureGenerator ?? new TrayIdTextureGenerator();
        _lampInfluenceTextureGenerator = lampInfluenceTextureGenerator ?? new LampInfluenceTextureGenerator();
    }

    public FaceRuntimeTextureGenerationPlan CreatePlan(FaceDocumentModel faceDocument, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ValidateDimensions(width, height);

        var emitters = CreateTemporaryEmitters(faceDocument).ToArray();
        ValidateLampIds(emitters);
        var lampWindowsById = faceDocument.Elements
            .OfType<FaceLampWindowElement>()
            .Where(element => IsValidVisibleLampWindow(element) && !string.IsNullOrWhiteSpace(element.ObjectId))
            .GroupBy(element => element.ObjectId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var trays = emitters
            .Select(emitter => CreateTemporaryTray(emitter, lampWindowsById, faceDocument.MaskLayer))
            .ToArray();

        var overlaps = DetectTrayOwnershipOverlaps(trays, width, height);

        return new FaceRuntimeTextureGenerationPlan(width, height, trays, emitters, overlaps);
    }

    public FaceRuntimeTextureGenerationResult Generate(FaceDocumentModel faceDocument, int width, int height, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Runtime texture output directory is required.", nameof(outputDirectory));
        }

        var plan = CreatePlan(faceDocument, width, height);
        Directory.CreateDirectory(outputDirectory);

        var trayIdPath = Path.Combine(outputDirectory, TrayIdFileName);
        var lampIds0Path = Path.Combine(outputDirectory, LampIds0FileName);
        var lampWeights0Path = Path.Combine(outputDirectory, LampWeights0FileName);
        var trayIdDebugPath = Path.Combine(outputDirectory, TrayIdDebugFileName);
        var lampWeightsDebugPath = Path.Combine(outputDirectory, LampWeightsDebugFileName);

        _trayIdTextureGenerator.Generate(plan.Trays, width, height, trayIdPath, trayIdDebugPath);
        _lampInfluenceTextureGenerator.Generate(plan.Trays, plan.Emitters, width, height, lampIds0Path, lampWeights0Path, lampWeightsDebugPath);

        return new FaceRuntimeTextureGenerationResult(
            plan,
            trayIdPath,
            lampIds0Path,
            lampWeights0Path,
            trayIdDebugPath,
            lampWeightsDebugPath);
    }


    private static FaceRuntimeTrayElement CreateTemporaryTray(
        FaceLampEmitterElement emitter,
        IReadOnlyDictionary<string, FaceLampWindowElement> lampWindowsById,
        FaceMaskLayerModel? maskLayer)
    {
        var bounds = ResolveTemporaryTrayBounds(emitter, lampWindowsById, maskLayer);
        return new FaceRuntimeTrayElement
        {
            TrayId = emitter.TrayId,
            ObjectId = CreateTemporaryTrayObjectId(emitter),
            SourceLampWindowObjectId = emitter.SourceLampWindowObjectId,
            Name = string.IsNullOrWhiteSpace(emitter.Name) ? $"Tray {emitter.TrayId}" : emitter.Name,
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            LampEmitterObjectId = emitter.ObjectId,
            LampId = emitter.LampId
        };
    }

    private static RuntimeRect ResolveTemporaryTrayBounds(
        FaceLampEmitterElement emitter,
        IReadOnlyDictionary<string, FaceLampWindowElement> lampWindowsById,
        FaceMaskLayerModel? maskLayer)
    {
        if (lampWindowsById.TryGetValue(emitter.SourceLampWindowObjectId, out var lampWindow))
        {
            var contribution = FindMaskContribution(lampWindow, maskLayer);
            if (contribution?.Bounds is { IsValid: true } contributionBounds)
            {
                return new RuntimeRect(
                    contributionBounds.X,
                    contributionBounds.Y,
                    contributionBounds.Width,
                    contributionBounds.Height);
            }
        }

        return new RuntimeRect(emitter.X, emitter.Y, emitter.Width, emitter.Height);
    }

    private static FaceMaskContributionModel? FindMaskContribution(FaceLampWindowElement lampWindow, FaceMaskLayerModel? maskLayer)
    {
        if (maskLayer is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(lampWindow.LinkedPanel2DElementId))
        {
            var linkedPanelElementId = lampWindow.LinkedPanel2DElementId.Trim();
            var contribution = maskLayer.Contributions.FirstOrDefault(candidate =>
                string.Equals(candidate.SourcePanel2DElementId, linkedPanelElementId, StringComparison.Ordinal));
            if (contribution is not null)
            {
                return contribution;
            }
        }

        var reference = lampWindow.LinkedMachineObjectReference?.ToString();
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        return maskLayer.Contributions.FirstOrDefault(candidate =>
            string.Equals(candidate.LinkedMachineObjectReference?.ToString(), reference, StringComparison.Ordinal));
    }

    public static IEnumerable<FaceLampEmitterElement> CreateTemporaryEmitters(FaceDocumentModel faceDocument)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);

        var trayId = 1;
        foreach (var lampWindow in faceDocument.Elements
            .OfType<FaceLampWindowElement>()
            .Where(IsValidVisibleLampWindow)
            .OrderBy(element => element.ObjectId, StringComparer.Ordinal)
            .ThenBy(element => element.Name, StringComparer.Ordinal))
        {
            var lampId = TryGetIntId(lampWindow.LinkedMachineObjectReference, MachineObjectKind.Lamp);
            yield return new FaceLampEmitterElement
            {
                ObjectId = CreateTemporaryEmitterObjectId(lampWindow, trayId),
                Name = string.IsNullOrWhiteSpace(lampWindow.Name) ? $"Lamp {lampId?.ToString() ?? trayId.ToString()} Emitter" : $"{lampWindow.Name} Emitter",
                X = lampWindow.X,
                Y = lampWindow.Y,
                Width = lampWindow.Width,
                Height = lampWindow.Height,
                IsVisible = lampWindow.IsVisible,
                IsLocked = true,
                LinkedMachineObjectReference = lampWindow.LinkedMachineObjectReference,
                LinkedPanel2DElementId = lampWindow.LinkedPanel2DElementId,
                SourceLampWindowObjectId = lampWindow.ObjectId,
                TrayId = trayId,
                LampId = lampId,
                CenterX = lampWindow.X + (lampWindow.Width / 2d),
                CenterY = lampWindow.Y + (lampWindow.Height / 2d)
            };
            trayId++;
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Runtime texture output dimensions must be positive. Actual: {width}x{height}.");
        }
    }

    private static void ValidateLampIds(IReadOnlyList<FaceLampEmitterElement> emitters)
    {
        foreach (var emitter in emitters)
        {
            if (emitter.LampId is not int lampId || lampId <= 0 || lampId > byte.MaxValue)
            {
                throw new InvalidOperationException($"Face lamp emitter '{emitter.ObjectId}' has invalid lamp ID '{emitter.LampId?.ToString() ?? "<missing>"}'. Phase 3a lamp ID textures require IDs from 1 to 255.");
            }
        }
    }

    private static IReadOnlyList<FaceRuntimeTrayOverlap> DetectTrayOwnershipOverlaps(IReadOnlyList<FaceRuntimeTrayElement> trays, int width, int height)
    {
        const int maxRecordedOverlaps = 100;
        var overlaps = new List<FaceRuntimeTrayOverlap>();
        var ownership = new int[width * height];
        foreach (var tray in trays)
        {
            var bounds = RasterBounds.FromElement(tray, width, height);
            for (var y = bounds.Top; y < bounds.Bottom; y++)
            {
                for (var x = bounds.Left; x < bounds.Right; x++)
                {
                    var index = (y * width) + x;
                    var existing = ownership[index];
                    if (existing != 0)
                    {
                        if (overlaps.Count < maxRecordedOverlaps)
                        {
                            overlaps.Add(new FaceRuntimeTrayOverlap(x, y, existing, tray.TrayId));
                        }

                        continue;
                    }

                    ownership[index] = tray.TrayId;
                }
            }
        }

        return overlaps;
    }

    private static bool IsValidVisibleLampWindow(FaceLampWindowElement element)
    {
        return element.IsVisible && element.Width > 0d && element.Height > 0d;
    }

    private static string CreateTemporaryEmitterObjectId(FaceLampWindowElement lampWindow, int trayId)
    {
        var sourceId = string.IsNullOrWhiteSpace(lampWindow.ObjectId) ? trayId.ToString() : lampWindow.ObjectId.Trim();
        return $"runtime-emitter-{sourceId}";
    }

    private static string CreateTemporaryTrayObjectId(FaceLampEmitterElement emitter)
    {
        var sourceId = string.IsNullOrWhiteSpace(emitter.SourceLampWindowObjectId) ? emitter.TrayId.ToString() : emitter.SourceLampWindowObjectId.Trim();
        return $"runtime-tray-{sourceId}";
    }

    internal static int? TryGetIntId(MachineObjectReference? reference, MachineObjectKind expectedKind)
    {
        return reference is MachineObjectReference machineReference
            && machineReference.Kind == expectedKind
            && int.TryParse(machineReference.Id, out var id)
            ? id
            : null;
    }
}

public sealed class TrayIdTextureGenerator
{
    public void Generate(
        IReadOnlyList<FaceRuntimeTrayElement> trays,
        int width,
        int height,
        string trayIdPath,
        string trayIdDebugPath)
    {
        ArgumentNullException.ThrowIfNull(trays);
        using var trayBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var debugBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        trayBitmap.Erase(SKColors.Transparent);
        debugBitmap.Erase(SKColors.Transparent);

        var ownership = new int[width * height];
        foreach (var tray in trays)
        {
            var bounds = RasterBounds.FromElement(tray, width, height);
            var debugColor = ColorForTray(tray.TrayId);
            for (var y = bounds.Top; y < bounds.Bottom; y++)
            {
                for (var x = bounds.Left; x < bounds.Right; x++)
                {
                    var index = (y * width) + x;
                    if (ownership[index] != 0)
                    {
                        continue;
                    }

                    ownership[index] = tray.TrayId;
                    trayBitmap.SetPixel(x, y, new SKColor((byte)tray.TrayId, 0, 0, 255));
                    debugBitmap.SetPixel(x, y, debugColor);
                }
            }
        }

        WritePng(trayBitmap, trayIdPath, "tray ID texture");
        WritePng(debugBitmap, trayIdDebugPath, "tray ID debug texture");
    }

    private static SKColor ColorForTray(int trayId)
    {
        var hue = (float)((trayId * 137) % 360);
        return SKColor.FromHsl(hue, 85f, 55f, 255);
    }

    private static void WritePng(SKBitmap bitmap, string path, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            throw new IOException($"Face runtime {description} could not be encoded as PNG.");
        }

        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }
}

public sealed class LampInfluenceTextureGenerator
{
    public void Generate(
        IReadOnlyList<FaceRuntimeTrayElement> trays,
        IReadOnlyList<FaceLampEmitterElement> emitters,
        int width,
        int height,
        string lampIds0Path,
        string lampWeights0Path,
        string lampWeightsDebugPath)
    {
        ArgumentNullException.ThrowIfNull(trays);
        ArgumentNullException.ThrowIfNull(emitters);
        var emittersByTray = emitters.ToDictionary(emitter => emitter.TrayId);

        using var idsBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var weightsBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var debugBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        idsBitmap.Erase(SKColors.Transparent);
        weightsBitmap.Erase(SKColors.Transparent);
        debugBitmap.Erase(SKColors.Transparent);

        var ownership = new int[width * height];
        foreach (var tray in trays)
        {
            if (!emittersByTray.TryGetValue(tray.TrayId, out var emitter) || emitter.LampId is not int lampId)
            {
                throw new InvalidOperationException($"Face runtime tray {tray.TrayId} does not have a valid lamp emitter.");
            }

            var bounds = RasterBounds.FromElement(tray, width, height);
            for (var y = bounds.Top; y < bounds.Bottom; y++)
            {
                for (var x = bounds.Left; x < bounds.Right; x++)
                {
                    var index = (y * width) + x;
                    if (ownership[index] != 0)
                    {
                        continue;
                    }

                    ownership[index] = tray.TrayId;
                    idsBitmap.SetPixel(x, y, new SKColor((byte)lampId, 0, 0, 255));
                    weightsBitmap.SetPixel(x, y, new SKColor(255, 0, 0, 255));
                    debugBitmap.SetPixel(x, y, SKColors.White);
                }
            }
        }

        WritePng(idsBitmap, lampIds0Path, "lamp ID texture");
        WritePng(weightsBitmap, lampWeights0Path, "lamp weight texture");
        WritePng(debugBitmap, lampWeightsDebugPath, "lamp weight debug texture");
    }

    private static void WritePng(SKBitmap bitmap, string path, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            throw new IOException($"Face runtime {description} could not be encoded as PNG.");
        }

        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }
}

public sealed record FaceRuntimeTextureGenerationPlan(
    int Width,
    int Height,
    IReadOnlyList<FaceRuntimeTrayElement> Trays,
    IReadOnlyList<FaceLampEmitterElement> Emitters,
    IReadOnlyList<FaceRuntimeTrayOverlap> Overlaps);

public sealed record FaceRuntimeTrayOverlap(int X, int Y, int ExistingTrayId, int OverlappingTrayId);

public sealed record FaceRuntimeTextureGenerationResult(
    FaceRuntimeTextureGenerationPlan Plan,
    string TrayIdPath,
    string LampIds0Path,
    string LampWeights0Path,
    string TrayIdDebugPath,
    string LampWeightsDebugPath);

public sealed class FaceRuntimeTrayElement
{
    public int TrayId { get; init; }
    public string ObjectId { get; init; } = string.Empty;
    public string SourceLampWindowObjectId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public string LampEmitterObjectId { get; init; } = string.Empty;
    public int? LampId { get; init; }
}

internal readonly record struct RuntimeRect(double X, double Y, double Width, double Height);

internal readonly record struct RasterBounds(int Left, int Top, int Right, int Bottom)
{
    public static RasterBounds FromElement(FaceRuntimeTrayElement element, int width, int height)
    {
        var left = Math.Clamp((int)Math.Floor(element.X), 0, width);
        var top = Math.Clamp((int)Math.Floor(element.Y), 0, height);
        var right = Math.Clamp((int)Math.Ceiling(element.X + element.Width), left, width);
        var bottom = Math.Clamp((int)Math.Ceiling(element.Y + element.Height), top, height);
        return new RasterBounds(left, top, right, bottom);
    }
}
