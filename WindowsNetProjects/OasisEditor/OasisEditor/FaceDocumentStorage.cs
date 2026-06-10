using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor;

public static class FaceDocumentStorage
{
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions s_readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static FaceDocumentFile CreateEmpty(string title)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "Face" : title.Trim();
        return new FaceDocumentFile
        {
            SchemaVersion = CurrentSchemaVersion,
            Id = Guid.NewGuid().ToString("N"),
            Title = resolvedTitle,
            Summary = "Face document placeholder.",
            SavedAtUtc = DateTime.UtcNow,
            Layers =
            [
                new FaceLayerFile
                {
                    Id = "layer-artwork",
                    Name = "Artwork",
                    IsVisible = true
                },
                new FaceLayerFile
                {
                    Id = "layer-face-mask",
                    Name = "Face Mask",
                    IsVisible = true
                },
                new FaceLayerFile
                {
                    Id = "layer-runtime-lamps",
                    Name = "Runtime Lamps",
                    IsVisible = true
                },
                new FaceLayerFile
                {
                    Id = "layer-displays",
                    Name = "Displays",
                    IsVisible = true
                },
                new FaceLayerFile
                {
                    Id = "layer-buttons",
                    Name = "Buttons",
                    IsVisible = true
                }
            ],
            Elements = []
        };
    }

    public static string Serialize(FaceDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Serialize(ToFile(model));
    }

    public static string Serialize(FaceDocumentFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var normalized = file with
        {
            SchemaVersion = CurrentSchemaVersion,
            SavedAtUtc = DateTime.UtcNow
        };
        return JsonSerializer.Serialize(normalized, s_writeOptions);
    }

    public static bool TryRead(string? json, out FaceDocumentFile file)
    {
        file = new FaceDocumentFile();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<FaceDocumentFile>(json, s_readOptions);
            if (parsed is null)
            {
                return false;
            }

            file = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryReadValidated(string? json, out FaceDocumentFile file, out string errorMessage)
    {
        file = new FaceDocumentFile();
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "Face document is empty.";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<FaceDocumentFile>(json, s_readOptions);
            if (parsed is null)
            {
                errorMessage = "Face document could not be parsed.";
                return false;
            }

            if (parsed.SchemaVersion > CurrentSchemaVersion)
            {
                errorMessage = $"Unsupported face schema version '{parsed.SchemaVersion}'. This editor supports up to version {CurrentSchemaVersion}.";
                return false;
            }

            file = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"Malformed JSON: {ex.Message}";
            return false;
        }
    }

    public static FaceDocumentModel ToModel(FaceDocumentFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new FaceDocumentModel
        {
            Id = string.IsNullOrWhiteSpace(file.Id) ? Guid.NewGuid().ToString("N") : file.Id.Trim(),
            Title = file.Title ?? string.Empty,
            Summary = file.Summary,
            SourcePanel2DDocumentId = string.IsNullOrWhiteSpace(file.SourcePanel2DDocumentId) ? null : file.SourcePanel2DDocumentId.Trim(),
            SourceRegion = ToModel(file.SourceRegion),
            LastRegeneratedAtUtc = file.LastRegeneratedAtUtc,
            RuntimeRenderAssets = ToModel(file.RuntimeRenderAssets),
            MaskLayer = ToModel(file.MaskLayer),
            Layers = (file.Layers ?? [])
                .Select(layer => new FaceLayerModel
                {
                    Id = string.IsNullOrWhiteSpace(layer.Id) ? Guid.NewGuid().ToString("N") : layer.Id.Trim(),
                    Name = layer.Name ?? string.Empty,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked
                })
                .ToArray(),
            Elements = (file.Elements ?? [])
                .Select(ToModel)
                .ToArray()
        };
    }

    public static FaceDocumentFile ToFile(FaceDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new FaceDocumentFile
        {
            SchemaVersion = CurrentSchemaVersion,
            Id = string.IsNullOrWhiteSpace(model.Id) ? Guid.NewGuid().ToString("N") : model.Id.Trim(),
            Title = model.Title,
            Summary = model.Summary,
            SourcePanel2DDocumentId = model.SourcePanel2DDocumentId,
            SourceRegion = ToFile(model.SourceRegion),
            LastRegeneratedAtUtc = model.LastRegeneratedAtUtc,
            RuntimeRenderAssets = ToFile(model.RuntimeRenderAssets),
            MaskLayer = ToFile(model.MaskLayer),
            SavedAtUtc = DateTime.UtcNow,
            Layers = model.Layers.Select(layer => new FaceLayerFile
            {
                Id = layer.Id,
                Name = layer.Name,
                IsVisible = layer.IsVisible,
                IsLocked = layer.IsLocked
            }).ToArray(),
            Elements = model.Elements.Select(ToFile).ToArray()
        };
    }

    private static FaceRuntimeRenderAssetsModel? ToModel(FaceRuntimeRenderAssetsFile? file)
    {
        if (file is null)
        {
            return null;
        }

        return new FaceRuntimeRenderAssetsModel
        {
            ManifestPath = file.ManifestPath,
            ArtworkPath = file.ArtworkPath,
            MaskPath = file.MaskPath,
            TrayIdPath = file.TrayIdPath,
            LampIds0Path = file.LampIds0Path,
            LampWeights0Path = file.LampWeights0Path,
            LampIds1Path = file.LampIds1Path,
            LampWeights1Path = file.LampWeights1Path,
            TrayIdDebugPath = file.TrayIdDebugPath,
            LampWeightsDebugPath = file.LampWeightsDebugPath,
            Width = file.Width,
            Height = file.Height,
            GeneratedUtc = file.GeneratedUtc
        };
    }

    private static FaceRuntimeRenderAssetsFile? ToFile(FaceRuntimeRenderAssetsModel? model)
    {
        if (model is null)
        {
            return null;
        }

        return new FaceRuntimeRenderAssetsFile
        {
            ManifestPath = model.ManifestPath,
            ArtworkPath = model.ArtworkPath,
            MaskPath = model.MaskPath,
            TrayIdPath = model.TrayIdPath,
            LampIds0Path = model.LampIds0Path,
            LampWeights0Path = model.LampWeights0Path,
            LampIds1Path = model.LampIds1Path,
            LampWeights1Path = model.LampWeights1Path,
            TrayIdDebugPath = model.TrayIdDebugPath,
            LampWeightsDebugPath = model.LampWeightsDebugPath,
            Width = model.Width,
            Height = model.Height,
            GeneratedUtc = model.GeneratedUtc
        };
    }

    private static FaceMaskLayerModel? ToModel(FaceMaskLayerFile? file)
    {
        if (file is null)
        {
            return null;
        }

        return new FaceMaskLayerModel
        {
            Id = string.IsNullOrWhiteSpace(file.Id) ? "face-mask-layer" : file.Id.Trim(),
            Name = string.IsNullOrWhiteSpace(file.Name) ? "Face Mask" : file.Name.Trim(),
            AssetPath = file.AssetPath,
            SourcePanel2DDocumentId = string.IsNullOrWhiteSpace(file.SourcePanel2DDocumentId) ? null : file.SourcePanel2DDocumentId.Trim(),
            SourceRegion = ToModel(file.SourceRegion),
            ExtractionThreshold = file.ExtractionThreshold,
            GeneratedUtc = file.GeneratedUtc,
            Width = file.Width,
            Height = file.Height,
            Contributions = (file.Contributions ?? [])
                .Select(ToModel)
                .ToArray()
        };
    }

    private static FaceMaskContributionModel ToModel(FaceMaskContributionFile file)
    {
        MachineObjectReference? reference = null;
        if (MachineObjectReference.TryParse(file.LinkedMachineObjectReference, out var parsedReference))
        {
            reference = parsedReference;
        }

        return new FaceMaskContributionModel
        {
            SourcePanel2DElementId = file.SourcePanel2DElementId,
            LinkedMachineObjectReference = reference,
            Bounds = ToModel(file.Bounds),
            PixelCount = file.PixelCount
        };
    }

    private static FaceMaskLayerFile? ToFile(FaceMaskLayerModel? model)
    {
        if (model is null)
        {
            return null;
        }

        return new FaceMaskLayerFile
        {
            Id = model.Id,
            Name = model.Name,
            AssetPath = model.AssetPath,
            SourcePanel2DDocumentId = model.SourcePanel2DDocumentId,
            SourceRegion = ToFile(model.SourceRegion),
            ExtractionThreshold = model.ExtractionThreshold,
            GeneratedUtc = model.GeneratedUtc,
            Width = model.Width,
            Height = model.Height,
            Contributions = model.Contributions.Select(ToFile).ToArray()
        };
    }

    private static FaceMaskContributionFile ToFile(FaceMaskContributionModel model)
    {
        return new FaceMaskContributionFile
        {
            SourcePanel2DElementId = model.SourcePanel2DElementId,
            LinkedMachineObjectReference = model.LinkedMachineObjectReference?.ToString(),
            Bounds = ToFile(model.Bounds),
            PixelCount = model.PixelCount
        };
    }

    private static FaceSourceRegionModel? ToModel(FaceSourceRegionFile? file)
    {
        if (file is null)
        {
            return null;
        }

        var model = new FaceSourceRegionModel
        {
            Kind = file.Kind,
            X = file.X,
            Y = file.Y,
            Width = file.Width,
            Height = file.Height
        };

        return model.IsValid ? model : null;
    }

    private static FaceSourceRegionFile? ToFile(FaceSourceRegionModel? model)
    {
        if (model is null)
        {
            return null;
        }

        return new FaceSourceRegionFile
        {
            Kind = model.Kind,
            X = model.X,
            Y = model.Y,
            Width = model.Width,
            Height = model.Height
        };
    }

    private static FaceElementModel ToModel(FaceElementFile file)
    {
        MachineObjectReference? reference = null;
        if (MachineObjectReference.TryParse(file.LinkedMachineObjectReference, out var parsedReference))
        {
            reference = parsedReference;
        }

        if (string.Equals(file.Kind, "artwork", StringComparison.OrdinalIgnoreCase))
        {
            return new FaceArtworkElement
            {
                ObjectId = file.ObjectId ?? string.Empty,
                Name = file.Name ?? string.Empty,
                X = file.X,
                Y = file.Y,
                Width = file.Width,
                Height = file.Height,
                IsVisible = file.IsVisible,
                IsLocked = file.IsLocked,
                LinkedMachineObjectReference = reference,
                LinkedPanel2DElementId = file.LinkedPanel2DElementId,
                AssetPath = file.AssetPath,
                SourcePanel2DDocumentId = file.SourcePanel2DDocumentId,
                SourceRegion = ToModel(file.SourceRegion),
                Provenance = ToModel(file.ArtworkProvenance)
            };
        }

        if (string.Equals(file.Kind, "reelDisplay", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.Kind, "reel", StringComparison.OrdinalIgnoreCase))
        {
            return new FaceReelDisplayElement
            {
                ObjectId = file.ObjectId ?? string.Empty,
                Name = file.Name ?? string.Empty,
                X = file.X,
                Y = file.Y,
                Width = file.Width,
                Height = file.Height,
                IsVisible = file.IsVisible,
                IsLocked = file.IsLocked,
                LinkedMachineObjectReference = reference,
                LinkedPanel2DElementId = file.LinkedPanel2DElementId,
                AssetPath = file.AssetPath,
                Stops = file.Stops,
                VisibleScale = file.VisibleScale,
                BandOffset = file.BandOffset,
                IsReversed = file.IsReversed
            };
        }

        if (string.Equals(file.Kind, "sevenSegmentDisplay", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.Kind, "sevenSegment", StringComparison.OrdinalIgnoreCase))
        {
            return new FaceSevenSegmentDisplayElement
            {
                ObjectId = file.ObjectId ?? string.Empty,
                Name = file.Name ?? string.Empty,
                X = file.X,
                Y = file.Y,
                Width = file.Width,
                Height = file.Height,
                IsVisible = file.IsVisible,
                IsLocked = file.IsLocked,
                LinkedMachineObjectReference = reference,
                LinkedPanel2DElementId = file.LinkedPanel2DElementId,
                OnColorHex = file.OnColorHex,
                OffColorHex = file.OffColorHex,
                ShowDecimalPoint = file.ShowDecimalPoint
            };
        }

        if (string.Equals(file.Kind, "alphaDisplay", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.Kind, "alpha", StringComparison.OrdinalIgnoreCase))
        {
            return new FaceAlphaDisplayElement
            {
                ObjectId = file.ObjectId ?? string.Empty,
                Name = file.Name ?? string.Empty,
                X = file.X,
                Y = file.Y,
                Width = file.Width,
                Height = file.Height,
                IsVisible = file.IsVisible,
                IsLocked = file.IsLocked,
                LinkedMachineObjectReference = reference,
                LinkedPanel2DElementId = file.LinkedPanel2DElementId,
                SegmentDisplayType = file.SegmentDisplayType,
                OnColorHex = file.OnColorHex,
                OffColorHex = file.OffColorHex,
                ShowDecimalPoint = file.ShowDecimalPoint,
                ShowCommaTail = file.ShowCommaTail,
                IsReversed = file.IsReversed
            };
        }

        if (string.Equals(file.Kind, "button", StringComparison.OrdinalIgnoreCase))
        {
            var linkedInputReference = ResolveInputReference(file.LinkedInputReference, reference);
            return new FaceButtonElement
            {
                ObjectId = file.ObjectId ?? string.Empty,
                Name = file.Name ?? string.Empty,
                X = file.X,
                Y = file.Y,
                Width = file.Width,
                Height = file.Height,
                IsVisible = file.IsVisible,
                IsLocked = file.IsLocked,
                LinkedMachineObjectReference = linkedInputReference?.Reference ?? reference,
                LinkedPanel2DElementId = file.LinkedPanel2DElementId,
                LinkedInputReference = linkedInputReference
            };
        }

        return new FaceLampWindowElement
        {
            ObjectId = file.ObjectId ?? string.Empty,
            Name = file.Name ?? string.Empty,
            X = file.X,
            Y = file.Y,
            Width = file.Width,
            Height = file.Height,
            IsVisible = file.IsVisible,
            IsLocked = file.IsLocked,
            LinkedMachineObjectReference = reference,
            LinkedPanel2DElementId = file.LinkedPanel2DElementId
        };
    }

    private static MachineInputReference? ResolveInputReference(string? linkedInputReference, MachineObjectReference? linkedMachineObjectReference)
    {
        if (MachineObjectReference.TryParse(linkedInputReference, out var parsedInputReference)
            && parsedInputReference.Kind == MachineObjectKind.Input)
        {
            return new MachineInputReference(parsedInputReference);
        }

        if (linkedMachineObjectReference is MachineObjectReference reference
            && reference.Kind == MachineObjectKind.Input
            && !reference.IsEmpty)
        {
            return new MachineInputReference(reference);
        }

        return null;
    }

    private static FaceArtworkProvenanceModel? ToModel(FaceArtworkProvenanceFile? file)
    {
        if (file is null)
        {
            return null;
        }

        return new FaceArtworkProvenanceModel
        {
            Generator = file.Generator ?? string.Empty,
            GeneratedAtUtc = file.GeneratedAtUtc,
            SourcePanel2DElementId = file.SourcePanel2DElementId,
            SourcePanel2DElementKind = file.SourcePanel2DElementKind,
            SourceAssetPath = file.SourceAssetPath,
            SourceElementBounds = ToModel(file.SourceElementBounds)
        };
    }

    private static FaceElementFile ToFile(FaceElementModel model)
    {
        return new FaceElementFile
        {
            ObjectId = model.ObjectId,
            Name = model.Name,
            Kind = model switch
            {
                FaceArtworkElement => "artwork",
                FaceButtonElement => "button",
                FaceReelDisplayElement => "reelDisplay",
                FaceAlphaDisplayElement => "alphaDisplay",
                FaceSevenSegmentDisplayElement => "sevenSegmentDisplay",
                FaceLampWindowElement => "lampWindow",
                _ => "unknown"
            },
            X = model.X,
            Y = model.Y,
            Width = model.Width,
            Height = model.Height,
            IsVisible = model.IsVisible,
            IsLocked = model.IsLocked,
            LinkedMachineObjectReference = model.LinkedMachineObjectReference?.ToString(),
            LinkedPanel2DElementId = model.LinkedPanel2DElementId,
            LinkedInputReference = model is FaceButtonElement button ? button.LinkedInputReference?.ToString() : null,
            OnColorHex = model switch { FaceSevenSegmentDisplayElement sevenSegment => sevenSegment.OnColorHex, FaceAlphaDisplayElement alpha => alpha.OnColorHex, _ => null },
            OffColorHex = model switch { FaceSevenSegmentDisplayElement sevenSegment => sevenSegment.OffColorHex, FaceAlphaDisplayElement alpha => alpha.OffColorHex, _ => null },
            ShowDecimalPoint = model switch { FaceSevenSegmentDisplayElement sevenSegment => sevenSegment.ShowDecimalPoint, FaceAlphaDisplayElement alpha => alpha.ShowDecimalPoint, _ => false },
            ShowCommaTail = model is FaceAlphaDisplayElement alphaComma && alphaComma.ShowCommaTail,
            IsReversed = model switch { FaceAlphaDisplayElement alphaReversed => alphaReversed.IsReversed, FaceReelDisplayElement reelReversed => reelReversed.IsReversed, _ => false },
            SegmentDisplayType = model is FaceAlphaDisplayElement alphaDisplay ? alphaDisplay.SegmentDisplayType : null,
            Stops = model is FaceReelDisplayElement reelDisplay ? reelDisplay.Stops : null,
            VisibleScale = model is FaceReelDisplayElement reelVisibleScale ? reelVisibleScale.VisibleScale : null,
            BandOffset = model is FaceReelDisplayElement reelBandOffset ? reelBandOffset.BandOffset : null,
            AssetPath = model switch { FaceArtworkElement artwork => artwork.AssetPath, FaceReelDisplayElement reel => reel.AssetPath, _ => null },
            SourcePanel2DDocumentId = model is FaceArtworkElement artworkSource ? artworkSource.SourcePanel2DDocumentId : null,
            SourceRegion = model is FaceArtworkElement artworkRegion ? ToFile(artworkRegion.SourceRegion) : null,
            ArtworkProvenance = model is FaceArtworkElement artworkProvenance ? ToFile(artworkProvenance.Provenance) : null
        };
    }

    private static FaceArtworkProvenanceFile? ToFile(FaceArtworkProvenanceModel? model)
    {
        if (model is null)
        {
            return null;
        }

        return new FaceArtworkProvenanceFile
        {
            Generator = model.Generator,
            GeneratedAtUtc = model.GeneratedAtUtc,
            SourcePanel2DElementId = model.SourcePanel2DElementId,
            SourcePanel2DElementKind = model.SourcePanel2DElementKind,
            SourceAssetPath = model.SourceAssetPath,
            SourceElementBounds = ToFile(model.SourceElementBounds)
        };
    }
}

public sealed record FaceDocumentFile
{
    public int SchemaVersion { get; init; } = FaceDocumentStorage.CurrentSchemaVersion;
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? SourcePanel2DDocumentId { get; init; }
    public FaceSourceRegionFile? SourceRegion { get; init; }
    public DateTime? LastRegeneratedAtUtc { get; init; }
    public FaceRuntimeRenderAssetsFile? RuntimeRenderAssets { get; init; }
    public FaceMaskLayerFile? MaskLayer { get; init; }
    public DateTime SavedAtUtc { get; init; }
    public IReadOnlyList<FaceLayerFile>? Layers { get; init; } = [];
    public IReadOnlyList<FaceElementFile>? Elements { get; init; } = [];
}

public sealed record FaceRuntimeRenderAssetsFile
{
    public string? ManifestPath { get; init; }
    public string? ArtworkPath { get; init; }
    public string? MaskPath { get; init; }
    public string? TrayIdPath { get; init; }
    public string? LampIds0Path { get; init; }
    public string? LampWeights0Path { get; init; }
    public string? LampIds1Path { get; init; }
    public string? LampWeights1Path { get; init; }
    public string? TrayIdDebugPath { get; init; }
    public string? LampWeightsDebugPath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record FaceMaskLayerFile
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? AssetPath { get; init; }
    public string? SourcePanel2DDocumentId { get; init; }
    public FaceSourceRegionFile? SourceRegion { get; init; }
    public byte ExtractionThreshold { get; init; }
    public DateTime GeneratedUtc { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public IReadOnlyList<FaceMaskContributionFile>? Contributions { get; init; } = [];
}

public sealed record FaceMaskContributionFile
{
    public string? SourcePanel2DElementId { get; init; }
    public string? LinkedMachineObjectReference { get; init; }
    public FaceSourceRegionFile? Bounds { get; init; }
    public int PixelCount { get; init; }
}

public sealed record FaceSourceRegionFile
{
    public FaceSourceRegionKind Kind { get; init; } = FaceSourceRegionKind.Rect;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

public sealed record FaceLayerFile
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public bool IsVisible { get; init; } = true;
    public bool IsLocked { get; init; }
}

public sealed record FaceElementFile
{
    public string? ObjectId { get; init; }
    public string? Name { get; init; }
    public string? Kind { get; init; } = "lampWindow";
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsVisible { get; init; } = true;
    public bool IsLocked { get; init; }
    public string? LinkedMachineObjectReference { get; init; }
    public string? LinkedPanel2DElementId { get; init; }
    public string? LinkedInputReference { get; init; }
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public bool ShowDecimalPoint { get; init; }
    public bool ShowCommaTail { get; init; }
    public bool IsReversed { get; init; }
    public string? SegmentDisplayType { get; init; }
    public int? Stops { get; init; }
    public double? VisibleScale { get; init; }
    public double? BandOffset { get; init; }
    public string? AssetPath { get; init; }
    public string? SourcePanel2DDocumentId { get; init; }
    public FaceSourceRegionFile? SourceRegion { get; init; }
    public FaceArtworkProvenanceFile? ArtworkProvenance { get; init; }
}

public sealed record FaceArtworkProvenanceFile
{
    public string? Generator { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public string? SourcePanel2DElementId { get; init; }
    public string? SourcePanel2DElementKind { get; init; }
    public string? SourceAssetPath { get; init; }
    public FaceSourceRegionFile? SourceElementBounds { get; init; }
}
