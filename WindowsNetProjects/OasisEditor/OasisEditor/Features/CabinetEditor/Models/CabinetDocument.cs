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

public sealed record CabinetTargetOverride(string TargetId, string FrontSide, int TextureRotation = 0)
{
    public const string NormalFrontSide = "normal";
    public const string InvertedFrontSide = "inverted";

    public static CabinetTargetOverride Default(string targetId) => new(targetId, NormalFrontSide, 0);

    public CabinetTargetOverride Normalized() => new(TargetId, NormalizeFrontSide(FrontSide), NormalizeTextureRotation(TextureRotation));

    public static string NormalizeFrontSide(string? frontSide)
    {
        return string.Equals(frontSide?.Trim(), InvertedFrontSide, StringComparison.OrdinalIgnoreCase)
            ? InvertedFrontSide
            : NormalFrontSide;
    }

    public static int NormalizeTextureRotation(int textureRotation) => textureRotation switch
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

public sealed record CabinetPreviewSettings(bool ShowTargetOverlays, bool ShowFaceBackgrounds)
{
    public static CabinetPreviewSettings Default => new(true, true);
}
