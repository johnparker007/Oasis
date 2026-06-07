using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor;

public static class FaceDocumentStorage
{
    public const int CurrentSchemaVersion = 1;

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
                    Id = "layer-lamp-windows",
                    Name = "Lamp Windows",
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

        return string.Equals(file.Kind, "artwork", StringComparison.OrdinalIgnoreCase)
            ? new FaceArtworkElement
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
            }
            : new FaceLampWindowElement
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
            AssetPath = model is FaceArtworkElement artwork ? artwork.AssetPath : null,
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
    public DateTime SavedAtUtc { get; init; }
    public IReadOnlyList<FaceLayerFile>? Layers { get; init; } = [];
    public IReadOnlyList<FaceElementFile>? Elements { get; init; } = [];
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
