namespace OasisEditor;

public enum NativeFaceComponentKind { Button, SevenSegmentDisplay, AlphaDisplay }

/// <summary>Atomic optional updates for a native Face button or display.</summary>
public sealed record FaceComponentUpdateOptions
{
    public string? Name { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public bool? Visible { get; init; }
    public bool? Locked { get; init; }
    public MachineObjectReference? LogicalReference { get; init; }
    public bool ClearLogicalReference { get; init; }
    public string? InputId { get; init; }
    public bool ClearInputReference { get; init; }
    public string? OnColorHex { get; init; }
    public bool ClearOnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public bool ClearOffColorHex { get; init; }
    public int? DigitCount { get; init; }
    public string? SegmentDisplayType { get; init; }
    public bool ClearSegmentDisplayType { get; init; }
    public bool? ShowDecimalPoint { get; init; }
    public bool? ShowCommaTail { get; init; }
    public bool? IsReversed { get; init; }
}

/// <summary>Creation and editing for native Face buttons and display components.</summary>
public sealed partial class FaceAuthoringSession
{
    public string AddButton(string name, double x, double y, double width = 80, double height = 40, string? inputId = null)
        => AddFaceComponent(NativeFaceComponentKind.Button, name, x, y, width, height, inputId: inputId);

    public string AddSevenSegmentDisplay(string name, double x, double y, double width = 100, double height = 48,
        int? displayIndex = null, int digitCount = FaceSevenSegmentDisplayElement.DefaultDigitCount)
        => AddFaceComponent(NativeFaceComponentKind.SevenSegmentDisplay, name, x, y, width, height,
            displayIndex: displayIndex, digitCount: digitCount);

    public string AddAlphaDisplay(string name, double x, double y, double width = 220, double height = 48,
        int? displayIndex = null, int digitCount = 16)
        => AddFaceComponent(NativeFaceComponentKind.AlphaDisplay, name, x, y, width, height,
            displayIndex: displayIndex, digitCount: digitCount);

    public bool UpdateFaceComponent(string elementId, FaceComponentUpdateOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentNullException.ThrowIfNull(options);
        var document = RequireDocument();
        ValidateComponentOptions(options);
        ValidateGeometry(options.X, options.Y, options.Width, options.Height);
        if (options.ClearLogicalReference && options.LogicalReference.HasValue
            || options.ClearInputReference && options.InputId is not null
            || options.ClearOnColorHex && options.OnColorHex is not null
            || options.ClearOffColorHex && options.OffColorHex is not null
            || options.ClearSegmentDisplayType && options.SegmentDisplayType is not null)
            throw new ArgumentException("A field cannot be updated and cleared in the same Face component edit.", nameof(options));

        var elements = document.Elements?.ToArray() ?? [];
        var index = Array.FindIndex(elements, element => string.Equals(element.ObjectId, elementId, StringComparison.Ordinal));
        if (index < 0 || elements[index].Kind is not ("button" or "sevenSegmentDisplay" or "alphaDisplay"))
            throw new KeyNotFoundException($"Face button or display '{elementId}' was not found.");
        var current = elements[index];
        var isButton = string.Equals(current.Kind, "button", StringComparison.OrdinalIgnoreCase);
        var isSevenSegment = string.Equals(current.Kind, "sevenSegmentDisplay", StringComparison.OrdinalIgnoreCase);
        var isAlpha = string.Equals(current.Kind, "alphaDisplay", StringComparison.OrdinalIgnoreCase);
        if (isButton && (options.LogicalReference.HasValue || options.ClearLogicalReference || options.DigitCount.HasValue
                || options.OnColorHex is not null || options.ClearOnColorHex || options.OffColorHex is not null || options.ClearOffColorHex
                || options.ShowDecimalPoint.HasValue || options.ShowCommaTail.HasValue || options.IsReversed.HasValue
                || options.SegmentDisplayType is not null || options.ClearSegmentDisplayType))
            throw new ArgumentException("Display settings can only be assigned to a Face display.", nameof(options));
        if (!isButton && (options.InputId is not null || options.ClearInputReference))
            throw new ArgumentException("An input reference can only be assigned to a Face button.", nameof(options));
        if (!isAlpha && (options.ShowCommaTail.HasValue || options.IsReversed.HasValue
                || options.SegmentDisplayType is not null || options.ClearSegmentDisplayType))
            throw new ArgumentException("Comma, direction and segment type settings can only be assigned to an alpha display.", nameof(options));
        if (options.LogicalReference is { } typedReference
            && (isSevenSegment && typedReference.Kind != MachineObjectKind.SevenSegmentDisplay
                || isAlpha && typedReference.Kind != MachineObjectKind.AlphaDisplay))
            throw new ArgumentException("The display reference kind must match the Face display type.", nameof(options));
        var name = options.Name is null ? current.Name : string.IsNullOrWhiteSpace(options.Name)
            ? throw new ArgumentException("Face component name cannot be blank.", nameof(options)) : options.Name.Trim();
        var updated = current with
        {
            Name = name,
            X = options.X ?? current.X,
            Y = options.Y ?? current.Y,
            Width = options.Width ?? current.Width,
            Height = options.Height ?? current.Height,
            IsVisible = options.Visible ?? current.IsVisible,
            LockTransform = options.Locked ?? current.LockTransform,
            LinkedMachineObjectReference = options.ClearLogicalReference ? null
                : options.LogicalReference?.ToString() ?? current.LinkedMachineObjectReference,
            LinkedInputReference = options.ClearInputReference ? null
                : options.InputId is not null ? MachineInputReference.FromInputId(ValidateInputId(options.InputId)).ToString()
                : current.LinkedInputReference,
            OnColorHex = options.ClearOnColorHex ? null : NormalizeColor(options.OnColorHex, current.OnColorHex),
            OffColorHex = options.ClearOffColorHex ? null : NormalizeColor(options.OffColorHex, current.OffColorHex),
            DigitCount = options.DigitCount ?? current.DigitCount,
            SegmentDisplayType = options.ClearSegmentDisplayType ? null
                : options.SegmentDisplayType is not null ? NormalizeOptional(options.SegmentDisplayType) : current.SegmentDisplayType,
            ShowDecimalPoint = options.ShowDecimalPoint ?? current.ShowDecimalPoint,
            ShowCommaTail = options.ShowCommaTail ?? current.ShowCommaTail,
            IsReversed = options.IsReversed ?? current.IsReversed
        };
        ValidateUnlockedGeometry(current.LockTransform, current.X, current.Y, current.Width, current.Height,
            updated.X, updated.Y, updated.Width, updated.Height);
        if (updated == current) return false;
        elements[index] = updated;
        CommitFaceComponentDocument(document with { Elements = elements });
        return true;
    }

    private string AddFaceComponent(NativeFaceComponentKind kind, string name, double x, double y, double width, double height,
        string? inputId = null, int? displayIndex = null, int digitCount = 1)
    {
        var document = RequireDocument();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Face component name cannot be blank.", nameof(name));
        ValidateGeometry(x, y, width, height);
        if (kind == NativeFaceComponentKind.Button && displayIndex.HasValue)
            throw new ArgumentException("A button cannot have a display reference.", nameof(displayIndex));
        if (kind != NativeFaceComponentKind.Button && inputId is not null)
            throw new ArgumentException("Only a button can have an input reference.", nameof(inputId));
        if (kind != NativeFaceComponentKind.Button && digitCount is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(digitCount), "Display digit count must be from 1 to 256.");
        if (kind == NativeFaceComponentKind.Button && digitCount != 1)
            throw new ArgumentException("Digit count only applies to displays.", nameof(digitCount));
        var id = $"face-component-{Guid.NewGuid():N}";
        var reference = displayIndex is int display ? kind switch
        {
            NativeFaceComponentKind.SevenSegmentDisplay when display >= 0 => MachineObjectReference.SevenSegmentDisplay(display).ToString(),
            NativeFaceComponentKind.AlphaDisplay when display >= 0 => MachineObjectReference.AlphaDisplay(display).ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(displayIndex), "Display index must be zero or greater.")
        } : null;
        var element = new FaceElementFile
        {
            ObjectId = id,
            Name = name.Trim(),
            Kind = kind switch
            {
                NativeFaceComponentKind.Button => "button",
                NativeFaceComponentKind.SevenSegmentDisplay => "sevenSegmentDisplay",
                NativeFaceComponentKind.AlphaDisplay => "alphaDisplay",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            },
            X = x, Y = y, Width = width, Height = height, IsVisible = true,
            LinkedMachineObjectReference = reference,
            LinkedInputReference = inputId is null ? null : MachineInputReference.FromInputId(ValidateInputId(inputId)).ToString(),
            DigitCount = kind == NativeFaceComponentKind.Button ? null : digitCount
        };
        CommitFaceComponentDocument(document with { Elements = (document.Elements ?? []).Append(element).ToArray() }, id);
        return id;
    }

    private void CommitFaceComponentDocument(FaceDocumentFile document, string? selectedId = null)
    {
        var serialized = FaceDocumentStorage.Serialize(document);
        if (!FaceDocumentStorage.TryReadValidated(serialized, out _, out var error)) throw new InvalidDataException(error);
        Commit(document, selectedId);
    }

    private static void ValidateComponentOptions(FaceComponentUpdateOptions options)
    {
        if (options.DigitCount is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(options.DigitCount), "Display digit count must be from 1 to 256.");
        if (options.LogicalReference is { } reference && reference.Kind is not (MachineObjectKind.SevenSegmentDisplay or MachineObjectKind.AlphaDisplay))
            throw new ArgumentException("Face displays can only link to an alpha or seven-segment display.", nameof(options));
        if (options.SegmentDisplayType is not null && options.SegmentDisplayType.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(options.SegmentDisplayType), "Segment display type is too long.");
    }

    private static string ValidateInputId(string inputId)
    {
        var value = inputId.Trim();
        if (value.Length is 0 or > 128 || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-')))
            throw new ArgumentException("Button input ID must contain only letters, numbers, underscores or hyphens.", nameof(inputId));
        return value;
    }

    private static string? NormalizeColor(string? requested, string? current)
    {
        if (requested is null) return current;
        var value = requested.Trim();
        if (value.Length is not (7 or 9) || value[0] != '#' || !value.AsSpan(1).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Display colours must be #RRGGBB or #AARRGGBB values.");
        return value.ToUpperInvariant();
    }

    private static string? NormalizeOptional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
