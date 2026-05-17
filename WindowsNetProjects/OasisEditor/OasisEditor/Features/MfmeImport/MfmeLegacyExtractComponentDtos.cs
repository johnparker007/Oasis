namespace OasisEditor.Features.MfmeImport;

internal readonly record struct MfmeLegacyPoint(int X, int Y);

internal readonly record struct MfmeLegacyColor(float R, float G, float B, float A);

internal abstract record MfmeLegacyComponentBase(
    string SourceType,
    MfmeLegacyPoint Position,
    MfmeLegacyPoint Size,
    string? TextBoxText,
    string? TextBoxFontName,
    string? TextBoxFontStyle,
    string? TextBoxFontSize);

internal sealed record MfmeLegacyBackgroundComponent(
    MfmeLegacyPoint Position,
    MfmeLegacyPoint Size,
    string? BmpImageFilename,
    MfmeLegacyColor? Color)
    : MfmeLegacyComponentBase(
        "Background",
        Position,
        Size,
        null,
        null,
        null,
        null);

internal sealed record MfmeLegacyLampElement(
    string? NumberAsText,
    int? Number,
    MfmeLegacyColor? OnColor,
    string? BmpImageFilename,
    string? BmpMaskImageFilename,
    bool Graphic);

internal sealed record MfmeLegacyLampComponent(
    MfmeLegacyPoint Position,
    MfmeLegacyPoint Size,
    string? TextBoxText,
    string? TextBoxFontName,
    string? TextBoxFontStyle,
    string? TextBoxFontSize,
    MfmeLegacyLampElement? FirstLampElement,
    MfmeLegacyColor? OffImageColor,
    MfmeLegacyColor? TextColor,
    bool NoOutline)
    : MfmeLegacyComponentBase(
        "Lamp",
        Position,
        Size,
        TextBoxText,
        TextBoxFontName,
        TextBoxFontStyle,
        TextBoxFontSize);

internal sealed record MfmeLegacyReelComponent(
    MfmeLegacyPoint Position,
    MfmeLegacyPoint Size,
    int Number,
    int Stops,
    bool Reversed,
    int Height,
    string? BandBmpImageFilename,
    bool HasOverlay,
    string? OverlayBmpImageFilename)
    : MfmeLegacyComponentBase(
        "Reel",
        Position,
        Size,
        null,
        null,
        null,
        null);

internal sealed record MfmeLegacySevenSegmentComponent(
    MfmeLegacyPoint Position,
    MfmeLegacyPoint Size,
    int Number,
    MfmeLegacyColor? SegmentOnColor)
    : MfmeLegacyComponentBase(
        "SevenSegment",
        Position,
        Size,
        null,
        null,
        null,
        null);

internal sealed record MfmeLegacyAlphaComponent(
    string AlphaSourceType,
    MfmeLegacyPoint Position,
    MfmeLegacyPoint Size,
    bool Reversed)
    : MfmeLegacyComponentBase(
        AlphaSourceType,
        Position,
        Size,
        null,
        null,
        null,
        null);

internal sealed record MfmeLegacyLabelComponent(
    MfmeLegacyPoint Position,
    MfmeLegacyPoint Size,
    string? TextBoxText,
    string? TextBoxFontName,
    string? TextBoxFontStyle,
    string? TextBoxFontSize,
    MfmeLegacyColor? TextColor)
    : MfmeLegacyComponentBase(
        "Label",
        Position,
        Size,
        TextBoxText,
        TextBoxFontName,
        TextBoxFontStyle,
        TextBoxFontSize);


internal sealed record MfmeLegacyButtonComponent(
    MfmeLegacyPoint Position,
    MfmeLegacyPoint Size,
    string? TextBoxText,
    string? TextBoxFontName,
    string? TextBoxFontStyle,
    string? TextBoxFontSize,
    bool HasButtonInput,
    bool HasCoinInput,
    string? ButtonNumberAsString,
    bool Inverted,
    string? Shortcut1,
    string? Shortcut2,
    MfmeLegacyLampElement? FirstLampElement,
    MfmeLegacyColor? OffImageColor,
    MfmeLegacyColor? TextColor,
    bool NoOutline)
    : MfmeLegacyComponentBase(
        "ExtractComponentButton",
        Position,
        Size,
        TextBoxText,
        TextBoxFontName,
        TextBoxFontStyle,
        TextBoxFontSize);
