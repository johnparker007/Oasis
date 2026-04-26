using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor;

internal static class Panel2DDocumentStorage
{
    public const int CurrentSchemaVersion = 1;

    internal static PanelElementKind ParseElementKind(string? kind)
    {
        if (string.Equals(kind, "rectangle", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Rectangle;
        }

        if (string.Equals(kind, "image", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Image;
        }

        if (string.Equals(kind, "anchor", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Anchor;
        }

        if (string.Equals(kind, "zone", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Zone;
        }

        return PanelElementKind.Unknown;
    }

    internal static string SerializeElementKind(PanelElementKind kind)
    {
        return kind switch
        {
            PanelElementKind.Rectangle => "rectangle",
            PanelElementKind.Image => "image",
            PanelElementKind.Anchor => "anchor",
            PanelElementKind.Zone => "zone",
            _ => string.Empty
        };
    }

    public static string Serialize(string title, string summary, IReadOnlyList<PanelElementFile> elements)
    {
        var payload = new Panel2DDocumentFile
        {
            SchemaVersion = CurrentSchemaVersion,
            Title = title,
            Summary = summary,
            SavedAtUtc = DateTime.UtcNow,
            Elements = elements.ToArray()
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static bool TryCreateSummary(string content, out string summary)
    {
        if (!TryReadValidated(content, out var document, out _))
        {
            summary = string.Empty;
            return false;
        }

        summary = string.IsNullOrWhiteSpace(document.Summary)
            ? "Panel document opened."
            : document.Summary.Trim();
        return true;
    }

    public static bool TryRead(string content, out Panel2DDocumentFile document)
    {
        try
        {
            document = JsonSerializer.Deserialize<Panel2DDocumentFile>(content) ?? new Panel2DDocumentFile();
            return true;
        }
        catch (JsonException)
        {
            document = new Panel2DDocumentFile();
            return false;
        }
    }

    public static bool TryReadValidated(string content, out Panel2DDocumentFile document, out string errorMessage)
    {
        if (!TryRead(content, out var parsed))
        {
            document = new Panel2DDocumentFile();
            errorMessage = "Malformed JSON.";
            return false;
        }

        if (!TryMigrateToCurrentSchema(parsed, out var migrated, out errorMessage))
        {
            document = new Panel2DDocumentFile();
            return false;
        }

        if (!TryValidateAndNormalize(migrated, out var normalized, out errorMessage))
        {
            document = new Panel2DDocumentFile();
            return false;
        }

        document = normalized;
        return true;
    }

    public static bool TryMigrateToCurrentSchema(Panel2DDocumentFile source, out Panel2DDocumentFile migrated, out string errorMessage)
    {
        if (source.SchemaVersion == CurrentSchemaVersion)
        {
            migrated = source;
            errorMessage = string.Empty;
            return true;
        }

        migrated = new Panel2DDocumentFile();
        errorMessage = source.SchemaVersion > CurrentSchemaVersion
            ? $"Unsupported schema version '{source.SchemaVersion}'. Current supported version is {CurrentSchemaVersion}."
            : $"Unsupported schema version '{source.SchemaVersion}'.";
        return false;
    }

    public static bool TryValidateAndNormalize(Panel2DDocumentFile source, out Panel2DDocumentFile normalized, out string errorMessage)
    {
        if (source.SchemaVersion != CurrentSchemaVersion)
        {
            normalized = new Panel2DDocumentFile();
            errorMessage = $"Unsupported schema version '{source.SchemaVersion}'.";
            return false;
        }

        var elements = source.Elements ?? [];
        foreach (var element in elements)
        {
            var normalizedKind = ParseElementKind(element.Kind);
            if (normalizedKind == PanelElementKind.Unknown)
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{element.ObjectId}' has unsupported kind '{element.Kind}'.";
                return false;
            }

            if (!IsValidDimension(element.Width) || !IsValidDimension(element.Height))
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{element.ObjectId}' has invalid dimensions.";
                return false;
            }
        }

        normalized = source with
        {
            Elements = elements.Select(NormalizeElement).ToArray()
        };
        errorMessage = string.Empty;
        return true;
    }


    public static Panel2DDocumentModel ToModel(Panel2DDocumentFile document)
    {
        return new Panel2DDocumentModel
        {
            Title = document.Title,
            Summary = document.Summary,
            Elements = document.Elements
                .Select(NormalizeElement)
                .Select(ToModel)
                .ToArray()
        };
    }

    public static IReadOnlyList<PanelElementFile> ToStorageElements(Panel2DDocumentModel model)
    {
        return model.Elements
            .Select(ToStorageElement)
            .ToArray();
    }

    public static PanelElementModel ToModel(PanelElementFile element)
    {
        var normalized = NormalizeElement(element);
        return new PanelElementModel
        {
            ObjectId = normalized.ObjectId,
            Name = normalized.Name,
            Kind = ParseElementKind(normalized.Kind),
            X = normalized.X,
            Y = normalized.Y,
            Width = normalized.Width,
            Height = normalized.Height
        };
    }

    public static PanelElementFile ToStorageElement(PanelElementModel element)
    {
        return NormalizeElement(new PanelElementFile
        {
            ObjectId = element.ObjectId,
            Name = element.Name,
            Kind = SerializeElementKind(element.Kind),
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height
        });
    }
    public static string SerializeLayout(IReadOnlyList<PanelElementFile> elements)
    {
        return JsonSerializer.Serialize(elements);
    }

    public static IReadOnlyList<PanelElementFile> DeserializeLayout(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson))
        {
            return [];
        }

        try
        {
            var elements = JsonSerializer.Deserialize<List<PanelElementFile>>(layoutJson) ?? [];
            return elements.Select(NormalizeElement).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static PanelElementFile NormalizeElement(PanelElementFile element)
    {
        var normalizedObjectId = string.IsNullOrWhiteSpace(element.ObjectId)
            ? Guid.NewGuid().ToString("N")
            : element.ObjectId.Trim();
        var normalizedKind = ParseElementKind(element.Kind);

        return element with
        {
            ObjectId = normalizedObjectId,
            Name = NormalizeElementName(element.Name, normalizedKind, normalizedObjectId),
            Kind = SerializeElementKind(normalizedKind)
        };
    }

    internal static string CreateDefaultElementName(string? kind, string? objectId)
    {
        return CreateDefaultElementName(ParseElementKind(kind), objectId);
    }

    internal static string CreateDefaultElementName(PanelElementKind kind, string? objectId)
    {
        var prefix = kind == PanelElementKind.Image
            ? "Image"
            : "Rectangle";

        if (string.IsNullOrWhiteSpace(objectId))
        {
            return prefix;
        }

        var trimmedObjectId = objectId.Trim();
        var suffixLength = Math.Min(6, trimmedObjectId.Length);
        var suffix = trimmedObjectId[..suffixLength].ToUpperInvariant();
        return $"{prefix} {suffix}";
    }

    private static string NormalizeElementName(string? name, PanelElementKind kind, string objectId)
    {
        return string.IsNullOrWhiteSpace(name)
            ? CreateDefaultElementName(kind, objectId)
            : name.Trim();
    }

    private static bool IsValidDimension(double value)
    {
        return !double.IsNaN(value)
            && !double.IsInfinity(value)
            && value > 0;
    }
}

internal enum PanelElementKind
{
    Unknown = 0,
    Rectangle,
    Image,
    Anchor,
    Zone
}

internal sealed record Panel2DDocumentFile
{
    public int SchemaVersion { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public DateTime SavedAtUtc { get; init; }
    public PanelElementFile[] Elements { get; init; } = [];
}

internal sealed record PanelElementFile : IPanelSelectableObject
{
    public string ObjectId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    [JsonIgnore]
    public PanelElementKind ElementKind => Panel2DDocumentStorage.ParseElementKind(Kind);
}
