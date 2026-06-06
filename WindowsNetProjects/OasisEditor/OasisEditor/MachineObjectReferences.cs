namespace OasisEditor;

public enum MachineObjectKind
{
    Unknown,
    Lamp,
    Reel,
    AlphaDisplay,
    SevenSegmentDisplay,
    Input
}

public readonly record struct MachineObjectReference(MachineObjectKind Kind, string Id)
{
    public bool IsEmpty => Kind == MachineObjectKind.Unknown || string.IsNullOrWhiteSpace(Id);

    public static MachineObjectReference Empty => new(MachineObjectKind.Unknown, string.Empty);

    public static MachineObjectReference Lamp(int lampId) => Create(MachineObjectKind.Lamp, lampId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static MachineObjectReference Reel(int reelId) => Create(MachineObjectKind.Reel, reelId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static MachineObjectReference AlphaDisplay(int displayId) => Create(MachineObjectKind.AlphaDisplay, displayId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static MachineObjectReference SevenSegmentDisplay(int displayId) => Create(MachineObjectKind.SevenSegmentDisplay, displayId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static MachineObjectReference Input(string inputId) => Create(MachineObjectKind.Input, inputId);

    public static MachineObjectReference Create(MachineObjectKind kind, string id)
    {
        if (kind == MachineObjectKind.Unknown || string.IsNullOrWhiteSpace(id))
        {
            return Empty;
        }

        return new MachineObjectReference(kind, id.Trim());
    }

    public static bool TryParse(string? value, out MachineObjectReference reference)
    {
        reference = Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        var kind = parts[0].ToLowerInvariant() switch
        {
            "lamp" => MachineObjectKind.Lamp,
            "reel" => MachineObjectKind.Reel,
            "alpha" or "alphadisplay" or "alpha-display" => MachineObjectKind.AlphaDisplay,
            "sevensegment" or "seven-segment" or "sevensegmentdisplay" => MachineObjectKind.SevenSegmentDisplay,
            "input" => MachineObjectKind.Input,
            _ => MachineObjectKind.Unknown
        };

        if (kind == MachineObjectKind.Unknown)
        {
            return false;
        }

        reference = Create(kind, parts[1]);
        return !reference.IsEmpty;
    }

    public override string ToString()
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        var prefix = Kind switch
        {
            MachineObjectKind.Lamp => "lamp",
            MachineObjectKind.Reel => "reel",
            MachineObjectKind.AlphaDisplay => "alpha",
            MachineObjectKind.SevenSegmentDisplay => "sevenSegment",
            MachineObjectKind.Input => "input",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(prefix) ? string.Empty : $"{prefix}:{Id}";
    }
}

public readonly record struct MachineLampReference(MachineObjectReference Reference)
{
    public static MachineLampReference FromLampId(int lampId) => new(MachineObjectReference.Lamp(lampId));
    public override string ToString() => Reference.ToString();
}

public readonly record struct MachineReelReference(MachineObjectReference Reference)
{
    public static MachineReelReference FromReelId(int reelId) => new(MachineObjectReference.Reel(reelId));
    public override string ToString() => Reference.ToString();
}

public readonly record struct MachineDisplayReference(MachineObjectReference Reference)
{
    public static MachineDisplayReference FromAlphaDisplayId(int displayId) => new(MachineObjectReference.AlphaDisplay(displayId));
    public static MachineDisplayReference FromSevenSegmentDisplayId(int displayId) => new(MachineObjectReference.SevenSegmentDisplay(displayId));
    public override string ToString() => Reference.ToString();
}

public readonly record struct MachineInputReference(MachineObjectReference Reference)
{
    public static MachineInputReference FromInputId(string inputId) => new(MachineObjectReference.Input(inputId));
    public override string ToString() => Reference.ToString();
}
