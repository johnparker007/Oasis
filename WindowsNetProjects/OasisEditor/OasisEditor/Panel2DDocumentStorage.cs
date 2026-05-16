using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor;

internal static class Panel2DDocumentStorage
{
    public const int CurrentSchemaVersion = 2;
    public const int LegacySchemaVersion = 1;

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

        if (string.Equals(kind, "background", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Background;
        }

        if (string.Equals(kind, "lamp", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Lamp;
        }

        if (string.Equals(kind, "reel", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Reel;
        }

        if (string.Equals(kind, "sevensegment", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.SevenSegment;
        }

        if (string.Equals(kind, "alpha", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Alpha;
        }
        if (string.Equals(kind, "label", StringComparison.OrdinalIgnoreCase))
        {
            return PanelElementKind.Label;
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
            PanelElementKind.Background => "background",
            PanelElementKind.Lamp => "lamp",
            PanelElementKind.Reel => "reel",
            PanelElementKind.SevenSegment => "sevenSegment",
            PanelElementKind.Alpha => "alpha",
            PanelElementKind.Label => "label",
            _ => string.Empty
        };
    }

    public static string Serialize(string title, string summary, IReadOnlyList<PanelElementFile> elements)
    {
        var schemaVersion = DetermineSchemaVersion(elements);
        var payload = new Panel2DDocumentFile
        {
            SchemaVersion = schemaVersion,
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

        if (source.SchemaVersion == LegacySchemaVersion)
        {
            migrated = MigrateFromSchemaVersion1(source);
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
            errorMessage = $"Unsupported schema version '{source.SchemaVersion}'. Current supported version is {CurrentSchemaVersion}.";
            return false;
        }

        var elements = source.Elements ?? [];
        var normalizedElements = new List<PanelElementFile>(elements.Length);
        var objectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements)
        {
            var normalizedElement = NormalizeElement(element);
            var normalizedKind = ParseElementKind(normalizedElement.Kind);
            if (normalizedKind == PanelElementKind.Unknown)
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{element.ObjectId}' has unsupported kind '{element.Kind}'.";
                return false;
            }

            if (!IsValidDimension(normalizedElement.Width) || !IsValidDimension(normalizedElement.Height))
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{normalizedElement.ObjectId}' has invalid dimensions.";
                return false;
            }

            if (normalizedElement.DisplayNumber is < 0)
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{normalizedElement.ObjectId}' has invalid display number '{normalizedElement.DisplayNumber}'.";
                return false;
            }

            if (normalizedElement.Stops is < 0)
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{normalizedElement.ObjectId}' has invalid stops value '{normalizedElement.Stops}'.";
                return false;
            }

            if (normalizedElement.VisibleScale.HasValue && !IsValidVisibleScale(normalizedElement.VisibleScale.Value))
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{normalizedElement.ObjectId}' has invalid visible scale '{normalizedElement.VisibleScale}'.";
                return false;
            }

            if (normalizedElement.ImportSource is not null && string.IsNullOrWhiteSpace(normalizedElement.ImportSource.Format))
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{normalizedElement.ObjectId}' has import source with missing format.";
                return false;
            }

            if (!objectIds.Add(normalizedElement.ObjectId))
            {
                normalized = new Panel2DDocumentFile();
                errorMessage = $"Element '{normalizedElement.ObjectId}' is duplicated. Object IDs must be unique.";
                return false;
            }

            normalizedElements.Add(normalizedElement);
        }

        normalized = source with
        {
            Elements = normalizedElements.ToArray()
        };
        errorMessage = string.Empty;
        return true;
    }

    private static int DetermineSchemaVersion(IReadOnlyList<PanelElementFile> elements)
    {
        foreach (var element in elements)
        {
            var kind = ParseElementKind(element.Kind);
            if (kind is PanelElementKind.Background or PanelElementKind.Lamp or PanelElementKind.Reel or PanelElementKind.SevenSegment or PanelElementKind.Alpha or PanelElementKind.Label)
            {
                return CurrentSchemaVersion;
            }

            if (element.Native is not null
                || !string.IsNullOrWhiteSpace(element.AssetPath)
                || !string.IsNullOrWhiteSpace(element.SecondaryAssetPath)
                || element.DisplayNumber.HasValue
                || !string.IsNullOrWhiteSpace(element.OnColorHex)
                || !string.IsNullOrWhiteSpace(element.OffColorHex)
                || !string.IsNullOrWhiteSpace(element.TextColorHex)
                || !string.IsNullOrWhiteSpace(element.DisplayText)
                || element.IsReversed.HasValue
                || element.Stops.HasValue
                || element.VisibleScale.HasValue
                || element.IsLocked
                || element.IsVisible is false
                || element.ImportSource is not null)
            {
                return CurrentSchemaVersion;
            }
        }

        return LegacySchemaVersion;
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
            Height = normalized.Height,
            AssetPath = normalized.AssetPath,
            SecondaryAssetPath = normalized.SecondaryAssetPath,
            DisplayNumber = normalized.DisplayNumber,
            OnColorHex = normalized.OnColorHex,
            OffColorHex = normalized.OffColorHex,
            TextColorHex = normalized.TextColorHex,
            DisplayText = normalized.DisplayText,
            TextBoxFontName = normalized.TextBoxFontName,
            TextBoxFontStyle = normalized.TextBoxFontStyle,
            TextBoxFontSize = normalized.TextBoxFontSize,
            IsReversed = normalized.IsReversed,
            Stops = normalized.Stops,
            VisibleScale = normalized.VisibleScale,
            IsLocked = normalized.IsLocked,
            IsVisible = normalized.IsVisible ?? true,
            ImportSource = normalized.ImportSource is null
                ? null
                : new PanelElementImportSourceModel
                {
                    Format = normalized.ImportSource.Format,
                    Reference = normalized.ImportSource.Reference
                }
        };
    }

    public static PanelElementFile ToStorageElement(PanelElementModel element)
    {
        var importSource = element.ImportSource is null
            ? null
            : new PanelElementImportSourceFile
            {
                Format = element.ImportSource.Format,
                Reference = element.ImportSource.Reference
            };

        return NormalizeElement(new PanelElementFile
        {
            ObjectId = element.ObjectId,
            Name = element.Name,
            Kind = SerializeElementKind(element.Kind),
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height,
            AssetPath = element.AssetPath,
            SecondaryAssetPath = element.SecondaryAssetPath,
            DisplayNumber = element.DisplayNumber,
            OnColorHex = element.OnColorHex,
            OffColorHex = element.OffColorHex,
            TextColorHex = element.TextColorHex,
            DisplayText = element.DisplayText,
            TextBoxFontName = element.TextBoxFontName,
            TextBoxFontStyle = element.TextBoxFontStyle,
            TextBoxFontSize = element.TextBoxFontSize,
            IsReversed = element.IsReversed,
            Stops = element.Stops,
            VisibleScale = element.VisibleScale,
            IsLocked = element.IsLocked,
            IsVisible = element.IsVisible,
            ImportSource = importSource,
            Native = CreateNativeFromLegacyFields(
                assetPath: element.AssetPath,
                secondaryAssetPath: element.SecondaryAssetPath,
                number: element.DisplayNumber,
                text: element.DisplayText,
                textBoxFontName: element.Kind == PanelElementKind.Lamp ? NormalizeLampFontName(element.TextBoxFontName) : NormalizeOptionalString(element.TextBoxFontName),
                textBoxFontStyle: element.Kind == PanelElementKind.Lamp ? NormalizeLampFontStyle(element.TextBoxFontStyle) : NormalizeOptionalString(element.TextBoxFontStyle),
                textBoxFontSize: element.Kind == PanelElementKind.Lamp ? NormalizeLampFontSize(element.TextBoxFontSize) : NormalizeOptionalString(element.TextBoxFontSize),
                textColorHex: element.TextColorHex,
                onColorHex: element.OnColorHex,
                offColorHex: element.OffColorHex,
                reversed: element.IsReversed,
                stops: element.Stops,
                visibleScale: element.VisibleScale,
                importSource: importSource)
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
        var normalizedNative = NormalizeNative(element.Native);
        var normalizedImportSource = NormalizeImportSource(normalizedNative?.ImportSource ?? element.ImportSource);
        var normalizedAssetPath = NormalizeOptionalString(normalizedNative?.AssetPath ?? element.AssetPath);
        var normalizedSecondaryAssetPath = NormalizeOptionalString(normalizedNative?.SecondaryAssetPath ?? element.SecondaryAssetPath);
        var normalizedDisplayNumber = normalizedNative?.Number ?? element.DisplayNumber;
        var normalizedOnColorHex = NormalizeOptionalString(normalizedNative?.OnColorHex ?? normalizedNative?.DisplayColorHex ?? element.OnColorHex);
        var normalizedOffColorHex = NormalizeOptionalString(normalizedNative?.OffColorHex ?? element.OffColorHex);
        var normalizedTextColorHex = NormalizeOptionalString(normalizedNative?.TextColorHex ?? element.TextColorHex);
        var normalizedDisplayText = NormalizeOptionalString(normalizedNative?.Text ?? element.DisplayText);
        var normalizedTextBoxFontName = normalizedKind == PanelElementKind.Lamp
            ? NormalizeLampFontName(normalizedNative?.TextBoxFontName ?? element.TextBoxFontName)
            : NormalizeOptionalString(normalizedNative?.TextBoxFontName ?? element.TextBoxFontName);
        var normalizedTextBoxFontStyle = normalizedKind == PanelElementKind.Lamp
            ? NormalizeLampFontStyle(normalizedNative?.TextBoxFontStyle ?? element.TextBoxFontStyle)
            : NormalizeOptionalString(normalizedNative?.TextBoxFontStyle ?? element.TextBoxFontStyle);
        var normalizedTextBoxFontSize = normalizedKind == PanelElementKind.Lamp
            ? NormalizeLampFontSize(normalizedNative?.TextBoxFontSize ?? element.TextBoxFontSize)
            : NormalizeOptionalString(normalizedNative?.TextBoxFontSize ?? element.TextBoxFontSize);
        var normalizedIsReversed = normalizedNative?.Reversed ?? element.IsReversed;
        var normalizedStops = normalizedNative?.Stops ?? element.Stops;
        var normalizedVisibleScale = normalizedNative?.VisibleScale ?? element.VisibleScale;
        var normalizedIsLocked = element.IsLocked;
        var normalizedIsVisible = element.IsVisible ?? true;

        var mergedNative = normalizedNative is null
            ? CreateNativeFromLegacyFields(
                normalizedAssetPath,
                normalizedSecondaryAssetPath,
                normalizedDisplayNumber,
                normalizedDisplayText,
                normalizedTextBoxFontName,
                normalizedTextBoxFontStyle,
                normalizedTextBoxFontSize,
                normalizedTextColorHex,
                normalizedOnColorHex,
                normalizedOffColorHex,
                normalizedIsReversed,
                normalizedStops,
                normalizedVisibleScale,
                normalizedImportSource)
            : normalizedNative with
            {
                AssetPath = normalizedAssetPath,
                SecondaryAssetPath = normalizedSecondaryAssetPath,
                Number = normalizedDisplayNumber,
                Text = normalizedDisplayText,
                TextBoxFontName = normalizedTextBoxFontName,
                TextBoxFontStyle = normalizedTextBoxFontStyle,
                TextBoxFontSize = normalizedTextBoxFontSize,
                TextColorHex = normalizedTextColorHex,
                OnColorHex = normalizedOnColorHex,
                DisplayColorHex = NormalizeOptionalString(normalizedNative.DisplayColorHex ?? normalizedOnColorHex),
                OffColorHex = normalizedOffColorHex,
                Reversed = normalizedIsReversed,
                Stops = normalizedStops,
                VisibleScale = normalizedVisibleScale,
                ImportSource = normalizedImportSource
            };

        return element with
        {
            ObjectId = normalizedObjectId,
            Name = NormalizeElementName(element.Name, normalizedKind, normalizedObjectId),
            Kind = SerializeElementKind(normalizedKind),
            AssetPath = normalizedAssetPath,
            SecondaryAssetPath = normalizedSecondaryAssetPath,
            DisplayNumber = normalizedDisplayNumber,
            OnColorHex = normalizedOnColorHex,
            OffColorHex = normalizedOffColorHex,
            TextColorHex = normalizedTextColorHex,
            DisplayText = normalizedDisplayText,
            TextBoxFontName = normalizedTextBoxFontName,
            TextBoxFontStyle = normalizedTextBoxFontStyle,
            TextBoxFontSize = normalizedTextBoxFontSize,
            IsReversed = normalizedIsReversed,
            Stops = normalizedStops,
            VisibleScale = normalizedVisibleScale,
            IsLocked = normalizedIsLocked,
            IsVisible = normalizedIsVisible,
            ImportSource = normalizedImportSource,
            Native = mergedNative
        };
    }

    private static Panel2DDocumentFile MigrateFromSchemaVersion1(Panel2DDocumentFile source)
    {
        return source with
        {
            SchemaVersion = CurrentSchemaVersion,
            Elements = (source.Elements ?? [])
                .Select(NormalizeElement)
                .ToArray()
        };
    }

    internal static string CreateDefaultElementName(string? kind, string? objectId)
    {
        return CreateDefaultElementName(ParseElementKind(kind), objectId);
    }

    internal static string CreateDefaultElementName(PanelElementKind kind, string? objectId)
    {
        var prefix = kind switch
        {
            PanelElementKind.Image => "Image",
            PanelElementKind.Background => "Background",
            PanelElementKind.Lamp => "Lamp",
            PanelElementKind.Reel => "Reel",
            PanelElementKind.SevenSegment => "7 Segment",
            PanelElementKind.Alpha => "Alpha",
            PanelElementKind.Label => "Label",
            _ => "Rectangle"
        };

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

    private static bool IsValidVisibleScale(double value)
    {
        return !double.IsNaN(value)
            && !double.IsInfinity(value)
            && value > 0;
    }

    private static string? NormalizeOptionalString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static PanelElementImportSourceFile? NormalizeImportSource(PanelElementImportSourceFile? importSource)
    {
        if (importSource is null)
        {
            return null;
        }

        var format = NormalizeOptionalString(importSource.Format);
        var reference = NormalizeOptionalString(importSource.Reference);

        if (format is null && reference is null)
        {
            return null;
        }

        return new PanelElementImportSourceFile
        {
            Format = format ?? string.Empty,
            Reference = reference
        };
    }

    private static PanelElementNativeFile? NormalizeNative(PanelElementNativeFile? native)
    {
        if (native is null)
        {
            return null;
        }

        return new PanelElementNativeFile
        {
            AssetPath = NormalizeOptionalString(native.AssetPath),
            SecondaryAssetPath = NormalizeOptionalString(native.SecondaryAssetPath),
            Number = native.Number,
            Text = NormalizeOptionalString(native.Text),
            TextBoxFontName = NormalizeOptionalString(native.TextBoxFontName),
            TextBoxFontStyle = NormalizeOptionalString(native.TextBoxFontStyle),
            TextBoxFontSize = NormalizeOptionalString(native.TextBoxFontSize),
            TextColorHex = NormalizeOptionalString(native.TextColorHex),
            OnColorHex = NormalizeOptionalString(native.OnColorHex),
            OffColorHex = NormalizeOptionalString(native.OffColorHex),
            DisplayColorHex = NormalizeOptionalString(native.DisplayColorHex),
            Reversed = native.Reversed,
            Stops = native.Stops,
            VisibleScale = native.VisibleScale,
            Outline = native.Outline,
            ImportSource = NormalizeImportSource(native.ImportSource)
        };
    }

    private static PanelElementNativeFile? CreateNativeFromLegacyFields(
        string? assetPath,
        string? secondaryAssetPath,
        int? number,
        string? text,
        string? textBoxFontName,
        string? textBoxFontStyle,
        string? textBoxFontSize,
        string? textColorHex,
        string? onColorHex,
        string? offColorHex,
        bool? reversed,
        int? stops,
        double? visibleScale,
        PanelElementImportSourceFile? importSource)
    {
        if (assetPath is null
            && secondaryAssetPath is null
            && number is null
            && text is null
            && textBoxFontName is null
            && textBoxFontStyle is null
            && textBoxFontSize is null
            && textColorHex is null
            && onColorHex is null
            && offColorHex is null
            && reversed is null
            && stops is null
            && visibleScale is null
            && importSource is null)
        {
            return null;
        }

        return new PanelElementNativeFile
        {
            AssetPath = assetPath,
            SecondaryAssetPath = secondaryAssetPath,
            Number = number,
            Text = text,
            TextBoxFontName = textBoxFontName,
            TextBoxFontStyle = textBoxFontStyle,
            TextBoxFontSize = textBoxFontSize,
            TextColorHex = textColorHex,
            OnColorHex = onColorHex,
            OffColorHex = offColorHex,
            DisplayColorHex = onColorHex,
            Reversed = reversed,
            Stops = stops,
            VisibleScale = visibleScale,
            ImportSource = importSource
        };
    }

    private static string NormalizeLampFontName(string? value) => string.IsNullOrWhiteSpace(value) ? "Tahoma" : value.Trim();
    private static string NormalizeLampFontStyle(string? value) => string.IsNullOrWhiteSpace(value) ? "Regular" : value.Trim();
    private static string NormalizeLampFontSize(string? value) => string.IsNullOrWhiteSpace(value) ? "8" : value.Trim();
}

internal enum PanelElementKind
{
    Unknown = 0,
    Rectangle,
    Image,
    Anchor,
    Zone,
    Background,
    Lamp,
    Reel,
    SevenSegment,
    Alpha,
    Label
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
    public string? AssetPath { get; init; }
    public string? SecondaryAssetPath { get; init; }
    public int? DisplayNumber { get; init; }
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public string? TextColorHex { get; init; }
    public string? DisplayText { get; init; }
    public string? TextBoxFontName { get; init; }
    public string? TextBoxFontStyle { get; init; }
    public string? TextBoxFontSize { get; init; }
    public bool? IsReversed { get; init; }
    public int? Stops { get; init; }
    public double? VisibleScale { get; init; }
    public bool IsLocked { get; init; }
    public bool? IsVisible { get; init; }
    public PanelElementImportSourceFile? ImportSource { get; init; }
    public PanelElementNativeFile? Native { get; init; }

    [JsonIgnore]
    public PanelElementKind ElementKind => Panel2DDocumentStorage.ParseElementKind(Kind);
}

internal sealed record PanelElementNativeFile
{
    public string? AssetPath { get; init; }
    public string? SecondaryAssetPath { get; init; }
    public int? Number { get; init; }
    public string? Text { get; init; }
    public string? TextBoxFontName { get; init; }
    public string? TextBoxFontStyle { get; init; }
    public string? TextBoxFontSize { get; init; }
    public string? TextColorHex { get; init; }
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public string? DisplayColorHex { get; init; }
    public bool? Reversed { get; init; }
    public int? Stops { get; init; }
    public double? VisibleScale { get; init; }
    public bool? Outline { get; init; }
    public PanelElementImportSourceFile? ImportSource { get; init; }
}

internal sealed record PanelElementImportSourceFile
{
    public string Format { get; init; } = string.Empty;
    public string? Reference { get; init; }
}
