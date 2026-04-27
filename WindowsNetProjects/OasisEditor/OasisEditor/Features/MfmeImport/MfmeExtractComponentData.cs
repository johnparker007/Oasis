namespace OasisEditor.Features.MfmeImport;

internal enum MfmeComponentKind
{
    Background,
    Lamp,
    Reel,
    SevenSegment,
    Alpha,
    Unknown
}

internal abstract record MfmeExtractComponentData
{
    public required MfmeComponentKind Kind { get; init; }

    public required string SourceType { get; init; }

    public string? DisplayName { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public string RawJson { get; init; } = string.Empty;
}

internal sealed record MfmeBackgroundComponentData : MfmeExtractComponentData
{
    public string? ImageFileName { get; init; }

    public string? Color { get; init; }
}

internal sealed record MfmeLampComponentData : MfmeExtractComponentData
{
    public int? Number { get; init; }

    public string? ImageFileName { get; init; }

    public string? OnColor { get; init; }

    public string? OffColor { get; init; }

    public string? TextColor { get; init; }
}

internal sealed record MfmeReelComponentData : MfmeExtractComponentData
{
    public int? Number { get; init; }

    public int? Stops { get; init; }

    public int? ReelHeight { get; init; }

    public bool Reversed { get; init; }

    public string? BandImageFileName { get; init; }
}

internal sealed record MfmeSevenSegmentComponentData : MfmeExtractComponentData
{
    public int? Number { get; init; }

    public string? SegmentOnColor { get; init; }
}

internal sealed record MfmeAlphaComponentData : MfmeExtractComponentData
{
    public int? Number { get; init; }

    public bool Reversed { get; init; }

    public string? Color { get; init; }

    public string? ImageFileName { get; init; }

    public string AlphaVariant { get; init; } = "Alpha";
}


internal sealed record MfmeUnknownComponentData : MfmeExtractComponentData;
