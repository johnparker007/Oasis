namespace OasisEditor.Features.CabinetEditor.Models;

public sealed record CabinetDocument(
    int Version,
    CabinetModelReference Model,
    CabinetTargetOverride[] TargetOverrides,
    CabinetPreviewSettings Preview)
{
    public static CabinetDocument Empty => new(1, new CabinetModelReference(string.Empty, 1.0, "Y"), [], CabinetPreviewSettings.Default);

    public static CabinetDocument FromModelPath(string modelPath) => new(
        1,
        new CabinetModelReference(modelPath, 1.0, "Y"),
        [],
        CabinetPreviewSettings.Default);
}

public sealed record CabinetTargetOverride(string TargetId, string FrontSide, int FaceRotation = 0, bool FaceFlipHorizontal = false)
{
    public const string NormalFrontSide = "normal";
    public const string InvertedFrontSide = "inverted";

    public static CabinetTargetOverride Default(string targetId) => new(targetId, NormalFrontSide, 0, false);

    public CabinetTargetOverride Normalized() => new(TargetId, NormalizeFrontSide(FrontSide), NormalizeFaceRotation(FaceRotation), FaceFlipHorizontal);

    public static string NormalizeFrontSide(string? frontSide)
    {
        return string.Equals(frontSide?.Trim(), InvertedFrontSide, StringComparison.OrdinalIgnoreCase)
            ? InvertedFrontSide
            : NormalFrontSide;
    }

    public static int NormalizeFaceRotation(int faceRotation) => faceRotation switch
    {
        90 => 90,
        180 => 180,
        270 => 270,
        _ => 0
    };
}

public static class CabinetDocumentTargetOverrideExtensions
{
    public static CabinetTargetOverride GetTargetOverride(this CabinetDocument document, string targetId)
    {
        var normalizedTargetId = targetId.Trim();
        return (document.TargetOverrides ?? []).FirstOrDefault(candidate => string.Equals(candidate.TargetId, normalizedTargetId, StringComparison.Ordinal))?.Normalized()
            ?? CabinetTargetOverride.Default(normalizedTargetId);
    }

    public static CabinetDocument WithTargetOverride(this CabinetDocument document, CabinetTargetOverride targetOverride)
    {
        var normalizedOverride = targetOverride.Normalized();
        var overrides = (document.TargetOverrides ?? [])
            .Where(candidate => !string.Equals(candidate.TargetId, normalizedOverride.TargetId, StringComparison.Ordinal))
            .Append(normalizedOverride)
            .ToArray();
        return document with { TargetOverrides = overrides };
    }
}

public sealed record CabinetPreviewSettings(bool ShowTargetOverlays, bool ShowFaceBackgrounds, string LampPreviewMode = CabinetLampPreviewMode.BackgroundOnly)
{
    public static CabinetPreviewSettings Default => new(true, true, CabinetLampPreviewMode.BackgroundOnly);

    public CabinetPreviewSettings Normalized() => new(ShowTargetOverlays, ShowFaceBackgrounds, CabinetLampPreviewMode.Normalize(LampPreviewMode));
}

public static class CabinetLampPreviewMode
{
    public const string Live = "Live";
    public const string BackgroundOnly = "Background Only";
    public const string LampsOff = "Lamps Off";
    public const string LampsAllOn = "Lamps All On";

    public static string Normalize(string? mode)
    {
        return mode?.Trim() switch
        {
            Live => Live,
            LampsOff => LampsOff,
            LampsAllOn => LampsAllOn,
            _ => BackgroundOnly
        };
    }
}
