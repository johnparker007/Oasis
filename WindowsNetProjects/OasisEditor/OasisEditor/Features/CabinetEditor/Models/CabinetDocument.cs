namespace OasisEditor.Features.CabinetEditor.Models;

public sealed record CabinetDocument(
    int Version,
    CabinetModelReference Model,
    CabinetTargetOverride[] TargetOverrides,
    CabinetPreviewSettings Preview,
    CabinetReelSpecification[] ReelSpecifications = null!,
    string? DefaultReelSpecificationId = null,
    CabinetReflectionDefinition[]? Reflections = null)
{
    public static CabinetDocument Empty => new(5, new CabinetModelReference(string.Empty, 1.0, "Y"), [], CabinetPreviewSettings.Default, [], null);

    public static CabinetDocument FromModelPath(string modelPath) => new(
        5,
        new CabinetModelReference(modelPath, 1.0, "Y"),
        [],
        CabinetPreviewSettings.Default,
        [],
        null);
}

public sealed record CabinetReflectionVector(double X, double Y, double Z);

public sealed record CabinetReflectionPlane(CabinetReflectionVector Origin, CabinetReflectionVector Right, CabinetReflectionVector Up, double Width, double Height, CabinetReflectionVector? Normal = null);

public sealed record CabinetReflectionSettings(bool Enabled, double Strength, double UnlitArtworkStrength, double LitLampStrength, double FresnelPower, double FresnelStrength, double Roughness, double Distortion, double EdgeFade)
{
    public static CabinetReflectionSettings RoughPlastic => new(true, .2, .2, 1, 5, .5, .5, .005, .03);
    public static CabinetReflectionSettings PolishedChrome => new(true, .8, .8, 1.5, 4, 1, 0, 0, .015);
    public CabinetReflectionSettings Normalized() => new(Enabled, Math.Clamp(Strength, 0, 2), Math.Clamp(UnlitArtworkStrength, 0, 2), Math.Clamp(LitLampStrength, 0, 4), Math.Clamp(FresnelPower, .1, 10), Math.Clamp(FresnelStrength, 0, 2), Math.Clamp(Roughness, 0, 1), Math.Clamp(Distortion, 0, .05), Math.Clamp(EdgeFade, 0, .25));
}

public sealed record CabinetReflectionSource(string FaceId, CabinetReflectionPlane Plane, string PlaneSource = CabinetReflectionPlaneSource.Automatic)
{
    public CabinetReflectionSource Normalized() => this with { FaceId = FaceId?.Trim() ?? string.Empty };
}

public sealed record CabinetReflectionDefinition(string Id, string TargetId, int MaterialSlot, CabinetReflectionSource[] Sources, CabinetReflectionSettings Settings, string? VisibilityMask = null)
{
    public CabinetReflectionDefinition Normalized() => this with { Id = Id.Trim(), TargetId = TargetId?.Trim() ?? string.Empty, Sources = (Sources ?? []).Select(source => source.Normalized()).ToArray(), Settings = Settings.Normalized(), VisibilityMask = string.IsNullOrWhiteSpace(VisibilityMask) ? null : VisibilityMask.Trim() };
}

public static class CabinetReflectionContract { public const int MaximumSources = 4; }

public static class CabinetReflectionPlaneSource { public const string Automatic = "Automatic from Face target"; public const string Manual = "Manual"; }
public static class CabinetReflectionPreset
{
    public const string RoughPlastic = "Rough Plastic"; public const string PolishedChrome = "Polished Chrome"; public const string Custom = "Custom";
    public static string Detect(CabinetReflectionSettings value) => value == CabinetReflectionSettings.RoughPlastic ? RoughPlastic : value == CabinetReflectionSettings.PolishedChrome ? PolishedChrome : Custom;
    public static CabinetReflectionSettings Resolve(string preset, CabinetReflectionSettings current) => preset == RoughPlastic ? CabinetReflectionSettings.RoughPlastic : preset == PolishedChrome ? CabinetReflectionSettings.PolishedChrome : current;
}

public static class CabinetReflectionPlaneValidation
{
    public static bool TryValidate(CabinetReflectionPlane? plane, out string error)
    {
        error = string.Empty; if (plane is null || plane.Origin is null || plane.Right is null || plane.Up is null) { error = "Plane values are missing."; return false; }
        var values = new[] { plane.Origin.X, plane.Origin.Y, plane.Origin.Z, plane.Right.X, plane.Right.Y, plane.Right.Z, plane.Up.X, plane.Up.Y, plane.Up.Z, plane.Width, plane.Height };
        if (values.Any(value => double.IsNaN(value) || double.IsInfinity(value))) { error = "Plane values must be finite."; return false; }
        if (plane.Width <= 0 || plane.Height <= 0) { error = "Plane width and height must be positive."; return false; }
        var rightLength = Math.Sqrt(plane.Right.X * plane.Right.X + plane.Right.Y * plane.Right.Y + plane.Right.Z * plane.Right.Z); var upLength = Math.Sqrt(plane.Up.X * plane.Up.X + plane.Up.Y * plane.Up.Y + plane.Up.Z * plane.Up.Z);
        if (rightLength < 1e-6 || upLength < 1e-6) { error = "Plane axes must be non-zero."; return false; }
        var dot = (plane.Right.X * plane.Up.X + plane.Right.Y * plane.Up.Y + plane.Right.Z * plane.Up.Z) / (rightLength * upLength);
        if (Math.Abs(dot) > 1e-4) { error = "Plane right and up axes must be orthogonal."; return false; } return true;
    }
}

public sealed record CabinetReelSpecification(string Id, string Name, double DiameterMm, double WidthMm)
{
    public CabinetReelSpecification Normalized() => new(
        Id.Trim(),
        string.IsNullOrWhiteSpace(Name) ? Id.Trim() : Name.Trim(),
        DiameterMm,
        WidthMm);

    public bool HasValidDimensions => OasisEditor.PanelElementValidation.IsFinite(DiameterMm)
        && OasisEditor.PanelElementValidation.IsFinite(WidthMm)
        && DiameterMm > 0
        && WidthMm > 0;
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

public sealed record CabinetPreviewSettings(bool ShowTargetOverlays, bool ShowFaceBackgrounds, string LampPreviewMode = CabinetLampPreviewMode.Live)
{
    public static CabinetPreviewSettings Default => new(true, true, CabinetLampPreviewMode.Live);

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
