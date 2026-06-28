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

public sealed record CabinetTargetOverride(string TargetId, string FrontSide);

public sealed record CabinetPreviewSettings(bool ShowTargetOverlays, bool ShowFaceBackgrounds)
{
    public static CabinetPreviewSettings Default => new(true, true);
}
