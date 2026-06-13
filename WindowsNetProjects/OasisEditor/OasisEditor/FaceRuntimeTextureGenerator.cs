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

        FaceRuntimeTrayElement[] trays;
        FaceLampEmitterElement[] emitters;
        var exportSource = ResolveExportSource(faceDocument);
        if (exportSource == FaceRuntimeTextureExportSource.Authored)
        {
            ValidateAuthoredExportData(faceDocument);
            emitters = faceDocument.LampEmitters
                .OrderBy(emitter => emitter.TrayId)
                .ThenBy(emitter => emitter.ObjectId, StringComparer.Ordinal)
                .ToArray();
            ValidateLampIds(emitters);
            var emittersByTrayObjectId = emitters
                .GroupBy(emitter => emitter.TrayObjectId.Trim(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(emitter => emitter.ObjectId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            trays = faceDocument.Trays
                .OrderBy(tray => emittersByTrayObjectId[tray.ObjectId.Trim()][0].TrayId)
                .ThenBy(tray => tray.ObjectId, StringComparer.Ordinal)
                .Select(tray => CreateAuthoredTray(tray, emittersByTrayObjectId[tray.ObjectId.Trim()][0]))
                .ToArray();
        }
        else
        {
            emitters = CreateTemporaryEmitters(faceDocument).ToArray();
            ValidateLampIds(emitters);
            var lampWindowsById = faceDocument.Elements
                .OfType<FaceLampWindowElement>()
                .Where(element => IsValidVisibleLampWindow(element) && !string.IsNullOrWhiteSpace(element.ObjectId))
                .GroupBy(element => element.ObjectId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            trays = emitters
                .Select(emitter => CreateTemporaryTray(emitter, lampWindowsById, faceDocument.MaskLayer))
                .ToArray();
        }

        var overlaps = DetectTrayOwnershipOverlaps(trays, width, height);

        return new FaceRuntimeTextureGenerationPlan(width, height, trays, emitters, overlaps, exportSource);
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


    private static FaceRuntimeTextureExportSource ResolveExportSource(FaceDocumentModel faceDocument)
    {
        var hasAuthoredTrays = faceDocument.Trays.Count > 0;
        var hasAuthoredEmitters = faceDocument.LampEmitters.Count > 0;
        return hasAuthoredTrays || hasAuthoredEmitters
            ? FaceRuntimeTextureExportSource.Authored
            : FaceRuntimeTextureExportSource.LampWindowBridge;
    }

    private static FaceRuntimeTrayElement CreateAuthoredTray(FaceTrayModel tray, FaceLampEmitterElement emitter)
    {
        var bounds = tray.Bounds!;
        return new FaceRuntimeTrayElement
        {
            TrayId = emitter.TrayId,
            ObjectId = tray.ObjectId.Trim(),
            SourceLampWindowObjectId = string.IsNullOrWhiteSpace(tray.SourceLampWindowObjectId) ? emitter.SourceLampWindowObjectId : tray.SourceLampWindowObjectId.Trim(),
            Name = string.IsNullOrWhiteSpace(tray.Name) ? $"Tray {emitter.TrayId}" : tray.Name,
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Vertices = tray.Vertices.Count > 0 ? tray.Vertices.ToArray() : CreateRectangleVertices(bounds),
            LampEmitterObjectId = emitter.ObjectId,
            LampId = emitter.LampId
        };
    }

    private static IReadOnlyList<FacePointModel> CreateRectangleVertices(FaceSourceRegionModel bounds)
    {
        return
        [
            new FacePointModel { X = bounds.X, Y = bounds.Y },
            new FacePointModel { X = bounds.X + bounds.Width, Y = bounds.Y },
            new FacePointModel { X = bounds.X + bounds.Width, Y = bounds.Y + bounds.Height },
            new FacePointModel { X = bounds.X, Y = bounds.Y + bounds.Height }
        ];
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
            Vertices = [],
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

    private static void ValidateAuthoredExportData(FaceDocumentModel faceDocument)
    {
        var diagnostics = new List<string>();
        if (faceDocument.Trays.Count == 0)
        {
            diagnostics.Add("authored export contains emitters but no trays");
        }

        if (faceDocument.LampEmitters.Count == 0)
        {
            diagnostics.Add("authored export contains trays but no emitters");
        }

        var trayIds = faceDocument.Trays
            .Where(tray => !string.IsNullOrWhiteSpace(tray.ObjectId))
            .Select(tray => tray.ObjectId.Trim())
            .ToArray();
        var trayIdSet = trayIds.ToHashSet(StringComparer.Ordinal);

        AddDuplicateDiagnostics(trayIds, "duplicate tray ID", diagnostics);
        AddDuplicateDiagnostics(
            faceDocument.LampEmitters
                .Where(emitter => !string.IsNullOrWhiteSpace(emitter.ObjectId))
                .Select(emitter => emitter.ObjectId.Trim()),
            "duplicate emitter ID",
            diagnostics);
        foreach (var tray in faceDocument.Trays)
        {
            var displayName = DisplayName(tray.ObjectId, tray.Name);
            if (string.IsNullOrWhiteSpace(tray.ObjectId))
            {
                diagnostics.Add($"tray '{displayName}' is missing an ID");
            }

            if (tray.Bounds is not { IsValid: true } || tray.Bounds.Width <= 0d || tray.Bounds.Height <= 0d)
            {
                diagnostics.Add($"tray '{displayName}' has invalid tray geometry bounds");
            }

            if (tray.Vertices.Count > 0)
            {
                if (tray.Vertices.Count < 3
                    || tray.Vertices.Any(vertex => !PanelElementValidation.IsFinite(vertex.X) || !PanelElementValidation.IsFinite(vertex.Y))
                    || Math.Abs(PolygonArea(tray.Vertices)) <= double.Epsilon)
                {
                    diagnostics.Add($"tray '{displayName}' has invalid tray geometry vertices");
                }
            }
        }

        foreach (var tray in faceDocument.Trays.Where(tray => !string.IsNullOrWhiteSpace(tray.ObjectId)))
        {
            if (!faceDocument.LampEmitters.Any(emitter => string.Equals(emitter.TrayObjectId?.Trim(), tray.ObjectId.Trim(), StringComparison.Ordinal)))
            {
                diagnostics.Add($"tray '{DisplayName(tray.ObjectId, tray.Name)}' does not have an emitter");
            }
        }

        foreach (var emitter in faceDocument.LampEmitters)
        {
            var displayName = DisplayName(emitter.ObjectId, emitter.Name);
            if (string.IsNullOrWhiteSpace(emitter.ObjectId))
            {
                diagnostics.Add($"emitter '{displayName}' is missing an ID");
            }

            if (emitter.TrayId <= 0 || emitter.TrayId > byte.MaxValue)
            {
                diagnostics.Add($"emitter '{displayName}' has invalid tray ID '{emitter.TrayId}'");
            }

            if (string.IsNullOrWhiteSpace(emitter.TrayObjectId) || !trayIdSet.Contains(emitter.TrayObjectId.Trim()))
            {
                diagnostics.Add($"emitter '{displayName}' references missing tray '{emitter.TrayObjectId}'");
            }
        }

        if (diagnostics.Count > 0)
        {
            throw new InvalidOperationException("Face authored runtime texture export data is invalid: " + string.Join("; ", diagnostics) + ".");
        }
    }

    private static void AddDuplicateDiagnostics(IEnumerable<string> ids, string label, List<string> diagnostics)
    {
        foreach (var duplicate in ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            diagnostics.Add($"{label} '{duplicate}'");
        }
    }

    private static double PolygonArea(IReadOnlyList<FacePointModel> vertices)
    {
        var area = 0d;
        for (var index = 0; index < vertices.Count; index++)
        {
            var current = vertices[index];
            var next = vertices[(index + 1) % vertices.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area / 2d;
    }

    private static string DisplayName(string objectId, string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? string.IsNullOrWhiteSpace(objectId) ? "<unnamed>" : objectId.Trim()
            : name.Trim();
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
                    if (!RasterGeometry.ContainsPixelCenter(tray, x, y))
                    {
                        continue;
                    }

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
                    if (ownership[index] != 0 || !RasterGeometry.ContainsPixelCenter(tray, x, y))
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
    private const int SupportedChannelCount = 3;
    private const double MinimumSoftness = 1d;

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
        var emittersByTray = emitters
            .GroupBy(emitter => emitter.TrayId)
            .ToDictionary(group => group.Key, group => group.OrderBy(emitter => emitter.ObjectId, StringComparer.Ordinal).ToArray());

        using var idsBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var weightsBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var debugBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        idsBitmap.Erase(SKColors.Transparent);
        weightsBitmap.Erase(SKColors.Transparent);
        debugBitmap.Erase(SKColors.Transparent);

        var ownership = new int[width * height];
        foreach (var tray in trays)
        {
            if (!emittersByTray.TryGetValue(tray.TrayId, out var trayEmitters) || trayEmitters.Length == 0 || trayEmitters.Any(emitter => emitter.LampId is not int))
            {
                throw new InvalidOperationException($"Face runtime tray {tray.TrayId} does not have a valid lamp emitter.");
            }

            if (trayEmitters.Length > SupportedChannelCount)
            {
                throw new InvalidOperationException($"Face runtime tray {tray.TrayId} has {trayEmitters.Length} lamp emitters; the current lampIds0/lampWeights0 PNG writer preserves RGB data channels plus opaque alpha, so it supports up to 3 emitters per tray.");
            }

            var bounds = RasterBounds.FromElement(tray, width, height);
            var influences = CreateInfluences(tray, trayEmitters);
            for (var y = bounds.Top; y < bounds.Bottom; y++)
            {
                for (var x = bounds.Left; x < bounds.Right; x++)
                {
                    var index = (y * width) + x;
                    if (ownership[index] != 0 || !RasterGeometry.ContainsPixelCenter(tray, x, y))
                    {
                        continue;
                    }

                    ownership[index] = tray.TrayId;
                    var (idChannels, weightChannels) = CreateEmitterChannels(influences, x + 0.5d, y + 0.5d);
                    idsBitmap.SetPixel(x, y, new SKColor(idChannels[0], idChannels[1], idChannels[2], 255));
                    weightsBitmap.SetPixel(x, y, new SKColor(weightChannels[0], weightChannels[1], weightChannels[2], 255));
                    debugBitmap.SetPixel(x, y, new SKColor(weightChannels[0], weightChannels[1], weightChannels[2], 255));
                }
            }
        }

        WritePng(idsBitmap, lampIds0Path, "lamp ID texture");
        WritePng(weightsBitmap, lampWeights0Path, "lamp weight texture");
        WritePng(debugBitmap, lampWeightsDebugPath, "lamp weight debug texture");
    }

    private static IReadOnlyList<LampInfluence> CreateInfluences(FaceRuntimeTrayElement tray, IReadOnlyList<FaceLampEmitterElement> emitters)
    {
        var fallbackRadius = ResolveFallbackRadius(tray, emitters);
        return emitters
            .OrderBy(emitter => emitter.ObjectId, StringComparer.Ordinal)
            .Select((emitter, index) =>
            {
                var radius = emitter.Radius is double emitterRadius && emitterRadius > 0d && IsFinite(emitterRadius)
                    ? emitterRadius
                    : fallbackRadius;
                var radiusSquared = Math.Max(MinimumSoftness, radius * radius);
                return new LampInfluence(
                    (byte)emitter.LampId!.Value,
                    emitter.CenterX,
                    emitter.CenterY,
                    radiusSquared,
                    index);
            })
            .ToArray();
    }

    private static (byte[] IdChannels, byte[] WeightChannels) CreateEmitterChannels(IReadOnlyList<LampInfluence> influences, double pixelX, double pixelY)
    {
        if (influences.Count == 1)
        {
            var idChannels = new byte[SupportedChannelCount];
            var weightChannels = new byte[SupportedChannelCount];
            idChannels[0] = influences[0].LampId;
            weightChannels[0] = 255;
            return (idChannels, weightChannels);
        }

        var retained = influences
            .Select(influence =>
            {
                var dx = pixelX - influence.CenterX;
                var dy = pixelY - influence.CenterY;
                var distanceSquared = (dx * dx) + (dy * dy);
                var rawWeight = Math.Exp(-distanceSquared / (2d * influence.RadiusSquared));
                return new WeightedLampInfluence(influence, rawWeight);
            })
            .OrderBy(influence => influence.Influence.Order)
            .Take(SupportedChannelCount)
            .ToArray();

        var idChannels = new byte[SupportedChannelCount];
        var weightChannels = new byte[SupportedChannelCount];
        var rawByteWeights = retained
            .Select(influence => Math.Clamp(influence.RawWeight * 255d, 0d, 255d))
            .ToArray();
        var totalByteWeight = rawByteWeights.Sum();
        if (totalByteWeight <= 0d || !IsFinite(totalByteWeight))
        {
            return (idChannels, weightChannels);
        }

        var scale = totalByteWeight > 255d ? 255d / totalByteWeight : 1d;
        for (var channel = 0; channel < retained.Length; channel++)
        {
            idChannels[channel] = retained[channel].Influence.LampId;
            weightChannels[channel] = (byte)Math.Clamp(Math.Round(rawByteWeights[channel] * scale, MidpointRounding.AwayFromZero), 0d, 255d);
        }

        ClampTotalWeight(weightChannels);
        return (idChannels, weightChannels);
    }

    private static void ClampTotalWeight(byte[] weightChannels)
    {
        var overflow = weightChannels.Sum(weight => weight) - 255;
        for (var channel = weightChannels.Length - 1; channel >= 0 && overflow > 0; channel--)
        {
            var reduction = Math.Min(weightChannels[channel], overflow);
            weightChannels[channel] -= (byte)reduction;
            overflow -= reduction;
        }
    }

    private static double ResolveFallbackRadius(FaceRuntimeTrayElement tray, IReadOnlyList<FaceLampEmitterElement> emitters)
    {
        var trayRadius = Math.Max(1d, Math.Max(tray.Width, tray.Height) / Math.Max(1d, emitters.Count));
        if (emitters.Count < 2)
        {
            return trayRadius;
        }

        var nearestSpacing = double.PositiveInfinity;
        for (var left = 0; left < emitters.Count; left++)
        {
            for (var right = left + 1; right < emitters.Count; right++)
            {
                var dx = emitters[left].CenterX - emitters[right].CenterX;
                var dy = emitters[left].CenterY - emitters[right].CenterY;
                var spacing = Math.Sqrt((dx * dx) + (dy * dy));
                if (spacing > 0d && spacing < nearestSpacing)
                {
                    nearestSpacing = spacing;
                }
            }
        }

        return IsFinite(nearestSpacing)
            ? Math.Max(1d, Math.Min(trayRadius, nearestSpacing))
            : trayRadius;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private readonly record struct LampInfluence(byte LampId, double CenterX, double CenterY, double RadiusSquared, int Order);

    private readonly record struct WeightedLampInfluence(LampInfluence Influence, double RawWeight);

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

public enum FaceRuntimeTextureExportSource
{
    LampWindowBridge,
    Authored
}

public sealed record FaceRuntimeTextureGenerationPlan(
    int Width,
    int Height,
    IReadOnlyList<FaceRuntimeTrayElement> Trays,
    IReadOnlyList<FaceLampEmitterElement> Emitters,
    IReadOnlyList<FaceRuntimeTrayOverlap> Overlaps,
    FaceRuntimeTextureExportSource ExportSource);

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
    public IReadOnlyList<FacePointModel> Vertices { get; init; } = [];
    public string LampEmitterObjectId { get; init; } = string.Empty;
    public int? LampId { get; init; }
}

internal readonly record struct RuntimeRect(double X, double Y, double Width, double Height);


internal static class RasterGeometry
{
    public static bool ContainsPixelCenter(FaceRuntimeTrayElement tray, int x, int y)
    {
        if (tray.Vertices.Count == 0)
        {
            return true;
        }

        return ContainsPoint(tray.Vertices, x + 0.5d, y + 0.5d);
    }

    private static bool ContainsPoint(IReadOnlyList<FacePointModel> vertices, double x, double y)
    {
        var inside = false;
        var previous = vertices.Count - 1;
        for (var current = 0; current < vertices.Count; current++)
        {
            var currentVertex = vertices[current];
            var previousVertex = vertices[previous];
            if (((currentVertex.Y > y) != (previousVertex.Y > y))
                && (x < ((previousVertex.X - currentVertex.X) * (y - currentVertex.Y) / (previousVertex.Y - currentVertex.Y)) + currentVertex.X))
            {
                inside = !inside;
            }

            previous = current;
        }

        return inside;
    }
}

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
