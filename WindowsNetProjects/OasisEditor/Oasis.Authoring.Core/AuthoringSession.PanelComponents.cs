using OasisEditor.FacePipeline;

namespace OasisEditor;

public sealed record PanelSpecialistSettings(
    int? DisplayNumber = null,
    string? OnColorHex = null,
    string? OffColorHex = null,
    string? TextColorHex = null,
    string? DisplayText = null,
    bool? HasBorder = null,
    string? SegmentDisplayType = null,
    bool? ShowDecimalPoint = null,
    bool? ShowCommaTail = null,
    int? Stops = null,
    double? VisibleScale = null,
    double? BandOffset = null,
    bool? IsReversed = null,
    bool? ReelLampsEnabled = null,
    int? TopLampNumber = null,
    int? MiddleLampNumber = null,
    int? BottomLampNumber = null);

public sealed partial class AuthoringSession
{
    /// <summary>Add a production-native Panel2D specialist element with WPF-compatible defaults.</summary>
    public string AddSpecialistElement(string kind, double x, double y)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ValidateGeometry(x, y, null, null);
        var normalized = kind.Trim().ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        var id = Guid.NewGuid().ToString("N");
        var element = normalized switch
        {
            "lamp" => Base("lamp", "Lamp", 80, 40) with { OnColorHex = "#FF3030", OffColorHex = "#2A0505", TextColorHex = "#FFFFFF", TextBoxFontName = "Tahoma", TextBoxFontStyle = "Regular", TextBoxFontSize = "8" },
            "reel" => Base("reel", "Reel 0", 120, 180) with { DisplayNumber = 0, Stops = 24, VisibleScale = 3d / 24d, IsReversed = false },
            "sevensegment" or "sevensegmentdisplay" => Base("sevenSegment", "7 Segment Display", 80, 120) with { DisplayNumber = 1, OnColorHex = "#FF4040" },
            "alpha" or "segmentalpha" => Base("alpha", "Segment Alpha", 220, 60) with { SegmentDisplayType = "led16seg", OnColorHex = "#FFB000", IsReversed = false },
            "vfd" or "vfddotmatrix" => Base("vfdDotMatrix", "VFD Dot Matrix", 480, 80) with { OnColorHex = "#FFB000" },
            _ => throw new ArgumentException("Supported specialist kinds are Lamp, Reel, Seven Segment, Segment Alpha and VFD Dot Matrix.", nameof(kind))
        };
        var document = RequireDocument();
        Commit(document with { Elements = [.. document.Elements, element] }, notify: false);
        _selectedIds = [id]; _selectedId = id;
        StorePanelState();
        Changed?.Invoke();
        return id;

        PanelElementFile Base(string elementKind, string name, double width, double height) => new()
        {
            ObjectId = id, Name = name, Kind = elementKind, X = Math.Max(0, x), Y = Math.Max(0, y),
            Width = width, Height = height, IsVisible = true
        };
    }

    public PanelSpecialistSettings? GetSpecialistSettings(string id)
    {
        var element = RequireDocument().Elements.FirstOrDefault(value => value.ObjectId == id)
            ?? throw new KeyNotFoundException($"Panel element '{id}' was not found.");
        if (!IsSpecialistKind(element.Kind)) return null;
        int? LampAt(string position) => element.ReelLamps?.FirstOrDefault(value => string.Equals(value.Position, position, StringComparison.OrdinalIgnoreCase))?.LampNumber;
        return new(element.DisplayNumber, element.OnColorHex, element.OffColorHex, element.TextColorHex, element.DisplayText,
            element.HasBorder, element.SegmentDisplayType, element.ShowDecimalPoint, element.ShowCommaTail, element.Stops,
            element.VisibleScale, element.BandOffset, element.IsReversed, element.ReelLampsEnabled,
            LampAt("top"), LampAt("middle"), LampAt("bottom"));
    }

    public bool UpdateSpecialistSettings(string id, PanelSpecialistSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var document = RequireDocument();
        var index = Array.FindIndex(document.Elements, value => value.ObjectId == id);
        if (index < 0) throw new KeyNotFoundException($"Panel element '{id}' was not found.");
        var element = document.Elements[index];
        if (!IsSpecialistKind(element.Kind)) throw new InvalidOperationException("The selected Panel2D element has no specialist settings.");
        ValidateSpecialistSettings(settings);
        var next = element with
        {
            DisplayNumber = settings.DisplayNumber, OnColorHex = Normalize(settings.OnColorHex), OffColorHex = Normalize(settings.OffColorHex),
            TextColorHex = Normalize(settings.TextColorHex), DisplayText = Normalize(settings.DisplayText), HasBorder = settings.HasBorder,
            SegmentDisplayType = NormalizeSegmentType(settings.SegmentDisplayType), ShowDecimalPoint = settings.ShowDecimalPoint,
            ShowCommaTail = settings.ShowCommaTail, Stops = settings.Stops, VisibleScale = settings.VisibleScale,
            BandOffset = settings.BandOffset, IsReversed = settings.IsReversed, ReelLampsEnabled = settings.ReelLampsEnabled,
            ReelLamps = element.Kind == "reel" ? SetReelLamps(element.ReelLamps, settings) : element.ReelLamps
        };
        if (SpecialistEquivalent(element, next)) return false;
        var elements = (PanelElementFile[])document.Elements.Clone(); elements[index] = next;
        Commit(document with { Elements = elements });
        return true;
    }

    private static bool IsSpecialistKind(string kind) => kind is "lamp" or "reel" or "sevenSegment" or "alpha" or "vfdDotMatrix";
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeSegmentType(string? value) => string.IsNullOrWhiteSpace(value) ? null
        : value.Trim().ToLowerInvariant() switch { "led14seg" or "14 segment" => "led14seg", "led16seg" or "16 segment" => "led16seg", _ => throw new ArgumentException("Segment type must be led14seg or led16seg.") };

    private static void ValidateSpecialistSettings(PanelSpecialistSettings settings)
    {
        foreach (var number in new[] { settings.DisplayNumber, settings.TopLampNumber, settings.MiddleLampNumber, settings.BottomLampNumber })
            if (number is < 0) throw new ArgumentOutOfRangeException(nameof(settings), "Display and lamp numbers must be zero or greater.");
        if (settings.Stops is <= 0) throw new ArgumentOutOfRangeException(nameof(settings), "Reel stops must be greater than zero.");
        if (settings.VisibleScale is { } scale && (!double.IsFinite(scale) || scale <= 0 || scale > 1)) throw new ArgumentOutOfRangeException(nameof(settings), "Visible scale must be greater than zero and no greater than one.");
        if (settings.BandOffset is { } offset && !PanelElementValidation.IsValidBandOffset(offset)) throw new ArgumentOutOfRangeException(nameof(settings), "Band offset must be between -1 and 1.");
        foreach (var color in new[] { settings.OnColorHex, settings.OffColorHex, settings.TextColorHex })
            if (color is not null && (!(color.Length is 7 or 9) || color[0] != '#' || color.Skip(1).Any(character => !Uri.IsHexDigit(character))))
                throw new ArgumentException("Colors must use #RRGGBB or #AARRGGBB notation.", nameof(settings));
    }

    private static PanelElementReelLampFile[]? SetReelLamps(PanelElementReelLampFile[]? source, PanelSpecialistSettings settings)
    {
        if (source is null && settings.TopLampNumber is null && settings.MiddleLampNumber is null && settings.BottomLampNumber is null)
            return null;
        var existing = source?.ToDictionary(lamp => lamp.Position, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, PanelElementReelLampFile>(StringComparer.OrdinalIgnoreCase);
        var values = new[] { (Position: "top", Number: settings.TopLampNumber), (Position: "middle", Number: settings.MiddleLampNumber), (Position: "bottom", Number: settings.BottomLampNumber) };
        foreach (var (position, number) in values)
        {
            if (existing.TryGetValue(position, out var prior)) existing[position] = prior with { LampNumber = number };
            else if (number.HasValue)
                existing[position] = new PanelElementReelLampFile { Position = position, LampNumber = number, LocalVerticalCenter = position switch { "top" => 1d / 6d, "middle" => .5d, _ => 5d / 6d }, Radius = 0, Intensity = 1 };
        }
        return existing.Values.OrderBy(lamp => Array.IndexOf(new[] { "top", "middle", "bottom" }, lamp.Position)).ToArray();
    }

    private static bool SpecialistEquivalent(PanelElementFile a, PanelElementFile b)
        => a.DisplayNumber == b.DisplayNumber && a.OnColorHex == b.OnColorHex && a.OffColorHex == b.OffColorHex
        && a.TextColorHex == b.TextColorHex && a.DisplayText == b.DisplayText && a.HasBorder == b.HasBorder
        && a.SegmentDisplayType == b.SegmentDisplayType && a.ShowDecimalPoint == b.ShowDecimalPoint && a.ShowCommaTail == b.ShowCommaTail
        && a.Stops == b.Stops && a.VisibleScale == b.VisibleScale && a.BandOffset == b.BandOffset && a.IsReversed == b.IsReversed
        && a.ReelLampsEnabled == b.ReelLampsEnabled && (a.ReelLamps ?? []).SequenceEqual(b.ReelLamps ?? []);
}
