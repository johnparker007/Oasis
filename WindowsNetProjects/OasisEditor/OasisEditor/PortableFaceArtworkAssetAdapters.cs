namespace OasisEditor;

internal sealed record FaceArtworkImageImportResult(string AssetPath, int Width, int Height);

internal static class FaceArtworkImageImportService
{
    public static FaceArtworkModel CreateArtwork(FaceArtworkImageImportResult imported, string faceName)
        => FacePipeline.FaceArtworkImageImportService.CreateArtwork(
            new FacePipeline.FaceArtworkImageImportResult(imported.AssetPath, imported.Width, imported.Height), faceName);

    public static FaceArtworkImageImportResult Import(string externalPath, EditorProject project, string faceName)
    {
        var result = FacePipeline.FaceArtworkImageImportService.Import(externalPath, project, faceName);
        return new FaceArtworkImageImportResult(result.AssetPath, result.Width, result.Height);
    }
}

internal static class FaceArtworkOverrideAssetService
{
    public static FaceArtworkOverrideModel Import(string sourcePath, EditorProject project, string faceName,
        FaceArtworkOverrideModel? alignment = null)
        => FacePipeline.FaceArtworkOverrideAssetService.Import(sourcePath, project, faceName, alignment);

    public static FaceArtworkOverrideModel CreateFromBase(FaceArtworkModel artwork, EditorProject project, string faceName)
        => FacePipeline.FaceArtworkOverrideAssetService.CreateFromBase(artwork, project, faceName);

    public static FaceArtworkOverrideModel Reload(FaceArtworkOverrideModel value, EditorProject project)
        => FacePipeline.FaceArtworkOverrideAssetService.Reload(value, project);
}
